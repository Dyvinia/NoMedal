using DyviniaUtils.Dialogs;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoMedal {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        public class Program {
            public string Name { get; set; }
            public ImageSource Icon { get; set; }
            public string Path { get; set; }

            public Program(string path) {
                string name = FileVersionInfo.GetVersionInfo(path).ProductName;
                if (String.IsNullOrEmpty(name))
                    name = System.IO.Path.GetFileNameWithoutExtension(path);

                ImageSource iconImage = Icon;
                try {
                    Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                    iconImage = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                catch {

                }

                Name = name;
                Icon = iconImage;
                Path = path;
            }
        }
        public ObservableCollection<Program> Programs = new();

        public TaskbarIcon TrayIcon = new() {
            ToolTipText = "NoMedal",
            MenuActivation = PopupActivationMode.LeftOrRightClick
        };

        public void ShowNotification(string message) {
            if (Config.Settings.ShowNotifications)
                TrayIcon.ShowBalloonTip(null, message, BalloonIcon.None);
        }

        public void ShowNotification(string title, string message, Program program) {
            if (Config.Settings.ShowNotifications)
                TrayIcon.ShowBalloonTip(title, message, System.Drawing.Icon.ExtractAssociatedIcon(program.Path), true);
        }


        public MainWindow() {
            InitializeComponent();

            MouseDown += (_, _) => FocusManager.SetFocusedElement(this, this);
            StateChanged += (_, _) => {
                if (WindowState == WindowState.Minimized) {
                    Hide();
                    ShowNotification("Minimized to Tray");
                }
            };

            CheckForStartup();
            StartupCheckBox.Checked += (_, _) => StartOnStartup();
            StartupCheckBox.Unchecked += (_, _) => File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "NoMedal.lnk"));

            ProgramListBox.ItemsSource = Programs;

            SetupContextMenu();
            RefreshPrograms();

            Thread checkThread = new(CheckThread) { IsBackground = true };
            checkThread.Start();
        }

        private void SetupContextMenu() {
            TrayIcon.IconSource = Icon;
            ContextMenu contextMenu = new();
            MenuItem menuShow = new() { Header = "Show NoMedal" };
            MenuItem menuExit = new() { Header = "Exit NoMedal" };
            menuShow.Click += (_, _) => {
                Show();
                WindowState = WindowState.Normal;
            };
            menuExit.Click += (_, _) => {
                Application.Current.Shutdown();
            };

            contextMenu.Items.Add(menuShow);
            contextMenu.Items.Add(menuExit);
            TrayIcon.ContextMenu = contextMenu;
        }

        public void RefreshPrograms() {
            Programs.Clear();
            foreach (string path in Config.Settings.Programs)
                Programs.Add(new Program(path));
        }

        public void CheckThread() {
            while (true) {
                Process[] processes = Programs.SelectMany(p => Process.GetProcessesByName(Path.GetFileNameWithoutExtension(p.Path))).ToArray();

                if (processes.Length != 0) {
                    try {
                        foreach (Process process in processes) {
                            Program program = Programs.Where(p => Path.GetFileNameWithoutExtension(p.Path) == process.ProcessName).FirstOrDefault();
                            ShowNotification("Closing Medal", $"{program.Name} Started.", program);
                            foreach (Process medalProcess in Process.GetProcessesByName("Medal"))
                                medalProcess.Kill();
                            process.WaitForExit();

                            ShowNotification("Starting Medal");
                            Process medal = new();
                            medal.StartInfo.FileName = App.MedalPath.FullName;
                            medal.StartInfo.WorkingDirectory = App.MedalPath.DirectoryName;
                            medal.StartInfo.UseShellExecute = true;
                            medal.Start();
                        }
                    }
                    catch (Exception ex) {
                        Task.Run(() => {
                            ExceptionDialog.Show(ex, App.AppName);
                        });
                    }
                }
                else Thread.Sleep(5000);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new() {
                Title = "Select Program",
                Filter = "Executable (*.exe) |*.exe",
                FilterIndex = 2
            };
            if (dialog.ShowDialog() == true) {
                if (!Config.Settings.Programs.Any(p => p == dialog.FileName)) {
                    Config.Settings.Programs.Add(dialog.FileName);
                    Config.Save();
                    RefreshPrograms();
                }
                else {
                    MessageBoxDialog.Show("Program already exists", App.AppName, MessageBoxButton.OK, DialogSound.Error);
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e) {
            Program program;
            if (sender is Program)
                program = sender as Program;
            else
                program = ((Button)sender).DataContext as Program;

            MessageBoxResult result = MessageBoxDialog.Show("Remove Program from NoMedal?", App.AppName, MessageBoxButton.YesNo, DialogSound.Notify);
            if (result == MessageBoxResult.Yes) {
                string pConfig = Config.Settings.Programs.Find(p => p == program.Path);
                Config.Settings.Programs.Remove(pConfig);
                Config.Save();
                RefreshPrograms();
            }
        }

        private void StartOnStartup() {
            IWshRuntimeLibrary.WshShell wshShell = new();
            IWshRuntimeLibrary.IWshShortcut shortcut = wshShell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "NoMedal.lnk"));
            shortcut.TargetPath = Environment.ProcessPath;
            shortcut.WorkingDirectory = Environment.CurrentDirectory;
            shortcut.Save();
        }

        private void CheckForStartup() {
            if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "NoMedal.lnk"))) {
                StartupCheckBox.IsChecked = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if (e.Key == Key.F12)
                Process.Start("explorer.exe", $"/select, {Config.FilePath}");

            if (e.Key == Key.F1)
                StartOnStartup();
        }
    }
}
