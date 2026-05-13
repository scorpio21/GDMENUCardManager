using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using GDMENUCardManager.Core;

namespace GDMENUCardManager
{
    public class InfoWindow : Window, INotifyPropertyChanged
    {
        public string FileInfo { get; }

        private string _IpInfo;
        public string IpInfo
        {
            get { return _IpInfo; }
            private set { _IpInfo = value; RaisePropertyChanged(); }
        }

        private string _LabelText;
        public string LabelText
        {
            get { return _LabelText; }
            private set { _LabelText = value; RaisePropertyChanged(); }
        }

        private Avalonia.Media.Imaging.Bitmap _GdTexture = null;
        public Avalonia.Media.Imaging.Bitmap GdTexture
        {
            get { return _GdTexture; }
            private set { _GdTexture = value; RaisePropertyChanged(); }
        }

        private GdItem item;
        public new event PropertyChangedEventHandler PropertyChanged;

        public InfoWindow() { }

        private string GetString(string key)
        {
            if (Application.Current.TryFindResource(key, out object res) && res is string s)
                return s;
            return key;
        }

        public InfoWindow(GdItem item)
        {
            InitializeComponent();
#if DEBUG
            //this.AttachDevTools();
            //this.OpenDevTools();
#endif

            this.Opened += InfoWindow_Opened;

            this.item = item;
            _IpInfo = GetString("StringLoading");
            _LabelText = GetString("StringLoading");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{GetString("StringFolderColon")} {Path.GetFileName(item.FullFolderPath)}");
            sb.Append($"{GetString("StringFileColon")} {Path.GetFileName(item.ImageFile)}");

            FileInfo = sb.ToString();

            if (item.FileFormat != FileFormat.Uncompressed)
            {
                IpInfo = GetString("StringCompressedFile");
            }

            this.KeyUp += (ss, ee) => { if (ee.Key == Avalonia.Input.Key.Escape) Close(); };
            DataContext = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void InfoWindow_Opened(object sender, EventArgs e)
        {
            await Task.Delay(100);

            if (item.FileFormat == FileFormat.SevenZip)
            {
                IpInfo = GetString("StringCantLoadCompressed");
                LabelText = GetString("StringCantLoadCompressed");
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
                    sb.AppendLine($"{GetString("StringTitleColon")} {ip.Name}");
                    sb.AppendLine($"{GetString("StringVersionColon")} {ip.Version}");
                    sb.AppendLine($"{GetString("StringDiscColon")} {ip.Disc}");
                    sb.AppendLine($"{GetString("StringVgaColon")} {(ip.Vga ? GetString("StringYes") : GetString("StringNo"))}");
                    sb.AppendLine($"{GetString("StringSerialColon")} {ip.ProductNumber}");
                    sb.Append($"{GetString("StringRegionColon")} {ip.Region}");

                    if (ip.SpecialDisc != SpecialDisc.None)
                    {
                        sb.AppendLine();
                        sb.Append($"{GetString("StringDetectedAsColon")} " + ip.SpecialDisc);
                    }
                    IpInfo = sb.ToString();
                }
                else
                {
                    IpInfo = GetString("StringCouldNotReadIpBin");
                }
            }
            catch (Exception ex)
            {
                IpInfo = $"{GetString("StringError")}: {ex.Message}";
            }

            // Load GDTEX texture
            try
            {
                var gdtexture = await Task.Run(() => ImageHelper.GetGdText(filePath));

                if (gdtexture == null)
                {
                    LabelText = GetString("StringUnableToRead");
                }
                else
                {
                    var decoded = await Task.Run(() => new PuyoTools.PvrTexture().GetDecoded(gdtexture));

                    using (var memory = new MemoryStream())
                    {
                        byte[] data = decoded.Item1;
                        using (var writeableBitmap = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(decoded.Item2, decoded.Item3), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Unpremul))
                        {
                            using (var l = writeableBitmap.Lock())
                                System.Runtime.InteropServices.Marshal.Copy(data, 0, l.Address, data.Length);

                            writeableBitmap.Save(memory);
                            memory.Position = 0;
                            GdTexture = new Avalonia.Media.Imaging.Bitmap(memory);
                            LabelText = null;
                        }
                    }
                }
            }
            catch
            {
                LabelText = GetString("StringUnableToRead");
            }
        }
    }
}
