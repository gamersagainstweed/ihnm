using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ihnm.Enums;
using ihnm.Managers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace ihnm;

public partial class Voices : Window
{

    private VoiceCard selectedVoiceCard;

    List<EnumLanguage> selectedLangs = new List<EnumLanguage>();
    List<EnumGender> selectedGenders = new List<EnumGender>();

    Dictionary<ToggleButton, EnumLanguage> langTogglesDict = new Dictionary<ToggleButton, EnumLanguage>();

    AudioPlaybackEngine engine1;

    public Voices()
    {
        this.InitializeComponent();

    }

    public Voices(AudioPlaybackEngine engine1)
    {
        this.InitializeComponent();

        

        this.Topmost = true;
        this.CanResize = false;

        for (int j=0;j<1000;j++)
        {
            VoicesGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
        }

        this.Closing += Voices_Closing;

        this.engine1 = engine1;

        if (File.Exists("sounds/loops/scp/_lczambience.mp3"))
            this.engine1.PlayLoop("sounds/loops/scp/_lczambience.mp3");

        const int ENUM_CURRENT_SETTINGS = -1;
        DEVMODE devMode = default;
        devMode.dmSize = (short)Marshal.SizeOf(devMode);
        EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode);

        int screenWidth = devMode.dmPelsWidth;
        int screenHeight = devMode.dmPelsHeight;

        this.Width = screenWidth*0.9;
        this.Height = screenHeight*0.8;

        this.Position = new PixelPoint((int)(screenWidth * 0.05), (int)(screenHeight * 0.05));

        this.rootGrid.RowDefinitions[1].Height = GridLength.Parse((this.Height - 200).ToString());
        this.rootGrid.Width = (this.Width - 200);


        this.filtersPanel.Width = this.Width - 250;

        {
            ToggleButton maleButton = new ToggleButton();
            maleButton.Content = "Male";

            maleButton.IsCheckedChanged += MaleButton_IsCheckedChanged;

            filtersPanel.Children.Add(maleButton);



            ToggleButton femaleButton = new ToggleButton();
            femaleButton.Content = "Female";

            femaleButton.IsCheckedChanged += FemaleButton_IsCheckedChanged;

            filtersPanel.Children.Add(femaleButton);

        }

        foreach (EnumLanguage lang in Enum.GetValues(typeof(EnumLanguage)))
        {
            //Grid langGrid = new Grid();
            //langGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            //langGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            //CheckBox langBox = new CheckBox(); this.boxesDict.Add(langBox, lang);
            //TextBlock langTXT = new TextBlock() { Text=lang.ToString()};

            //langGrid.Children.Add(langBox); langBox.IsChecked = true;
            //langGrid.Children.Add(langTXT); Grid.SetColumn(langTXT, 1);

            //langBox.IsCheckedChanged += LangBox_IsCheckedChanged;

            ToggleButton langButton = new ToggleButton();
            langButton.Content = lang.ToString();

            langTogglesDict.Add(langButton, lang);

            langButton.IsCheckedChanged += LangButton_IsCheckedChanged;

            filtersPanel.Children.Add(langButton);

        }

