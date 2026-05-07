using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Models;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GDMENUCardManager.Core;

namespace GDMENUCardManager
{
    public partial class DatToolsWindow : Window
    {
        private readonly Core.Manager _manager;
        private readonly Func<Task> _reloadCallback;

        private string _importSourcePath;
        private string _exportTargetPath;

        private const int MaxPathDisplayLength = 50;

        // UI controls
        private TextBlock TextImportInstructions;
        private TextBlock TextClearInstructions;
        private TextBlock TextOverwriteInstructions;
        private TextBlock TextImportSourcePath;
        private TextBlock TextExportTargetPath;
        private Button ButtonBeginImport;
        private Button ButtonBeginExport;
        private RadioButton RadioImportMissing;
        private RadioButton RadioImportAll;

        public DatToolsWindow()
        {
            InitializeComponent();
        }

        private string GetString(string key)
        {
            if (Application.Current.TryFindResource(key, out object res) && res is string s)
                return s;
            return key;
        }

        public DatToolsWindow(Core.Manager manager, Func<Task> reloadCallback)
        {
            InitializeComponent();

            _manager = manager;
            _reloadCallback = reloadCallback;

            // Get references to UI controls
            TextImportInstructions = this.FindControl<TextBlock>("TextImportInstructions");
            TextClearInstructions = this.FindControl<TextBlock>("TextClearInstructions");
            TextOverwriteInstructions = this.FindControl<TextBlock>("TextOverwriteInstructions");
            TextImportSourcePath = this.FindControl<TextBlock>("TextImportSourcePath");
            TextExportTargetPath = this.FindControl<TextBlock>("TextExportTargetPath");
            ButtonBeginImport = this.FindControl<Button>("ButtonBeginImport");
            ButtonBeginExport = this.FindControl<Button>("ButtonBeginExport");
            RadioImportMissing = this.FindControl<RadioButton>("RadioImportMissing");
            RadioImportAll = this.FindControl<RadioButton>("RadioImportAll");

            SetPlatformSpecificText();

            this.KeyUp += (s, e) => { if (e.Key == Avalonia.Input.Key.Escape) Close(); };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SetPlatformSpecificText()
        {
            bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            if (!isMacOS && !isLinux)
                return; // Windows: keep XAML defaults

            string datFolder = isMacOS
                ? "\"~/Library/Application Support/GDMENUCardManager/menu_data\""
                : "\"tools/openMenu/menu_data\"";

            string backupFolder = isMacOS
                ? "\"~/Library/Application Support/GDMENUCardManager/dat_backups\""
                : "\"dat_backups\"";

            // Localize instructions with dynamic paths
            string importBase = GetString("StringImportInstructions");
            // Simple replacement for technical paths if needed, but here we just rebuild to ensure translation
            TextImportInstructions.Text = importBase.Replace("\"tools\\openMenu\\menu_data\"", datFolder).Replace("\"dat_backups\"", backupFolder);

            string clearBase = GetString("StringClearInstructions");
            TextClearInstructions.Text = clearBase.Replace("\"tools\\openMenu\\menu_data\"", datFolder).Replace("\"dat_backups\"", backupFolder);

            if (isMacOS)
            {
                // Special case for MacOS overwriting
                TextOverwriteInstructions.Text = GetString("StringOverwriteInstructions").Replace("\"tools\\openMenu\\menu_data\"", datFolder);
            }
        }

        /// <summary>
        /// Truncate a path for display, adding "..." if too long.
        /// </summary>
        private string TruncatePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return GetString("StringNoFolderSelected");

            if (path.Length <= MaxPathDisplayLength)
                return path;

            // Show beginning and end with ... in middle
            int halfLength = (MaxPathDisplayLength - 3) / 2;
            return path.Substring(0, halfLength) + "..." + path.Substring(path.Length - halfLength);
        }

        #region Import Tab

        private async void ChooseImportFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = GetString("StringSelectImportFolderTitle")
            };

