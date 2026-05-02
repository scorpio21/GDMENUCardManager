using System;
using System.Linq;
using System.Windows;

namespace GDMENUCardManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static void ChangeLanguage(string languageCode)
        {
            var appResources = Current.Resources;
            var oldLang = appResources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Languages"));
            
            if (oldLang != null)
            {
                appResources.MergedDictionaries.Remove(oldLang);
            }

            var newLang = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Assets/Languages/{languageCode}.xaml")
            };
            appResources.MergedDictionaries.Add(newLang);
        }
    }
}
