using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace BackupApp.Avalonia.Converters
{
    public static class Converters
    {
        public static readonly IValueConverter TypeToColorConverter = new TypeToColorConverter();
        public static readonly IValueConverter NullToBoolConverter = new NullToBoolConverter();
        public static readonly IValueConverter NullableDateTimeConverter = new NullableDateTimeToStringConverter();
    }

    public class TypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string type)
                {
                    return type.ToUpperInvariant() switch
                    {
                        "FULL" => "#27ae60",    // Green
                        "DIFFERENTIAL" => "#f39c12", // Orange
                        _ => "#3498db"           // Blue (default)
                    };
                }
                return "#3498db"; // Default color
            }
            catch
            {
                return BindingOperations.DoNothing;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("One-way conversion only");
        }
    }

    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                bool result = value != null;
                if (parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true)
                {
                    result = !result;
                }
                return result;
            }
            catch
            {
                return BindingOperations.DoNothing;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("One-way conversion only");
        }
    }

    public class NullableDateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is DateTime dateTime)
                {
                    string format = parameter as string ?? "g";
                    return dateTime.ToString(format, culture ?? CultureInfo.CurrentCulture);
                }
                return parameter as string ?? "Never";
            }
            catch
            {
                return BindingOperations.DoNothing;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("One-way conversion only");
        }
    }
}