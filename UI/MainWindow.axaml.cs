using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace DBCycle
{
    public class Config
    {
        public string DbcFileDirectory { get; set; }
        public string JsonDefinitionFile { get; set; }
        public string DbcConnectionString { get; set; }
        public string Db2ConnectionString { get; set; }
        public string ExportPath { get; set; }
    }

    public partial class MainWindow : Window
    {
        // Controls that are not auto-generated.
        private TextBlock _logConsole;
        private CheckBox _developerModeCheckBox;

        private readonly string _configPath = "config.json";

        // Fields to manage cancellation and pause/resume.
        private CancellationTokenSource _importCts;
        private ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true); // Initially not paused

        public MainWindow()
        {
            InitializeComponent();

            _logConsole = this.FindControl<TextBlock>("LogConsole");
            if (_logConsole == null)
                Console.WriteLine("Warning: LogConsole not found in XAML.");

            _developerModeCheckBox = this.FindControl<CheckBox>("DeveloperModeCheckBox");
            if (_developerModeCheckBox == null)
                Console.WriteLine("Warning: DeveloperModeCheckBox not found in XAML.");

            // Set the initial images for the Pause/Resume and Cancel buttons.
            var pauseResumeButton = this.FindControl<Button>("PauseResumeButton");
            if (pauseResumeButton != null)
            {
                pauseResumeButton.Content = new Image
                {
                    Source = new Bitmap("assets/btn_pause.png"),
                    Stretch = Avalonia.Media.Stretch.Uniform
                };
            }
            var cancelImportButton = this.FindControl<Button>("CancelImportButton");
            if (cancelImportButton != null)
            {
                cancelImportButton.Content = new Image
                {
                    Source = new Bitmap("assets/btn_stop.png"),
                    Stretch = Avalonia.Media.Stretch.Uniform
                };
            }

            // Initially hide overlay buttons.
            var copyLogButton = this.FindControl<Button>("CopyLogButton");
            if (copyLogButton != null)
            {
                copyLogButton.Opacity = 0;
                copyLogButton.IsHitTestVisible = false;
            }
            // The grouped buttons are inside the StackPanel; their visibility will be controlled via pointer events.

            EnsureConfigFile();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void EnsureConfigFile()
        {
            if (!File.Exists(_configPath))
            {
                Config defaultConfig = new Config()
                {
                    DbcFileDirectory = "",
                    JsonDefinitionFile = "schema_18414.json",
                    DbcConnectionString = "server=localhost;database=dbc;uid=root;pwd=Kittens123",
                    Db2ConnectionString = "server=localhost;database=db2;uid=root;pwd=Kittens123",
                    ExportPath = ""
                };
                string json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
                File.WriteAllText(_configPath, json);
                UpdateLog("Created default config file at " + _configPath);
            }
        }

        private async void Import_Click(object? sender, RoutedEventArgs e)
        {
            _importCts = new CancellationTokenSource();
            _pauseEvent.Set();
            bool developerModeEnabled = _developerModeCheckBox?.IsChecked == true;
            await Task.Run(() => ImportData(developerModeEnabled, _importCts.Token, _pauseEvent));
        }

        private void ImportData(bool developerModeEnabled, CancellationToken token, ManualResetEventSlim pauseEvent)
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    UpdateLog("Config file not found: " + _configPath);
                    return;
                }
                string configJson = File.ReadAllText(_configPath);
                Config config = JsonConvert.DeserializeObject<Config>(configJson);
                if (config == null)
                {
                    UpdateLog("Failed to parse config file.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(config.JsonDefinitionFile) || string.IsNullOrWhiteSpace(config.DbcFileDirectory))
                {
                    UpdateLog("Error: JsonDefinitionFile and/or DbcFileDirectory is not set in the config.");
                    return;
                }
                if (developerModeEnabled)
                {
                    UpdateLog("Developer Mode enabled: resetting database.");
                    ResetAllDatabases(config);
                }
                var importer = new DBCImporter(
                    config.JsonDefinitionFile,
                    config.DbcFileDirectory,
                    config.DbcConnectionString,
                    config.Db2ConnectionString,
                    developerModeEnabled,
                    UpdateLog);
                importer.ImportAll(token, pauseEvent);
                UpdateLog("Import complete.");
            }
            catch (OperationCanceledException)
            {
                UpdateLog("Import operation canceled.");
            }
            catch (Exception ex)
            {
                UpdateLog("Error: " + ex.Message);
            }
        }

        private void ResetDatabase(string connectionString, string label)
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder(connectionString);
                string dbName = builder.Database;
                builder.Database = "";
                string connectionStringWithoutDb = builder.ConnectionString;
                using (var conn = new MySqlConnection(connectionStringWithoutDb))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand($"DROP DATABASE IF EXISTS `{dbName}`;", conn))
                    {
                        cmd.ExecuteNonQuery();
                        UpdateLog($"{label} database `{dbName}` dropped.");
                    }
                    using (var cmd = new MySqlCommand($"CREATE DATABASE `{dbName}` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;", conn))
                    {
                        cmd.ExecuteNonQuery();
                        UpdateLog($"{label} database `{dbName}` created.");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateLog($"Error resetting {label} database: " + ex.Message);
            }
        }

        private void ResetAllDatabases(Config config)
        {
            ResetDatabase(config.DbcConnectionString, "DBC");
            ResetDatabase(config.Db2ConnectionString, "DB2");
        }

        private async void Export_Click(object? sender, RoutedEventArgs e)
        {
            await Task.Run(() => ExportData());
        }

        private void ExportData()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    UpdateLog("Config file not found: " + _configPath);
                    return;
                }
                string configJson = File.ReadAllText(_configPath);
                Config config = JsonConvert.DeserializeObject<Config>(configJson);
                if (config == null)
                {
                    UpdateLog("Failed to parse config file.");
                    return;
                }
                var exporter = new DBCExporter(
                    config.JsonDefinitionFile,
                    config.ExportPath,
                    config.DbcConnectionString,
                    config.Db2ConnectionString,
                    UpdateLog);
                exporter.ExportAll();
                UpdateLog("Export complete.");
            }
            catch (Exception ex)
            {
                UpdateLog("Error: " + ex.Message);
            }
        }

        private void Settings_Click(object? sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.ShowDialog(this);
        }

        private void SingleFile_Click(object? sender, RoutedEventArgs e)
        {
            var singleFileWindow = new SingleFileWindow();
            singleFileWindow.ShowDialog(this);
        }

        private void PauseResume_Click(object? sender, RoutedEventArgs e)
        {
            if (_pauseEvent.IsSet)
            {
                _pauseEvent.Reset();
                UpdateLog("Import paused.");
                if (sender is Button btn)
                {
                    btn.Content = new Image
                    {
                        Source = new Bitmap("assets/btn_play.png"),
                        Stretch = Avalonia.Media.Stretch.Uniform
                    };
                }
            }
            else
            {
                _pauseEvent.Set();
                UpdateLog("Import resumed.");
                if (sender is Button btn)
                {
                    btn.Content = new Image
                    {
                        Source = new Bitmap("assets/btn_pause.png"),
                        Stretch = Avalonia.Media.Stretch.Uniform
                    };
                }
            }
        }

        private void CancelImport_Click(object? sender, RoutedEventArgs e)
        {
            _importCts?.Cancel();
            UpdateLog("Cancel requested.");
        }

        private async void CopyLog_Click(object? sender, RoutedEventArgs e)
        {
            string logText = GetLogText();
            if (!string.IsNullOrEmpty(logText))
            {
                await this.Clipboard.SetTextAsync(logText);
                UpdateLog("Log copied to clipboard.");
            }
        }

        private string GetLogText()
        {
            var stringBuilder = new StringBuilder();
            if (_logConsole?.Inlines != null)
            {
                foreach (var inline in _logConsole.Inlines)
                {
                    if (inline is Run run)
                        stringBuilder.Append(run.Text);
                }
            }
            return stringBuilder.ToString();
        }

        private void UpdateLog(string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_logConsole != null)
                {
                    var run = new Run { Text = message + Environment.NewLine };

                    if (message.Contains("Successful", StringComparison.OrdinalIgnoreCase))
                        run.Foreground = Brushes.Green;
                    else if (message.Contains("Error", StringComparison.OrdinalIgnoreCase))
                        run.Foreground = Brushes.Red;
                    else if (message.Contains("Warning", StringComparison.OrdinalIgnoreCase))
                        run.Foreground = Brushes.Yellow;
                    else if (message.Contains("Invalid", StringComparison.OrdinalIgnoreCase))
                        run.Foreground = Brushes.Orange;
                    else if (message.Contains("Field Count", StringComparison.OrdinalIgnoreCase))
                        run.Foreground = Brushes.HotPink;
                    else if (message.Contains("Record size", StringComparison.OrdinalIgnoreCase))
                        run.Foreground = Brushes.SkyBlue;

                    _logConsole.Inlines.Add(run);

                    var scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
                    if (scrollViewer != null)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            scrollViewer.ScrollToEnd();
                        }, DispatcherPriority.Normal);
                    }
                }
                else
                {
                    Console.WriteLine("LogConsole is null, unable to update log.");
                }
            });
        }

        // Overlay button event handlers for showing/hiding controls.
        private void LogGrid_PointerEntered(object? sender, PointerEventArgs e)
        {
            var copyLogButton = this.FindControl<Button>("CopyLogButton");
            if (copyLogButton != null)
            {
                copyLogButton.Opacity = 0.5;
                copyLogButton.IsHitTestVisible = true;
            }
            // The grouped Cancel and Pause/Resume buttons are inside the right-aligned StackPanel.
            var pauseResumeButton = this.FindControl<Button>("PauseResumeButton");
            var cancelImportButton = this.FindControl<Button>("CancelImportButton");
            if (pauseResumeButton != null)
            {
                pauseResumeButton.Opacity = 0.5;
                pauseResumeButton.IsHitTestVisible = true;
            }
            if (cancelImportButton != null)
            {
                cancelImportButton.Opacity = 0.5;
                cancelImportButton.IsHitTestVisible = true;
            }
        }

        private void LogGrid_PointerExited(object? sender, PointerEventArgs e)
        {
            var copyLogButton = this.FindControl<Button>("CopyLogButton");
            if (copyLogButton != null)
            {
                copyLogButton.Opacity = 0;
                copyLogButton.IsHitTestVisible = false;
            }
            var pauseResumeButton = this.FindControl<Button>("PauseResumeButton");
            var cancelImportButton = this.FindControl<Button>("CancelImportButton");
            if (pauseResumeButton != null)
            {
                pauseResumeButton.Opacity = 0;
                pauseResumeButton.IsHitTestVisible = false;
            }
            if (cancelImportButton != null)
            {
                cancelImportButton.Opacity = 0;
                cancelImportButton.IsHitTestVisible = false;
            }
        }
    }
}
