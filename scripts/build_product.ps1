[CmdletBinding()]
param(
    [string]$GodotPath,
    [string]$OutputDirectory = "dist/product-win-x64",
    [string]$SmokeTestSource = "",
    [string]$BatchSmokeTestFolder = "",
    [switch]$CandidateBuild,
    [switch]$DistributionBuild,
    [switch]$SpineLicenseAcknowledged
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($CandidateBuild -and $DistributionBuild) {
    throw "CandidateBuild and DistributionBuild are mutually exclusive."
}
if ($DistributionBuild -and -not $SpineLicenseAcknowledged) {
    throw "DistributionBuild requires -SpineLicenseAcknowledged. This confirms that the publishing person or entity holds an applicable valid Spine Editor license at the time this Runtime is integrated into the product build."
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "dist"))
if (-not $outputPath.StartsWith($distRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be a child of $distRoot"
}

if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $godot = Get-Command "Godot_v4.7.1-stable_win64_console.exe" -ErrorAction SilentlyContinue
    if ($null -eq $godot) {
        throw "Godot 4.7.1 was not found. Pass -GodotPath explicitly."
    }
    $GodotPath = $godot.Source
}
$GodotPath = (Resolve-Path -LiteralPath $GodotPath).Path

$versionOutput = & $GodotPath --version
if ($LASTEXITCODE -ne 0 -or ($versionOutput -join " ") -notmatch '^4\.7\.1') {
    throw "This build is pinned to Godot 4.7.1; found: $($versionOutput -join ' ')"
}

$converterPublish = Join-Path $projectRoot "build/converter-win-x64"
$productStaging = Join-Path ([System.IO.Path]::GetTempPath()) ("SpineConverterPreview-build-" + [guid]::NewGuid().ToString("N"))
$testsProject = Join-Path $projectRoot "converter/SpineConverter.Tests/SpineConverter.Tests.csproj"
$legacyConverterSource = Join-Path $projectRoot "third_party/SpineSkeletonDataConverter"
$legacyConverterBuild = Join-Path ([System.IO.Path]::GetTempPath()) "SpineSkeletonDataConverter-build"
$spine43Patch = Join-Path $projectRoot "patches/spine-converter-43-json-compat.patch"

