param([string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$portableDir = Join-Path $projectDir 'Grove Swift Video Converter'
$releaseDir = Join-Path $projectDir 'Release'
$wixDir = Join-Path $projectDir '.tools\wix314'
$candle = Join-Path $wixDir 'candle.exe'
$light = Join-Path $wixDir 'light.exe'

& (Join-Path $projectDir 'build.ps1')
if (-not (Test-Path (Join-Path $portableDir 'tools\ffmpeg.exe')) -or -not (Test-Path (Join-Path $portableDir 'tools\ffprobe.exe'))) { throw 'FFmpeg and FFprobe must be present in the portable tools folder.' }
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
$zip = Join-Path $releaseDir "Grove-Swift-Video-Converter-$Version-portable.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -Force -LiteralPath $zip }
Compress-Archive -Path $portableDir -DestinationPath $zip -CompressionLevel Optimal

if (-not (Test-Path -LiteralPath $candle) -or -not (Test-Path -LiteralPath $light)) { throw "WiX 3.14 tools are missing from $wixDir" }
$wixObject = Join-Path $releaseDir 'Product.wixobj'
$msi = Join-Path $releaseDir "Grove-Swift-Video-Converter-$Version-x64.msi"
& $candle -nologo -arch x64 -dSourceDir="$portableDir" -out $wixObject (Join-Path $projectDir 'Installer\Product.wxs')
if ($LASTEXITCODE -ne 0) { throw 'WiX compilation failed.' }
& $light -nologo -sval -spdb -out $msi $wixObject
if ($LASTEXITCODE -ne 0) { throw 'MSI creation failed.' }
Remove-Item -Force -LiteralPath $wixObject

$hashes = Get-FileHash -Algorithm SHA256 -LiteralPath $zip,$msi
$hashLines = $hashes | ForEach-Object { "$($_.Hash.ToLowerInvariant())  $(Split-Path -Leaf $_.Path)" }
Set-Content -LiteralPath (Join-Path $releaseDir 'SHA256SUMS.txt') -Value $hashLines -Encoding ascii
Write-Host "Release created in $releaseDir"
