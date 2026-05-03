using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

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
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();

            ChangeLanguage("en-US"); // Default language

            base.OnFrameworkInitializationCompleted();
        }

        public static void ChangeLanguage(string languageCode)
        {
            var uri = new System.Uri($"avares://GDMENUCardManager.AvaloniaUI/Assets/Languages/{languageCode}.axaml");
            var dict = (Avalonia.Controls.IResourceDictionary)Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(uri);

            var appResources = (Avalonia.Controls.ResourceDictionary)Current.Resources;
            appResources.MergedDictionaries.Clear();
            appResources.MergedDictionaries.Add(dict);
        }
    }
}
