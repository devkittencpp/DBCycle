using System;
using System.Data;
using System.Globalization;
using Avalonia.Data.Converters;

public class DataRowViewValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DataRowView rowView && parameter is string columnName)
        {
            return rowView.Row[columnName]?.ToString() ?? string.Empty;
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
