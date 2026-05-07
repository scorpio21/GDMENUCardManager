using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GDMENUCardManager.Core;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GDMENUCardManager
{
    public class ManualUpdateDialog : Window
    {
        public string LatestTag { get; private set; }

        public ManualUpdateDialog()
        {
            InitializeComponent();
        }

        private string GetString(string key)
        {
            if (Application.Current.TryFindResource(key, out object res) && res is string s)
                return s;
            return key;
        }

        public ManualUpdateDialog(string latestTag, string latestVersion, ManualUpdateReason reason)
        {
            InitializeComponent();
            LatestTag = latestTag;

            var reasonText = this.FindControl<TextBlock>("ReasonText");
            if (reason == ManualUpdateReason.UnsupportedPlatform)
                reasonText.Text = string.Format(GetString("StringUpdateNotSupportedPlatform"), latestVersion);
            else
                reasonText.Text = string.Format(GetString("StringUpdateNotSupportedRelease"), latestVersion);

            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    Close();
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateAvailableDialog.SaveSkippedVersion(LatestTag);
            Close();
        }

        private void ReleasesLink_Click(object sender, RoutedEventArgs e)
        {
            var url = "https://github.com/DerekPascarella/openMenu-Virtual-Folder-Bundle/releases";
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start("xdg-open", url);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", url);
            }
            catch { }
        }
    }
}
