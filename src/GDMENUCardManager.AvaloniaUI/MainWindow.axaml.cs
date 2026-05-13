using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GDMENUCardManager.Core;
using System.Configuration;
using Avalonia.Media;
using Avalonia.Platform;

namespace GDMENUCardManager
{
    public partial class MainWindow : Window
    {
        public static string GetString(string key)
        {
            if (Avalonia.Application.Current.TryFindResource(key, out var value) && value is string str)
                return str;
            return key;
        }

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            //this.AttachDevTools();
            //this.OpenDevTools();
#endif

            var compressedFileFormats = new string[] { ".7z", ".rar", ".zip" };
            _ManagerInstance = GDMENUCardManager.Core.Manager.CreateInstance(new DependencyManager(), compressedFileFormats);
            var fullList = Manager.supportedImageFormats.Concat(compressedFileFormats).ToArray();
            fileFilterList = new List<FileDialogFilter>
            {
                new FileDialogFilter
                {
                    Name = $"Dreamcast Game ({string.Join("; ", fullList.Select(x => $"*{x}"))})",
                    Extensions = fullList.Select(x => x.Substring(1)).ToList()
                }
            };

            // Clean up any leftover staging data from a previous update attempt
            UpdateManager.CleanupStaleStagingData();

            this.Opened += async (ss, ee) =>
            {
                await CheckConfigWritability();

                // macOS first-time setup: copy BOX.DAT, ICON.DAT, META.DAT from the bundle to
                // ~/Library/Application Support/GDMENUCardManager/menu_data/ with a progress bar.
                // This runs fully before anything else so the DAT files are in place before any
                // card loading or artwork operations occur.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    && MacOsDataMigration.NeedsFirstTimeDatSetup())
                {
                    var progressWindow = new ProgressWindow();
                    progressWindow.Title = Helper.DependencyManager.GetString("StringFirstTimeSetup");
                    progressWindow.TotalItems = 3;
                    progressWindow.TextContent = Helper.DependencyManager.GetString("StringPerformingFirstTimeSetup");
                    progressWindow.Show(this);

                    var progress = new Progress<(int current, int total, string name)>(p =>
                    {
                        progressWindow.ProcessedItems = p.current;
                        progressWindow.TextContent = Helper.DependencyManager.GetFormattedString("StringPerformingFirstTimeDatCopying", p.current, p.total, p.name);
                    });

                    await Task.Run(() =>
                        MacOsDataMigration.PerformFirstTimeDatCopy(
                            AppDomain.CurrentDomain.BaseDirectory, progress));

                    progressWindow.AllowClose();
                    progressWindow.Close();
                }

                // If custom path is set, load from it instead of searching for drives
                if (IsUsingCustomPath)
                {
                    await LoadItemsFromCard();
                }
                else
                {
                    FillDriveList();
                }
                // Defer column visibility update until DataGrid is fully loaded
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => UpdateFolderColumnVisibility(), Avalonia.Threading.DispatcherPriority.Loaded);

                // Check for updates (non-blocking, silent on failure)
                _ = CheckForUpdateAsync();
            };

            this.Closing += MainWindow_Closing;
            this.PropertyChanged += MainWindow_PropertyChanged;
            this.KeyDown += MainWindow_KeyDown;
            Manager.ItemList.CollectionChanged += ItemList_CollectionChanged;
            Manager.MenuKindChanged += Manager_MenuKindChanged;

            //config parsing. all settings are optional and must reverse to default values if missing
            bool.TryParse(ConfigurationManager.AppSettings["ShowAllDrives"], out showAllDrives);
            bool.TryParse(ConfigurationManager.AppSettings["Debug"], out Manager.debugEnabled);
            if (bool.TryParse(ConfigurationManager.AppSettings["UseBinaryString"], out bool useBinaryString))
                Converter.ByteSizeToStringConverter.UseBinaryString = useBinaryString;
            if (int.TryParse(ConfigurationManager.AppSettings["CharLimit"], out int charLimit))
                GdItem.namemaxlen = Math.Min(256, Math.Max(charLimit, 1));
            if (int.TryParse(ConfigurationManager.AppSettings["ProductIdMaxLength"], out int productIdMaxLength))
                GdItem.serialmaxlen = Math.Min(32, Math.Max(productIdMaxLength, 1));
            if (bool.TryParse(ConfigurationManager.AppSettings["TruncateMenuGDI"], out bool truncateMenuGDI))
                Manager.TruncateMenuGDI = truncateMenuGDI;
            if (bool.TryParse(ConfigurationManager.AppSettings["LockCheck"], out bool lockCheck))
                Manager.EnableLockCheck = lockCheck;

