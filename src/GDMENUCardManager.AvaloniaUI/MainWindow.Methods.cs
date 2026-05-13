using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GDMENUCardManager.Core;
using System.Configuration;

namespace GDMENUCardManager
{
    public partial class MainWindow
    {
        private void UpdateFolderColumnVisibility()
        {
            if (dg1?.Columns == null)
                return;

            // Find columns by iterating and checking their Header
            DataGridColumn folderColumn = null;
            DataGridColumn typeColumn = null;
            DataGridColumn artColumn = null;
            DataGridTemplateColumn discColumn = null;

            foreach (var col in dg1.Columns)
            {
                if (col.Header?.ToString() == "Folder")
                    folderColumn = col;
                else if (col is DataGridTemplateColumn templateCol && templateCol.Header?.ToString() == "Type")
                    typeColumn = col;
                else if (col is DataGridTemplateColumn discTemplateCol && discTemplateCol.Header?.ToString() == "Disc")
                    discColumn = discTemplateCol;
                else if (col.Header?.ToString() == "Art")
                    artColumn = col;
            }

            if (folderColumn != null)
            {
                if (MenuKindSelected == MenuKind.openMenu)
                {
                    folderColumn.IsVisible = true;
                    folderColumn.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                }
                else
                {
                    folderColumn.IsVisible = false;
                }
            }

            if (typeColumn != null)
            {
                if (MenuKindSelected == MenuKind.openMenu)
                {
                    typeColumn.IsVisible = true;
                }
                else
                {
                    typeColumn.IsVisible = false;
                }
            }

            // Art column: only visible in openMenu mode
            if (artColumn != null)
            {
                bool showArt = MenuKindSelected == MenuKind.openMenu;
                artColumn.IsVisible = showArt;
            }

            // Disc column read-only handling is now done via BeginningEdit event
            // since it's a template column
        }

        private void UpdateSortButtonTooltip()
        {
            if (ButtonSort == null) return;
            ToolTip.SetTip(ButtonSort, MenuKindSelected == MenuKind.openMenu
                ? "Sort list by folder path + title"
                : "Sort list by title");
        }

        private void RevertProperty(GdItem item, string propertyName, object oldValue)
        {
            switch (propertyName)
            {
                case nameof(GdItem.Name):
                    item.Name = oldValue as string;
                    break;
                case nameof(GdItem.ProductNumber):
                    item.ProductNumber = oldValue as string;
                    break;
                case nameof(GdItem.Folder):
                    item.Folder = oldValue as string;
                    break;
            }
        }

        private void updateTotalSize()
        {
            var bsize = ByteSizeLib.ByteSize.FromBytes(Manager.ItemList.Sum(x => x.Length.Bytes));
            TotalFilesLength = Converter.ByteSizeToStringConverter.UseBinaryString ? bsize.ToBinaryString() : bsize.ToString();
        }

        private async Task CheckForUpdateAsync()
        {
            try
            {
                var result = await UpdateManager.CheckForUpdateAsync();
                if (result.ManualUpdateRequired && !UpdateAvailableDialog.ShouldSkipVersion(result.LatestTag))
                {
                    var manualDialog = new ManualUpdateDialog(result.LatestTag, result.LatestVersion, result.ManualReason);
                    await manualDialog.ShowDialog(this);
                }
                else if (result.UpdateAvailable && !UpdateAvailableDialog.ShouldSkipVersion(result.LatestTag))
                {
                    var dialog = new UpdateAvailableDialog(result.LatestTag, result.LatestVersion);
                    await dialog.ShowDialog(this);

                    if (dialog.UserWantsUpdate)
                    {
                        var wizard = new UpdateWizardWindow(result.LatestTag, result.LatestVersion);
                        await wizard.ShowDialog(this);
                    }
                }
            }
            catch
            {
                // Silently ignore any update check errors
            }
        }

