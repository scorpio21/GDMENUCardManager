using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GDMENUCardManager.Core;

namespace GDMENUCardManager
{
    public partial class MainWindow
    {
        private void DataGrid_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            _blockContextMenu = false;
            _dragStartPos = null;

            var pointerProperties = e.GetCurrentPoint(dg1).Properties;

            if (pointerProperties.IsLeftButtonPressed)
            {
                _dragStartPos = e.GetPosition(this);
            }

            // Only handle right-clicks for context menu
            if (!pointerProperties.IsRightButtonPressed)
                return;

            // Find the DataGridRow under the pointer
            var source = e.Source as Avalonia.Controls.Control;
            Avalonia.Controls.DataGridRow clickedRow = null;
            GdItem clickedItem = null;
            while (source != null)
            {
                if (source is Avalonia.Controls.DataGridRow row)
                {
                    clickedRow = row;
                    clickedItem = row.DataContext as GdItem;
                    break;
                }
                source = source.Parent as Avalonia.Controls.Control;
            }

            // block context menu on menu entry (folder 01)
            if (clickedItem?.SdNumber == 1)
            {
                _blockContextMenu = true;
                e.Handled = true;
                return;
            }

            // Determine if multiple items will be selected after this click
            // If clicked item is already in selection, use current selection count (excluding menu entry)
            // If not, it will become a single selection
            int count;
            if (dg1.SelectedItems.Contains(clickedItem))
            {
                // Exclude menu entry (folder 01) from count
                count = dg1.SelectedItems.Cast<GdItem>().Count(x => x.SdNumber != 1);
            }
            else
            {
                count = 1;
            }
            bool isMultiple = count > 1;
            string singleItemName = isMultiple ? null : (clickedItem?.Name ?? ((GdItem)dg1.SelectedItem)?.Name ?? "");

            // Update context menu headers before it opens
            if (dg1.TryFindResource("rowmenu", out var resource) && resource is ContextMenu menu)
            {
                // Update title header
                var titleItem = menu.Items.OfType<MenuItem>()
                    .FirstOrDefault(m => m.Name == "MenuItemTitle");
                if (titleItem != null)
                {
                    titleItem.Header = isMultiple ? Helper.DependencyManager.GetFormattedString("StringNewDiscImages", count, "") : singleItemName;
                }

                // Update auto rename header and folder/file sub-items
                var autoRenameItem = menu.Items.OfType<MenuItem>()
                    .FirstOrDefault(m => m.Name == "MenuItemAutoRename");
                if (autoRenameItem != null)
                {
                    autoRenameItem.Header = isMultiple ? Helper.DependencyManager.GetString("StringAutoRename") : Helper.DependencyManager.GetString("StringAutoRename");

                    // Folder/file rename only available when ALL selected non-menu items are off the SD card
                    // When right-clicking an unselected item, SelectedItems hasn't updated yet,
                    // so use clickedItem directly for the single-selection case
                    bool allOffSdCard;
                    if (dg1.SelectedItems.Contains(clickedItem))
                    {
                        allOffSdCard = dg1.SelectedItems.Cast<GdItem>()
                            .Where(g => g.SdNumber != 1)
                            .All(g => g.IsNotOnSdCard);
                    }
                    else
                    {
                        allOffSdCard = clickedItem?.IsNotOnSdCard ?? true;
                    }

                    var renameFolderItem = autoRenameItem.Items.OfType<MenuItem>()
                        .FirstOrDefault(m => m.Name == "MenuItemRenameFolder");
                    if (renameFolderItem != null)
                        renameFolderItem.IsEnabled = allOffSdCard;

                    var renameFileItem = autoRenameItem.Items.OfType<MenuItem>()
                        .FirstOrDefault(m => m.Name == "MenuItemRenameFile");
                    if (renameFileItem != null)
                        renameFileItem.IsEnabled = allOffSdCard;
                }

                // Update assign folder header
                var assignFolderItem = menu.Items.OfType<MenuItem>()
                    .FirstOrDefault(m => m.Name == "MenuItemAssignFolder");
                if (assignFolderItem != null)
                {
                    assignFolderItem.Header = isMultiple ? Helper.DependencyManager.GetFormattedString("StringAssignFolderPathToCount", count) : Helper.DependencyManager.GetString("StringAssignFolder");
                }

                var assignAltItem = menu.Items.OfType<MenuItem>()
                    .FirstOrDefault(m => m.Name == "MenuItemAssignAltFolders");
                if (assignAltItem != null)
                {
                    assignAltItem.Header = Helper.DependencyManager.GetString("StringAssignAltFolders");
                    assignAltItem.IsEnabled = !isMultiple;
                }
            }
        }

        private void DataGrid_PointerReleased(object sender, Avalonia.Input.PointerReleasedEventArgs e)
        {
            // Block context menu for menu entry (folder 01) on pointer release too
            if (_blockContextMenu && e.InitialPressMouseButton == Avalonia.Input.MouseButton.Right)
            {
                e.Handled = true;
                _blockContextMenu = false;
            }
        }

