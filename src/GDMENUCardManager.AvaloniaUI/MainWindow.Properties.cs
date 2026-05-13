using Avalonia;
using Avalonia.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using GDMENUCardManager.Core;

namespace GDMENUCardManager
{
    public partial class MainWindow : Window, INotifyPropertyChanged, IDiscImageOptionsViewModel
    {
        private GDMENUCardManager.Core.Manager _ManagerInstance;
        public GDMENUCardManager.Core.Manager Manager { get { return _ManagerInstance; } }

        private readonly bool showAllDrives = false;

        public new event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<DriveInfo> DriveList { get; } = new ObservableCollection<DriveInfo>();

        public static List<string> DiscTypes { get; } = new List<string> { "Game", "Other", "PSX" };

        private bool _IsBusy;
        public bool IsBusy
        {
            get { return _IsBusy; }
            private set { _IsBusy = value; RaisePropertyChanged(); }
        }

        private DriveInfo _DriveInfo;
        public DriveInfo SelectedDrive
        {
            get { return _DriveInfo; }
            set
            {
                _DriveInfo = value;
                Manager.ItemList.Clear();
                if (value != null)
                {
                    // Clear custom path when selecting a drive
                    if (IsUsingCustomPath)
                    {
                        CustomSdPath = null;
                    }
                    Manager.sdPath = value.RootDirectory.ToString();
                }
                else if (!IsUsingCustomPath)
                {
                    Manager.sdPath = null;
                }
                if (IsFilterActive)
                    ClearFilterFromGrid();
                else
                    Filter = null;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasSdPath));
            }
        }

        private string _TempFolder;
        public string TempFolder
        {
            get { return _TempFolder; }
            set { _TempFolder = value; RaisePropertyChanged(); }
        }

        private string _CustomSdPath;
        public string CustomSdPath
        {
            get { return _CustomSdPath; }
            set
            {
                _CustomSdPath = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(IsUsingCustomPath));
                RaisePropertyChanged(nameof(HasSdPath));
            }
        }

        public bool IsUsingCustomPath => !string.IsNullOrEmpty(CustomSdPath);

        public bool HasSdPath => SelectedDrive != null || IsUsingCustomPath;

        private string _TotalFilesLength = "N/A";
        public string TotalFilesLength
        {
            get { return _TotalFilesLength; }
            private set { _TotalFilesLength = value; RaisePropertyChanged(); }
        }

        public MenuKind MenuKindSelected
        {
            get { return Manager.MenuKindSelected; }
            set
            {
                Manager.MenuKindSelected = value;
                RaisePropertyChanged();
                UpdateFolderColumnVisibility();
                UpdateSortButtonTooltip();
            }
        }

        private string _Filter;
        public string Filter
        {
            get { return _Filter; }
            set { _Filter = value; RaisePropertyChanged(); }
        }

        private bool _IsFilterActive;
        public bool IsFilterActive
        {
            get { return _IsFilterActive; }
            set { _IsFilterActive = value; RaisePropertyChanged(); RaisePropertyChanged(nameof(IsNotFilterActive)); }
        }
        public bool IsNotFilterActive => !IsFilterActive;

        private string _activeFilterText;

        public bool IsArtworkEnabled
        {
            get { return !Manager.ArtworkDisabled; }
        }

        public bool EnableRegionPatch
        {
            get { return Manager.EnableRegionPatch; }
            set { Manager.EnableRegionPatch = value; RaisePropertyChanged(); }
        }

        public bool EnableRegionPatchExisting
        {
            get { return Manager.EnableRegionPatchExisting; }
            set { Manager.EnableRegionPatchExisting = value; RaisePropertyChanged(); }
        }

        public bool EnableVgaPatch
        {
            get { return Manager.EnableVgaPatch; }
            set { Manager.EnableVgaPatch = value; RaisePropertyChanged(); }
        }

        public bool EnableVgaPatchExisting
        {
            get { return Manager.EnableVgaPatchExisting; }
            set { Manager.EnableVgaPatchExisting = value; RaisePropertyChanged(); }
        }

        public bool EnableLockCheck
        {
            get { return Manager.EnableLockCheck; }
            set { Manager.EnableLockCheck = value; RaisePropertyChanged(); }
        }

        private readonly List<FileDialogFilter> fileFilterList;

        #region window controls
        DataGrid dg1;
        Button ButtonSort;
        #endregion

        // Undo tracking for cell edits
        private GdItem _editingItem;
        private string _editingPropertyName;
        private object _editingOldValue;

        // Flag to prevent duplicate serial translation dialogs
        private bool _handlingSerialTranslation;

        // Track if we should block context menu for current right-click
        private bool _blockContextMenu = false;
        private Point? _dragStartPos;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
