[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ConverterPath
)

$ErrorActionPreference = 'Stop'
$ConverterPath = (Resolve-Path -LiteralPath $ConverterPath).Path
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('spine43-compat-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null

function Invoke-ExpectedExit {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][int]$Expected,
        [Parameter(Mandatory = $true)][string]$Name
    )
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & $ConverterPath @Arguments *> (Join-Path $testRoot ($Name + '.log'))
    $actualExit = $LASTEXITCODE
    $ErrorActionPreference = $previousPreference
    if ($actualExit -ne $Expected) {
        throw "$Name expected exit $Expected, got $actualExit"
    }
}

$common43 = Join-Path $testRoot 'common-43.json'
$unsupported43 = Join-Path $testRoot 'unsupported-43.json'
[System.IO.File]::WriteAllText(
    $common43,
    '{"skeleton":{"spine":"4.3.00"},"bones":[{"name":"root"}],"slots":[],"skins":[]}',
    (New-Object System.Text.UTF8Encoding($false))
)
[System.IO.File]::WriteAllText(
    $unsupported43,
    '{"skeleton":{"spine":"4.3.00"},"bones":[{"name":"root"}],"constraints":{"slider":[]}}',
    (New-Object System.Text.UTF8Encoding($false))
)

$same43 = Join-Path $testRoot 'same-43.json'
$down42 = Join-Path $testRoot 'down-42.json'
Invoke-ExpectedExit -Arguments @($common43, $same43, '-v', '4.3.00') -Expected 0 -Name 'json-43-to-43'
Invoke-ExpectedExit -Arguments @($same43, $down42, '-v', '4.2.11') -Expected 0 -Name 'json-43-to-42'
Invoke-ExpectedExit -Arguments @($common43, (Join-Path $testRoot 'invalid-43.skel'), '-v', '4.3.00') -Expected 1 -Name 'reject-skel-43'
Invoke-ExpectedExit -Arguments @($unsupported43, (Join-Path $testRoot 'unsupported-out.json'), '-v', '4.2.11') -Expected 1 -Name 'reject-slider-43'

$sameData = Get-Content -LiteralPath $same43 -Raw | ConvertFrom-Json
$downData = Get-Content -LiteralPath $down42 -Raw | ConvertFrom-Json
if ($sameData.skeleton.spine -ne '4.3.00') { throw '4.3 output metadata mismatch.' }
if ($downData.skeleton.spine -ne '4.2.11') { throw '4.2 output metadata mismatch.' }
if ($sameData.bones.Count -ne 1 -or $downData.bones.Count -ne 1) { throw 'Common skeleton data was not preserved.' }

Write-Host 'SPINE43_COMPAT PASS json43=PASS downgrade42=PASS skel43=REJECT slider43=REJECT'
exit 0
