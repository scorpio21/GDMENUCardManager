using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GDMENUCardManager.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GDMENUCardManager
{
    public class SdHealthWindow : Window
    {
        private string _sdPath;
        private CancellationTokenSource _cts;

        public SdHealthWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        public SdHealthWindow(string sdPath) : this()
        {
            _sdPath = sdPath;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            var btnRun = this.FindControl<Button>("BtnRun");
            var txtStatus = this.FindControl<TextBlock>("TxtStatus");
            var progressBar = this.FindControl<ProgressBar>("ProgressBar");

            if (_cts != null)
            {
                _cts.Cancel();
                return;
            }

            if (string.IsNullOrEmpty(_sdPath))
            {
                this.FindControl<TextBlock>("TxtError").Text = "SD path is not set.";
                return;
            }

            ResetUI();
            _cts = new CancellationTokenSource();
            
            btnRun.Content = "Cancel"; 
            txtStatus.Text = (string)this.FindResource("StringRunningDiagnostics");
            txtStatus.IsVisible = true;
            progressBar.IsVisible = true;

            try
            {
                var progress = new Progress<double>(v => progressBar.Value = v);
                var status = await SdHealthManager.CheckHealthAsync(_sdPath, progress, _cts.Token);
                UpdateUI(status);
            }
            catch (Exception ex)
            {
                this.FindControl<TextBlock>("TxtError").Text = ex.Message;
            }
            finally
            {
                _cts = null;
                btnRun.Content = (string)this.FindResource("StringRunDiagnostics");
                txtStatus.IsVisible = false;
                progressBar.IsVisible = false;
            }
        }

        private void ResetUI()
        {
            this.FindControl<TextBlock>("TxtHealth").Text = "-";
            this.FindControl<TextBlock>("TxtOperational").Text = "-";
            this.FindControl<TextBlock>("TxtWriteSpeed").Text = "-";
            this.FindControl<TextBlock>("TxtReadSpeed").Text = "-";
            this.FindControl<TextBlock>("TxtIntegrity").Text = "-";
            this.FindControl<TextBlock>("TxtWarning").IsVisible = false;
            this.FindControl<TextBlock>("TxtError").Text = "";
            this.FindControl<ProgressBar>("ProgressBar").Value = 0;
        }

        private void UpdateUI(SdHealthStatus status)
        {
            this.FindControl<TextBlock>("TxtHealth").Text = GetLocalizedStatus(status.HealthStatus);
            this.FindControl<TextBlock>("TxtOperational").Text = GetLocalizedStatus(status.OperationalStatus);
            
            if (status.WriteSpeedMBs > 0)
                this.FindControl<TextBlock>("TxtWriteSpeed").Text = $"{status.WriteSpeedMBs:F2} MB/s";
            
            if (status.ReadSpeedMBs > 0)
                this.FindControl<TextBlock>("TxtReadSpeed").Text = $"{status.ReadSpeedMBs:F2} MB/s";
            
            var txtIntegrity = this.FindControl<TextBlock>("TxtIntegrity");
            if (status.IntegrityPass)
            {
                txtIntegrity.Text = MainWindow.GetString("StringPass");
                txtIntegrity.Foreground = Avalonia.Media.Brushes.Green;
            }
            else if (status.WriteSpeedMBs > 0) // Only show FAIL if we actually ran the test
            {
                txtIntegrity.Text = MainWindow.GetString("StringFail");
                txtIntegrity.Foreground = Avalonia.Media.Brushes.Red;
            }

            if (status.IsFakeCard)
            {
                this.FindControl<TextBlock>("TxtWarning").IsVisible = true;
            }

            if (!string.IsNullOrEmpty(status.ErrorMessage) && status.ErrorMessage != "Operation cancelled by user.")
            {
                this.FindControl<TextBlock>("TxtError").Text = status.ErrorMessage;
            }
        }

        private string GetLocalizedStatus(string status)
        {
            if (string.IsNullOrEmpty(status) || status == "Unknown") 
                return MainWindow.GetString("StringUnknown");

            switch (status.ToLowerInvariant())
            {
                case "healthy": return MainWindow.GetString("StringHealthy");
                case "ok": return MainWindow.GetString("StringOK");
                case "warning": return MainWindow.GetString("StringWarning");
                case "critical": return MainWindow.GetString("StringCritical");
                default: return status;
            }
        }
    }
}