        private void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            // Check if this is a menu item
            if (e.Row?.DataContext is GdItem item)
            {
                bool isMenuItem = item.Ip?.Name == "GDMENU" || item.Ip?.Name == "openMenu";

                if (isMenuItem)
                {
                    // Prevent editing ANY cell for menu items
                    e.Cancel = true;
                    return;
                }

                // Prevent editing Serial, Type, or Disc for compressed files
                if (item.FileFormat == FileFormat.SevenZip)
                {
                    var headerText = "";
                    if (e.Column.Header is TextBlock tb)
                        headerText = tb.Text;
                    else if (e.Column.Header is string s)
                        headerText = s;

                    bool isLocked = false;
                    if (headerText == GetString("StringSerial") ||
                        headerText == GetString("StringType") ||
                        headerText == GetString("StringDisc"))
                    {
                        isLocked = true;
                    }

                    if (isLocked)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                // Capture old value for undo
                _editingItem = item;
                var column = e.Column;
                if (column.Header?.ToString() == "Title")
                {
                    _editingPropertyName = nameof(GdItem.Name);
                    _editingOldValue = item.Name;
                }
                else if (column.Header?.ToString() == "Serial")
                {
                    _editingPropertyName = nameof(GdItem.ProductNumber);
                    _editingOldValue = item.ProductNumber;
                }
                else if (column.Header?.ToString() == "Folder")
                {
                    _editingPropertyName = nameof(GdItem.Folder);
                    _editingOldValue = item.Folder;
                }
                else if (column.Header?.ToString() == "Type")
                {
                    _editingPropertyName = nameof(GdItem.DiscType);
                    _editingOldValue = item.DiscType;
                }
                else if (column.Header?.ToString() == "Disc")
                {
                    _editingPropertyName = nameof(GdItem.Disc);
                    _editingOldValue = item.Disc;
                }
                else
                {
                    _editingItem = null;
                    _editingPropertyName = null;
                    _editingOldValue = null;
                }
            }

            // Prevent editing the Disc column when not in openMenu mode (for non-menu items)
            if (e.Column?.Header?.ToString() == "Disc" && MenuKindSelected != MenuKind.openMenu)
            {
                e.Cancel = true;
                _editingItem = null;
                _editingPropertyName = null;
                _editingOldValue = null;
            }
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel)
            {
                // Edit was cancelled, no undo needed
                _editingItem = null;
                _editingPropertyName = null;
                _editingOldValue = null;
                return;
            }

            if (_editingItem == null || _editingPropertyName == null)
                return;

            // Capture values in local variables
            var item = _editingItem;
            var propertyName = _editingPropertyName;
            var oldValue = _editingOldValue;

            // Try to get the new value directly from the editing element
            // This is more reliable than waiting for binding to update
            object newValue = null;
            if (e.EditingElement is TextBox textBox)
            {
                newValue = textBox.Text;
            }
            else if (e.EditingElement is ComboBox comboBox)
            {
                // Avalonia ComboBox uses SelectedItem
                newValue = comboBox.SelectedItem;
            }
            else if (e.EditingElement is Panel panel)
            {
                // Template columns may wrap the actual control in a panel
                var innerTextBox = panel.Children.OfType<TextBox>().FirstOrDefault();
                if (innerTextBox != null)
                {
                    newValue = innerTextBox.Text;
                }
                else
                {
                    var innerComboBox = panel.Children.OfType<ComboBox>().FirstOrDefault();
                    if (innerComboBox != null)
                    {
                        newValue = innerComboBox.SelectedItem;
                    }
                }
            }

