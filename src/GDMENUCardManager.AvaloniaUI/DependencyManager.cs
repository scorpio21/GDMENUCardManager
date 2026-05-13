using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GDMENUCardManager.Core;
using GDMENUCardManager.Core.Interface;
using SharpCompress.Readers;
using SharpCompress.Common;
using SharpCompress.Archives;

namespace GDMENUCardManager
{
    public class DependencyManager : IDependencyManager
    {
        private Window getMainWindow() => ((IClassicDesktopStyleApplicationLifetime)App.Current.ApplicationLifetime).MainWindow;

        public string GetString(string key)
        {
            if (App.Current.TryFindResource(key, out var value) && value is string str)
                return str;
            return key;
        }

        public string GetFormattedString(string key, params object[] args)
        {
            var format = GetString(key);
            return string.Format(format, args);
        }

        public IProgressWindow CreateAndShowProgressWindow()
        {
            var p = new ProgressWindow();
            p.Show(getMainWindow());
            return p;
        }

        public GdItem[] GdiShrinkWindowShowDialog(System.Collections.Generic.IEnumerable<GdItem> items, string title = null) => null;

        public async ValueTask<bool> ShowYesNoDialog(string caption, string text)
        {
            return await MessageBoxManager.GetMessageBoxStandardWindow(caption, text, ButtonEnum.YesNo).ShowDialog(getMainWindow()) == ButtonResult.Yes;
        }

        public async ValueTask<bool> ShowLockedFilesDialog(Dictionary<string, string> lockedFiles)
        {
            var dialog = new LockedFilesDialog(lockedFiles);
            await dialog.ShowDialog(getMainWindow());
            return dialog.Result;
        }

        public async ValueTask<bool> ShowConfigReadOnlyDialog(string configPath, string error)
        {
            var dialog = new ConfigReadOnlyDialog(configPath, error);
            await dialog.ShowDialog(getMainWindow());
            return dialog.Result;
        }

        public async ValueTask ShowSerialTranslationDialog(IEnumerable<GdItem> translatedItems)
        {
            var itemsList = translatedItems.ToList();
            if (itemsList.Count > 0)
            {
                var dialog = new SerialTranslationDialog(itemsList);
                await dialog.ShowDialog(getMainWindow());
            }
        }

        public async ValueTask<bool> ShowGdemuTypeDialog()
        {
            var dialog = new GdemuTypeDialog();
            await dialog.ShowDialog(getMainWindow());
            return dialog.IsAuthentic;
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

            var result = await MessageBoxManager.GetMessageBoxStandardWindow(
                GetString("StringInsufficientSpaceTitle"),
                sb.ToString(),
                ButtonEnum.YesNo,
                Icon.Warning).ShowDialog(getMainWindow());

            return result == ButtonResult.Yes;
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

            await MessageBoxManager.GetMessageBoxStandardWindow(
                GetString("StringDiskFullTitle"),
                sb.ToString(),
                ButtonEnum.Ok,
                Icon.Error).ShowDialog(getMainWindow());

            // Exit the application
            var lifetime = App.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            lifetime?.Shutdown();
        }

        public void ExtractArchive(string archivePath, string extractTo)
        {
            using (var stream = File.OpenRead(archivePath))
            using (var reader = ReaderFactory.OpenReader(stream))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        reader.WriteEntryToDirectory(extractTo, new ExtractionOptions()
                        {
                            ExtractFullPath = false,
                            Overwrite = true
                        });
                    }
                }
            }
        }

        public Dictionary<string, long> GetArchiveFiles(string archivePath)
        {
            var toReturn = new Dictionary<string, long>();
            using (var stream = File.OpenRead(archivePath))
            using (var reader = ReaderFactory.OpenReader(stream))
            {
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory && !toReturn.ContainsKey(reader.Entry.Key))
                    {
                        toReturn.Add(reader.Entry.Key, reader.Entry.Size);
                    }
                }
            }
            return toReturn;
        }
    }
}
