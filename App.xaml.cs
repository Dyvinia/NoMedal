using DyviniaUtils;
using DyviniaUtils.Dialogs;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace NoMedal {

    public class Config : SettingsManager<Config> {
        public bool UpdateChecker { get; set; } = true;

        public List<string> Programs { get; set; } = new();
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public static readonly string Version = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString()[..5];
        public static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string AppName = Assembly.GetEntryAssembly().GetName().Name;

        public static readonly FileInfo MedalPath = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Medal\\Medal.exe"));

        public App() {
            Config.Load();

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            DispatcherUnhandledException += ExceptionDialog.UnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e) {
            new MainWindow().Show();

            if (Config.Settings.UpdateChecker)
                GitHub.CheckVersion("Dyvinia", "NoMedal");

            if (!MedalPath.Exists) {
                MessageBoxDialog.Show("Cannot locate Medal", AppName, MessageBoxButton.OK, DialogSound.Error);
                Current.Shutdown();
            }

            foreach (Process existingProcess in Process.GetProcessesByName("NoMedal"))
                if (existingProcess.Id != Environment.ProcessId)
                    existingProcess.Kill();
        }
    }
}
