using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GDMENUCardManager.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace GDMENUCardManager
{
    public partial class BatchFolderRenameWindow : Window, INotifyPropertyChanged
    {
        private Stack<UndoOperation> _undoStack = new Stack<UndoOperation>();
        private const int MaxUndoOperations = 10;

        public ObservableCollection<FolderTreeNode> RootNodes { get; } = new ObservableCollection<FolderTreeNode>();

        private bool _canUndo;
        public bool CanUndo
        {
            get => _canUndo;
            set
            {
                if (_canUndo != value)
                {
                    _canUndo = value;
                    OnPropertyChanged();
                }
            }
        }

        private abstract class UndoOperation
        {
            public abstract void Undo();
        }

        private class MoveOperation : UndoOperation
        {
            public FolderTreeNode Node { get; set; }
            public FolderTreeNode OldParent { get; set; }
            public FolderTreeNode NewParent { get; set; }
            public int OldIndex { get; set; }

            public override void Undo()
            {
                NewParent.Children.Remove(Node);
                Node.Parent = OldParent;
                if (OldIndex >= OldParent.Children.Count)
                    OldParent.Children.Add(Node);
                else
                    OldParent.Children.Insert(OldIndex, Node);

                var node = OldParent;
                while (node != null)
                {
                    node.RecalculateCounts();
                    node = node.Parent;
                }
                node = NewParent;
                while (node != null)
                {
                    node.RecalculateCounts();
                    node = node.Parent;
                }
                Node.UpdateFullPath();
                OldParent?.SortChildren();
                NewParent?.SortChildren();
            }
        }

        private class RenameOperation : UndoOperation
        {
            public FolderTreeNode Node { get; set; }
            public string OldName { get; set; }
            public string NewName { get; set; }

            public override void Undo()
            {
                Node.Name = OldName;
                Node.Parent?.SortChildren();
            }
        }

        public Dictionary<string, string> FolderMappings { get; private set; }

        public BatchFolderRenameWindow()
        {
            InitializeComponent();
#if DEBUG
            //this.AttachDevTools();
#endif
        }

        public BatchFolderRenameWindow(Dictionary<string, int> folderCounts, int totalItemCount) : this()
        {
            DataContext = this;
            BuildTree(folderCounts, totalItemCount);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void BuildTree(Dictionary<string, int> folderCounts, int totalItemCount)
        {
            var allNodes = new Dictionary<string, FolderTreeNode>(StringComparer.Ordinal);
            var topLevelNodes = new List<FolderTreeNode>();

            var sortedPaths = folderCounts.Keys
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .OrderBy(p => p.Count(c => c == '\\'))
                .ThenBy(p => p);

            foreach (var path in sortedPaths)
            {
                var segments = path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                FolderTreeNode parent = null;
                string currentPath = "";

                for (int i = 0; i < segments.Length; i++)
                {
                    currentPath = i == 0 ? segments[i] : $"{currentPath}\\{segments[i]}";

                    if (!allNodes.ContainsKey(currentPath))
                    {
                        var node = new FolderTreeNode
                        {
                            Name = segments[i],
                            FullPath = currentPath,
                            OriginalFullPath = currentPath,
                            Parent = parent
                        };

                        if (currentPath == path && folderCounts.ContainsKey(path))
                        {
                            node.DirectGameCount = folderCounts[path];
                        }

                        allNodes[currentPath] = node;

                        if (parent == null)
                        {
                            topLevelNodes.Add(node);
                        }
                        else
                        {
                            parent.Children.Add(node);
                        }
                    }

                    parent = allNodes[currentPath];
                }
            }

            var rootNode = new FolderTreeNode
            {
                Name = "(Root)",
                IsRootNode = true,
                IsExpanded = true,
                FullPath = "",
                OriginalFullPath = "",
                DirectGameCount = totalItemCount,
                TotalGameCount = totalItemCount
            };

            foreach (var topNode in topLevelNodes)
            {
                topNode.Parent = rootNode;
                rootNode.Children.Add(topNode);
                topNode.RecalculateCounts();
            }

            rootNode.SortChildren();
            RootNodes.Add(rootNode);
        }

        private string _editingOriginalName;

        private void TreeViewItem_DoubleTapped(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item && item.DataContext is FolderTreeNode node)
            {
                if (!node.IsRootNode)
                {
                    _editingOriginalName = node.Name;
                    node.IsEditing = true;
                    e.Handled = true;
                }
            }
        }

        private void EditTextBox_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is FolderTreeNode node)
            {
                FinishEditing(node);
            }
        }

        private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is FolderTreeNode node)
            {
                if (e.Key == Key.Enter)
                {
                    FinishEditing(node);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    var originalName = node.OriginalFullPath.Split('\\').Last();
                    node.Name = originalName;
                    node.IsEditing = false;
                    _editingOriginalName = null;
                    e.Handled = true;
                }
            }
        }

        private void FinishEditing(FolderTreeNode node)
        {
            node.IsEditing = false;

            if (!Helper.IsValidPrintableAscii(node.Name))
            {
                // In Avalonia we don't have MessageBox.Show directly, but we can use our helper if available
                // For now just revert or set to a safe name
                node.Name = "PLEASE RENAME";
                _editingOriginalName = null;
                return;
            }

            if (_editingOriginalName != null && _editingOriginalName != node.Name)
            {
                RecordRename(node, _editingOriginalName, node.Name);
                node.Parent?.SortChildren();
            }
            _editingOriginalName = null;
        }

        private void RecordRename(FolderTreeNode node, string oldName, string newName)
        {
            var operation = new RenameOperation
            {
                Node = node,
                OldName = oldName,
                NewName = newName
            };

            if (_undoStack.Count >= MaxUndoOperations)
            {
                var temp = new Stack<UndoOperation>(_undoStack.Reverse().Skip(1).Reverse());
                _undoStack = temp;
            }

            _undoStack.Push(operation);
            CanUndo = true;
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count > 0)
            {
                var operation = _undoStack.Pop();
                operation.Undo();
                CanUndo = _undoStack.Count > 0;
            }
        }

        private void CollectMappings(FolderTreeNode node, Dictionary<string, string> mappings)
        {
            if (!node.IsRootNode)
            {
                if (node.OriginalFullPath != node.FullPath)
                {
                    mappings[node.OriginalFullPath] = node.FullPath;
                }
            }

            foreach (var child in node.Children)
            {
                CollectMappings(child, mappings);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            FolderMappings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var root in RootNodes)
            {
                CollectMappings(root, FolderMappings);
            }
            Close(true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
