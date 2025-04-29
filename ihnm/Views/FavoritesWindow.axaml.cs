using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Runtime.InteropServices;

namespace ihnm;

public partial class FavoritesWindow : Window
{
    public FavoritesWindow()
    {
        InitializeComponent();

        this.CanResize = false;
        this.Height = 25;
        this.Width = 1000;
        this.SystemDecorations = SystemDecorations.None;
        this.ShowInTaskbar = false;

        this.Topmost = true;


        const int ENUM_CURRENT_SETTINGS = -1;
        DEVMODE devMode = default;
        devMode.dmSize = (short)Marshal.SizeOf(devMode);
        EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode);

        int screenWidth = devMode.dmPelsWidth;
        int screenHeight = devMode.dmPelsHeight;

        this.Position = new PixelPoint(550, screenHeight-100);

    }

    [StructLayout(LayoutKind.Sequential)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [DllImport("user32.dll")]
    static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);


}