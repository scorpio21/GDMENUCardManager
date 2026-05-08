using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GDMENUCardManager.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MessageBox.Avalonia;

namespace GDMENUCardManager.AvaloniaUI
{
    internal class DropResult
    {
        public bool IsReorder { get; set; }
        public bool IsAdd { get; set; }
        public List<GdItem> OldOrder { get; set; }
        public List<GdItem> NewOrder { get; set; }
        public List<(GdItem Item, int Index)> AddedItems { get; set; } = new List<(GdItem, int)>();
        public List<(GdItem Item, int Index)> RemovedItems { get; set; } = new List<(GdItem, int)>();
    }

    internal static class DragDropHandler
    {
        public static int GetDropIndex(DataGrid dg, DragEventArgs e)
        {
            var pos = e.GetPosition(dg);
            
            // Avalonia 0.10 implementation to find row and index
            var hit = dg.InputHitTest(pos);
            if (hit is Avalonia.Visual v)
            {
                var row = FindParent<DataGridRow>(v);
                if (row != null)
                {
                    // In Avalonia 0.10 DataGrid, we can get the index from the row directly if it's available
                    // or via the Items collection by looking up the DataContext
                    var item = row.DataContext;
                    var items = dg.Items.Cast<object>().ToList();
                    int index = items.IndexOf(item);
                    
                    if (index != -1)
                    {
                        // If we are in the bottom half of the row, insert after it
                        var rowPos = e.GetPosition(row);
                        if (rowPos.Y > row.Bounds.Height / 2)
                            return index + 1;
                        
                        return index;
                    }
                }
            }

            return dg.Items.Cast<object>().Count();
        }

        private static T FindParent<T>(Avalonia.Visual v) where T : class
        {
            while (v != null)
            {
                if (v is T t) return t;
                v = v.GetVisualParent();
            }
            return null;
        }

        public static async Task<DropResult> ExecuteDrop(DataGrid dg, DragEventArgs e, Manager manager, Window owner, Func<string, string> getString)
        {
            var result = new DropResult();
            var itemList = manager.ItemList;
            int insertIndex = GetDropIndex(dg, e);

            // Don't allow dropping at index 0 if it's a menu item
            if (insertIndex == 0 && itemList.Count > 0 && (itemList[0].Ip?.Name == "GDMENU" || itemList[0].Ip?.Name == "openMenu"))
                insertIndex = 1;

            if (e.Data.Contains(DataFormats.FileNames))
            {
                // External file drop
                result.IsAdd = true;
                var files = e.Data.GetFileNames()?.OrderBy(x => x).ToList();
                if (files == null) return null;

                foreach (var file in files)
                {
                    try
                    {
                        var newItem = await ImageHelper.CreateGdItemAsync(file);
                        
                        // Check if game already exists (by serial)
                        if (!string.IsNullOrEmpty(newItem.ProductNumber))
                        {
                            var existing = itemList.FirstOrDefault(x => x.ProductNumber == newItem.ProductNumber && !x.IsMenuItem);
                            if (existing != null)
                            {
                                var title = getString("StringReplaceGameTitle");
                                var msg = string.Format(getString("StringReplaceGameMessage"), existing.Name, existing.ProductNumber);
                                var prompt = await MessageBoxManager.GetMessageBoxStandardWindow(title, msg, MessageBox.Avalonia.Enums.ButtonEnum.YesNo, MessageBox.Avalonia.Enums.Icon.Question).ShowDialog(owner);
                                
                                if (prompt == MessageBox.Avalonia.Enums.ButtonResult.Yes)
                                {
                                    // Replace: Remove existing and insert new
                                    int existingIndex = itemList.IndexOf(existing);
                                    result.RemovedItems.Add((existing, existingIndex));
                                    itemList.RemoveAt(existingIndex);
                                    if (existingIndex < insertIndex) insertIndex--;
                                }
                                else
                                {
                                    // Don't replace, skip this file
                                    continue;
                                }
                            }
                        }

                        itemList.Insert(insertIndex, newItem);
                        result.AddedItems.Add((newItem, insertIndex));
                        insertIndex++;
                    }
                    catch { /* ignore invalid files */ }
                }
            }
            else if (e.Data.Contains("GDMENU_GdItems"))
            {
                // Internal reorder
                var draggedItems = e.Data.Get("GDMENU_GdItems") as List<GdItem>;
                if (draggedItems == null || !draggedItems.Any()) return null;

                // Don't allow reordering menu items
                if (draggedItems.Any(x => x.IsMenuItem)) return null;

                result.IsReorder = true;
                result.OldOrder = itemList.ToList();

                // Remove dragged items from current positions
                foreach (var item in draggedItems)
                {
                    int oldIndex = itemList.IndexOf(item);
                    if (oldIndex != -1)
                    {
                        itemList.RemoveAt(oldIndex);
                        if (oldIndex < insertIndex) insertIndex--;
                    }
                }

                // Insert at new position
                foreach (var item in draggedItems)
                {
                    itemList.Insert(insertIndex++, item);
                }

                result.NewOrder = itemList.ToList();
            }

            return result;
        }
    }
}
