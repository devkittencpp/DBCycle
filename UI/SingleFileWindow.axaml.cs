using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.Data;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace DBCycle
{
    // Helper class to hold file info for display.
    public class FileItem
    {
        public string DisplayName { get; set; }
        public string FullPath { get; set; }
        public override string ToString() => DisplayName;
    }

    public partial class SingleFileWindow : Window
    {
        private readonly string _schemaPath = "schema_18414.json";
        private string? _selectedFileName;
        private string? _selectedFilePath;
        private List<FileItem> _allFiles = new List<FileItem>();
        // ObservableCollection for smooth filtering updates.
        private ObservableCollection<FileItem> _filteredFiles = new ObservableCollection<FileItem>();
        private Config _config;

        public SingleFileWindow()
        {
            InitializeComponent();
            LoadConfigAndPopulateFiles();
            FileSelectorComboBox.SelectionChanged += FileSelectorComboBox_SelectionChanged;
            FileSelectorComboBox.AddHandler(TextBox.TextChangedEvent, FileSelectorComboBox_TextChanged, RoutingStrategies.Bubble);
            UpdateSchemaButton.Click += UpdateSchemaButton_Click;
        }

        private async Task FlashErrorTextBlockAsync()
        {
            if (ErrorTextBlock != null)
            {
                Color originalColor;
                if (ErrorTextBlock.Foreground is ISolidColorBrush solidBrush)
                {
                    originalColor = solidBrush.Color;
                }
                else
                {
                    originalColor = Colors.White;
                }

                var originalSolidBrush = new SolidColorBrush(originalColor);

                for (int i = 0; i < 3; i++)
                {
                    ErrorTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                    await Task.Delay(300);
                    ErrorTextBlock.Foreground = originalSolidBrush;
                    await Task.Delay(300);
                }
            }
        }

        /// <summary>
        /// Loads the config file and populates the ComboBox with eligible files.
        /// </summary>
        private void LoadConfigAndPopulateFiles()
        {
            // Load configuration (assumes config.json exists in the same folder)
            if (File.Exists("config.json"))
            {
                string json = File.ReadAllText("config.json");
                _config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
            else
            {
                // Create a default config if none exists.
                _config = new Config()
                {
                    DbcFileDirectory = "",
                    JsonDefinitionFile = "schema_18414.json",
                    DbcConnectionString = "server=localhost;database=dbc;uid=root;pwd=Kittens123",
                    Db2ConnectionString = "server=localhost;database=db2;uid=root;pwd=Kittens123",
                    ExportPath = ""
                };
            }

            // Load valid table names from the schema.
            var validTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(_config.JsonDefinitionFile))
            {
                string schemaJson = File.ReadAllText(_config.JsonDefinitionFile);
                DbDefinition dbDef = JsonConvert.DeserializeObject<DbDefinition>(schemaJson);
                if (dbDef?.Tables != null)
                {
                    foreach (var table in dbDef.Tables)
                    {
                        validTableNames.Add(table.Name);
                    }
                }
            }

            _allFiles.Clear();
            if (!string.IsNullOrEmpty(_config.DbcFileDirectory) && Directory.Exists(_config.DbcFileDirectory))
            {
                var files = Directory.GetFiles(_config.DbcFileDirectory)
                    .Where(f => f.EndsWith(".dbc", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".db2", StringComparison.OrdinalIgnoreCase));
                foreach (var file in files)
                {
                    // Get the file name without extension.
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                    // Only add the file if it exists in the schema.
                    if (validTableNames.Contains(fileNameWithoutExtension))
                    {
                        _allFiles.Add(new FileItem
                        {
                            DisplayName = Path.GetFileName(file),
                            FullPath = file
                        });
                    }
                }
            }
            // Initialize the filtered collection with all files.
            _filteredFiles.Clear();
            foreach (var item in _allFiles)
            {
                _filteredFiles.Add(item);
            }
            FileSelectorComboBox.ItemsSource = _filteredFiles;
        }


        /// <summary>
        /// Handles filtering the ComboBox items as the user types.
        /// </summary>
        private void FileSelectorComboBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                string filter = tb.Text.ToLowerInvariant() ?? "";
                var filtered = _allFiles.Where(f => f.DisplayName.ToLowerInvariant().Contains(filter)).ToList();
                _filteredFiles.Clear();
                foreach (var item in filtered)
                {
                    _filteredFiles.Add(item);
                }
                FileSelectorComboBox.IsDropDownOpen = _filteredFiles.Any();
            }
        }

        /// <summary>
        /// When a file is selected from the ComboBox, load it.
        /// </summary>
        private async void FileSelectorComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (FileSelectorComboBox.SelectedItem is FileItem item)
            {
                _selectedFilePath = item.FullPath;
                await LoadFileAsync(_selectedFilePath);
            }
        }

        /// <summary>
        /// Extracted method to load the file, import data and update UI.
        /// </summary>
        private async Task LoadFileAsync(string filePath)
        {
            try
            {
                ErrorTextBlock.Text = string.Empty;
                _selectedFileName = Path.GetFileNameWithoutExtension(filePath);
                FileNameTextBlock.Text = $"Selected File: {Path.GetFileName(filePath)}";

                TableDefinition tableDef = null;
                if (File.Exists(_schemaPath))
                {
                    string schemaJson = File.ReadAllText(_schemaPath);
                    DbDefinition dbDef = JsonConvert.DeserializeObject<DbDefinition>(schemaJson);
                    tableDef = dbDef.Tables
                        .FirstOrDefault(t => t.Name.Equals(_selectedFileName, StringComparison.OrdinalIgnoreCase));
                }

                SingleFileImporter importer = new SingleFileImporter();
                DataTable dt;

                if (tableDef != null)
                {
                    dt = importer.ImportFile(filePath, tableDef);
                }
                else
                {
                    ErrorTextBlock.Text = "No matching schema found for this file.";
                    await FlashErrorTextBlockAsync();
                    return;
                }

                if (dt.Rows.Count == 0)
                {
                    ErrorTextBlock.Text = "No records found in file!";
                    await FlashErrorTextBlockAsync();
                    return;
                }

                FileDataGrid.Columns.Clear();

                foreach (DataColumn col in dt.Columns)
                {
                    var textColumn = new DataGridTextColumn
                    {
                        Header = col.ColumnName,
                        Binding = new Binding($"[{col.ColumnName}]"),
                        SortMemberPath = col.ColumnName
                    };
                    FileDataGrid.Columns.Add(textColumn);
                }

                var data = dt.AsEnumerable()
                             .Select(row => dt.Columns.Cast<DataColumn>()
                                 .ToDictionary(col => col.ColumnName, col => row[col]))
                             .ToList();
                FileDataGrid.ItemsSource = data;

                FileInfoTextBlock.Text = $"Rows: {dt.Rows.Count}, Columns: {dt.Columns.Count}";
            }
            catch (Exception ex)
            {
                ErrorTextBlock.Text = $"Error reading file: {ex.Message}";
                await FlashErrorTextBlockAsync();
            }
        }

        private async void UpdateSchemaButton_Click(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFileName))
            {
                ErrorTextBlock.Text = "No file selected. Please select a file first.";
                await FlashErrorTextBlockAsync();
                return;
            }

            DbDefinition dbDef = null;
            if (File.Exists(_schemaPath))
            {
                string schemaJson = File.ReadAllText(_schemaPath);
                dbDef = JsonConvert.DeserializeObject<DbDefinition>(schemaJson);
            }
            else
            {
                dbDef = new DbDefinition { Tables = new List<TableDefinition>() };
            }

            string targetTableName = _selectedFileName;
            TableDefinition tableDef = dbDef.Tables
                .FirstOrDefault(t => t.Name.Equals(targetTableName, StringComparison.OrdinalIgnoreCase));

            if (tableDef == null)
            {
                ErrorTextBlock.Text = "No matching schema found.";
                await FlashErrorTextBlockAsync();
                return;
            }

            var editWindow = new EditSchemaWindow(tableDef);
            bool? result = await editWindow.ShowDialog<bool?>(this);

            if (result == true)
            {
                int index = dbDef.Tables.FindIndex(t => t.Name.Equals(tableDef.Name, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    dbDef.Tables[index] = tableDef;
                }
                else
                {
                    dbDef.Tables.Add(tableDef);
                }

                string updatedJson = JsonConvert.SerializeObject(dbDef, Formatting.Indented);
                File.WriteAllText(_schemaPath, updatedJson);

                if (!string.IsNullOrEmpty(_selectedFilePath))
                {
                    await LoadFileAsync(_selectedFilePath);
                }
            }
        }
    }
}
