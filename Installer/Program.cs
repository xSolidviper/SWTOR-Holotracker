using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SwtorHolotrackerInstaller;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
    }
}

internal sealed class InstallerForm : Form
{
    private static readonly Version PackageVersion =
        typeof(InstallerForm).Assembly.GetName().Version ?? new Version(1, 0, 0);

    private readonly TextBox _pathBox = new();
    private readonly Button _browseButton = new();
    private readonly Button _installButton = new();
    private readonly Label _statusLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly CheckBox _desktopShortcut = new();
    private readonly CheckBox _startMenuShortcut = new();
    private readonly CheckBox _launchAfterInstall = new();

    public InstallerForm()
    {
        Text = "SWTOR Holotracker Setup";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(620, 330);
        BackColor = Color.FromArgb(10, 16, 26);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "data", "images", "Icon-removebg-preview.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        Controls.Add(new Label
        {
            Text = "SWTOR Holotracker",
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 184, 64),
            Location = new Point(28, 24),
            Size = new Size(560, 40)
        });

        Controls.Add(new Label
        {
            Text = "Choose where to install the app.",
            ForeColor = Color.FromArgb(178, 196, 216),
            Location = new Point(31, 70),
            Size = new Size(560, 24)
        });

        Controls.Add(new Label
        {
            Text = "Install location",
            ForeColor = Color.FromArgb(178, 196, 216),
            Location = new Point(31, 112),
            Size = new Size(160, 22)
        });

        _pathBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SWTOR Holotracker");
        _pathBox.Location = new Point(31, 137);
        _pathBox.Size = new Size(455, 27);
        _pathBox.TextChanged += (_, _) => RefreshInstallMode();
        Controls.Add(_pathBox);

        _browseButton.Text = "Browse...";
        _browseButton.Location = new Point(498, 136);
        _browseButton.Size = new Size(90, 29);
        _browseButton.Click += (_, _) => BrowseInstallPath();
        Controls.Add(_browseButton);

        _desktopShortcut.Text = "Create desktop shortcut";
        _desktopShortcut.Checked = true;
        _desktopShortcut.ForeColor = Color.White;
        _desktopShortcut.BackColor = Color.Transparent;
        _desktopShortcut.Location = new Point(31, 183);
        _desktopShortcut.Size = new Size(220, 24);
        Controls.Add(_desktopShortcut);

        _startMenuShortcut.Text = "Create Start Menu shortcut";
        _startMenuShortcut.Checked = true;
        _startMenuShortcut.ForeColor = Color.White;
        _startMenuShortcut.BackColor = Color.Transparent;
        _startMenuShortcut.Location = new Point(31, 211);
        _startMenuShortcut.Size = new Size(240, 24);
        Controls.Add(_startMenuShortcut);

        _launchAfterInstall.Text = "Launch SWTOR Holotracker after install";
        _launchAfterInstall.Checked = true;
        _launchAfterInstall.ForeColor = Color.White;
        _launchAfterInstall.BackColor = Color.Transparent;
        _launchAfterInstall.Location = new Point(31, 239);
        _launchAfterInstall.Size = new Size(300, 24);
        Controls.Add(_launchAfterInstall);

        _progress.Location = new Point(31, 278);
        _progress.Size = new Size(360, 18);
        _progress.Style = ProgressBarStyle.Continuous;
        Controls.Add(_progress);

        _statusLabel.Text = "Ready to install.";
        _statusLabel.ForeColor = Color.FromArgb(178, 196, 216);
        _statusLabel.Location = new Point(31, 300);
        _statusLabel.Size = new Size(360, 22);
        Controls.Add(_statusLabel);

        _installButton.Text = "Install";
        _installButton.BackColor = Color.FromArgb(32, 41, 56);
        _installButton.ForeColor = Color.White;
        _installButton.FlatStyle = FlatStyle.Flat;
        _installButton.FlatAppearance.BorderColor = Color.FromArgb(255, 184, 64);
        _installButton.Location = new Point(478, 272);
        _installButton.Size = new Size(110, 34);
        _installButton.Click += async (_, _) => await InstallAsync();
        Controls.Add(_installButton);

        RefreshInstallMode();
    }

    private void BrowseInstallPath()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the SWTOR Holotracker install folder",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_pathBox.Text)
                ? _pathBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathBox.Text = dialog.SelectedPath;
            RefreshInstallMode();
        }
    }

    private async Task InstallAsync()
    {
        var installDir = _pathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(installDir))
        {
            MessageBox.Show(this, "Choose an install location first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ToggleUi(false);
        try
        {
            await Task.Run(() => InstallTo(installDir));
            _progress.Value = 100;
            _statusLabel.Text = "Finished.";

            var exePath = Path.Combine(installDir, "SWTOR Holotracker.exe");
            if (_launchAfterInstall.Checked && File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = installDir, UseShellExecute = true });
            }

            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Install failed.";
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            ToggleUi(true);
        }
    }

    private void InstallTo(string installDir)
    {
        SetProgress(10, "Preparing files...");
        var tempZip = Path.Combine(Path.GetTempPath(), "SWTOR-Holotracker-" + Guid.NewGuid().ToString("N") + ".zip");
        var tempExtract = Path.Combine(Path.GetTempPath(), "SWTOR-Holotracker-" + Guid.NewGuid().ToString("N"));

        try
        {
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.zip")
                ?? throw new InvalidOperationException("Installer payload is missing."))
            using (var file = File.Create(tempZip))
            {
                resource.CopyTo(file);
            }

            SetProgress(30, "Extracting app...");
            Directory.CreateDirectory(tempExtract);
            ZipFile.ExtractToDirectory(tempZip, tempExtract, true);

            SetProgress(55, "Installing...");
            PreserveSettingsThenReplace(tempExtract, installDir);

            SetProgress(78, "Creating shortcuts...");
            var exePath = Path.Combine(installDir, "SWTOR Holotracker.exe");
            if (_desktopShortcut.Checked)
            {
                CreateShortcut(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SWTOR Holotracker.lnk"),
                    exePath,
                    installDir);
            }
            if (_startMenuShortcut.Checked)
            {
                var startMenuDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft",
                    "Windows",
                    "Start Menu",
                    "Programs",
                    "SWTOR Holotracker");
                Directory.CreateDirectory(startMenuDir);
                CreateShortcut(Path.Combine(startMenuDir, "SWTOR Holotracker.lnk"), exePath, installDir);
            }

            WriteUninstaller(installDir);
            SetProgress(95, "Finishing...");
        }
        finally
        {
            TryDeleteFile(tempZip);
            TryDeleteDirectory(tempExtract);
        }
    }

    private void RefreshInstallMode()
    {
        var installDir = _pathBox.Text.Trim();
        var installedVersion = GetInstalledVersion(installDir);
        if (installedVersion is null)
        {
            _installButton.Text = "Install";
            _statusLabel.Text = $"Ready to install v{FormatVersion(PackageVersion)}.";
            return;
        }

        var comparison = installedVersion.CompareTo(PackageVersion);
        if (comparison < 0)
        {
            _installButton.Text = "Update App";
            _statusLabel.Text = $"Installed v{FormatVersion(installedVersion)}. Ready to update to v{FormatVersion(PackageVersion)}.";
        }
        else if (comparison == 0)
        {
            _installButton.Text = "Reinstall";
            _statusLabel.Text = $"v{FormatVersion(installedVersion)} is already installed.";
        }
        else
        {
            _installButton.Text = "Install";
            _statusLabel.Text = $"Installed v{FormatVersion(installedVersion)} is newer than this setup v{FormatVersion(PackageVersion)}.";
        }
    }

    private static Version? GetInstalledVersion(string installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir))
        {
            return null;
        }

        var exePath = Path.Combine(installDir, "SWTOR Holotracker.exe");
        if (!File.Exists(exePath))
        {
            return null;
        }

        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            return Version.TryParse(info.FileVersion, out var version) ? version : null;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatVersion(Version version)
    {
        return version.Revision > 0
            ? version.ToString()
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static void PreserveSettingsThenReplace(string sourceDir, string installDir)
    {
        var settingsBackup = Path.Combine(Path.GetTempPath(), "SWTOR-Holotracker-settings-" + Guid.NewGuid().ToString("N") + ".json");
        var settingsPath = Path.Combine(installDir, "data", "settings.json");
        var hadSettings = File.Exists(settingsPath);
        if (hadSettings)
        {
            File.Copy(settingsPath, settingsBackup, true);
        }

        foreach (var process in Process.GetProcessesByName("SWTOR Holotracker"))
        {
            process.Kill();
            process.WaitForExit(3000);
        }

        if (Directory.Exists(installDir))
        {
            Directory.Delete(installDir, true);
        }
        Directory.CreateDirectory(installDir);
        CopyDirectory(sourceDir, installDir);

        if (hadSettings && File.Exists(settingsBackup))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.Copy(settingsBackup, settingsPath, true);
            TryDeleteFile(settingsBackup);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var destination = file.Replace(sourceDir, destinationDir, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
        var shortcut = shell!.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcutPath]);
        shortcut!.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, [targetPath]);
        shortcut.GetType().InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, [workingDirectory]);
        shortcut.GetType().InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, [targetPath]);
        shortcut.GetType().InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, ["SWTOR Holotracker"]);
        shortcut.GetType().InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
        if (shortcut is not null && Marshal.IsComObject(shortcut))
        {
            Marshal.FinalReleaseComObject(shortcut);
        }
        if (shell is not null && Marshal.IsComObject(shell))
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void WriteUninstaller(string installDir)
    {
        var uninstallScript = Path.Combine(installDir, "Uninstall-SWTOR-Holotracker.ps1");
        var uninstallCommand = Path.Combine(installDir, "Uninstall SWTOR Holotracker.cmd");
        var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SWTOR Holotracker.lnk");
        var startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Windows",
            "Start Menu",
            "Programs",
            "SWTOR Holotracker");

        var ps =
            "$ErrorActionPreference = \"SilentlyContinue\"\r\n" +
            "Get-Process -Name \"SWTOR Holotracker\" | Stop-Process -Force\r\n" +
            $"Remove-Item -LiteralPath \"{desktopShortcut}\" -Force\r\n" +
            $"Remove-Item -LiteralPath \"{startMenuDir}\" -Recurse -Force\r\n" +
            "Start-Process -FilePath \"cmd.exe\" " +
            $"-ArgumentList \"/c timeout /t 1 /nobreak >nul & rmdir /s /q \\\"{installDir}\\\"\" " +
            "-WindowStyle Hidden\r\n";
        File.WriteAllText(uninstallScript, ps);
        File.WriteAllText(uninstallCommand, "@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0Uninstall-SWTOR-Holotracker.ps1\"\r\n");
    }

    private void ToggleUi(bool enabled)
    {
        _pathBox.Enabled = enabled;
        _browseButton.Enabled = enabled;
        _desktopShortcut.Enabled = enabled;
        _startMenuShortcut.Enabled = enabled;
        _launchAfterInstall.Enabled = enabled;
        _installButton.Enabled = enabled;
    }

    private void SetProgress(int value, string message)
    {
        BeginInvoke(() =>
        {
            _progress.Value = Math.Clamp(value, 0, 100);
            _statusLabel.Text = message;
        });
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
