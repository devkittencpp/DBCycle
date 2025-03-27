using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace DBCycle
{
    public partial class SettingsWindow : Window
    {
        private Config _config;
        private readonly string _configPath = "config.json";

        public SettingsWindow()
        {
            InitializeComponent();
            LoadConfig();
        }

        private void LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                _config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
            else
            {
                _config = new Config()
                {
                    DbcFileDirectory = "",
                    JsonDefinitionFile = "schema.json",
                    DbcConnectionString = "server=localhost;database=dbc;uid=root;pwd=Kittens123",
                    Db2ConnectionString = "server=localhost;database=db2;uid=root;pwd=Kittens123",
                    ExportPath = ""
                };

                // Save default config to file
                string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }

            DbcConnectionStringTextBox.Text = _config.DbcConnectionString;
            Db2ConnectionStringTextBox.Text = _config.Db2ConnectionString;
            DbcFileDirectoryTextBox.Text = _config.DbcFileDirectory;
            ExportPathTextBox.Text = _config.ExportPath;
        }

        private async void TestDbcConnection_Click(object sender, RoutedEventArgs e)
        {
            string connStr = DbcConnectionStringTextBox.Text;
            DbcConnectionStatus.Text = "Testing...";
            DbcConnectionStatus.Foreground = Brushes.Gray;

            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    DbcConnectionStatus.Text = "Connected";
                    DbcConnectionStatus.Foreground = Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                DbcConnectionStatus.Text = "Failed: " + ex.Message;
                DbcConnectionStatus.Foreground = Brushes.Red;
            }
        }

        private async void TestDb2Connection_Click(object sender, RoutedEventArgs e)
        {
            string connStr = Db2ConnectionStringTextBox.Text;
            Db2ConnectionStatus.Text = "Testing...";
            Db2ConnectionStatus.Foreground = Brushes.Gray;

            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    Db2ConnectionStatus.Text = "Connected";
                    Db2ConnectionStatus.Foreground = Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                Db2ConnectionStatus.Text = "Failed: " + ex.Message;
                Db2ConnectionStatus.Foreground = Brushes.Red;
            }
        }

        private async void BrowseDbcFileDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            string? result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result))
            {
                DbcFileDirectoryTextBox.Text = result;
            }
        }

        private async void BrowseExportPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            string? result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result))
            {
                ExportPathTextBox.Text = result;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _config.DbcConnectionString = DbcConnectionStringTextBox.Text;
            _config.Db2ConnectionString = Db2ConnectionStringTextBox.Text;
            _config.DbcFileDirectory = DbcFileDirectoryTextBox.Text;
            _config.ExportPath = ExportPathTextBox.Text;

            string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(_configPath, json);
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
