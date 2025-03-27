using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;
using System.Collections.Generic;
using System;
using System.Data;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DBCycle
{
    public partial class SingleFileWindow : Window
    {
        private readonly string _schemaPath = "schema.json";
        // Store the selected file's name without extension.
        private string? _selectedFileName;

        public SingleFileWindow()
        {
            InitializeComponent();
            SelectFileButton.Click += SelectFileButton_Click;
            UpdateSchemaButton.Click += UpdateSchemaButton_Click;
        }

        private async void UpdateSchemaButton_Click(object? sender, RoutedEventArgs e)
        {
            // Ensure that a file was selected before updating the schema.
            if (string.IsNullOrEmpty(_selectedFileName))
            {
                ErrorTextBlock.Text = "No file selected. Please select a file first.";
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
                // If the file doesn't exist, initialize a new definition.
                dbDef = new DbDefinition { Tables = new List<TableDefinition>() };
            }

            // Use the selected file's name (without extension) as the target table name.
            string targetTableName = _selectedFileName;
            TableDefinition tableDef = dbDef.Tables
                .FirstOrDefault(t => t.Name.Equals(targetTableName, StringComparison.OrdinalIgnoreCase));

            if (tableDef == null)
            {
                ErrorTextBlock.Text = "No matching schema found.";
                return;
            }

            // Create and show the EditSchemaWindow.
            var editWindow = new EditSchemaWindow(tableDef);
            bool? result = await editWindow.ShowDialog<bool?>(this);

            if (result == true)
            {
                // Update the specific record in the schema.
                int index = dbDef.Tables.FindIndex(t => t.Name.Equals(tableDef.Name, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    // Replace the table definition with the updated version.
                    dbDef.Tables[index] = tableDef;
                }
                else
                {
                    // Optionally, add the table definition if it wasn't present.
                    dbDef.Tables.Add(tableDef);
                }

                // Write the full schema back to the file.
                string updatedJson = JsonConvert.SerializeObject(dbDef, Formatting.Indented);
                File.WriteAllText(_schemaPath, updatedJson);
            }
        }

        private async void SelectFileButton_Click(object? sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Select a DBC/DB2 file",
                Filters = { new FileDialogFilter { Name = "DBC/DB2 Files", Extensions = { "dbc", "db2" } } }
            };

            string[]? result = await ofd.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                string filePath = result[0];
                try
                {
                    ErrorTextBlock.Text = string.Empty;

                    // Extract the file name without extension and store it.
                    _selectedFileName = Path.GetFileNameWithoutExtension(filePath);

                    // Display file name.
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
                        return;
                    }

                    if (dt.Rows.Count == 0)
                    {
                        ErrorTextBlock.Text = "No records found in file!";
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

                    // Convert DataTable to List of Dictionary<string, object>
                    var data = dt.AsEnumerable()
                                 .Select(row => dt.Columns.Cast<DataColumn>()
                                     .ToDictionary(col => col.ColumnName, col => row[col]))
                                 .ToList();
                    FileDataGrid.ItemsSource = data;

                    // Display file info (Row count and Column count)
                    FileInfoTextBlock.Text = $"Rows: {dt.Rows.Count}, Columns: {dt.Columns.Count}";
                }
                catch (Exception ex)
                {
                    ErrorTextBlock.Text = $"Error reading file: {ex.Message}";
                }
            }
        }
    }
}
