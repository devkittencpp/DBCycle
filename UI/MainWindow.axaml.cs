using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DBCycle;
using System.Text;
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
        private TextBlock _logConsole;
        private CheckBox _developerModeCheckBox;
        private readonly string _configPath = "config.json";

        public MainWindow()
        {
            InitializeComponent();
            _logConsole = this.FindControl<TextBlock>("LogConsole");
            _developerModeCheckBox = this.FindControl<CheckBox>("DeveloperModeCheckBox");
            EnsureConfigFile();
        }

        private void SingleFile_Click(object? sender, RoutedEventArgs e)
        {
            var singleFileWindow = new SingleFileWindow();
            singleFileWindow.ShowDialog(this);
        }

        private void EnsureConfigFile()
        {
            if (!File.Exists(_configPath))
            {
                Config defaultConfig = new Config()
                {
                    DbcFileDirectory = "",
                    JsonDefinitionFile = "schema.json",
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
            // Capture the Developer Mode state on the UI thread
            bool developerModeEnabled = _developerModeCheckBox?.IsChecked == true;
            await Task.Run(() => ImportData(developerModeEnabled));
        }

        private void ImportData(bool developerModeEnabled)
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

                // Use the captured developer mode flag
                if (developerModeEnabled)
                {
                    UpdateLog("Developer Mode enabled: resetting database.");
                    ResetAllDatabases(config);
                }

                // Instantiate the importer with both DBC and DB2 connection strings.
                var importer = new DBCImporter(
                    config.JsonDefinitionFile,
                    config.DbcFileDirectory,
                    config.DbcConnectionString,
                    config.Db2ConnectionString,
                    developerModeEnabled,
                    UpdateLog);
                importer.ImportAll();
                UpdateLog("Import complete.");
            }
            catch (Exception ex)
            {
                UpdateLog("Error: " + ex.Message);
            }
        }

        // Resets a single database given its connection string and a label.
        private void ResetDatabase(string connectionString, string label)
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder(connectionString);
                string dbName = builder.Database;
                // Remove the database from the connection string
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

        // Resets both the DBC and DB2 databases.
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

                // Instantiate the exporter with both DBC and DB2 connection strings.
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
            foreach (var inline in _logConsole.Inlines)
            {
                if (inline is Run run)
                {
                    stringBuilder.Append(run.Text);
                }
            }
            return stringBuilder.ToString();
        }
    }
}