function Invoke-PackagedTest {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string]$UserArgument,
        [Parameter(Mandatory = $true)][string]$Name
    )
    $logRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SpineConverterPreview-test-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $logRoot | Out-Null
    $stdoutPath = Join-Path $logRoot "stdout.log"
    $stderrPath = Join-Path $logRoot "stderr.log"
    $quotedUserArgument = '"' + $UserArgument.Replace('"', '\"') + '"'
    $process = Start-Process -FilePath $Executable `
        -ArgumentList @("--headless", "--", $quotedUserArgument) `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath `
        -WindowStyle Hidden -Wait -PassThru
    if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath }
    if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath | Write-Error }
    if ($process.ExitCode -ne 0) { throw "$Name failed with exit code $($process.ExitCode)." }
}

foreach ($path in @($converterPublish, $productStaging)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $path | Out-Null
}

Write-Host "[1/5] Running self-developed converter regression tests"
dotnet run --project $testsProject -c Release
if ($LASTEXITCODE -ne 0) { throw "Converter regression tests failed." }

Write-Host "[2/5] Building the noncommercial multi-version converter"
if (-not (Test-Path -LiteralPath (Join-Path $legacyConverterSource "CMakeLists.txt"))) {
    throw "Missing third-party converter submodule. Run: git submodule update --init --recursive"
}
if (Test-Path -LiteralPath $spine43Patch) {
    $converterMain = Join-Path $legacyConverterSource "src/main.cpp"
    $patchAlreadyApplied = Select-String -LiteralPath $converterMain -Pattern 'Version43' -Quiet
    if (-not $patchAlreadyApplied) {
        & git -C $legacyConverterSource apply --ignore-space-change $spine43Patch
        if ($LASTEXITCODE -ne 0) { throw "Spine 4.3 compatibility patch could not be applied." }
    }
}
$cmakeCommand = Get-Command cmake.exe -ErrorAction SilentlyContinue
if ($null -eq $cmakeCommand) {
    $cmakeCommand = Get-ChildItem "$env:LOCALAPPDATA/Microsoft/WinGet/Packages" -Recurse -Filter cmake.exe -ErrorAction SilentlyContinue | Select-Object -First 1
}
if ($null -eq $cmakeCommand) { throw "CMake is required to build the multi-version converter." }
$cmakePath = if ($cmakeCommand -is [System.Management.Automation.ApplicationInfo]) {
    $cmakeCommand.Source
} else {
    $cmakeCommand.FullName
}
& $cmakePath -S $legacyConverterSource -B $legacyConverterBuild
if ($LASTEXITCODE -ne 0) { throw "Multi-version converter configuration failed." }
& $cmakePath --build $legacyConverterBuild --config Release --parallel
if ($LASTEXITCODE -ne 0) { throw "Multi-version converter build failed." }
$legacyConverterExe = Join-Path $legacyConverterBuild "Release/SpineSkeletonDataConverter.exe"
if (-not (Test-Path -LiteralPath $legacyConverterExe)) { throw "Multi-version converter executable was not produced." }
Copy-Item -LiteralPath $legacyConverterExe -Destination (Join-Path $converterPublish "SpineConverter.exe") -Force

Write-Host "[QA] Running guarded Spine 4.3 compatibility tests"
& (Join-Path $projectRoot "scripts/test_spine43_compat.ps1") -ConverterPath $legacyConverterExe
if ($LASTEXITCODE -ne 0) { throw "Spine 4.3 compatibility tests failed." }

$toolsDirectory = Join-Path $projectRoot "tools"
New-Item -ItemType Directory -Force -Path $toolsDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $converterPublish "SpineConverter.exe") -Destination (Join-Path $toolsDirectory "SpineConverter.exe") -Force

Write-Host "[3/5] Exporting the Godot 4.7.1 release"
$productExe = Join-Path $productStaging "SpineConverterPreview.exe"
& $GodotPath --headless --path $projectRoot --export-release "Windows Desktop" $productExe
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $productExe)) {
    throw "Godot release export failed."
}

Write-Host "[4/5] Assembling licenses, converter, and GDExtension"
$productTools = Join-Path $productStaging "tools"
$productAddonWindows = Join-Path $productStaging "addons/spine_godot/windows"
New-Item -ItemType Directory -Force -Path $productTools, $productAddonWindows | Out-Null
Copy-Item -LiteralPath (Join-Path $converterPublish "SpineConverter.exe") -Destination $productTools -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination $productStaging -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") -Destination $productStaging -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "NOTICE") -Destination $productStaging -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "OPEN_SOURCE_DISTRIBUTION.md") -Destination $productStaging -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "TRADEMARKS.md") -Destination $productStaging -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "SPINE-RUNTIMES-LICENSE.txt") -Destination $productStaging -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "THIRD_PARTY_NOTICES.md") -Destination $productStaging -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "GODOT-LICENSE.txt") -Destination $productStaging -Force
Copy-Item -LiteralPath (Join-Path $legacyConverterSource "LICENSE") -Destination (Join-Path $productStaging "POLYFORM-NONCOMMERCIAL-LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "addons/spine_godot/spine_godot_extension.gdextension") -Destination (Split-Path $productAddonWindows -Parent) -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "addons/spine_godot/windows/libspine_godot.windows.template_release.x86_64.dll") -Destination $productAddonWindows -Force

$editionDescription = "SOURCE-AVAILABLE NONCOMMERCIAL EDITION - all features enabled"
$buildStatus = if ($DistributionBuild) {
    "NONCOMMERCIAL DISTRIBUTION BUILD`r`n$editionDescription`r`nCommercial use is prohibited by the bundled PolyForm Noncommercial component. The builder also acknowledged the Spine license gate.`r`n"
} elseif ($CandidateBuild) {
    "NONCOMMERCIAL CANDIDATE RELEASE`r`n$editionDescription`r`nNot for sale or other commercial use. Confirm the applicable Spine Editor license before distributing Runtime components.`r`n"
} else {
    "NONCOMMERCIAL DEVELOPMENT BUILD`r`n$editionDescription`r`nNot for sale or other commercial use. Rebuild with -DistributionBuild -SpineLicenseAcknowledged only after the Runtime license gate is verified.`r`n"
}
$buildStatus | Set-Content -LiteralPath (Join-Path $productStaging "BUILD-STATUS.txt") -Encoding utf8

# Godot exports the GDExtension DLL beside the executable as well. Runtime
# validation shows that the resource-relative addons layout is required and the
# root copy is redundant.
$redundantRootDll = Join-Path $productStaging "libspine_godot.windows.template_release.x86_64.dll"
if (Test-Path -LiteralPath $redundantRootDll) {
    Remove-Item -LiteralPath $redundantRootDll -Force
}

$forbiddenFiles = Get-ChildItem -LiteralPath $productStaging -Recurse -File |
    Where-Object { $_.Name -match '(?i)(editor|template_debug|pdb)' }
if ($forbiddenFiles) {
    throw "Forbidden development files in product: $($forbiddenFiles.FullName -join ', ')"
}

if (-not [string]::IsNullOrWhiteSpace($SmokeTestSource)) {
	$SmokeTestSource = (Resolve-Path -LiteralPath $SmokeTestSource).Path
	Write-Host "[5/5] Running packaged runtime smoke test"
	Invoke-PackagedTest -Executable $productExe -UserArgument "--smoke-test=$SmokeTestSource" -Name "Packaged runtime smoke test"
} else {
    Write-Host "[5/5] Smoke test skipped; pass -SmokeTestSource to enable it."
}

if (-not [string]::IsNullOrWhiteSpace($BatchSmokeTestFolder)) {
    $BatchSmokeTestFolder = (Resolve-Path -LiteralPath $BatchSmokeTestFolder).Path
    $batchOutput = Join-Path ([System.IO.Path]::GetTempPath()) ("SpineConverterPreview-batch-smoke-" + [guid]::NewGuid().ToString("N"))
	New-Item -ItemType Directory -Path $batchOutput | Out-Null
	Write-Host "[QA] Running packaged folder import/navigation/batch export test"
	Invoke-PackagedTest -Executable $productExe -UserArgument "--batch-smoke-test=$BatchSmokeTestFolder|$batchOutput" -Name "Packaged batch workflow smoke test"
}

$stagingUri = [Uri]((Resolve-Path -LiteralPath $productStaging).Path.TrimEnd('\') + '\')
$manifest = Get-ChildItem -LiteralPath $productStaging -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $relative = [Uri]::UnescapeDataString($stagingUri.MakeRelativeUri([Uri]$_.FullName).ToString())
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $relative"
    }
$manifest | Set-Content -LiteralPath (Join-Path $productStaging "SHA256SUMS.txt") -Encoding ascii

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}
$moveSucceeded = $false
for ($attempt = 1; $attempt -le 10; $attempt++) {
    try {
        Move-Item -LiteralPath $productStaging -Destination $outputPath -ErrorAction Stop
        $moveSucceeded = $true
        break
    } catch [System.IO.IOException] {
        if ($attempt -eq 10) { throw }
        Start-Sleep -Milliseconds 300
    }
}
if (-not $moveSucceeded) { throw "Unable to publish the staged product directory." }
if ($DistributionBuild -or $CandidateBuild) {
    $archiveLabel = if ($DistributionBuild) { "1.0.0" } else { "1.0.0-rc1" }
    $archivePath = Join-Path $distRoot "SpineConverterPreview-SourceAvailable-win-x64-$archiveLabel.zip"
    if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
    Compress-Archive -Path (Join-Path $outputPath '*') -DestinationPath $archivePath -CompressionLevel Optimal
    Write-Host "Distribution archive built at: $archivePath"
}
Write-Host "Noncommercial package built at: $outputPath"