        private async Task LoadItemsFromCard()
        {
            IsBusy = true;

            try
            {
                await Manager.LoadItemsFromCard();

                // Check if any items need metadata scan (old SD cards without cache files)
                var itemsNeedingScan = Manager.GetItemsNeedingMetadataScan();
                if (itemsNeedingScan.Any())
                {
                    var scanDialog = new MetadataScanDialog(itemsNeedingScan.Count);
                    await scanDialog.ShowDialog(this);

                    if (scanDialog.StartScan)
                    {
                        // Perform the metadata scan with progress window
                        await PerformMetadataScan(itemsNeedingScan);
                    }
                    else
                    {
                        // quit
                        Close();
                        return;
                    }
                }

                // Initialize BoxDat for artwork management (openMenu only)
                Manager.InitializeBoxDat();

                // Check DAT file status for openMenu
                if (MenuKindSelected == MenuKind.openMenu)
                {
                    await HandleDatFileStatus();
                }

                // Show serial translation dialog if any items were translated
                await ShowSerialTranslationDialogIfNeeded();
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Invalid Folders", $"Problem loading the following folder(s):\n\n{ex.Message}", icon: MessageBox.Avalonia.Enums.Icon.Warning).ShowDialog(this);
            }
            finally
            {
                RaisePropertyChanged(nameof(MenuKindSelected));
                UpdateFolderColumnVisibility();
                IsBusy = false;
            }
        }

        private async Task ShowSerialTranslationDialogIfNeeded()
        {
            var translatedItems = Manager.ItemList.Where(item => item.WasSerialTranslated).ToList();
            if (translatedItems.Count > 0)
            {
                await Helper.DependencyManager.ShowSerialTranslationDialog(translatedItems);
            }
        }

        private async Task PerformMetadataScan(List<GdItem> items)
        {
            var progressWindow = new ProgressWindow();
            progressWindow.Title = Helper.DependencyManager.GetString("StringScanningDiscImages");
            progressWindow.TotalItems = items.Count;
            progressWindow.Show();

            var progress = new Progress<(int current, int total, string name)>(p =>
            {
                progressWindow.ProcessedItems = p.current;
                progressWindow.TextContent = Helper.DependencyManager.GetFormattedString("StringCachingMetadata", p.name);
            });

            try
            {
                await Manager.PerformMetadataScan(items, progress);
            }
            finally
            {
                progressWindow.AllowClose();
                progressWindow.Close();
            }
        }

