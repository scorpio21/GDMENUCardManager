using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GDMENUCardManager
{
    public class CopyNameWindow : Window, INotifyPropertyChanged
    {
        private bool _onCard = true;
        public bool OnCard
        {
            get => _onCard;
            set
            {
                if (_onCard != value)
                {
                    _onCard = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _notOnCard = true;
        public bool NotOnCard
        {
            get => _notOnCard;
            set
            {
                if (_notOnCard != value)
                {
                    _notOnCard = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _folderName = true;
        public bool FolderName
        {
            get => _folderName;
            set
            {
                if (_folderName != value)
                {
                    _folderName = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _parseTosec = true;
        public bool ParseTosec
        {
            get => _parseTosec;
            set
            {
                if (_parseTosec != value)
                {
                    _parseTosec = value;
                    OnPropertyChanged();
                }
            }
        }

        public CopyNameWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close(true);
        }

        public new event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
