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
        private bool _isPageAccessible = true;

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

                // --- PART 2: LOAD OR INIT CONFIGURATION ---
                
                myWebView.WebMessageReceived += WebView_WebMessageReceived;
                myWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

                string settingsFilePath = Path.Combine(exeDirectory, "settings.json");

                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath);
                    string dashboardUrl = ParseDashboardUrl(json);

                    if (!string.IsNullOrEmpty(dashboardUrl))
                    {
                        NavigateToDashboard(dashboardUrl);
                    }
                    else
                    {
                        NavigateToSetup();
                    }
                }
                else
                {
                    NavigateToSetup();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing WebView2: {ex.Message}", "WebView2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string ParseDashboardUrl(string json)
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(json, @"""DashboardUrl""\s*:\s*""([^""]+)""");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse settings.json: {ex.Message}");
            }
            return null;
        }

        private void NavigateToSetup()
        {
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string setupPath = Path.Combine(exeDirectory, "setup.html");
            if (File.Exists(setupPath))
            {
                myWebView.CoreWebView2.Navigate(new Uri(setupPath).AbsoluteUri);
            }
            else
            {
                MessageBox.Show("Error: 'setup.html' was not found in the application directory.", "Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToDashboard(string dashboardUrl)
        {
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            Uri dashboardUri;

            try
            {
                if (Uri.TryCreate(dashboardUrl, UriKind.Absolute, out dashboardUri))
                {
                    // Valid absolute URI
                }
                else
                {
                    // Relative path
                    string fullPath = Path.Combine(exeDirectory, dashboardUrl);
                    dashboardUri = new Uri(fullPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: '{dashboardUrl}' is not a valid path or URL.\n\nError: {ex.Message}",
                                "Invalid Path",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                NavigateToSetup();
                return;
            }

            if (dashboardUri.IsFile && !File.Exists(dashboardUri.LocalPath))
            {
                MessageBox.Show($"Error: The file specified in settings.json was not found.\n\nPath: {dashboardUri.LocalPath}",
                                "File Not Found",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                NavigateToSetup();
                return;
            }

            myWebView.CoreWebView2.Navigate(dashboardUri.AbsoluteUri);
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string messageJson = e.TryGetWebMessageAsString();

                var actionMatch = System.Text.RegularExpressions.Regex.Match(messageJson, @"""action""\s*:\s*""([^""]+)""");
                var urlMatch = System.Text.RegularExpressions.Regex.Match(messageJson, @"""dashboardUrl""\s*:\s*""([^""]+)""");

                if (actionMatch.Success && actionMatch.Groups[1].Value == "saveSettings" && urlMatch.Success)
                {
                    string dashboardUrl = urlMatch.Groups[1].Value;

                    string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string settingsFilePath = Path.Combine(exeDirectory, "settings.json");

                    string escapedUrl = dashboardUrl.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    string jsonContent = $"{{\r\n  \"DashboardUrl\": \"{escapedUrl}\"\r\n}}";

                    File.WriteAllText(settingsFilePath, jsonContent);

                    NavigateToDashboard(dashboardUrl);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Save Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            myWebView?.Dispose();

            // If the target page was not accessible when closing, delete settings.json
            // so that the next run falls back to setup.html.
            if (!_isPageAccessible)
            {
                try
                {
                    string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string settingsFilePath = Path.Combine(exeDirectory, "settings.json");
                    if (File.Exists(settingsFilePath))
                    {
                        File.Delete(settingsFilePath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete settings.json on closing: {ex.Message}");
                }
            }

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

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                string currentUri = myWebView.CoreWebView2.Source;
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string setupUri = new Uri(Path.Combine(exeDirectory, "setup.html")).AbsoluteUri;

                // Do not count the setup page itself as an accessibility failure
                if (currentUri.Equals(setupUri, StringComparison.OrdinalIgnoreCase))
                {
                    _isPageAccessible = true;
                    return;
                }

                if (!e.IsSuccess)
                {
                    _isPageAccessible = false;
                }
                else
                {
                    _isPageAccessible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in NavigationCompleted: {ex.Message}");
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
        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    this.WindowState = WindowState.Normal;
                }
                else
                {
                    this.WindowState = WindowState.Maximized;
                }
            }
        }
    }
}