        private async Task HandleDatFileStatus()
        {
            var status = Manager.CheckDatFilesStatus();

            switch (status)
            {
                case DatFileStatus.BothMissing:
                    {
                        var result = await MessageBoxManager.GetMessageBoxCustomWindow(new MessageBox.Avalonia.DTO.MessageBoxCustomParams
                        {
                            ContentTitle = GetString("StringDatFilesMissing"),
                            ContentMessage = GetString("StringDatFilesMissingMsg"),
                            Icon = MessageBox.Avalonia.Enums.Icon.Warning,
                            ShowInCenter = true,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            ButtonDefinitions = new ButtonDefinition[]
                            {
                                new ButtonDefinition { Name = GetString("StringCreate") },
                                new ButtonDefinition { Name = GetString("StringClose") },
                                new ButtonDefinition { Name = GetString("StringSkip") }
                            }
                        }).ShowDialog(this);

                        if (result == GetString("StringCreate"))
                        {
                            if (!await Manager.EnsureDatFilesWritable()) { Manager.ArtworkDisabled = true; break; }
                            var (success, error) = Manager.CreateEmptyDatFiles();
                            if (!success)
                            {
                                await MessageBoxManager.GetMessageBoxStandardWindow(GetString("StringError"), GetString("StringDatErrorOccurred") + error, icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
                                Manager.ArtworkDisabled = true;
                            }
                        }
                        else if (result == GetString("StringClose"))
                        {
                            SelectedDrive = null;
                        }
                        else
                        {
                            Manager.ArtworkDisabled = true;
                        }
                        break;
                    }

                case DatFileStatus.BoxMissingIconExists:
                    {
                        var result = await MessageBoxManager.GetMessageBoxCustomWindow(new MessageBox.Avalonia.DTO.MessageBoxCustomParams
                        {
                            ContentTitle = GetString("StringBoxMissingIconExistsTitle"),
                            ContentMessage = GetString("StringBoxMissingIconExistsMsg"),
                            Icon = MessageBox.Avalonia.Enums.Icon.Warning,
                            ShowInCenter = true,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            ButtonDefinitions = new ButtonDefinition[]
                            {
                                new ButtonDefinition { Name = GetString("StringCreate") },
                                new ButtonDefinition { Name = GetString("StringClose") },
                                new ButtonDefinition { Name = GetString("StringSkip") }
                            }
                        }).ShowDialog(this);

                        if (result == GetString("StringCreate"))
                        {
                            if (!await Manager.EnsureDatFilesWritable()) { Manager.ArtworkDisabled = true; break; }
                            var (success, error) = Manager.CreateEmptyBoxDat();
                            if (!success)
                            {
                                await MessageBoxManager.GetMessageBoxStandardWindow(GetString("StringError"), GetString("StringDatErrorOccurred") + error, icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
                                Manager.ArtworkDisabled = true;
                            }
                        }
                        else if (result == GetString("StringClose"))
                        {
                            SelectedDrive = null;
                        }
                        else
                        {
                            Manager.ArtworkDisabled = true;
                        }
                        break;
                    }

                case DatFileStatus.BoxExistsIconMissing:
                    {
                        var result = await MessageBoxManager.GetMessageBoxCustomWindow(new MessageBox.Avalonia.DTO.MessageBoxCustomParams
                        {
                            ContentTitle = GetString("StringIconMissingTitle"),
                            ContentMessage = GetString("StringIconMissingMsg"),
                            Icon = MessageBox.Avalonia.Enums.Icon.Question,
                            ShowInCenter = true,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            ButtonDefinitions = new ButtonDefinition[]
                            {
                                new ButtonDefinition { Name = GetString("StringCreate") },
                                new ButtonDefinition { Name = GetString("StringClose") },
                                new ButtonDefinition { Name = GetString("StringSkip") }
                            }
                        }).ShowDialog(this);

                        if (result == GetString("StringCreate"))
                        {
                            if (!await Manager.EnsureDatFilesWritable()) { Manager.ArtworkDisabled = true; break; }
                            var (success, error) = Manager.GenerateIconDatFromBox();
                            if (!success)
                            {
                                await MessageBoxManager.GetMessageBoxStandardWindow(GetString("StringError"), GetString("StringDatErrorOccurred") + error, icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
                                Manager.ArtworkDisabled = true;
                            }
                        }
                        else if (result == GetString("StringClose"))
                        {
                            SelectedDrive = null;
                        }
                        else
                        {
                            Manager.ArtworkDisabled = true;
                        }
                        break;
                    }

                case DatFileStatus.SerialsMismatch:
                    {
                        var result = await MessageBoxManager.GetMessageBoxCustomWindow(new MessageBox.Avalonia.DTO.MessageBoxCustomParams
                        {
                            ContentTitle = GetString("StringDatMismatchTitle"),
                            ContentMessage = GetString("StringDatMismatchMsg"),
                            Icon = MessageBox.Avalonia.Enums.Icon.Warning,
                            ShowInCenter = true,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            ButtonDefinitions = new ButtonDefinition[]
                            {
                                new ButtonDefinition { Name = GetString("StringCreate") },
                                new ButtonDefinition { Name = GetString("StringClose") },
                                new ButtonDefinition { Name = GetString("StringSkip") }
                            }
                        }).ShowDialog(this);

                        if (result == GetString("StringCreate"))
                        {
                            if (!await Manager.EnsureDatFilesWritable()) break;
                            var (success, error) = Manager.GenerateIconDatFromBox();
                            if (!success)
                            {
                                await MessageBoxManager.GetMessageBoxStandardWindow(GetString("StringError"), GetString("StringDatErrorOccurred") + error, icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
                            }
                        }
                        else if (result == GetString("StringSkip"))
                        {
                            Manager.ArtworkDisabled = true;
                        }
                        // Close = proceed with mismatched files, do nothing
                        break;
                    }

                case DatFileStatus.OK:
                default:
                    // All good, nothing to do
                    break;
            }

            // Update UI based on artwork disabled state
            RaisePropertyChanged(nameof(IsArtworkEnabled));
            UpdateFolderColumnVisibility();
        }

        private async Task Save()
        {
            IsBusy = true;
            try
            {
                // Check for multi-disc items without serial (openMenu only)
                if (MenuKindSelected == MenuKind.openMenu && HasMultiDiscItemsWithoutSerial())
                {
                    var result = await MessageBoxManager.GetMessageBoxCustomWindow(new MessageBox.Avalonia.DTO.MessageBoxCustomParams
                    {
                        ContentTitle = "Warning",
                        ContentMessage = "One or more disc images that are part of multi-disc sets do not have a required Serial value assigned to them, which will break their display in openMenu.\n\nDo you want to proceed and ignore the disc numbers and counts, or return to make edits?",
                        Icon = MessageBox.Avalonia.Enums.Icon.Warning,
                        ShowInCenter = true,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        ButtonDefinitions = new ButtonDefinition[]
                        {
                            new ButtonDefinition { Name = "Return" },
                            new ButtonDefinition { Name = "Proceed" }
                        }
                    }).ShowDialog(this);

                    if (result == "Return")
                    {
                        IsBusy = false;
                        return;
                    }

                    // reset disc to 1/1 for items without serial
                    ResetDiscValuesForItemsWithoutSerial();
                }

                // Check for multi-disc sets exceeding 10 discs (openMenu only)
                if (MenuKindSelected == MenuKind.openMenu && HasMultiDiscSetsExceeding10())
                {
                    var result = await MessageBoxManager.GetMessageBoxCustomWindow(new MessageBox.Avalonia.DTO.MessageBoxCustomParams
                    {
                        ContentTitle = "Warning",
                        ContentMessage = "One or more multi-disc set exceeds 10 discs total, the maximum supported by openMenu.\n\nDo you want to proceed or return to make edits?",
                        Icon = MessageBox.Avalonia.Enums.Icon.Warning,
                        ShowInCenter = true,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        ButtonDefinitions = new ButtonDefinition[]
                        {
                            new ButtonDefinition { Name = "Return" },
                            new ButtonDefinition { Name = "Proceed" }
                        }
                    }).ShowDialog(this);

                    if (result == "Return")
                    {
                        IsBusy = false;
                        return;
                    }
                }

                if (await Manager.Save(TempFolder))
                {
                    SaveTempFolderConfig();
                    SaveLockCheckConfig();
                    await MessageBoxManager.GetMessageBoxStandardWindow(GetString("StringMessage"), GetString("StringDone")).ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow(GetString("StringError"), ex.Message, icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
            }
            finally
            {
                IsBusy = false;
                updateTotalSize();
            }
        }

        private bool HasMultiDiscItemsWithoutSerial()
        {
            return Manager.ItemList.Any(item =>
            {
                // Skip menu items and compressed files (serial assigned during extraction)
                if (item.Ip?.Name == "GDMENU" || item.Ip?.Name == "openMenu")
                    return false;
                if (item.FileFormat == Core.FileFormat.SevenZip || item.FileFormat == Core.FileFormat.CueBinNonGame)
                    return false;

                if (string.IsNullOrWhiteSpace(item.ProductNumber))
                {
                    var disc = item.Ip?.Disc;
                    if (!string.IsNullOrEmpty(disc))
                    {
                        var parts = disc.Split('/');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[1], out int totalDiscs) &&
                            totalDiscs > 1)
                        {
                            return true;
                        }
                    }
                }
                return false;
            });
        }

        private bool HasMultiDiscSetsExceeding10()
        {
            return Manager.ItemList.Any(item =>
            {
                // Skip menu items
                if (item.Ip?.Name == "GDMENU" || item.Ip?.Name == "openMenu")
                    return false;

                var disc = item.Ip?.Disc;
                if (!string.IsNullOrEmpty(disc))
                {
                    var parts = disc.Split('/');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[1], out int totalDiscs) &&
                        totalDiscs > 10)
                    {
                        return true;
                    }
                }
                return false;
            });
        }

        private void ResetDiscValuesForItemsWithoutSerial()
        {
            foreach (var item in Manager.ItemList)
            {
                // Skip menu items
                if (item.Ip?.Name == "GDMENU" || item.Ip?.Name == "openMenu")
                    continue;

                // If no serial and has multi-disc value, reset to 1/1
                if (string.IsNullOrWhiteSpace(item.ProductNumber) && item.Ip != null)
                {
                    var disc = item.Ip.Disc;
                    if (!string.IsNullOrEmpty(disc))
                    {
                        var parts = disc.Split('/');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[1], out int totalDiscs) &&
                            totalDiscs > 1)
                        {
                            item.Ip.Disc = "1/1";
                            // Trigger UI update
                            item.NotifyIpChanged();
                        }
                    }
                }
            }
        }

