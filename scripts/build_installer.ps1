[CmdletBinding()]
param(
    [string]$NsisCompilerPath = "",
    [switch]$DistributionBuild
)
$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$productDirectory = "product-open-source-win-x64"
$status = Join-Path $projectRoot ("dist/" + $productDirectory + "/BUILD-STATUS.txt")
if (-not (Test-Path -LiteralPath $status)) { throw "Build the product candidate first." }
if ([string]::IsNullOrWhiteSpace($NsisCompilerPath)) {
    $compiler = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($null -eq $compiler) { throw "NSIS compiler not found. Install NSIS or pass -NsisCompilerPath." }
    $NsisCompilerPath = $compiler.Source
}
$NsisCompilerPath = (Resolve-Path -LiteralPath $NsisCompilerPath).Path
$script = Join-Path $projectRoot "installer/SpineConverterPreview.nsi"
$arguments = @('/V2')
if ($DistributionBuild) { $arguments += '/DRELEASE_SUFFIX=' }
$arguments += ('"' + $script + '"')
$process = Start-Process -FilePath $NsisCompilerPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "NSIS failed with exit code $($process.ExitCode)." }
$suffix = if ($DistributionBuild) { "" } else { "-rc1" }
$installerName = "SpineConverterPreview-SourceAvailable-Setup-win-x64-1.0.0$suffix.exe"
$installer = Get-Item (Join-Path $projectRoot ("dist/" + $installerName))
$hash = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $($installer.Name)" | Set-Content -LiteralPath ($installer.FullName + '.sha256') -Encoding ascii
Write-Host "Installer built at: $($installer.FullName)"
