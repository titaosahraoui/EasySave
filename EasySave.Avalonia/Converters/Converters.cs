using Avalonia.Data;
using Avalonia.Data.Converters;
using BackupApp.Models;
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

    public class EnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && Enum.TryParse(targetType, str, out var result))
            {
                return result;
            }
            return value;
        }
    }

    public class BackupTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BackupType type)
            {
                return type == BackupType.Full ? "#3498db" : "#2ecc71"; // Blue for Full, Green for Differential
            }
            return "#95a5a6"; // Default gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status switch
                {
                    "Completed" => "#2ecc71", // Green
                    "Active" => "#f39c12",    // Orange
                    "Error" => "#e74c3c",     // Red
                    _ => "#95a5a6"            // Gray
                };
            }
            return "#95a5a6"; // Default gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public enum BooleanOperation
    {
        Inverse
    }
}