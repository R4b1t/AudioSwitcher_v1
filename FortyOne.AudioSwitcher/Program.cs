using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FortyOne.AudioSwitcher.Configuration;
using FortyOne.AudioSwitcher.Helpers;
using FortyOne.AudioSwitcher.Properties;

namespace FortyOne.AudioSwitcher
{
    internal static class Program
    {
        public static string AppDataDirectory { get; private set; }

        public static ConfigurationSettings Settings { get; private set; }

        [STAThread]
        private static void Main()
        {
            Application.ThreadException += WinFormExceptionHandler.OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += WinFormExceptionHandler.OnUnhandledCLRException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            TryEnableDpiAwareness();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (Environment.OSVersion.Version.Major < 6)
            {
                MessageBox.Show("Audio Switcher only supports Windows Vista and above", "Unsupported Operating System");
                return;
            }

            Application.ApplicationExit += Application_ApplicationExit;
            AppDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioSwitcher");

            if (!Directory.Exists(AppDataDirectory))
                Directory.CreateDirectory(AppDataDirectory);

            var settingsPath = Path.Combine(AppDataDirectory, Resources.ConfigFile);

            try
            {
                // Remove legacy AutoUpdater leftovers
                TryDelete(Application.StartupPath + "AutoUpdater.exe");
                TryDelete(Path.Combine(Directory.GetParent(Assembly.GetEntryAssembly().Location).FullName, "AutoUpdater.exe"));
                TryDelete(Path.Combine(AppDataDirectory, "AutoUpdater.exe"));
            }
            catch
            {
            }

            try
            {
                var iniSettingsPath = Path.Combine(Directory.GetParent(Assembly.GetEntryAssembly().Location).FullName, Resources.OldConfigFile);
                TryDelete(iniSettingsPath);
            }
            catch
            {
            }

            try
            {
                var oldJsonSettingsPath = Path.Combine(Directory.GetParent(Assembly.GetEntryAssembly().Location).FullName, Resources.ConfigFile);

                ISettingsSource jsonSource = new JsonSettings();
                jsonSource.SetFilePath(settingsPath);

                Settings = new ConfigurationSettings(jsonSource);

                if (File.Exists(oldJsonSettingsPath))
                {
                    try
                    {
                        ISettingsSource oldSource = new JsonSettings();
                        oldSource.SetFilePath(oldJsonSettingsPath);

                        var oldSettings = new ConfigurationSettings(oldSource);
                        Settings.LoadFrom(oldSettings);
                    }
                    finally
                    {
                        TryDelete(oldJsonSettingsPath);
                    }
                }

                Settings.CreateDefaults();
                Settings.Flush();
                AppLog.Info("Audio Switcher started (updates disabled)");
            }
            catch (Exception ex)
            {
                AppLog.Error("Settings init failed", ex);
                var errorMessage = string.Format(
                    "Error creating/reading settings file [{0}]. Make sure you have read/write access to this file.\r\nOr try running as Administrator",
                    settingsPath);
                MessageBox.Show(errorMessage, "Settings File - Cannot Access");
                return;
            }

            try
            {
                Application.Run(AudioSwitcher.Instance);
            }
            catch (Exception ex)
            {
                AppLog.Error("Unhandled UI exception", ex);
                var edf = new ExceptionDisplayForm("An Unexpected Error Occurred", ex);
                edf.ShowDialog();
            }
            finally
            {
                try
                {
                    if (Settings != null)
                        Settings.Flush();
                }
                catch (Exception ex)
                {
                    AppLog.Error("Settings flush on exit failed", ex);
                }
            }
        }

        private static void Application_ApplicationExit(object sender, EventArgs e)
        {
            try
            {
                if (Settings != null)
                    Settings.Flush();
            }
            catch
            {
            }

            try
            {
                AudioSwitcher.Instance.TrayIconVisible = false;
            }
            catch
            {
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static void TryEnableDpiAwareness()
        {
            try
            {
                // Prefer Per-Monitor V2 when available (Windows 10 1703+)
                if (Environment.OSVersion.Version.Major >= 10)
                    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                else
                    SetProcessDPIAware();
            }
            catch
            {
                try
                {
                    SetProcessDPIAware();
                }
                catch
                {
                }
            }
        }

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    }
}
