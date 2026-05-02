using GDMENUCardManager.Core;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GDMENUCardManager
{
    /// <summary>
    /// Interaction logic for infoWindow.xaml
    /// </summary>
    public partial class InfoWindow : Window, INotifyPropertyChanged
    {
        public string FileInfo { get; }

        private string _IpInfo = MainWindow.GetString("StringLoading");
        public string IpInfo
        {
            get { return _IpInfo; }
            private set { _IpInfo = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IpInfo))); }
        }

        private string _LabelText = MainWindow.GetString("StringLoading");
        public string LabelText
        {
            get { return _LabelText; }
            private set { _LabelText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LabelText))); }
        }

        private BitmapImage _GdTexture = null;
        public BitmapImage GdTexture
        {
            get { return _GdTexture; }
            private set { _GdTexture = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GdTexture))); }
        }

        private GdItem item;

        public event PropertyChangedEventHandler PropertyChanged;


        public InfoWindow(GdItem item)
        {
            InitializeComponent();
            Loaded += InfoWindow_Loaded;

            this.item = item;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(MainWindow.GetString("StringFolder") + ": " + Path.GetFileName(item.FullFolderPath));
            sb.Append(MainWindow.GetString("StringFile") + ": " + Path.GetFileName(item.ImageFile));

            FileInfo = sb.ToString();

            if (item.FileFormat != FileFormat.Uncompressed)
            {
                IpInfo = MainWindow.GetString("StringCompressedFile");
            }

            this.KeyUp += (ss, ee) => { if (ee.Key == Key.Escape) Close(); };
            DataContext = this;

        }

        private async void InfoWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (item.FileFormat == FileFormat.SevenZip)
            {
                IpInfo = MainWindow.GetString("StringCantLoadCompressed");
                LabelText = MainWindow.GetString("StringCantLoadCompressed");
                return;
            }

            var filePath = Path.Combine(item.FullFolderPath, item.ImageFile);

            // Load IP.BIN data
            try
            {
                var ip = await ImageHelper.GetIpBinFromImage(filePath);
                if (ip != null)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(MainWindow.GetString("StringTitle") + ": " + ip.Name);
                    sb.AppendLine(MainWindow.GetString("StringVersion") + ": " + ip.Version);
                    sb.AppendLine(MainWindow.GetString("StringDisc") + ": " + ip.Disc);
                    sb.AppendLine(MainWindow.GetString("StringVga") + ": " + (ip.Vga ? MainWindow.GetString("StringYes") : MainWindow.GetString("StringNo")));
                    sb.AppendLine(MainWindow.GetString("StringSerial") + ": " + ip.ProductNumber);
                    sb.Append(MainWindow.GetString("StringRegion") + ": " + ip.Region);

                    if (ip.SpecialDisc != SpecialDisc.None)
                    {
                        sb.AppendLine();
                        sb.Append(MainWindow.GetString("StringDetectedAs") + ": " + ip.SpecialDisc);
                    }
                    IpInfo = sb.ToString();
                }
                else
                {
                    IpInfo = MainWindow.GetString("StringCouldNotReadIpBin");
                }
            }
            catch (Exception ex)
            {
                IpInfo = MainWindow.GetString("StringError") + ": " + ex.Message;
            }

            // Load GDTEX texture
            try
            {
                var gdtexture = await Task.Run(() => ImageHelper.GetGdText(filePath));
                if (gdtexture == null)
                {
                    LabelText = MainWindow.GetString("StringUnableToFindOrReadFile");
                }
                else
                {
                    var decoded = await Task.Run(() => new PuyoTools.PvrTexture().GetDecoded(gdtexture));

                    using (MemoryStream memory = new MemoryStream())
                    {
                        byte[] data = decoded.Item1;

                        using (Bitmap img = new Bitmap(decoded.Item2, decoded.Item3, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                        {
                            BitmapData bitmapData = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.WriteOnly, img.PixelFormat);
                            System.Runtime.InteropServices.Marshal.Copy(data, 0, bitmapData.Scan0, data.Length);
                            img.UnlockBits(bitmapData);

                            img.Save(memory, ImageFormat.Png);
                            memory.Position = 0;
                            BitmapImage bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.StreamSource = memory;
                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapImage.EndInit();
                            GdTexture = bitmapImage;
                            LabelText = null;
                        }
                    }
                }
            }
            catch
            {
                LabelText = MainWindow.GetString("StringUnableToFindOrReadFile");
            }
        }
    }
}
