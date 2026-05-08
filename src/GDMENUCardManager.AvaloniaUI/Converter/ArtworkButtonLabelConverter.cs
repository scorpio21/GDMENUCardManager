using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace GDMENUCardManager.Converter
{
    public class ArtworkButtonLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool hasArtwork)
            {
                if (parameter is string param && param == "tooltip")
                    return hasArtwork ? MainWindow.GetString("StringEditArtworkTooltip") : MainWindow.GetString("StringAssignArtworkTooltip");
                return hasArtwork ? MainWindow.GetString("StringManage") : MainWindow.GetString("StringAssign");
            }
            return MainWindow.GetString("StringAssign");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
