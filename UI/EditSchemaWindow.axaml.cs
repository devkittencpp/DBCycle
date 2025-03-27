using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;

namespace DBCycle
{
    public partial class EditSchemaWindow : Window
    {
        // The available field types for the dropdown.
        public List<string> AvailableTypes { get; } = new List<string>
        {
            "byte", "short", "int", "uint", "long", "float", "string"
        };

        private TableDefinition _tableDefinition;

        public EditSchemaWindow(TableDefinition tableDefinition)
{
    InitializeComponent();
    _tableDefinition = tableDefinition;
    // Set the DataContext to this window so that AvailableTypes is available
    DataContext = this;
    FieldsDataGrid.ItemsSource = _tableDefinition.Fields;
}

        private void AddFieldButton_Click(object? sender, RoutedEventArgs e)
        {
            // Create a new field with default values.
            var newField = new FieldDefinition
            {
                Name = "NewField",
                Type = "int",
                IsIndex = false,
                ArraySize = null
            };
            _tableDefinition.Fields.Add(newField);
            // Refresh the DataGrid ItemsSource.
            FieldsDataGrid.ItemsSource = null;
            FieldsDataGrid.ItemsSource = _tableDefinition.Fields;
        }

        private void RemoveFieldButton_Click(object? sender, RoutedEventArgs e)
        {
            if (FieldsDataGrid.SelectedItem is FieldDefinition selectedField)
            {
                _tableDefinition.Fields.Remove(selectedField);
                // Refresh the DataGrid ItemsSource.
                FieldsDataGrid.ItemsSource = null;
                FieldsDataGrid.ItemsSource = _tableDefinition.Fields;
            }
        }

        private void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            // Close the window returning true.
            Close(true);
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            // Close the window returning false.
            Close(false);
        }
    }
}
