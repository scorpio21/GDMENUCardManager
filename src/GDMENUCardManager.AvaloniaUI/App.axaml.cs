using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Configuration;

namespace GDMENUCardManager
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            string lang = ConfigurationManager.AppSettings["Language"] ?? "en-US";
            ChangeLanguage(lang);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();

            base.OnFrameworkInitializationCompleted();
        }

        public static void ChangeLanguage(string languageCode)
        {
            var uri = new System.Uri($"avares://GDMENUCardManager.AvaloniaUI/Assets/Languages/{languageCode}.axaml");
            var dict = (Avalonia.Controls.IResourceDictionary)Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(uri);

            if (Current.Resources is Avalonia.Controls.ResourceDictionary appResources)
            {
                appResources.MergedDictionaries.Clear();
                appResources.MergedDictionaries.Add(dict);
            }
        }
    }
}
