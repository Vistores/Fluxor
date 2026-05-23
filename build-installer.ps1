$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$installerOutput = Join-Path $repoRoot 'artifacts\installer'
$isccPath = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'

if (-not (Test-Path $isccPath)) {
    throw "Inno Setup compiler was not found at '$isccPath'."
}

if (Test-Path $installerOutput) {
    Remove-Item -Recurse -Force $installerOutput
}

dotnet build .\Plugins\Fluxor.PluginCursorFlameContour\Fluxor.PluginCursorFlameContour.csproj -c Release -m:1 -v minimal
if ($LASTEXITCODE -ne 0) { throw "Fluxor.PluginCursorFlameContour build failed." }
dotnet build .\Plugins\Fluxor.PluginVelvetVoid\Fluxor.PluginVelvetVoid.csproj -c Release -m:1 -v minimal
if ($LASTEXITCODE -ne 0) { throw "Fluxor.PluginVelvetVoid build failed." }
dotnet build .\Plugins\Fluxor.PluginFireflySwarm\Fluxor.PluginFireflySwarm.csproj -c Release -m:1 -v minimal
if ($LASTEXITCODE -ne 0) { throw "Fluxor.PluginFireflySwarm build failed." }
dotnet build .\Plugins\Fluxor.PluginRetroTrace\Fluxor.PluginRetroTrace.csproj -c Release -m:1 -v minimal
if ($LASTEXITCODE -ne 0) { throw "Fluxor.PluginRetroTrace build failed." }
dotnet build .\Plugins\Fluxor.PluginLightningTail\Fluxor.PluginLightningTail.csproj -c Release -m:1 -v minimal
if ($LASTEXITCODE -ne 0) { throw "Fluxor.PluginLightningTail build failed." }
dotnet build .\Plugins\Fluxor.PluginSakuraInk\Fluxor.PluginSakuraInk.csproj -c Release -m:1 -v minimal
if ($LASTEXITCODE -ne 0) { throw "Fluxor.PluginSakuraInk build failed." }

dotnet build .\CursorFX.App\CursorFX.App.csproj -c Release -m:1 -v minimal
if ($LASTEXITCODE -ne 0) { throw "Fluxor app build failed." }

& $isccPath .\Installer\Fluxor.iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }

Write-Host ''
Write-Host "Installer created in: $installerOutput"