            // Validate printable ASCII for Title, Serial, and Folder columns
            if (newValue is string newStr &&
                (propertyName == nameof(GdItem.Name) || propertyName == nameof(GdItem.ProductNumber) || propertyName == nameof(GdItem.Folder)) &&
                !Helper.IsValidPrintableAscii(newStr))
            {
                // Revert the editing element and the property (binding may have already pushed)
                var revertValue = oldValue as string ?? "";
                if (e.EditingElement is TextBox revertTb)
                    revertTb.Text = revertValue;
                else if (e.EditingElement is Panel revertPanel)
                {
                    var innerTb = revertPanel.Children.OfType<TextBox>().FirstOrDefault();
                    if (innerTb != null) innerTb.Text = revertValue;
                }
                // Revert the property in case binding already updated it
                RevertProperty(item, propertyName, oldValue);
                e.Cancel = true;
                // keep editing state so the next commit attempt can validate
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow("Invalid Characters",
                        "Only printable ASCII characters (letters, numbers, and standard symbols) are supported by openMenu.",
                        icon: MessageBox.Avalonia.Enums.Icon.Warning).ShowDialog(this);
                });
                return;
            }

            // Clear immediately so next edit can capture its own values
            _editingItem = null;
            _editingPropertyName = null;
            _editingOldValue = null;

            // Only record if we got a new value and it's different from old
            if (newValue != null && !Equals(oldValue, newValue))
            {
                // check if Folder edit conflicts with an alt folder
                if (propertyName == nameof(GdItem.Folder) && newValue is string newFolder)
                {
                    var trimmed = newFolder.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && item.AlternativeFolders.Contains(trimmed))
                    {
                        // Revert the property in case binding already updated it
                        RevertProperty(item, propertyName, oldValue);
                        e.Cancel = true;
                        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                        {
                            await MessageBoxManager.GetMessageBoxStandardWindow("Duplicate Folder Path",
                                "This folder path is already assigned to this disc image as an additional folder path.",
                                icon: MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
                        });
                        return;
                    }
                }

                Manager.UndoManager.RecordChange(new PropertyEditOperation
                {
                    Item = item,
                    PropertyName = propertyName,
                    OldValue = oldValue,
                    NewValue = newValue
                });

                // If Serial column was edited, check for translation after binding updates
                if (propertyName == nameof(GdItem.ProductNumber))
                {
                    // Post to dispatcher so the binding has time to update the property
                    // Skip if a button handler is already handling the translation
                    Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        if (!_handlingSerialTranslation && item.WasSerialTranslated)
                        {
                            await Helper.DependencyManager.ShowSerialTranslationDialog(new[] { item });
                        }
                    }, Avalonia.Threading.DispatcherPriority.Background);
                }
            }
        }

        private async void MainWindow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectedDrive) && SelectedDrive != null)
                await LoadItemsFromCard();
            else if (e.PropertyName == nameof(MenuKindSelected))
            {
                UpdateFolderColumnVisibility();
                UpdateSortButtonTooltip();
            }
        }

        private void Manager_MenuKindChanged(object sender, EventArgs e)
        {
            // Update column visibility and sort tooltip immediately when menu kind is detected during loading
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RaisePropertyChanged(nameof(MenuKindSelected));
                UpdateFolderColumnVisibility();
                UpdateSortButtonTooltip();
            }, Avalonia.Threading.DispatcherPriority.Send);
        }

        private void ItemList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            updateTotalSize();

            // If filter is active, refresh the filtered view (e.g., after undo re-inserts items)
            if (IsFilterActive && _activeFilterText != null)
            {
                var filteredItems = Manager.ItemList.Where(item => FilterInItem(item, _activeFilterText)).ToList();
                if (filteredItems.Count == 0)
                    ClearFilterFromGrid();
                else
                    dg1.Items = filteredItems;
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (IsBusy)
                e.Cancel = true;
            else
            {
                Manager.ItemList.CollectionChanged -= ItemList_CollectionChanged;//release events
                SaveWindowBounds();
            }
        }

        private void DataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (IsFilterActive || IsBusy || Manager.sdPath == null)
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }

            if (e.Data.Contains(DataFormats.FileNames))
            {
                e.DragEffects = DragDropEffects.Copy;
            }
            else if (e.Data.Contains("GDMENU_GdItems"))
            {
                e.DragEffects = DragDropEffects.Move;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }

        private async void DataGrid_Drop(object sender, DragEventArgs e)
        {
            if (IsFilterActive || IsBusy || Manager.sdPath == null)
                return;

            IsBusy = true;
            try
            {
                var dropResult = await DragDropHandler.ExecuteDrop(dg1, e, Manager, this, GetString);
                if (dropResult == null) return;

                if (dropResult.IsAdd && (dropResult.AddedItems.Any() || dropResult.RemovedItems.Any()))
                {
                    // Record undo for additions and replacements
                    if (dropResult.RemovedItems.Any())
                    {
                        var removeOp = new MultiItemRemoveOperation { ItemList = Manager.ItemList };
                        foreach (var item in dropResult.RemovedItems)
                            removeOp.Items.Add(item);
                        Manager.UndoManager.RecordChange(removeOp);
                    }

                    if (dropResult.AddedItems.Any())
                    {
                        var addOp = new MultiItemAddOperation { ItemList = Manager.ItemList };
                        foreach (var item in dropResult.AddedItems)
                            addOp.Items.Add(item);
                        Manager.UndoManager.RecordChange(addOp);
                    }

                    await ShowSerialTranslationDialogIfNeeded();
                }
                else if (dropResult.IsReorder && dropResult.OldOrder != null && dropResult.NewOrder != null)
                {
                    Manager.UndoManager.RecordChange(new ListReorderOperation("Manual Reorder")
                    {
                        ItemList = Manager.ItemList,
                        OldOrder = dropResult.OldOrder,
                        NewOrder = dropResult.NewOrder
                    });
                }
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Error", ex.Message, icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void DataGrid_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_dragStartPos != null && !IsBusy && !IsFilterActive)
            {
                var currentPos = e.GetPosition(this);
                var delta = _dragStartPos.Value - currentPos;
                if (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5)
                {
                    _dragStartPos = null;
                    var selectedItems = dg1.SelectedItems.Cast<GdItem>().ToList();
                    if (selectedItems.Any() && !selectedItems.Any(x => x.IsMenuItem))
                    {
                        var data = new DataObject();
                        data.Set("GDMENU_GdItems", selectedItems);
                        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
                    }
                }
            }
        }

        private async void ButtonSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (IsFilterActive)
                return;

            var emptySerials = Manager.ItemList
                .Where(x => x.Ip?.Name != "GDMENU" && x.Ip?.Name != "openMenu"
                    && x.FileFormat != Core.FileFormat.SevenZip
                    && x.FileFormat != Core.FileFormat.CueBinNonGame
                    && string.IsNullOrWhiteSpace(x.ProductNumber))
                .ToList();

            if (emptySerials.Count > 0)
            {
                var count = emptySerials.Count;
                var msg = count == 1
                    ? Helper.DependencyManager.GetString("StringMissingSerialID")
                    : Helper.DependencyManager.GetFormattedString("StringMissingSerialIDs", count);
                msg += "\n\n" + Helper.DependencyManager.GetString("StringMissingSerialIDNote");
                var msgBox = MessageBoxManager.GetMessageBoxStandardWindow(Helper.DependencyManager.GetString("StringMissingSerialIDTitle"),
                    msg, MessageBox.Avalonia.Enums.ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Error);
                await msgBox.ShowDialog(this);
                return;
            }

            await Save();
        }

        private async void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            IsBusy = true;
            if (Manager.debugEnabled)
            {
                var list = DriveInfo.GetDrives().Where(x => x.IsReady).Select(x => $"{x.DriveType}; {x.DriveFormat}; {x.Name}").ToArray();
                await MessageBoxManager.GetMessageBoxStandardWindow("Debug", string.Join(Environment.NewLine, list), icon: MessageBox.Avalonia.Enums.Icon.None).ShowDialog(this);
            }
            await new AboutWindow().ShowDialog(this);
            IsBusy = false;
        }

        private async void ButtonFolder_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog { Title = "Select Temporary Folder" };

            if (!string.IsNullOrEmpty(TempFolder))
                folderDialog.Directory = TempFolder;

            var selectedFolder = await folderDialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(selectedFolder))
                TempFolder = selectedFolder;
        }

        private async void ButtonResetTempFolder_Click(object sender, RoutedEventArgs e)
        {
            var result = await MessageBoxManager.GetMessageBoxStandardWindow("Reset", "Reset the Temporary Folder path to default?", MessageBox.Avalonia.Enums.ButtonEnum.YesNo, MessageBox.Avalonia.Enums.Icon.Question).ShowDialog(this);
            if (result == MessageBox.Avalonia.Enums.ButtonResult.Yes)
            {
                TempFolder = Path.GetTempPath();
                SaveTempFolderConfig();
            }
        }

        private async void ButtonInfo_Click(object sender, RoutedEventArgs e)
        {
            IsBusy = true;
            try
            {
                var btn = (Button)sender;
                var item = (GdItem)btn.CommandParameter;

                if (item.Ip == null)
                    await Manager.LoadIP(item);

                await new InfoWindow(item).ShowDialog(this);
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Error", ex.Message, icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
            }
            IsBusy = false;
        }

        private async void ButtonArtwork_Click(object sender, RoutedEventArgs e)
        {
            // Commit any pending cell edits to ensure we read the current Serial value
            dg1.CommitEdit();

            IsBusy = true;
            try
            {
                var btn = (Button)sender;
                var item = (GdItem)btn.CommandParameter;

                if (item == null || !item.CanManageArtwork)
                    return;

                // handle serial translation before opening artwork window
                if (item.WasSerialTranslated)
                {
                    _handlingSerialTranslation = true;
                    try
                    {
                        await Helper.DependencyManager.ShowSerialTranslationDialog(new[] { item });
                    }
                    finally
                    {
                        _handlingSerialTranslation = false;
                    }
                }

                var navigableItems = Manager.ItemList.Where(i => i.CanManageArtwork).ToList();
                await new ArtworkWindow(item, Manager, navigableItems).ShowDialog(this);

                // Refresh column visibility in case BoxDat state changed
                UpdateFolderColumnVisibility();
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Error", ex.Message, icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ButtonUndo_Click(object sender, RoutedEventArgs e)
        {
            Manager.UndoManager.Undo();
        }

        private void ButtonRedo_Click(object sender, RoutedEventArgs e)
        {
            Manager.UndoManager.Redo();
        }

        private async void ButtonSort_Click(object sender, RoutedEventArgs e)
        {
            if (IsFilterActive)
                return;
            var sortDescription = MenuKindSelected == MenuKind.openMenu
                ? GetString("StringSortDescriptionOpenMenu")
                : GetString("StringSortDescriptionGDMenu");
            var result = await MessageBoxManager.GetMessageBoxStandardWindow(
                GetString("StringSortListTitle"),
                sortDescription,
                MessageBox.Avalonia.Enums.ButtonEnum.YesNo,
                MessageBox.Avalonia.Enums.Icon.Question).ShowDialog(this);

            if (result != MessageBox.Avalonia.Enums.ButtonResult.Yes)
                return;

            IsBusy = true;
            try
            {
                await Manager.SortList();
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow(GetString("StringError"), ex.Message, icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
            }
            IsBusy = false;
        }

        private async void ButtonDiscImageOptions_Click(object sender, RoutedEventArgs e)
        {
            var window = new DiscImageOptionsWindow(SaveDiscImageOptionsConfig);
            window.DataContext = this;
            await window.ShowDialog(this);
        }

        private async void ButtonSdHealth_Click(object sender, RoutedEventArgs e)
        {
            var window = new SdHealthWindow(Manager.sdPath);
            await window.ShowDialog(this);
        }

        private async void ButtonDatTools_Click(object sender, RoutedEventArgs e)
        {
            var window = new DatToolsWindow(Manager, async () => await LoadItemsFromCard());
            await window.ShowDialog(this);
        }

        private async void ButtonPreload_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.ItemList.Count == 0)
                return;

            IsBusy = true;
            try
            {
                await Manager.LoadIpAll();
            }
            catch (ProgressWindowClosedException) { }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Error", ex.Message, icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ButtonRefreshDrive_Click(object sender, RoutedEventArgs e)
        {
            // Clear custom path if set
            if (IsUsingCustomPath)
            {
                CustomSdPath = null;
                Manager.sdPath = null;
                Manager.ItemList.Clear();
            }
            FillDriveList(true);
        }

        private async void ButtonBrowseSdPath_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog { Title = "Select SD Card Folder" };
            var selectedPath = await folderDialog.ShowAsync(this);

            if (!string.IsNullOrEmpty(selectedPath))
            {
                // Check if it looks like a GDEMU SD card
                bool hasGdemuIni = System.IO.File.Exists(System.IO.Path.Combine(selectedPath, Constants.MenuConfigTextFile));
                bool has01Folder = System.IO.Directory.Exists(System.IO.Path.Combine(selectedPath, "01"));

                if (!hasGdemuIni && !has01Folder)
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow(
                        "Notice",
                        "The selected folder does not appear to be a GDEMU SD card.\n\n" +
                        "No GDEMU.INI file or numbered folders (01, 02, etc.) were found.\n\n" +
                        "You may proceed, but the folder may not work as expected.",
                        MessageBox.Avalonia.Enums.ButtonEnum.Ok,
                        MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
                }

                // Set the custom path
                CustomSdPath = selectedPath;
                Manager.sdPath = selectedPath;
                SelectedDrive = null; // Clear drive selection

                // Load items from the custom path
                await LoadItemsFromCard();
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update context menu headers based on selection
            if (dg1.TryFindResource("rowmenu", out var resource) && resource is ContextMenu menu)
            {
                int count = dg1.SelectedItems.Count;
                bool isMultiple = count > 1;

                // Update title header
                var titleItem = menu.Items.OfType<MenuItem>()
                    .FirstOrDefault(m => m.Name == "MenuItemTitle");
                if (titleItem != null)
                {
                    titleItem.Header = isMultiple ? Helper.DependencyManager.GetFormattedString("StringDiscImagesCount", count) : ((GdItem)dg1.SelectedItem)?.Name;
                }

                // Update auto rename header and folder/file sub-items
                var autoRenameItem = menu.Items.OfType<MenuItem>()
                    .FirstOrDefault(m => m.Name == "MenuItemAutoRename");
                if (autoRenameItem != null)
                {
                    autoRenameItem.Header = isMultiple ? Helper.DependencyManager.GetString("StringAutoRename") : Helper.DependencyManager.GetString("StringAutoRename");

                    // Folder/file rename only available when ALL selected non-menu items are off the SD card
                    bool allOffSdCard = dg1.SelectedItems.Cast<GdItem>()
                        .Where(g => g.SdNumber != 1)
                        .All(g => g.IsNotOnSdCard);

                    var renameFolderItem = autoRenameItem.Items.OfType<MenuItem>()
                        .FirstOrDefault(m => m.Name == "MenuItemRenameFolder");
                    if (renameFolderItem != null)
                        renameFolderItem.IsEnabled = allOffSdCard;

                    var renameFileItem = autoRenameItem.Items.OfType<MenuItem>()
                        .FirstOrDefault(m => m.Name == "MenuItemRenameFile");
                    if (renameFileItem != null)
                        renameFileItem.IsEnabled = allOffSdCard;
                }

                // Update assign folder header
                var assignFolderItem = menu.Items.OfType<MenuItem>()
                    .FirstOrDefault(m => m.Name == "MenuItemAssignFolder");
                if (assignFolderItem != null)
                {
                    assignFolderItem.Header = isMultiple ? Helper.DependencyManager.GetFormattedString("StringAssignFolderPathToCount", count) : Helper.DependencyManager.GetString("StringAssignFolder");
                }

                var assignAltItem = menu.Items.OfType<MenuItem>()
                    .FirstOrDefault(m => m.Name == "MenuItemAssignAltFolders");
                if (assignAltItem != null)
                {
                    assignAltItem.Header = Helper.DependencyManager.GetString("StringAssignAltFolders");
                    assignAltItem.IsEnabled = !isMultiple;
                }

                // Disable context menu for menu entry (folder 01) by setting all items disabled
                // Note: In Avalonia, we can't easily prevent context menu from showing,
                // but individual handlers already filter out SdNumber == 1
            }
        }

        private async void MenuItemRename_Click(object sender, RoutedEventArgs e)
        {
            var menuitem = (MenuItem)sender;
            var item = (GdItem)menuitem.CommandParameter;

            // Protect menu entry (folder 01) from renaming
            if (item?.SdNumber == 1)
                return;

            var oldName = item.Name;

            var result = await MessageBoxManager.GetMessageBoxInputWindow(new MessageBox.Avalonia.DTO.MessageBoxInputParams
            {
                ContentTitle = Helper.DependencyManager.GetString("StringRename"),
                ContentHeader = Helper.DependencyManager.GetString("StringInformNewName"),
                ContentMessage = Helper.DependencyManager.GetString("StringNewName"),
                WatermarkText = item.Name,
                ShowInCenter = true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ButtonDefinitions = new ButtonDefinition[] { new ButtonDefinition { Name = Helper.DependencyManager.GetString("StringOk") }, new ButtonDefinition { Name = Helper.DependencyManager.GetString("StringCancel") } },
            }).ShowDialog(this);

            if (result?.Button == Helper.DependencyManager.GetString("StringOk") && !string.IsNullOrWhiteSpace(result.Message))
            {
                var newName = result.Message.Trim();
                if (newName != oldName)
                {
                    item.Name = newName;
                    Manager.UndoManager.RecordChange(new PropertyEditOperation
                    {
                        Item = item,
                        PropertyName = nameof(GdItem.Name),
                        OldValue = oldName,
                        NewValue = newName
                    });
                }
            }
        }

        private void MenuItemRenameSentence_Click(object sender, RoutedEventArgs e)
        {
            // Filter out menu entry (folder 01) from renaming
            var items = dg1.SelectedItems.Cast<GdItem>().Where(x => x.SdNumber != 1).ToList();

            if (items.Count == 0)
                return;

            var undoOp = new MultiPropertyEditOperation("Title Case")
            {
                PropertyName = nameof(GdItem.Name)
            };

            foreach (var item in items)
            {
                var oldName = item.Name;
                var newName = TitleCaseHelper.ToTitleCase(item.Name);
                if (newName != oldName)
                {
                    undoOp.Edits.Add((item, oldName, newName));
                    item.Name = newName;
                }
            }

            if (undoOp.Edits.Count > 0)
            {
                Manager.UndoManager.RecordChange(undoOp);
            }
        }

        private void MenuItemRenameUppercase_Click(object sender, RoutedEventArgs e)
        {
            // Filter out menu entry (folder 01) from renaming
            var items = dg1.SelectedItems.Cast<GdItem>().Where(x => x.SdNumber != 1).ToList();

            if (items.Count == 0)
                return;

            var undoOp = new MultiPropertyEditOperation("Uppercase")
            {
                PropertyName = nameof(GdItem.Name)
            };

            foreach (var item in items)
            {
                var oldName = item.Name;
                var newName = item.Name.ToUpperInvariant();
                if (newName != oldName)
                {
                    undoOp.Edits.Add((item, oldName, newName));
                    item.Name = newName;
                }
            }

            if (undoOp.Edits.Count > 0)
            {
                Manager.UndoManager.RecordChange(undoOp);
            }
        }

        private void MenuItemRenameLowercase_Click(object sender, RoutedEventArgs e)
        {
            // Filter out menu entry (folder 01) from renaming
            var items = dg1.SelectedItems.Cast<GdItem>().Where(x => x.SdNumber != 1).ToList();

            if (items.Count == 0)
                return;

            var undoOp = new MultiPropertyEditOperation("Lowercase")
            {
                PropertyName = nameof(GdItem.Name)
            };

            foreach (var item in items)
            {
                var oldName = item.Name;
                var newName = item.Name.ToLowerInvariant();
                if (newName != oldName)
                {
                    undoOp.Edits.Add((item, oldName, newName));
                    item.Name = newName;
                }
            }

            if (undoOp.Edits.Count > 0)
            {
                Manager.UndoManager.RecordChange(undoOp);
            }
        }

        private async void MenuItemRenameIP_Click(object sender, RoutedEventArgs e)
        {
            await renameSelection(RenameBy.Ip);
        }
        private async void MenuItemRenameFolder_Click(object sender, RoutedEventArgs e)
        {
            await renameSelection(RenameBy.Folder);

        }
        private async void MenuItemRenameFile_Click(object sender, RoutedEventArgs e)
        {
            await renameSelection(RenameBy.File);
        }

        private async void MenuItemAssignFolder_Click(object sender, RoutedEventArgs e)
        {
            // Commit any pending cell edits
            dg1.CommitEdit();

            // Only allow in openMenu mode
            if (MenuKindSelected != MenuKind.openMenu)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Info", "Assign Folder Path is only available in openMenu mode.", icon: MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
                return;
            }

            var selectedItems = dg1.SelectedItems.Cast<GdItem>().ToList();

            // Filter out menu items
            selectedItems = selectedItems.Where(item =>
                item.Ip?.Name != "GDMENU" && item.Ip?.Name != "openMenu").ToList();

            if (selectedItems.Count == 0)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Info", "No valid items selected.", icon: MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
                return;
            }

            // handle serial translations before proceeding
            var translatedItems = selectedItems.Where(item => item.WasSerialTranslated).ToList();
            if (translatedItems.Count > 0)
            {
                _handlingSerialTranslation = true;
                try
                {
                    await Helper.DependencyManager.ShowSerialTranslationDialog(translatedItems);
                }
                finally
                {
                    _handlingSerialTranslation = false;
                }
            }

            Manager.InitializeKnownFolders();
            var dialog = new AssignFolderWindow(selectedItems.Count, Manager.KnownFolders);
            var result = await dialog.ShowDialog<bool?>(this);

            if (result == true)
            {
                var folderPath = dialog.FolderPath?.Trim() ?? string.Empty;

                // check if the new primary folder conflicts with any item's alt folders
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var conflicting = selectedItems.Where(item =>
                        item.AlternativeFolders.Contains(folderPath)).ToList();
                    if (conflicting.Count > 0)
                    {
                        await MessageBoxManager.GetMessageBoxStandardWindow("Duplicate Folder Path",
                            "This folder path is already assigned to this disc image as an additional folder path.",
                            icon: MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
                        return;
                    }
                }

                var undoOp = new MultiPropertyEditOperation("Assign Folder Path")
                {
                    PropertyName = nameof(GdItem.Folder)
                };

                foreach (var item in selectedItems)
                {
                    var oldFolder = item.Folder;
                    if (oldFolder != folderPath)
                    {
                        undoOp.Edits.Add((item, oldFolder, folderPath));
                        item.Folder = folderPath;
                    }
                }

                if (undoOp.Edits.Count > 0)
                {
                    Manager.UndoManager.RecordChange(undoOp);
                }
            }
        }

        private async void MenuItemAssignAltFolders_Click(object sender, RoutedEventArgs e)
        {
            dg1.CommitEdit();

            if (MenuKindSelected != MenuKind.openMenu)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Info",
                    "Additional folder paths are only available in openMenu mode.",
                    icon: MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
                return;
            }

            var item = dg1.SelectedItems.Cast<GdItem>()
                .FirstOrDefault(x => x.SdNumber != 1);

            if (item == null)
                return;

            Manager.InitializeKnownFolders();
            var dlg = new AssignAltFoldersWindow(item, Manager.KnownFolders);
            var dlgResult = await dlg.ShowDialog<bool?>(this);

            if (dlgResult == true)
            {
                var oldAltFolders = new List<string>(item.AlternativeFolders);
                var newAltFolders = dlg.GetAltFolders();

                if (!oldAltFolders.SequenceEqual(newAltFolders))
                {
                    item.AlternativeFolders = newAltFolders;
                    Manager.UndoManager.RecordChange(new AltFoldersChangeOperation
                    {
                        Item = item,
                        OldAltFolders = oldAltFolders,
                        NewAltFolders = new List<string>(item.AlternativeFolders)
                    });
                }
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl+Z for Undo
            if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
            {
                if (Manager.UndoManager.CanUndo)
                {
                    Manager.UndoManager.Undo();
                    e.Handled = true;
                }
            }
            // Handle Ctrl+Y for Redo
            else if (e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control)
            {
                if (Manager.UndoManager.CanRedo)
                {
                    Manager.UndoManager.Redo();
                    e.Handled = true;
                }
            }
        }

        private async void GridOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && !(e.Source is TextBox))
            {
                List<GdItem> toRemove = new List<GdItem>();
                foreach (GdItem item in dg1.SelectedItems)
                {
                    if (item.SdNumber == 1)
                    {
                        if (item.Ip == null)
                        {
                            IsBusy = true;
                            await Manager.LoadIP(item);
                            IsBusy = false;
                        }
                        if (item.Ip.Name != "GDMENU" && item.Ip.Name != "openMenu")//dont let the user exclude GDMENU, openMenu
                            toRemove.Add(item);
                    }
                    else
                    {
                        toRemove.Add(item);
                    }
                }

                if (toRemove.Count > 0)
                {
                    // Record undo operation with indices before removal
                    var undoOp = new MultiItemRemoveOperation { ItemList = Manager.ItemList };
                    foreach (var item in toRemove)
                    {
                        undoOp.Items.Add((item, Manager.ItemList.IndexOf(item)));
                    }

                    foreach (var item in toRemove)
                        Manager.ItemList.Remove(item);

                    Manager.UndoManager.RecordChange(undoOp);

                    if (IsFilterActive)
                    {
                        var filteredItems = Manager.ItemList.Where(item => FilterInItem(item, _activeFilterText)).ToList();
                        if (filteredItems.Count == 0)
                        {
                            await MessageBoxManager.GetMessageBoxStandardWindow("Filter",
                                "Nothing to show for the currently applied filter.",
                                icon: MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
                            ClearFilterFromGrid();
                        }
                        else
                        {
                            dg1.Items = filteredItems;
                        }
                    }
                }

                e.Handled = true;
            }
        }

        private async void ButtonAddGames_Click(object sender, RoutedEventArgs e)
        {
            if (IsFilterActive)
                return;
            var fileDialog = new OpenFileDialog
            {
                Title = "Select File(s)",
                AllowMultiple = true,
                Filters = fileFilterList
            };

            var files = await fileDialog.ShowAsync(this);
            if (files != null && files.Any())
            {
                IsBusy = true;

                var invalid = await Manager.AddGames(files);

                if (invalid.Any())
                    await MessageBoxManager.GetMessageBoxStandardWindow("Ignored folders/files", string.Join(Environment.NewLine, invalid), icon: MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);

                // Show serial translation dialog if any items were translated
                await ShowSerialTranslationDialogIfNeeded();

                IsBusy = false;
            }
        }

        private async void ButtonRemoveGame_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = dg1.SelectedItems.Cast<GdItem>().ToArray();
            if (selectedItems.Length == 0)
                return;

            // Collect items and indices before removal for undo
            var undoOp = new MultiItemRemoveOperation { ItemList = Manager.ItemList };
            foreach (var item in selectedItems)
            {
                undoOp.Items.Add((item, Manager.ItemList.IndexOf(item)));
            }

            foreach (var item in selectedItems)
                Manager.ItemList.Remove(item);

            Manager.UndoManager.RecordChange(undoOp);

            if (IsFilterActive)
            {
                var filteredItems = Manager.ItemList.Where(item => FilterInItem(item, _activeFilterText)).ToList();
                if (filteredItems.Count == 0)
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow("Filter",
                        "Nothing to show for the currently applied filter.",
                        icon: MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
                    ClearFilterFromGrid();
                }
                else
                {
                    dg1.Items = filteredItems;
                }
            }
        }

        private void ButtonMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (IsFilterActive)
                return;
            var selectedItems = dg1.SelectedItems.Cast<GdItem>().ToArray();

            if (!selectedItems.Any())
                return;

            // Don't allow moving menu items
            if (selectedItems.Any(item => item.Ip?.Name == "GDMENU" || item.Ip?.Name == "openMenu"))
                return;

            int moveTo = Manager.ItemList.IndexOf(selectedItems.First()) - 1;

            // Don't allow moving items above the menu (position 0)
            if (moveTo < 1)
                return;

            // Capture order before move for undo
            var oldOrder = new List<GdItem>(Manager.ItemList);

            foreach (var item in selectedItems)
                Manager.ItemList.Remove(item);

            foreach (var item in selectedItems)
                Manager.ItemList.Insert(moveTo++, item);

            // Record undo operation
            Manager.UndoManager.RecordChange(new ListReorderOperation("Move Up")
            {
                ItemList = Manager.ItemList,
                OldOrder = oldOrder,
                NewOrder = new List<GdItem>(Manager.ItemList)
            });

            dg1.SelectedItems.Clear();
            foreach (var item in selectedItems)
                dg1.SelectedItems.Add(item);
        }

        private void ButtonMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (IsFilterActive)
                return;
            var selectedItems = dg1.SelectedItems.Cast<GdItem>().ToArray();

            if (!selectedItems.Any())
                return;

            // Don't allow moving menu items
            if (selectedItems.Any(item => item.Ip?.Name == "GDMENU" || item.Ip?.Name == "openMenu"))
                return;

            int moveTo = Manager.ItemList.IndexOf(selectedItems.Last()) - selectedItems.Length + 2;

            if (moveTo > Manager.ItemList.Count - selectedItems.Length)
                return;

            // Capture order before move for undo
            var oldOrder = new List<GdItem>(Manager.ItemList);

            foreach (var item in selectedItems)
                Manager.ItemList.Remove(item);

            foreach (var item in selectedItems)
                Manager.ItemList.Insert(moveTo++, item);

            // Record undo operation
            Manager.UndoManager.RecordChange(new ListReorderOperation("Move Down")
            {
                ItemList = Manager.ItemList,
                OldOrder = oldOrder,
                NewOrder = new List<GdItem>(Manager.ItemList)
            });

            dg1.SelectedItems.Clear();
            foreach (var item in selectedItems)
                dg1.SelectedItems.Add(item);
        }

        private async void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.ItemList.Count == 0 || string.IsNullOrWhiteSpace(Filter))
                return;

            try
            {
                IsBusy = true;
                await Manager.LoadIpAll();
                IsBusy = false;
            }
            catch (ProgressWindowClosedException)
            {

            }

            if (dg1.SelectedIndex == -1 || !searchInGrid(dg1.SelectedIndex))
            {
                if (!searchInGrid(0))
                    await MessageBoxManager.GetMessageBoxStandardWindow("Search", "No matches found.",
                        icon: MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
            }
        }

        private async void ButtonFilter_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.ItemList.Count == 0 || string.IsNullOrWhiteSpace(Filter))
                return;

            var filterText = Filter;

            try
            {
                IsBusy = true;
                await Manager.LoadIpAll();
                IsBusy = false;
            }
            catch (ProgressWindowClosedException) { }

            bool hasMatches = Manager.ItemList.Any(item => FilterInItem(item, filterText));
            if (!hasMatches)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Filter", "No matches found.",
                    icon: MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
                return;
            }

            ApplyFilterToGrid(filterText);

            Manager.UndoManager.RecordChange(new FilterApplyOperation
            {
                FilterText = filterText,
                ApplyFilter = text => ApplyFilterToGrid(text),
                ClearFilter = () => ClearFilterFromGrid()
            });
        }

        private void ButtonFilterReset_Click(object sender, RoutedEventArgs e)
        {
            if (!IsFilterActive)
                return;
            ClearFilterFromGrid();
        }

        private void ButtonLangEn_Click(object sender, RoutedEventArgs e)
        {
            App.ChangeLanguage("en-US");
            SaveLanguageConfig("en-US");
            RefreshDataGrid();
        }

        private void ButtonLangEs_Click(object sender, RoutedEventArgs e)
        {
            App.ChangeLanguage("es-ES");
            SaveLanguageConfig("es-ES");
            RefreshDataGrid();
        }

        private async void ButtonBatchFolderRename_Click(object sender, RoutedEventArgs e)
        {
            if (IsFilterActive)
                return;
            if (Manager.ItemList.Count == 0)
                return;

            try
            {
                var folderCounts = Manager.GetFolderCounts();

                if (folderCounts.Count == 0)
                {
                    await MessageBoxManager.GetMessageBoxStandardWindow("Information", GetString("NoFoldersFound") ?? "No folders found in the current game list.", MessageBox.Avalonia.Enums.ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
                    return;
                }

                var window = new BatchFolderRenameWindow(folderCounts, Manager.ItemList.Count);
                var result = await window.ShowDialog<bool>(this);

                if (result && window.FolderMappings != null)
                {
                    // snapshot before applying
                    var snapshots = Manager.ItemList.Select(i => new BatchFolderRenameOperation.ItemSnapshot
                    {
                        Item = i,
                        OldFolder = i.Folder,
                        OldAltFolders = new List<string>(i.AlternativeFolders)
                    }).ToList();

                    var (updatedCount, conflictsRemoved) = Manager.ApplyFolderMappings(window.FolderMappings);

                    if (updatedCount > 0 || conflictsRemoved > 0)
                    {
                        var undoOp = new BatchFolderRenameOperation();
                        foreach (var s in snapshots)
                        {
                            s.NewFolder = s.Item.Folder;
                            s.NewAltFolders = new List<string>(s.Item.AlternativeFolders);
                            if (s.OldFolder != s.NewFolder || !s.OldAltFolders.SequenceEqual(s.NewAltFolders))
                                undoOp.Snapshots.Add(s);
                        }

                        if (undoOp.Snapshots.Count > 0)
                            Manager.UndoManager.RecordChange(undoOp);

                        var msg = $"{updatedCount} disc image(s) updated across {window.FolderMappings.Count} folder(s).";
                        if (conflictsRemoved > 0)
                            msg += $"\n\n{conflictsRemoved} additional folder path(s) were automatically removed because they became duplicates of their disc image's primary folder path after renaming.";
                        msg += "\n\nClick 'Save Changes' to write updates to SD card.";
                        await MessageBoxManager.GetMessageBoxStandardWindow("Information", msg, MessageBox.Avalonia.Enums.ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Info).ShowDialog(this);
                        
                        // Force refresh
                        var list = Manager.ItemList.ToList();
                        Manager.ItemList.Clear();
                        foreach (var item in list)
                            Manager.ItemList.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Error", "Error opening Batch Folder Rename:\n" + ex.Message, MessageBox.Avalonia.Enums.ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Error).ShowDialog(this);
            }
        }
    }
}