            // Disc Image Options
            if (bool.TryParse(ConfigurationManager.AppSettings["EnableGDIShrink"], out bool gdiShrink))
                Manager.EnableGDIShrink = gdiShrink;
            if (bool.TryParse(ConfigurationManager.AppSettings["EnableGDIShrinkCompressed"], out bool gdiShrinkCompressed))
                Manager.EnableGDIShrinkCompressed = gdiShrinkCompressed;
            if (bool.TryParse(ConfigurationManager.AppSettings["EnableGDIShrinkBlackList"], out bool gdiShrinkBlackList))
                Manager.EnableGDIShrinkBlackList = gdiShrinkBlackList;
            if (bool.TryParse(ConfigurationManager.AppSettings["EnableGDIShrinkExisting"], out bool gdiShrinkExisting))
                Manager.EnableGDIShrinkExisting = gdiShrinkExisting;
            if (bool.TryParse(ConfigurationManager.AppSettings["EnableRegionPatch"], out bool regionPatch))
                Manager.EnableRegionPatch = regionPatch;
            if (bool.TryParse(ConfigurationManager.AppSettings["EnableRegionPatchExisting"], out bool regionPatchExisting))
                Manager.EnableRegionPatchExisting = regionPatchExisting;
            if (bool.TryParse(ConfigurationManager.AppSettings["EnableVgaPatch"], out bool vgaPatch))
                Manager.EnableVgaPatch = vgaPatch;
            if (bool.TryParse(ConfigurationManager.AppSettings["EnableVgaPatchExisting"], out bool vgaPatchExisting))
                Manager.EnableVgaPatchExisting = vgaPatchExisting;

            var tempFolderConfig = ConfigurationManager.AppSettings["TempFolder"];
            if (!string.IsNullOrEmpty(tempFolderConfig) && Directory.Exists(tempFolderConfig))
                TempFolder = tempFolderConfig;
            else
                TempFolder = Path.GetTempPath();

            // Update repo override (for testing)
            UpdateManager.RepoOverride = ConfigurationManager.AppSettings["UpdateRepoOverride"];

            Title = "GD MENU Card Manager " + Constants.Version;

            // Restore window position and size from config
            RestoreWindowBounds();

            //showAllDrives = true;

            DataContext = this;

            if (bool.TryParse(ConfigurationManager.AppSettings["PALVersion"], out bool palVersion) && palVersion)
            {
                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                this.Icon = new WindowIcon(assets.Open(new Uri("avares://GDMENUCardManager.AvaloniaUI/Assets/GDMENUCardManagerPAL.ico")));
                if (Application.Current.Resources.TryGetResource("BrandColor", out _))
                {
                    Application.Current.Resources["BrandColor"] = Color.FromRgb(1, 32, 255);
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            dg1 = this.FindControl<DataGrid>("dg1");
            ButtonSort = this.FindControl<Button>("ButtonSort");

            // Drag and Drop handlers
            dg1.AddHandler(DragDrop.DragOverEvent, DataGrid_DragOver);
            dg1.AddHandler(DragDrop.DropEvent, DataGrid_Drop);

            // Add tunneling handler to intercept pointer events for context menu and dragging
            dg1.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, DataGrid_PointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            dg1.AddHandler(Avalonia.Input.InputElement.PointerReleasedEvent, DataGrid_PointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            dg1.AddHandler(Avalonia.Input.InputElement.PointerMovedEvent, DataGrid_PointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }
    }
}
