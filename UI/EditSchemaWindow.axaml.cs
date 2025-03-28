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
            DataContext = this;
            FieldsDataGrid.ItemsSource = _tableDefinition.Fields;
        }

        private void AddFieldButton_Click(object? sender, RoutedEventArgs e)
        {
            var newField = new FieldDefinition
            {
                Name = "NewField",
                Type = "int",
                IsIndex = false,
                ArraySize = null
            };
            _tableDefinition.Fields.Add(newField);
            FieldsDataGrid.ItemsSource = null;
            FieldsDataGrid.ItemsSource = _tableDefinition.Fields;
        }

        private void RemoveFieldButton_Click(object? sender, RoutedEventArgs e)
        {
            if (FieldsDataGrid.SelectedItem is FieldDefinition selectedField)
            {
                _tableDefinition.Fields.Remove(selectedField);
                FieldsDataGrid.ItemsSource = null;
                FieldsDataGrid.ItemsSource = _tableDefinition.Fields;
            }
        }

        private void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
