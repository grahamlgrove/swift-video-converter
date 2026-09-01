$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseDir = Join-Path $projectDir 'Grove Swift Video Converter'
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$frameworkDir = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$wpfDir = Join-Path $frameworkDir 'WPF'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows C# compiler was not found.' }
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:"$projectDir\Assets\grove-swift-icon.ico" /out:"$releaseDir\GroveSwiftVideoConverter.exe" /reference:"$frameworkDir\System.dll" /reference:"$frameworkDir\System.Core.dll" /reference:"$frameworkDir\System.Xaml.dll" /reference:"$frameworkDir\System.Windows.Forms.dll" /reference:"$wpfDir\WindowsBase.dll" /reference:"$wpfDir\PresentationCore.dll" /reference:"$wpfDir\PresentationFramework.dll" "$projectDir\VideoConverter.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -Force -LiteralPath (Join-Path $projectDir 'README.txt') -Destination (Join-Path $releaseDir 'README.txt')
Copy-Item -Force -LiteralPath (Join-Path $projectDir 'Assets\grove-swift-icon.ico') -Destination (Join-Path $releaseDir 'grove-swift-icon.ico')
Copy-Item -Force -LiteralPath (Join-Path $projectDir 'THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $releaseDir 'THIRD-PARTY-NOTICES.txt')
Copy-Item -Force -Recurse -LiteralPath (Join-Path $projectDir 'Licenses') -Destination $releaseDir
Write-Host "Built: $releaseDir\GroveSwiftVideoConverter.exe"
