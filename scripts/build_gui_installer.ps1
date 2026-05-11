param(
    [string]$Version = "1.0.0",
    [switch]$SkipPayloadPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repoRoot "SwtorDailyTool.csproj"
$installerProject = Join-Path $repoRoot "Installer\SWTORHolotrackerInstaller.csproj"
$payloadRoot = Join-Path $repoRoot "artifacts\installer-gui\payload"
$appPublishDir = Join-Path $payloadRoot "app"
$zipPath = Join-Path $payloadRoot "app.zip"
$installerPublishDir = Join-Path $repoRoot "artifacts\installer-gui\publish"
$releaseDir = Join-Path $repoRoot "artifacts\release"
$outputInstaller = Join-Path $releaseDir "SWTOR-Holotracker-GUI-Setup-$Version.exe"

New-Item -ItemType Directory -Force -Path $payloadRoot, $appPublishDir, $installerPublishDir, $releaseDir | Out-Null

if (-not $SkipPayloadPublish) {
    if (Test-Path $appPublishDir) {
        Get-ChildItem -LiteralPath $appPublishDir -Force | Remove-Item -Recurse -Force
    }
    dotnet publish $appProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=$Version -p:FileVersion=$Version -p:AssemblyVersion=$Version -o $appPublishDir
    if ($LASTEXITCODE -ne 0) {
        throw "App publish failed with exit code $LASTEXITCODE"
    }
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $appPublishDir "*") -DestinationPath $zipPath -Force

if (Test-Path $installerPublishDir) {
    Get-ChildItem -LiteralPath $installerPublishDir -Force | Remove-Item -Recurse -Force
}
dotnet publish $installerProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=$Version -p:FileVersion=$Version -p:AssemblyVersion=$Version -p:DebugType=none -p:DebugSymbols=false -o $installerPublishDir
if ($LASTEXITCODE -ne 0) {
    throw "Installer publish failed with exit code $LASTEXITCODE"
}

$builtInstaller = Join-Path $installerPublishDir "SWTOR Holotracker Setup.exe"
if (-not (Test-Path $builtInstaller)) {
    throw "Expected installer output was not found at $builtInstaller"
}

Copy-Item -LiteralPath $builtInstaller -Destination $outputInstaller -Force
Write-Host "GUI installer created: $outputInstaller"
