using DyviniaUtils.Dialogs;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
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
            public int Action { get; set; } = 0;
        }
        public ObservableCollection<Program> Programs = new();

        public MainWindow() {
            InitializeComponent();

            MouseDown += (_, _) => FocusManager.SetFocusedElement(this, this);
            StateChanged += (_, _) => {
                if (WindowState == WindowState.Minimized)
                    Hide();
            };
            ProgramListBox.ItemsSource = Programs;

            SetupContextMenu();
            LoadProgramsConfig();

            Thread checkThread = new(CheckThread) { IsBackground = true };
            checkThread.Start();
        }

        private void SetupContextMenu() {
            TaskbarIcon tbi = new() {
                IconSource = Icon,
                ToolTipText = "NoMedal",
                MenuActivation = PopupActivationMode.LeftOrRightClick
            };
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
            tbi.ContextMenu = contextMenu;
        }

        public void LoadProgramsConfig() {
            Programs.Clear();
            foreach (ProgramConfig pConfig in Config.Settings.Programs) {
                string name = FileVersionInfo.GetVersionInfo(pConfig.Path).ProductName;
                ImageSource iconImage = Icon;
                try {
                    Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(pConfig.Path);
                    iconImage = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                catch {
                    
                }

                Program program = new() { Name = name, Icon = iconImage, Path = pConfig.Path, Action = pConfig.Action };
                Programs.Add(program);
            }
        }
        
        public void CheckThread() {
            while (true) {
                Process[] processes = Programs.SelectMany(p => Process.GetProcessesByName(Path.GetFileNameWithoutExtension(p.Path))).ToArray();

                if (processes.Length != 0) {
                    try {
                        foreach (Process process in processes) {
                            foreach (Process medalProcess in Process.GetProcessesByName("Medal"))
                                medalProcess.Kill();
                            process.WaitForExit();

                            Process medal = new();
                            medal.StartInfo.FileName = App.MedalPath.FullName;
                            medal.StartInfo.WorkingDirectory = App.MedalPath.DirectoryName;
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
                if (!Config.Settings.Programs.Any(p => p.Path == dialog.FileName)) {
                    Config.Settings.Programs.Add(new ProgramConfig() { Path = dialog.FileName, Action = 0 });
                    Config.Save();
                    LoadProgramsConfig();
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
                ProgramConfig pConfig = Config.Settings.Programs.Find(p => p.Path == program.Path);
                Config.Settings.Programs.Remove(pConfig);
                Config.Save();
                LoadProgramsConfig();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if (e.Key == Key.F12)
                Process.Start("explorer.exe", $"/select, {Config.FilePath}");
        }
    }
}