        this.updateVoicesList();

    }

    private void FemaleButton_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        ToggleButton button = sender as ToggleButton;
        if (button.IsChecked == true)
            selectedGenders.Add(EnumGender.female);
        else
            selectedGenders.Remove(EnumGender.female);
        this.updateVoicesList();
    }

    private void MaleButton_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        ToggleButton button = sender as ToggleButton;
        if (button.IsChecked == true)
            selectedGenders.Add(EnumGender.male);
        else
            selectedGenders.Remove(EnumGender.male);
        this.updateVoicesList();
    }

    private void LangButton_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
       ToggleButton button = sender as ToggleButton;
        EnumLanguage lang = langTogglesDict[button];
        if (button.IsChecked == true)
            selectedLangs.Add(lang);
        else
            selectedLangs.Remove(lang);
        this.updateVoicesList();
    }


    public void updateVoicesList()
    {
        this.VoicesGrid.Children.Clear();

        TextBlock noneVoiceBlock = new TextBlock() { Text = "none" };

        VoiceCard noneVoiceCard = new VoiceCard();
        noneVoiceCard.voiceName.Text= "none";
        noneVoiceCard.langName.Text = "???";

        noneVoiceCard.voiceBtn.IsCheckedChanged += (sender, e) => { selectVoice(ref noneVoiceCard); };

        if (noneVoiceCard.voiceName.Text == Common.voice)
        {
            noneVoiceCard.voiceBtn.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("lime"));
        }
        this.VoicesGrid.Children.Add(noneVoiceCard);
        Grid.SetRow(noneVoiceCard, 1);

        Dictionary<string, WrapPanel> sherpaGrids = new Dictionary<string, WrapPanel>();
        Dictionary<string, WrapPanel> categoryGrids = new Dictionary<string, WrapPanel>();

        int i = 3;

        foreach (string mdl in Common.sherpaModels)
        {

            //bool cont = true;

            //foreach (EnumLanguage lang in Common.sherpaModelsDict[mdl].languages)
            //{
            //    if (selectedLangs.Contains(lang) || selectedLangs.Count==0)
            //        cont=false;
            //}

            //if (cont)
            //    continue;



        }


        foreach (KeyValuePair<string, (string, int, double, EnumLanguage, EnumGender)> kv in Common.sherpaVoices)
        {
            if ((selectedLangs.Contains(kv.Value.Item4)||selectedLangs.Count==0) && 
                (selectedGenders.Contains(kv.Value.Item5)||selectedGenders.Count==0))
            {


                //TextBlock voiceBlock = new TextBlock() { Text = kv.Key };

                //voiceBlock.PointerPressed += (sender, e) => { selectVoice(ref voiceBlock); };

                //if (kv.Key == Global.voice)
                //{
                //    voiceBlock.Foreground = new SolidColorBrush(Color.Parse("green"));
                //}

                VoiceCard voiceCard = new VoiceCard();

                voiceCard.voiceName.Text = kv.Key;

                if (kv.Value.Item5!=EnumGender.unspecified)
                {
                    voiceCard.langName.Text = kv.Value.Item4.ToString()+ " • " +
                        FirstCharToUpper( kv.Value.Item5.ToString());
                }
                else
                {
                    voiceCard.langName.Text = kv.Value.Item4.ToString();

                }

                if (kv.Key == Common.voice)
                {
                    voiceCard.voiceBtn.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("lime"));
                }

                voiceCard.voiceBtn.IsCheckedChanged += (sender, e) => { selectVoice(ref voiceCard); };


                string thumbnailname = "sherpavoices/" + kv.Key + "/thumbnail.png";

                if (File.Exists(thumbnailname))
                {
                    voiceCard.voiceImage.Source = new Avalonia.Media.Imaging.Bitmap(thumbnailname);
                }

                string categoryFile = "sherpavoices/" + kv.Key + "/category.txt";


                if (File.Exists(categoryFile))
                {
                    string category = File.ReadLines(categoryFile).First();
                    if (!categoryGrids.ContainsKey(category))
                    {
                        Grid categoryGrid = new Grid(); categoryGrid.Name = category;
                        categoryGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Parse("25") });
                        categoryGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                        TextBlock categoryTextBlock = new TextBlock() { Text = category, FontSize = 15 };
                        categoryGrid.Children.Add(categoryTextBlock);

                        WrapPanel categoryVoicesGrid = new WrapPanel();
                        Grid.SetRow(categoryVoicesGrid, 1);
                        categoryGrid.Children.Add(categoryVoicesGrid);
                        categoryGrids.Add(category, categoryVoicesGrid);

                        Grid.SetRow(categoryGrid, i);
                        this.VoicesGrid.Children.Add(categoryGrid);
                        i += 1;
                    }
                    categoryGrids[category].Children.Add(voiceCard);
                }
                else
                {
                    string mdl = kv.Value.Item1;
                    if (!sherpaGrids.ContainsKey(kv.Value.Item1))
                    {
                        Grid mdlGrid = new Grid();
                        mdlGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Parse("25") });
                        mdlGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                        TextBlock mdlTextBlock = new TextBlock() { Text = mdl, FontSize = 15 };
                        mdlGrid.Children.Add(mdlTextBlock);

                        WrapPanel mdlVoicesGrid = new WrapPanel();
                        Grid.SetRow(mdlVoicesGrid, 1);
                        mdlGrid.Children.Add(mdlVoicesGrid);

                        sherpaGrids.Add(mdl, mdlVoicesGrid);
                        Grid.SetRow(mdlGrid, i);
                        this.VoicesGrid.Children.Add(mdlGrid);
                        i += 1;
                    }

                    sherpaGrids[kv.Value.Item1].Children.Add(voiceCard);
                }


            }
        }

        Grid sampledGrid = new Grid();
        sampledGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Parse("25") });
        sampledGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
        TextBlock sampledTextBlock = new TextBlock() { Text = "Sample-based pseudo TTS", FontSize = 20 };
        sampledGrid.Children.Add(sampledTextBlock);
        WrapPanel sampledVoicesGrid = new WrapPanel();
        Grid.SetRow(sampledVoicesGrid, 1);
        sampledGrid.Children.Add(sampledVoicesGrid);
        Grid.SetRow(sampledGrid, i+1);
        this.VoicesGrid.Children.Add(sampledGrid);


        foreach (string voice in Common.voices)
        {
            //TextBlock voiceBlock = new TextBlock() { Text = voice };

            //voiceBlock.PointerPressed += (sender, e) => { selectVoice(ref voiceBlock); };

            //if (voice == Global.voice)
            //{
            //    voiceBlock.Foreground = new SolidColorBrush(Color.Parse("green"));
            //}

            VoiceCard voiceCard = new VoiceCard();

            voiceCard.voiceName.Text = voice;
            voiceCard.langName.Text = "English";


            voiceCard.voiceBtn.IsCheckedChanged += (sender, e) => { selectVoice(ref voiceCard); };

            if (voice == Common.voice)
            {
                voiceCard.voiceBtn.Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("lime"));
            }

            sampledVoicesGrid.Children.Add(voiceCard);
        }



    }

    public string FirstCharToUpper(string input)
    {
        switch (input)
        {
            case null: throw new ArgumentNullException(nameof(input));
            case "": throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
            default: return input[0].ToString().ToUpper() + input.Substring(1);
        }
    }

    private void selectVoice(ref VoiceCard voiceCard)
    {
        if (voiceCard.voiceBtn.IsChecked == true)
        {
            if (this.selectedVoiceCard != null)
                this.selectedVoiceCard.voiceBtn.IsChecked = false;
            this.selectedVoiceCard = voiceCard;
            
        }
        else
        {
            this.selectedVoiceCard = null;
        }
        if (File.Exists("sounds/soundboard/scp/.pickitem.mp3"))
            engine1.PlaySound("sounds/soundboard/scp/.pickitem.mp3", 1, 1, 1);
    }

    private void Voices_Closing(object? sender, WindowClosingEventArgs e)
    {
        this.engine1.StopAllSounds();
    }


    public void ClickHandler(object sender, RoutedEventArgs args)
    {
        if (this.selectedVoiceCard!=null)
        { 
            this.CallCommandExecute("/v " + this.selectedVoiceCard.voiceName.Text);
            this.Close();
            this.CallClearInput();
            if (File.Exists("sounds/soundboard/scp/.bell.mp3"))
                this.engine1.PlaySound("sounds/soundboard/scp/.bell.mp3", 1, 1, 1);
        }
    }

    public void ClickDefaultHandler(object sender, RoutedEventArgs args)
    {
        if (this.selectedVoiceCard != null)
        {
            if (File.Exists("cfg/config.txt"))
            {
                string path = "cfg/config.txt";
                var fileContent = File.ReadLines(path).ToList();

                fileContent[fileContent.Count - 4] = "voice:"+this.selectedVoiceCard.voiceName.Text;
                File.WriteAllLines(path, fileContent);

                
            }
        }
    }


    public class CommandExecuteEventArgs : EventArgs
    {
        public string cmdString { get; set; }
    }

    protected virtual void CallCommandExecute(string cmdString)
    {
        CommandExecuteEventArgs e = new CommandExecuteEventArgs();
        e.cmdString = cmdString;
        CommandExecute?.Invoke(this, e);
    }

    public delegate void CommandExecuteEventHandler(object myObject, CommandExecuteEventArgs myArgs);

    public event CommandExecuteEventHandler CommandExecute;


    protected virtual void CallClearInput()
    {
        ClearInput?.Invoke(this, null);
    }

    public event EventHandler ClearInput;

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