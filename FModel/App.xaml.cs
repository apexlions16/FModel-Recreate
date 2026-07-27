using AdonisUI.Controls;
using Microsoft.Win32;
using Serilog;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CUE4Parse;
using FModel.Localization;
using FModel.Services;
using FModel.Settings;
using FModel.Views;
using Serilog.Sinks.SystemConsole.Themes;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace FModel;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private const string RuntimeMarkerEnvironmentVariable = "FMODEL_RECREATE_RUNTIME_MARKER";
    private const string StartupLogEnvironmentVariable = "FMODEL_RECREATE_STARTUP_LOG";

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("winbrand.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    static extern string BrandingFormatString(string format);

    protected override void OnStartup(StartupEventArgs e)
    {
#if DEBUG
        AttachConsole(-1);
#endif
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var runtimeVerification = Array.Exists(e.Args, argument =>
            string.Equals(argument, "--verify-runtime", StringComparison.OrdinalIgnoreCase));

        try
        {
            UserSettings.Default = SettingsStorage.Load();
            LocalizationManager.Initialize();

            if (runtimeVerification)
            {
                VerifyRuntimeBundle();
                Environment.Exit(0);
                return;
            }

            if (!File.Exists(AppPaths.FirstRunMarker))
            {
                var wizardResult = new FirstRunWizard().ShowDialog();
                MainWindow = null;
                if (wizardResult != true)
                {
                    Shutdown(0);
                    return;
                }
            }

            InitializeApplicationDirectories();
            InitializeLogging();

            CacheManager.MigrateLegacyFiles();
            Log.Information("{Product} version {Version} ({CommitId})", Constants.APP_NAME, Constants.APP_VERSION, Constants.APP_COMMIT_ID);
            Log.Information("{OS}", GetOperatingSystemProductName());
            Log.Information("{RuntimeVer}", RuntimeInformation.FrameworkDescription);
            Log.Information("Culture {SysLang}", CultureInfo.CurrentCulture);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            WriteStartupFailure(exception);

            if (runtimeVerification)
            {
                Environment.Exit(-1);
                return;
            }

            System.Windows.MessageBox.Show(
                $"FModel-Recreate could not start.\n\n{exception.GetBaseException().GetType().Name}: {exception.GetBaseException().Message}\n\n" +
                $"A diagnostic log was written to:\n{AppPaths.StartupCrashLog}",
                "FModel-Recreate Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static void VerifyRuntimeBundle()
    {
        _ = Current.FindResource("BoolToVisibilityConverter");
        _ = typeof(FirstRunWizard).Assembly.GetName().Name;
        _ = new System.Windows.Controls.TextBlock { Text = Constants.APP_NAME };

        var markerPath = Environment.GetEnvironmentVariable(RuntimeMarkerEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(markerPath))
            markerPath = AppPaths.RuntimeVerificationMarker;

        var markerDirectory = Path.GetDirectoryName(markerPath);
        if (!string.IsNullOrWhiteSpace(markerDirectory))
            Directory.CreateDirectory(markerDirectory);

        File.WriteAllText(markerPath, DateTimeOffset.UtcNow.ToString("O"));
    }

    private static void InitializeApplicationDirectories()
    {
        var createExports = false;
        if (!Directory.Exists(UserSettings.Default.OutputDirectory))
        {
            var currentDir = AppContext.BaseDirectory;
            try
            {
                var outputDir = Directory.CreateDirectory(Path.Combine(currentDir, "Output"));
                using (File.Create(Path.Combine(outputDir.FullName, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
                {
                }

                UserSettings.Default.OutputDirectory = outputDir.FullName;
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new Exception(
                    "FModel-Recreate cannot create the output directory where it is currently located. " +
                    "Please move FModel-Recreate.exe to a different location.", exception);
            }
        }

        if (!Directory.Exists(UserSettings.Default.RawDataDirectory))
        {
            createExports = true;
            UserSettings.Default.RawDataDirectory = Path.Combine(UserSettings.Default.OutputDirectory, "Exports");
        }

        if (!Directory.Exists(UserSettings.Default.PropertiesDirectory))
        {
            createExports = true;
            UserSettings.Default.PropertiesDirectory = Path.Combine(UserSettings.Default.OutputDirectory, "Exports");
        }

        if (!Directory.Exists(UserSettings.Default.TextureDirectory))
        {
            createExports = true;
            UserSettings.Default.TextureDirectory = Path.Combine(UserSettings.Default.OutputDirectory, "Exports");
        }

        if (!Directory.Exists(UserSettings.Default.AudioDirectory))
        {
            createExports = true;
            UserSettings.Default.AudioDirectory = Path.Combine(UserSettings.Default.OutputDirectory, "Exports");
        }

        if (!Directory.Exists(UserSettings.Default.CodeDirectory))
        {
            createExports = true;
            UserSettings.Default.CodeDirectory = Path.Combine(UserSettings.Default.OutputDirectory, "Exports");
        }

        if (!Directory.Exists(UserSettings.Default.ModelDirectory))
        {
            createExports = true;
            UserSettings.Default.ModelDirectory = Path.Combine(UserSettings.Default.OutputDirectory, "Exports");
        }

        Directory.CreateDirectory(AppPaths.AppDataDirectory);
        Directory.CreateDirectory(Path.Combine(UserSettings.Default.OutputDirectory, "Backups"));
        if (createExports) Directory.CreateDirectory(Path.Combine(UserSettings.Default.OutputDirectory, "Exports"));
        Directory.CreateDirectory(Path.Combine(UserSettings.Default.OutputDirectory, "Logs"));
        CacheManager.EnsureDirectories();
    }

    private static void InitializeLogging()
    {
        const string template = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Enriched}: {Message:lj}{NewLine}{Exception}";
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .Enrich.With<SourceEnricher>()
            .MinimumLevel.Verbose()
            .WriteTo.Console(outputTemplate: template, theme: AnsiConsoleTheme.Literate)
            .WriteTo.File(outputTemplate: template,
                path: Path.Combine(UserSettings.Default.OutputDirectory, "Logs", $"FModel-Recreate-Debug-Log-{DateTime.Now:yyyy-MM-dd}.log"))
#else
            .Enrich.With<CallerEnricher>()
            .WriteTo.File(outputTemplate: template,
                path: Path.Combine(UserSettings.Default.OutputDirectory, "Logs", $"FModel-Recreate-Log-{DateTime.Now:yyyy-MM-dd}.log"))
#endif
            .CreateLogger();
    }

    private static void WriteStartupFailure(Exception exception)
    {
        try
        {
            var logPath = Environment.GetEnvironmentVariable(StartupLogEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(logPath))
                logPath = AppPaths.StartupCrashLog;

            var logDirectory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(logDirectory))
                Directory.CreateDirectory(logDirectory);

            File.AppendAllText(logPath, $"[{DateTimeOffset.Now:O}] {exception}\n\n");
        }
        catch
        {
            // A startup diagnostic must never replace the original failure.
        }
    }

    private void AppExit(object sender, ExitEventArgs e)
    {
        try
        {
            Log.Information("––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––");
            Log.CloseAndFlush();
            SettingsStorage.Save();
        }
        finally
        {
            Environment.Exit(e.ApplicationExitCode);
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("{Exception}", e.Exception);
        WriteStartupFailure(e.Exception);

        var messageBox = new MessageBoxModel
        {
            Text = $"{LocalizationManager.Get("An unexpected error occurred. You can reset settings, restart the application, or ignore the error.")}\n\n" +
                   $"{e.Exception.GetBaseException().GetType()}: {e.Exception.Message}",
            Caption = LocalizationManager.Get("Fatal Error"),
            Icon = MessageBoxImage.Error,
            Buttons =
            [
                MessageBoxButtons.Custom(LocalizationManager.Get("Reset Settings"), EErrorKind.ResetSettings),
                MessageBoxButtons.Custom(LocalizationManager.Get("Restart"), EErrorKind.Restart),
                MessageBoxButtons.Custom(LocalizationManager.Get("OK"), EErrorKind.Ignore)
            ],
            IsSoundEnabled = false
        };

        MessageBox.Show(messageBox);
        if (messageBox.Result == MessageBoxResult.Custom &&
            (EErrorKind) messageBox.ButtonPressed.Id != EErrorKind.Ignore)
        {
            if ((EErrorKind) messageBox.ButtonPressed.Id == EErrorKind.ResetSettings)
                SettingsStorage.Delete();

            try
            {
                ApplicationService.ApplicationView.Restart();
            }
            catch
            {
                Shutdown(-1);
            }
        }

        e.Handled = true;
    }

    private string GetOperatingSystemProductName()
    {
        var productName = string.Empty;
        try
        {
            productName = BrandingFormatString("%WINDOWS_LONG%");
        }
        catch
        {
            // ignored
        }

        if (string.IsNullOrEmpty(productName))
            productName = Environment.OSVersion.VersionString;

        return $"{productName} ({(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit)";
    }

    public static string GetRegistryValue(string path, string name = null, RegistryHive root = RegistryHive.CurrentUser)
    {
        using var rk = RegistryKey.OpenBaseKey(root, RegistryView.Default).OpenSubKey(path);
        if (rk != null)
            return rk.GetValue(name, null) as string;
        return string.Empty;
    }
}