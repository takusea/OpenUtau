using System;
using System.Globalization;
using System.IO;
using System.Text;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Serilog;

namespace OpenUtau.App.ViewModels {
    public class CultureNameConverter : IValueConverter {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value as CultureInfo)?.NativeName ?? string.Empty;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class EncodingNameConverter : IValueConverter {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value as Encoding)?.EncodingName ?? string.Empty;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class AvaterConverter : IValueConverter {
        public static readonly AvaterConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if(value is string avatar) {
                try {
                    using (var stream = new FileStream(avatar, FileMode.Open)) {
                        return new Bitmap(stream);
                    }
                } catch (Exception e) {
                    Log.Error(e, "Failed to load avatar.");
                    return null;
                }
            }
            return null;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