            var result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result))
            {
                // Validate the folder contains at least one DAT file
                var boxPath = Path.Combine(result, "BOX.DAT");
                var metaPath = Path.Combine(result, "META.DAT");

                if (!File.Exists(boxPath) && !File.Exists(metaPath))
                {
                    await ShowError(GetString("StringInvalidFolder"), GetString("StringInvalidFolderMsg"));
                    return;
                }

                _importSourcePath = result;
                TextImportSourcePath.Text = TruncatePath(result);
                TextImportSourcePath.Foreground = Avalonia.Media.Brushes.Black;
                ButtonBeginImport.IsEnabled = true;
            }
        }

        private async void BeginImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_importSourcePath))
                return;

            // Confirmation dialog
            var confirmResult = await MessageBoxManager.GetMessageBoxCustomWindow(new MessageBox.Avalonia.DTO.MessageBoxCustomParams
            {
                ContentTitle = GetString("StringConfirmImportTitle"),
                ContentMessage = GetString("StringConfirmImportMsg"),
                Icon = MessageBox.Avalonia.Enums.Icon.Warning,
                ShowInCenter = true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ButtonDefinitions = new ButtonDefinition[]
                {
                    new ButtonDefinition { Name = GetString("StringContinue") },
                    new ButtonDefinition { Name = GetString("StringCancel") }
                }
            }).ShowDialog(this);

            if (confirmResult != GetString("StringContinue"))
                return;

            bool overwriteExisting = RadioImportAll?.IsChecked == true;

            // Check DAT files are writable before proceeding
            if (!await EnsureDatFilesWritableWithDialog())
                return;

            // Show progress window
            var progressWindow = new ProgressWindow();
            progressWindow.Title = GetString("StringImportDatEntriesTitle");
            progressWindow.TextContent = GetString("StringImporting");
            progressWindow.TotalItems = 100;
            progressWindow.ProcessedItems = 0;

            _ = progressWindow.ShowDialog(this);

            try
            {
                var result = await Task.Run(() =>
                {
                    return _manager.ImportDatEntries(_importSourcePath, overwriteExisting, progress =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            progressWindow.ProcessedItems = (int)(progress * 100);
                        });
                    });
                });

                progressWindow.AllowClose();
                progressWindow.Close();

                if (!result.success)
                {
                    await ShowError(GetString("StringImportFailed"), result.errorMessage);
                    return;
                }

                // Show success message first
                var message = GetString("StringImportSuccessMsg") + "\n\n" + 
                              string.Format(GetString("StringBoxEntriesMerged"), result.boxEntriesMerged) + "\n" +
                              string.Format(GetString("StringMetaEntriesMerged"), result.metaEntriesMerged);
                
                if (result.boxEntriesMerged > 0)
                {
                    message += "\n\n" + GetString("StringIconRegenerated");
                }
                await ShowInfo(GetString("StringImportCompleteTitle"), message);

                // Close this window and reload
                this.Close();

                if (_reloadCallback != null)
                {
                    await _reloadCallback();
                }
            }
            catch (Exception ex)
            {
                progressWindow.AllowClose();
                progressWindow.Close();
                await ShowError(GetString("StringImportFailed"), $"{GetString("StringError")}: {ex.Message}");
            }
        }

        #endregion

        #region Export Tab

        private async void ChooseExportFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = GetString("StringSelectExportFolderTitle")
            };

            var result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result))
            {
                _exportTargetPath = result;
                TextExportTargetPath.Text = TruncatePath(result);
                TextExportTargetPath.Foreground = Avalonia.Media.Brushes.Black;
                ButtonBeginExport.IsEnabled = true;
            }
        }

        private async void BeginExport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_exportTargetPath))
                return;

            // Show progress window
            var progressWindow = new ProgressWindow();
            progressWindow.Title = GetString("StringExportingArtworkTitle");
            progressWindow.TextContent = GetString("StringExporting");
            progressWindow.TotalItems = 100;
            progressWindow.ProcessedItems = 0;

            _ = progressWindow.ShowDialog(this);

            try
            {
                var result = await Task.Run(() =>
                {
                    return _manager.ExportArtworkToPngs(_exportTargetPath, progress =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            progressWindow.ProcessedItems = (int)(progress * 100);
                        });
                    });
                });

                progressWindow.AllowClose();
                progressWindow.Close();

                if (!result.success)
                {
                    await ShowError(GetString("StringExportFailed"), result.errorMessage);
                    return;
                }

                // Keep window open, just show success
                await ShowInfo(GetString("StringExportCompleteTitle"), string.Format(GetString("StringExportSuccessMsg"), result.exportedCount));
            }
            catch (Exception ex)
            {
                progressWindow.AllowClose();
                progressWindow.Close();
                await ShowError(GetString("StringExportFailed"), $"{GetString("StringError")}: {ex.Message}");
            }
        }

        #endregion

        #region Clear Tab

        private async void ClearDats_Click(object sender, RoutedEventArgs e)
        {
            // Confirmation dialog
            var confirmResult = await MessageBoxManager.GetMessageBoxCustomWindow(new MessageBox.Avalonia.DTO.MessageBoxCustomParams
            {
                ContentTitle = GetString("StringConfirmClearTitle"),
                ContentMessage = GetString("StringConfirmClearMsg"),
                Icon = MessageBox.Avalonia.Enums.Icon.Warning,
                ShowInCenter = true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ButtonDefinitions = new ButtonDefinition[]
                {
                    new ButtonDefinition { Name = GetString("StringClearAll") },
                    new ButtonDefinition { Name = GetString("StringCancel") }
                }
            }).ShowDialog(this);

            if (confirmResult != GetString("StringClearAll"))
                return;

            // Check DAT files are writable before proceeding
            if (!await EnsureDatFilesWritableWithDialog())
                return;

            // Show progress window
            var progressWindow = new ProgressWindow();
            progressWindow.Title = GetString("StringClearingDatFilesTitle");
            progressWindow.TextContent = GetString("StringClearing");
            progressWindow.TotalItems = 100;
            progressWindow.ProcessedItems = 50; // Show some progress for indeterminate

            _ = progressWindow.ShowDialog(this);

            try
            {
                var result = await Task.Run(() => _manager.ClearAllDatEntries());

                progressWindow.AllowClose();
                progressWindow.Close();

                if (!result.success)
                {
                    await ShowError(GetString("StringClearFailed"), result.errorMessage);
                    return;
                }

                // Show success message first
                await ShowInfo(GetString("StringClearCompleteTitle"), GetString("StringClearSuccessMsg"));

                // Close this window and reload
                this.Close();

                if (_reloadCallback != null)
                {
                    await _reloadCallback();
                }
            }
            catch (Exception ex)
            {
                progressWindow.AllowClose();
                progressWindow.Close();
                await ShowError(GetString("StringClearFailed"), $"{GetString("StringError")}: {ex.Message}");
            }
        }

        #endregion

        #region Overwrite Tab

        private async void OverwriteDats_Click(object sender, RoutedEventArgs e)
        {
            // Confirmation dialog
            var confirmResult = await MessageBoxManager.GetMessageBoxCustomWindow(new MessageBox.Avalonia.DTO.MessageBoxCustomParams
            {
                ContentTitle = GetString("StringConfirmOverwriteTitle"),
                ContentMessage = GetString("StringConfirmOverwriteMsg"),
                Icon = MessageBox.Avalonia.Enums.Icon.Warning,
                ShowInCenter = true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ButtonDefinitions = new ButtonDefinition[]
                {
                    new ButtonDefinition { Name = GetString("StringContinue") },
                    new ButtonDefinition { Name = GetString("StringCancel") }
                }
            }).ShowDialog(this);

            if (confirmResult != GetString("StringContinue"))
                return;

            // Check DAT files are writable before proceeding
            if (!await EnsureDatFilesWritableWithDialog())
                return;

            // Show progress window
            var progressWindow = new ProgressWindow();
            progressWindow.Title = GetString("StringOverwritingDatFilesTitle");
            progressWindow.TextContent = GetString("StringExtractingDats");
            progressWindow.TotalItems = 100;
            progressWindow.ProcessedItems = 50;

            _ = progressWindow.ShowDialog(this);

            try
            {
                var result = await Task.Run(() => _manager.OverwriteDatsFromSdCard());

                progressWindow.AllowClose();
                progressWindow.Close();

                if (!result.success)
                {
                    await ShowError(GetString("StringOverwriteFailed"), result.errorMessage);
                    return;
                }

                await ShowInfo(GetString("StringOverwriteCompleteTitle"), GetString("StringOverwriteSuccessMsg"));

                // Close this window and reload
                this.Close();

                if (_reloadCallback != null)
                {
                    await _reloadCallback();
                }
            }
            catch (Exception ex)
            {
                progressWindow.AllowClose();
                progressWindow.Close();
                await ShowError(GetString("StringOverwriteFailed"), $"{GetString("StringError")}: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private async Task ShowError(string title, string message)
        {
            await MessageBoxManager.GetMessageBoxStandardWindow(title, message,
                MessageBox.Avalonia.Enums.ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Error)
                .ShowDialog(this);
        }

        private async Task ShowInfo(string title, string message)
        {
            await MessageBoxManager.GetMessageBoxStandardWindow(title, message,
                MessageBox.Avalonia.Enums.ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Info)
                .ShowDialog(this);
        }

        /// <summary>
        /// Checks DAT file writability with a retry dialog owned by this window
        /// (not MainWindow) so it appears on top of DatToolsWindow.
        /// </summary>
        private async Task<bool> EnsureDatFilesWritableWithDialog()
        {
            while (true)
            {
                var lockedFiles = _manager.CheckDatFilesAccessibility();
                if (lockedFiles.Count == 0) return true;

                var dialog = new LockedFilesDialog(lockedFiles);
                await dialog.ShowDialog(this);
                if (!dialog.Result) return false; // user cancelled
            }
        }

        #endregion
    }
}
