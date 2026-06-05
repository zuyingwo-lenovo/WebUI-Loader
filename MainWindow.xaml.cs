using System;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.Windows.Input;
using System.Configuration;
using System.Reflection;
namespace WebUI
{
    public partial class MainWindow : Window
    {
        private string _userDataFolder;

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;
            InitializeWebViewAsync();
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                // --- PART 1: WebView2 CACHE FOLDER (This is correct, leave it) ---

                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string appName = Assembly.GetEntryAssembly().GetName().Name;
                string dataFolderName = $"{appName}_123456781234123412341234567890AB";
                _userDataFolder = Path.Combine(exeDirectory, dataFolderName);

                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: _userDataFolder, // This uses the temporary cache folder
                    options: null);

                await myWebView.EnsureCoreWebView2Async(environment);

                // --- PART 2: LOAD CUSTOM CONFIG FILE (This is the new part) ---

                // 1. Define the path to your custom config file
                string configFilePath = Path.Combine(exeDirectory, $"{appName}.exe.config");

                if (!File.Exists(configFilePath))
                {
                    MessageBox.Show("Error: 'settings.config' not found.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 2. Map this file as the configuration
                ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap();
                fileMap.ExeConfigFilename = configFilePath;

                // 3. Open the mapped file
                Configuration config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);

                // 4. Read the setting from the file
                string dashboardUrl = config.AppSettings.Settings["DashboardUrl"]?.Value;

                // --- END OF NEW PART ---

                if (string.IsNullOrEmpty(dashboardUrl))
                {
                    MessageBox.Show("Error: 'DashboardUrl' not found in settings.config.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Uri dashboardUri;
                try
                {
                    // 2. Try to parse the string as a URI (path)
                    dashboardUri = new Uri(dashboardUrl);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: 'DashboardUrl' is not a valid path.\n\nError: {ex.Message}",
                                    "Invalid Path",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }

                // 3. Check if it's a local file path (not http://, etc.)
                if (dashboardUri.IsFile)
                {
                    // 4. Check if the local file actually exists on disk
                    //    We use .LocalPath to get the clean file path (e.g., "C:\my file.html")
                    if (!File.Exists(dashboardUri.LocalPath))
                    {
                        //MessageBox.Show($"Error: The file specified in 'DashboardUrl' was not found.\n\nPath: {dashboardUri.LocalPath}",
                        //                "File Not Found",
                        //                MessageBoxButton.OK,
                        //                MessageBoxImage.Error);
                        //Override with the default index.html
                        dashboardUri = new Uri("index.html");
                    }
                }

                // --- END OF NEW VALIDATION BLOCK ---

                // Use the variable to navigate
                myWebView.CoreWebView2.Navigate(dashboardUri.AbsoluteUri);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing WebView2: {ex.Message}", "WebView2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            myWebView?.Dispose();
            try
            {
                if (!string.IsNullOrEmpty(_userDataFolder) && Directory.Exists(_userDataFolder))
                {
                    Directory.Delete(_userDataFolder, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not delete user data folder: {ex.Message}");
            }
        }
        // Add this method inside your MainWindow class in MainWindow.xaml.cs

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void DragBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}