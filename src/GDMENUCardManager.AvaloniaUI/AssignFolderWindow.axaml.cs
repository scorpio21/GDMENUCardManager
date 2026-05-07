using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GDMENUCardManager
{
    public class AssignFolderWindow : Window, INotifyPropertyChanged
    {
        private string _folderPath = string.Empty;
        private string _selectionInfo = string.Empty;

        public string FolderPath
        {
            get => _folderPath;
            set
            {
                if (_folderPath != value)
                {
                    _folderPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectionInfo
        {
            get => _selectionInfo;
            set
            {
                if (_selectionInfo != value)
                {
                    _selectionInfo = value;
                    OnPropertyChanged();
                }
            }
        }

        public IEnumerable<string> KnownFolders { get; set; }

        public AssignFolderWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private string GetString(string key)
        {
            if (Application.Current.TryFindResource(key, out object res) && res is string s)
                return s;
            return key;
        }

        public AssignFolderWindow(int selectedCount, IEnumerable<string> knownFolders) : this()
        {
            SelectionInfo = string.Format(GetString("StringAssignFolderPathToCount"), selectedCount);
            KnownFolders = knownFolders;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
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
