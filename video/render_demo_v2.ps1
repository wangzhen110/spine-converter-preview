[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$videoDir = $PSScriptRoot
$rawVideo = Join-Path $videoDir 'demo-raw.mp4'
$subtitles = Join-Path $videoDir 'demo-v2.ass'
$narration = Join-Path $videoDir 'demo-v2-narration.wav'
$music = Join-Path $videoDir 'demo-v2-music.wav'
$output = Join-Path $videoDir 'SpineConverterPreview-Douyin-v2.mp4'
$preview = Join-Path $videoDir 'demo-v2-preview.png'

if (-not (Test-Path -LiteralPath $rawVideo)) {
    throw ('Missing raw demo video: ' + $rawVideo)
}

Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voice = $synth.GetInstalledVoices() |
    Where-Object { $_.VoiceInfo.Name -match 'Huihui' } |
    Select-Object -First 1
if ($null -ne $voice) {
    $synth.SelectVoice($voice.VoiceInfo.Name)
}
$synth.Rate = 1
$synth.Volume = 100
$scriptText = [System.IO.File]::ReadAllText((Join-Path $videoDir 'demo-v2-narration.txt'), [System.Text.Encoding]::UTF8)
$synth.SetOutputToWaveFile($narration)
$synth.Speak($scriptText)
$synth.Dispose()

$musicFilter = '[0:a]volume=0.035[a0];[1:a]volume=0.025[a1];[2:a]volume=0.02[a2];[a0][a1][a2]amix=inputs=3,afade=t=in:st=0:d=2,afade=t=out:st=33:d=3'
$musicArgs = @(
    '-y', '-hide_banner', '-loglevel', 'error',
    '-f', 'lavfi', '-i', 'sine=frequency=220:duration=36:sample_rate=48000',
    '-f', 'lavfi', '-i', 'sine=frequency=277.18:duration=36:sample_rate=48000',
    '-f', 'lavfi', '-i', 'sine=frequency=329.63:duration=36:sample_rate=48000',
    '-filter_complex', $musicFilter,
    '-c:a', 'pcm_s16le', $music
)
& ffmpeg @musicArgs
if ($LASTEXITCODE -ne 0) {
    throw 'Background music generation failed.'
}

$escapedAss = ($subtitles -replace '\\', '/') -replace ':', '\:'
$videoFilter = '[0:v]trim=duration=36,setpts=PTS-STARTPTS,scale=1040:-2:flags=lanczos[demo];color=c=0x090D13:s=1080x1920:d=36[bg];[bg][demo]overlay=20:300:shortest=1,drawbox=x=40:y=325:w=390:h=45:color=0x0D121A:t=fill,drawbox=x=40:y=385:w=700:h=70:color=0x0D121A:t=fill,drawbox=x=125:y=515:w=745:h=42:color=0x0D121A:t=fill,drawbox=x=620:y=940:w=420:h=32:color=0x0D121A:t=fill,ass=' + "'$escapedAss'" + '[v];[1:a]adelay=500|500,volume=1.2[voice];[2:a]volume=0.65[bed];[voice][bed]amix=inputs=2:duration=longest:normalize=0,alimiter=limit=0.92[a]'
$renderArgs = @(
    '-y', '-hide_banner', '-loglevel', 'error',
    '-stream_loop', '-1', '-i', $rawVideo,
    '-i', $narration,
    '-i', $music,
    '-t', '36',
    '-filter_complex', $videoFilter,
    '-map', '[v]', '-map', '[a]', '-r', '30',
    '-c:v', 'libx264', '-preset', 'medium', '-crf', '19', '-pix_fmt', 'yuv420p',
    '-c:a', 'aac', '-b:a', '192k', '-movflags', '+faststart', $output
)
& ffmpeg @renderArgs
if ($LASTEXITCODE -ne 0) {
    throw 'Final video render failed.'
}

& ffmpeg -y -hide_banner -loglevel error -ss 18 -i $output -frames:v 1 $preview
if ($LASTEXITCODE -ne 0) {
    throw 'Preview extraction failed.'
}

Write-Host ('Video: ' + $output)
Write-Host ('Preview: ' + $preview)