        private async Task CheckConfigWritability()
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                var configPath = config.FilePath;

                if (!File.Exists(configPath))
                    return; // nothing to check

                while (true)
                {
                    Core.Helper.TryMakeWritable(configPath);
                    var error = Core.Helper.CheckFileAccessibility(configPath);
                    if (error == null) break; // writable

                    // true=retry, false=proceed without saving
                    if (!await Core.Helper.DependencyManager.ShowConfigReadOnlyDialog(configPath, error))
                    {
                        Core.Manager.ConfigReadOnly = true;
                        break;
                    }
                }
            }
            catch { }
        }

        private void SaveTempFolderConfig()
        {
            if (Core.Manager.ConfigReadOnly) return;
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                var systemDefault = Path.GetTempPath();
                var normalized = Path.GetFullPath(TempFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var normalizedDefault = Path.GetFullPath(systemDefault.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.Equals(normalized, normalizedDefault, StringComparison.OrdinalIgnoreCase))
                    SetOrAddSetting(config, "TempFolder", "");
                else
                    SetOrAddSetting(config, "TempFolder", TempFolder);
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch { }
        }

        private void SaveDiscImageOptionsConfig()
        {
            if (Core.Manager.ConfigReadOnly) return;
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                SetOrAddSetting(config, "EnableGDIShrink", Manager.EnableGDIShrink.ToString());
                SetOrAddSetting(config, "EnableGDIShrinkCompressed", Manager.EnableGDIShrinkCompressed.ToString());
                SetOrAddSetting(config, "EnableGDIShrinkBlackList", Manager.EnableGDIShrinkBlackList.ToString());
                SetOrAddSetting(config, "EnableGDIShrinkExisting", Manager.EnableGDIShrinkExisting.ToString());
                SetOrAddSetting(config, "EnableRegionPatch", Manager.EnableRegionPatch.ToString());
                SetOrAddSetting(config, "EnableRegionPatchExisting", Manager.EnableRegionPatchExisting.ToString());
                SetOrAddSetting(config, "EnableVgaPatch", Manager.EnableVgaPatch.ToString());
                SetOrAddSetting(config, "EnableVgaPatchExisting", Manager.EnableVgaPatchExisting.ToString());
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch { }
        }

        private void SaveLockCheckConfig()
        {
            if (Core.Manager.ConfigReadOnly) return;
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                SetOrAddSetting(config, "LockCheck", Manager.EnableLockCheck.ToString());
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch
            {
                // Ignore errors saving config
            }
        }

        private void RestoreWindowBounds()
        {
            try
            {
                if (double.TryParse(ConfigurationManager.AppSettings["WindowLeft"], out double left)
                    && double.TryParse(ConfigurationManager.AppSettings["WindowTop"], out double top)
                    && double.TryParse(ConfigurationManager.AppSettings["WindowWidth"], out double width)
                    && double.TryParse(ConfigurationManager.AppSettings["WindowHeight"], out double height))
                {
                    // Validate saved size against minimums
                    if (width < MinWidth) width = MinWidth;
                    if (height < MinHeight) height = MinHeight;

                    // Check that at least part of the window is visible on some screen
                    bool isOnScreen = false;
                    foreach (var screen in Screens.All)
                    {
                        var bounds = screen.WorkingArea;
                        if (left + width > bounds.X && left < bounds.X + bounds.Width
                            && top + height > bounds.Y && top < bounds.Y + bounds.Height)
                        {
                            isOnScreen = true;
                            break;
                        }
                    }

                    if (isOnScreen)
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual;
                        Position = new Avalonia.PixelPoint((int)left, (int)top);
                        Width = width;
                        Height = height;
                    }
                }
            }
            catch { }
        }

        private static void SetOrAddSetting(System.Configuration.Configuration config, string key, string value)
        {
            if (config.AppSettings.Settings[key] != null)
                config.AppSettings.Settings[key].Value = value;
            else
                config.AppSettings.Settings.Add(key, value);
        }

        private void SaveWindowBounds()
        {
            if (Core.Manager.ConfigReadOnly) return;
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);

                // Save current bounds (Avalonia doesn't have RestoreBounds, use current values)
                SetOrAddSetting(config, "WindowLeft", Position.X.ToString());
                SetOrAddSetting(config, "WindowTop", Position.Y.ToString());
                SetOrAddSetting(config, "WindowWidth", Width.ToString());
                SetOrAddSetting(config, "WindowHeight", Height.ToString());
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch { }
        }

        private async Task renameSelection(RenameBy renameBy)
        {
            IsBusy = true;
            try
            {
                // Filter out menu entry (folder 01) from renaming
                var items = dg1.SelectedItems.Cast<GdItem>().Where(x => x.SdNumber != 1).ToList();

                if (items.Count == 0)
                {
                    IsBusy = false;
                    return;
                }

                // Capture old names before rename
                var oldNames = items.ToDictionary(i => i, i => i.Name);

                await Manager.RenameItems(items, renameBy);

                // Record undo for items whose names actually changed
                var undoOp = new MultiPropertyEditOperation($"Rename by {renameBy}")
                {
                    PropertyName = nameof(GdItem.Name)
                };

                foreach (var item in items)
                {
                    if (oldNames.TryGetValue(item, out var oldName) && item.Name != oldName)
                    {
                        undoOp.Edits.Add((item, oldName, item.Name));
                    }
                }

                if (undoOp.Edits.Count > 0)
                {
                    Manager.UndoManager.RecordChange(undoOp);
                }
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Error", ex.Message, icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
            }
            IsBusy = false;
        }

        private void FillDriveList(bool isRefreshing = false)
        {
            DriveInfo[] list;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                list = DriveInfo.GetDrives().Where(x => x.IsReady && (showAllDrives || (x.DriveType == DriveType.Removable && x.DriveFormat.StartsWith("FAT")))).ToArray();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                //list = DriveInfo.GetDrives().Where(x => x.IsReady && (showAllDrives || x.DriveType == DriveType.Removable || x.DriveType == DriveType.Fixed)).ToArray();//todo need to test
                list = DriveInfo.GetDrives().Where(x => x.IsReady && (showAllDrives || x.DriveType == DriveType.Removable || x.DriveType == DriveType.Fixed || (x.DriveType == DriveType.Unknown && x.DriveFormat.Equals("lifs", StringComparison.InvariantCultureIgnoreCase)))).ToArray();//todo need to test
            else//linux
                list = DriveInfo.GetDrives().Where(x => x.IsReady && (showAllDrives || ((x.DriveType == DriveType.Removable || x.DriveType == DriveType.Fixed) && x.DriveFormat.Equals("msdos", StringComparison.InvariantCultureIgnoreCase) && (x.Name.StartsWith("/media/", StringComparison.InvariantCultureIgnoreCase) || x.Name.StartsWith("/run/media/", StringComparison.InvariantCultureIgnoreCase))))).ToArray();


            if (isRefreshing)
            {
                if (DriveList.Select(x => x.Name).SequenceEqual(list.Select(x => x.Name)))
                    return;

                DriveList.Clear();
            }
            //fill drive list and try to find drive with gdemu contents
            //look for GDEMU.INI file
            foreach (DriveInfo drive in list)
            {
                try
                {
                    DriveList.Add(drive);
                    if (SelectedDrive == null && File.Exists(Path.Combine(drive.RootDirectory.FullName, Constants.MenuConfigTextFile)))
                        SelectedDrive = drive;
                }
                catch { }
            }

            //look for 01 folder
            if (SelectedDrive == null)
            {
                foreach (DriveInfo drive in list)
                {
                    try
                    {
                        if (Directory.Exists(Path.Combine(drive.RootDirectory.FullName, "01")))
                        {
                            SelectedDrive = drive;
                            break;
                        }
                    }
                    catch { }
                }
            }

            //look for /media mount
            if (SelectedDrive == null)
            {
                foreach (DriveInfo drive in list)
                {
                    try
                    {
                        if (drive.Name.StartsWith("/media/", StringComparison.InvariantCultureIgnoreCase))
                        {
                            SelectedDrive = drive;
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (!DriveList.Any())
                return;

            if (SelectedDrive == null)
                SelectedDrive = DriveList.LastOrDefault();
        }

        private bool searchInGrid(int start)
        {
            var visibleItems = (dg1.Items as System.Collections.IEnumerable)?.Cast<GdItem>().ToList()
                               ?? Manager.ItemList.ToList();

            for (int i = start; i < visibleItems.Count; i++)
            {
                var item = visibleItems[i];
                if (dg1.SelectedItem != item && Manager.SearchInItem(item, Filter))
                {
                    dg1.SelectedItem = item;
                    dg1.ScrollIntoView(item, null);
                    return true;
                }
            }
            return false;
        }

        private bool FilterInItem(GdItem item, string text)
        {
            if (item.Name?.IndexOf(text, 0, StringComparison.InvariantCultureIgnoreCase) >= 0)
                return true;
            if (item.ProductNumber?.IndexOf(text, 0, StringComparison.InvariantCultureIgnoreCase) >= 0)
                return true;
            return false;
        }

        private void ApplyFilterToGrid(string filterText)
        {
            _activeFilterText = filterText;
            Filter = filterText;
            IsFilterActive = true;

            var filteredItems = Manager.ItemList.Where(item => FilterInItem(item, filterText)).ToList();
            dg1.Items = filteredItems;

            DragDrop.SetAllowDrop(this, false);
        }

        private void ClearFilterFromGrid()
        {
            dg1.Items = Manager.ItemList;

            _activeFilterText = null;
            Filter = null;
            IsFilterActive = false;

            DragDrop.SetAllowDrop(this, !IsBusy);
        }

        private void SaveLanguageConfig(string lang)
        {
            if (Core.Manager.ConfigReadOnly) return;
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                SetOrAddSetting(config, "Language", lang);
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch { }
        }

        private void RefreshDataGrid()
        {
            var currentItems = dg1.Items;
            dg1.Items = null;
            dg1.Items = currentItems;
        }
    }
}
