param(
    [string]$Version = "1.0.0",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "SwtorDailyTool.csproj"
$artifacts = Join-Path $repoRoot "artifacts"
$installerRoot = Join-Path $artifacts "installer"
$payloadRoot = Join-Path $installerRoot "payload"
$appPublishDir = Join-Path $payloadRoot "app"
$outputDir = Join-Path $artifacts "release"
$zipPath = Join-Path $payloadRoot "app.zip"
$sedPath = Join-Path $installerRoot "SWTOR-Holotracker.sed"
$installerPath = Join-Path $outputDir "SWTOR-Holotracker-Setup-$Version.exe"

New-Item -ItemType Directory -Force -Path $artifacts, $installerRoot, $payloadRoot, $outputDir | Out-Null

if (Test-Path $payloadRoot) {
    Get-ChildItem -LiteralPath $payloadRoot -Force | Remove-Item -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $appPublishDir | Out-Null

if (-not $SkipPublish) {
    dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $appPublishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $appPublishDir "*") -DestinationPath $zipPath -Force

$installCmd = @'
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-SWTOR-Holotracker.ps1"
exit /b %ERRORLEVEL%
'@
Set-Content -LiteralPath (Join-Path $payloadRoot "Install-SWTOR-Holotracker.cmd") -Value $installCmd -Encoding ASCII

$installPs = @'
$ErrorActionPreference = "Stop"

$appName = "SWTOR Holotracker"
$installDir = Join-Path $env:LOCALAPPDATA $appName
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\SWTOR Holotracker"
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "SWTOR Holotracker.lnk"
$startMenuShortcut = Join-Path $startMenuDir "SWTOR Holotracker.lnk"
$sourceZip = Join-Path $PSScriptRoot "app.zip"
$tempDir = Join-Path $env:TEMP ("SWTOR-Holotracker-install-" + [Guid]::NewGuid().ToString("N"))

Get-Process -Name "SWTOR Holotracker" -ErrorAction SilentlyContinue | Stop-Process -Force

New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
Expand-Archive -LiteralPath $sourceZip -DestinationPath $tempDir -Force

$settingsBackup = $null
$settingsPath = Join-Path $installDir "data\settings.json"
if (Test-Path $settingsPath) {
    $settingsBackup = Join-Path $env:TEMP ("SWTOR-Holotracker-settings-" + [Guid]::NewGuid().ToString("N") + ".json")
    Copy-Item -LiteralPath $settingsPath -Destination $settingsBackup -Force
}

if (Test-Path $installDir) {
    Remove-Item -LiteralPath $installDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Path (Join-Path $tempDir "*") -Destination $installDir -Recurse -Force

if ($settingsBackup -and (Test-Path $settingsBackup)) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $settingsPath) | Out-Null
    Copy-Item -LiteralPath $settingsBackup -Destination $settingsPath -Force
    Remove-Item -LiteralPath $settingsBackup -Force
}

$exePath = Join-Path $installDir "SWTOR Holotracker.exe"
New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null

$shell = New-Object -ComObject WScript.Shell
foreach ($shortcutPath in @($desktopShortcut, $startMenuShortcut)) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $installDir
    $shortcut.IconLocation = $exePath
    $shortcut.Description = "SWTOR Holotracker"
    $shortcut.Save()
}

$uninstallPsPath = Join-Path $installDir "Uninstall-SWTOR-Holotracker.ps1"
$uninstallCmdPath = Join-Path $installDir "Uninstall SWTOR Holotracker.cmd"
$uninstallPs = @"
`$ErrorActionPreference = "SilentlyContinue"
Get-Process -Name "SWTOR Holotracker" | Stop-Process -Force
Remove-Item -LiteralPath "$desktopShortcut" -Force
Remove-Item -LiteralPath "$startMenuShortcut" -Force
Remove-Item -LiteralPath "$startMenuDir" -Recurse -Force
Start-Process -FilePath "cmd.exe" -ArgumentList "/c timeout /t 1 /nobreak >nul & rmdir /s /q `"$installDir`"" -WindowStyle Hidden
"@
Set-Content -LiteralPath $uninstallPsPath -Value $uninstallPs -Encoding UTF8
Set-Content -LiteralPath $uninstallCmdPath -Value "@echo off`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"%~dp0Uninstall-SWTOR-Holotracker.ps1`"`r`n" -Encoding ASCII

Remove-Item -LiteralPath $tempDir -Recurse -Force
Start-Process -FilePath $exePath
'@
Set-Content -LiteralPath (Join-Path $payloadRoot "Install-SWTOR-Holotracker.ps1") -Value $installPs -Encoding UTF8

$files = @(
    "Install-SWTOR-Holotracker.cmd",
    "Install-SWTOR-Holotracker.ps1",
    "app.zip"
)

$sourceEntries = for ($i = 0; $i -lt $files.Count; $i++) {
    "%FILE$i%="
}
$stringEntries = for ($i = 0; $i -lt $files.Count; $i++) {
    "FILE$i=`"$($files[$i])`""
}

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=SWTOR Holotracker has been installed.
TargetName=$installerPath
FriendlyName=SWTOR Holotracker Setup
AppLaunched=cmd /c Install-SWTOR-Holotracker.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles

[SourceFiles]
SourceFiles0=$payloadRoot\

[SourceFiles0]
$($sourceEntries -join "`r`n")

[Strings]
$($stringEntries -join "`r`n")
"@
Set-Content -LiteralPath $sedPath -Value $sed -Encoding ASCII

if (Test-Path $installerPath) {
    Remove-Item -LiteralPath $installerPath -Force
}

Start-Process -FilePath "iexpress.exe" -ArgumentList @("/N", $sedPath) -Wait -NoNewWindow
if (-not (Test-Path $installerPath)) {
    throw "iexpress did not create the expected installer at $installerPath"
}

Write-Host "Installer created: $installerPath"
