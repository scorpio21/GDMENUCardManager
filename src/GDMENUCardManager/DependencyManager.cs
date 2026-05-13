using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using GDMENUCardManager.Core;
using GDMENUCardManager.Core.Interface;
using SevenZip;

namespace GDMENUCardManager
{
    public class DependencyManager : IDependencyManager
    {
        private Window getMainWindow() => App.Current.MainWindow;

        public string GetString(string key)
        {
            return MainWindow.GetString(key);
        }

        public string GetFormattedString(string key, params object[] args)
        {
            var format = GetString(key);
            return string.Format(format, args);
        }

        public IProgressWindow CreateAndShowProgressWindow()
        {
            var p = new ProgressWindow() { Owner = getMainWindow() };
            p.Show();
            return p;
        }

        public GdItem[] GdiShrinkWindowShowDialog(IEnumerable<GdItem> items, string title = null)
        {
            var w = new GdiShrinkWindow(items, title) { Owner = getMainWindow() };
            return w.ShowDialog().GetValueOrDefault() ? w.List.Where(x => x.Value).Select(x => x.Key).ToArray() : null;
        }

        public ValueTask<bool> ShowYesNoDialog(string caption, string text)
        {
            return new ValueTask<bool>(MessageBox.Show(getMainWindow(), text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        }

        public ValueTask<bool> ShowLockedFilesDialog(Dictionary<string, string> lockedFiles)
        {
            var dialog = new LockedFilesDialog(lockedFiles) { Owner = getMainWindow() };
            var result = dialog.ShowDialog();
            return new ValueTask<bool>(result == true);
        }

        public ValueTask<bool> ShowConfigReadOnlyDialog(string configPath, string error)
        {
            var dialog = new ConfigReadOnlyDialog(configPath, error) { Owner = getMainWindow() };
            var result = dialog.ShowDialog();
            return new ValueTask<bool>(result == true);
        }

        public ValueTask ShowSerialTranslationDialog(IEnumerable<GdItem> translatedItems)
        {
            var itemsList = translatedItems.ToList();
            if (itemsList.Count > 0)
            {
                var dialog = new SerialTranslationDialog(itemsList) { Owner = getMainWindow() };
                dialog.ShowDialog();
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> ShowGdemuTypeDialog()
        {
            var dialog = new GdemuTypeDialog { Owner = getMainWindow() };
            dialog.ShowDialog();
            return new ValueTask<bool>(dialog.IsAuthentic);
        }

        public async ValueTask<bool> ShowSpaceWarningDialog(SpaceCheckResult spaceCheck)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetString("StringInsufficientSpace") + "\n");
            sb.AppendLine(GetString("StringSpaceNeeded"));
            sb.AppendLine($"  \u2022 " + GetFormattedString("StringNewDiscImages", spaceCheck.NewItemCount, Helper.FormatBytes(spaceCheck.NewItemsSize)));
            if (spaceCheck.MenuFolderExists)
            {
                // Old menu will be deleted before new is created - net impact is just wiggle room
                sb.AppendLine($"  \u2022 " + GetFormattedString("StringMenuUpdateBuffer", Helper.FormatBytes(spaceCheck.MenuWiggleRoom)));
            }
            else
            {
                // No existing menu - need full space for new menu
                sb.AppendLine($"  \u2022 " + GetFormattedString("StringMenuDiscImage", Helper.FormatBytes(spaceCheck.MenuBaseSize + spaceCheck.MenuWiggleRoom)));
            }
            sb.AppendLine($"  \u2022 " + GetFormattedString("StringMetadataFiles", Helper.FormatBytes(spaceCheck.MetadataBuffer)));
            sb.AppendLine($"  " + GetFormattedString("StringTotal", Helper.FormatBytes(spaceCheck.TotalNeeded)) + "\n");
            sb.AppendLine(GetFormattedString("StringSpaceAvailable", Helper.FormatBytes(spaceCheck.AvailableSpace)));
            if (spaceCheck.SpaceToBeFreed > 0)
            {
                sb.AppendLine(GetFormattedString("StringSpaceToBeFreed", Helper.FormatBytes(spaceCheck.SpaceToBeFreed)));
                sb.AppendLine(GetFormattedString("StringEffectiveAvailable", Helper.FormatBytes(spaceCheck.EffectiveAvailable)));
            }
            sb.AppendLine($"\n" + GetFormattedString("StringShortfall", Helper.FormatBytes(spaceCheck.Shortfall)));

            if (spaceCheck.ShrinkingEnabled)
            {
                sb.AppendLine("\n" + GetString("StringShrinkSpaceNote"));
            }
            if (spaceCheck.ContainsCompressedFiles)
            {
                sb.AppendLine("\n" + GetString("StringCompressedSpaceNote"));
            }

            sb.AppendLine("\n" + GetString("StringProceedAnyway"));

            var result = MessageBox.Show(
                getMainWindow(),
                sb.ToString(),
                GetString("StringInsufficientSpaceTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            return result == MessageBoxResult.Yes;
        }

        public async ValueTask ShowDiskFullError(string message, string incompleteFolderPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine(message);

            if (!string.IsNullOrEmpty(incompleteFolderPath) && Directory.Exists(incompleteFolderPath))
            {
                sb.AppendLine($"\n" + GetString("StringIncompleteFolderRemoved") + $"\n{incompleteFolderPath}");

                // Delete the incomplete folder
                try
                {
                    Directory.Delete(incompleteFolderPath, true);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"\nWarning: Could not delete incomplete folder: {ex.Message}");
                }
            }

            sb.AppendLine("\n" + GetString("StringFreeUpSpace"));
            sb.AppendLine("\n" + GetString("StringAppWillClose"));

            MessageBox.Show(
                getMainWindow(),
                sb.ToString(),
                GetString("StringDiskFullTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Exit the application
            Application.Current.Shutdown();
        }

        public void ExtractArchive(string archivePath, string extractTo)
        {
            using (var extr = new SevenZipExtractor(archivePath))
            {
                extr.PreserveDirectoryStructure = false;
                extr.ExtractArchive(extractTo);
            }
        }

        public Dictionary<string, long> GetArchiveFiles(string archivePath)
        {
            var toReturn = new Dictionary<string, long>();
            using (var compressedfile = new SevenZipExtractor(archivePath))
                foreach (var item in compressedfile.ArchiveFileData.Where(x => !x.IsDirectory))
                    if (!toReturn.ContainsKey(item.FileName))
                        toReturn.Add(item.FileName, (long)item.Size);
            return toReturn;
        }
    }
}
