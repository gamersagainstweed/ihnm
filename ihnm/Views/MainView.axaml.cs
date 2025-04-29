using Avalonia.Controls;
using Avalonia.Interactivity;
using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Diagnostics;
using ihnm.Managers;
using System;
using NAudio.Wave;
using System.Threading;
using Avalonia.Threading;
using System.IO;
using SharpHook.Native;

namespace ihnm.Views;

public partial class MainView : UserControl
{

    public overlayWindow overlay;

    public static List<string> comboIn = new List<string>();
    public static List<string> comboOut = new List<string>();
    public static List<MMDevice> inDeviceList = new List<MMDevice>();
    public static List<MMDevice> outDeviceList = new List<MMDevice>();
    public static List<string> micIDs = new List<string>();
    public static List<string> speakerIDs = new List<string>(); //stolen from TTS-Voice-Wizard

    public MMDevice selectedCableInput;
    public MMDevice selectedPlayback;
    public MMDevice selectedMicrophone;

    public DownloadModelsWindow dlModelsWnd;

    private bool downloadStarted = false;

    public MainView()
    {
        InitializeComponent();

        this.virtualCableBox.SelectionChanged += virtualCableBox_SelectedIndexChanged;
        this.playbackDeviceBox.SelectionChanged += playbackBox_SelectedIndexChanged;
        this.inputDeviceBox.SelectionChanged += comboBoxInput_SelectedIndexChanged;

        this.useSTT.IsCheckedChanged += UseSTT_IsCheckedChanged;
        this.realtimeCheckbox.IsCheckedChanged += RealtimeCheckbox_IsCheckedChanged;
        this.lipsyncCheckbox.IsCheckedChanged += LipsyncCheckbox_IsCheckedChanged;

        this.setupDevicesUI();

        modelBox.Items.Add("nothing");

        string sherpaSTTmodelsPath = "sherpa/stt-models/";

        if (!Directory.Exists(sherpaSTTmodelsPath))
            Directory.CreateDirectory(sherpaSTTmodelsPath);

        foreach (string fld in Directory.GetDirectories(sherpaSTTmodelsPath))
        {
            string fldName = Path.GetFileName(fld);
            modelBox.Items.Add(fldName);
        }

        invokeSpecialKey.Items.Add("");
        invokeSpecialKey.Items.Add("LCtrl");
        invokeSpecialKey.Items.Add("LShift");
        invokeSpecialKey.Items.Add("LAlt");

        invokeSpecialKey.SelectedItem = "";


        foreach (string key in Enum.GetNames(typeof(KeyCode)))
        {
            invokeKey.Items.Add(key[2..]);
        }

        invokeKey.SelectedItem = "Y";

        modelBox.SelectionChanged += modelBox_SelectedIndexChanged;

        DownloadManager.Output += DownloadManager_Output;
        DownloadManager.DownloadComplete += DownloadManager_DownloadComplete;

        this.inputDeviceBox.SelectedIndex = 0;

        this.readConfig();

    }

    private void DownloadManager_DownloadComplete(object? sender, EventArgs e)
    {
        modelBox.Items.Clear();
        modelBox.Items.Add("nothing");
        string sherpaSTTmodelsPath = "sherpa/stt-models/";
        foreach (string fld in Directory.GetDirectories(sherpaSTTmodelsPath))
        {
            string fldName = Path.GetFileNameWithoutExtension(fld);
            modelBox.Items.Add(fldName);
        }
    }

    public void readConfig()
    {
        if (File.Exists("cfg/config.txt"))
        {
            using (StreamReader sr = new StreamReader("cfg/config.txt"))
            {
                string ln1 = sr.ReadLine();
                string ln2 = sr.ReadLine();
                string ln3 = sr.ReadLine();
                string ln4 = sr.ReadLine();
                string ln5 = sr.ReadLine();
                string ln6 = sr.ReadLine();
                string ln7 = sr.ReadLine();
                string ln8 = sr.ReadLine();
                string ln9 = sr.ReadLine();
                string ln10 = sr.ReadLine();
                string ln11 = sr.ReadLine();
                string ln12 = sr.ReadLine();
                string ln13 = sr.ReadLine();
                string ln14 = sr.ReadLine();

                foreach (var opt in this.inputDeviceBox.Items)
                {
                    if (ln1.Contains(opt.ToString()))
                    {
                        this.inputDeviceBox.SelectedItem = opt;
                    }
                }

                foreach (var opt in this.virtualCableBox.Items)
                {
                    if (ln2.Contains(opt.ToString()))
                    {
                        this.virtualCableBox.SelectedItem = opt;
                    }
                }

                foreach (var opt in this.playbackDeviceBox.Items)
                {
                    if (ln3.Contains(opt.ToString()))
                    {
                        this.playbackDeviceBox.SelectedItem = opt;
                    }
                }

                double pvol = double.Parse( ln4.Split(new char[] { ':' })[1]);
                this.volumeSlider.Value = pvol * 100;

                useSTT.IsChecked = bool.Parse(ln5.Split(new char[] { ':' })[1]);

                foreach (var opt in this.modelBox.Items)
                {
                    if (ln6.Contains(opt.ToString()))
                    {
                        this.modelBox.SelectedItem = opt;
                    }
                }

                realtimeCheckbox.IsChecked = bool.Parse(ln7.Split(new char[] { ':' })[1]);

                micEnableCheckbox.IsChecked = bool.Parse(ln8.Split(new char[] { ':' })[1]);

                double mvol = double.Parse(ln9.Split(new char[] { ':' })[1]);
                this.micvolumeSlider.Value = mvol * 100/2;

                bool lipsync = bool.Parse(ln10.Split(new char[] { ':' })[1]);
                this.lipsyncCheckbox.IsChecked = lipsync;


                Common.voice = ln11.Split(new char[] { ':' })[1];
                
                invokeSpecialKey.SelectedItem= ln12.Split(new char[] { ':' })[1];
                invokeKey.SelectedItem = ln13.Split(new char[] { ':' })[1];

                Common.vttKey = (KeyCode)Enum.Parse(typeof(KeyCode),ln14.Split(new char[] { ':' })[1]);
            }
        }
    }

    public KeyCode getKeycode(string str)
    {
        str = "Vc" + str;
        return (KeyCode)Enum.Parse(typeof(KeyCode), str);
    }

    private void LipsyncCheckbox_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        Common.isLipsyncOn = (bool)this.lipsyncCheckbox.IsChecked;
    }

    private void RealtimeCheckbox_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        Common.isSttRealtime = (bool)realtimeCheckbox.IsChecked;
    }

    private void UseSTT_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        Common.sttEnabled = (bool)this.useSTT.IsChecked;
    }

    public void ClickHandler(object sender, RoutedEventArgs args)
    {
        if (overlay != null)
            return;

        if (this.micEnableCheckbox.IsChecked==true)
            Common.enableMicToCable = true;


        if (this.selectedMicrophone == null)
            overlay = new overlayWindow(this.selectedCableInput,this.selectedPlayback);
        else
            overlay = new overlayWindow(this.selectedCableInput, this.selectedPlayback, this.selectedMicrophone);
        overlay.Show();

        overlay.outputText.PropertyChanged += OutputText_PropertyChanged;

        Common.playbackVolume = this.volumeSlider.Value / 100;
        Common.micVolume = this.micvolumeSlider.Value / 100 * 2;

        if (invokeSpecialKey.SelectedItem == "")
            Common.invokeSpecialKey = KeyCode.VcUndefined;
        else if (invokeSpecialKey.SelectedItem == "LCtrl")
            Common.invokeSpecialKey = KeyCode.VcLeftControl;
        else if (invokeSpecialKey.SelectedItem == "LShift")
            Common.invokeSpecialKey = KeyCode.VcLeftShift;
        else if (invokeSpecialKey.SelectedItem == "LAlt")
            Common.invokeSpecialKey = KeyCode.VcLeftAlt;



        Common.invokeKey = getKeycode(invokeKey.SelectedItem.ToString());

        this.saveConfig();

    }

    public void saveConfig()
    {

        if (!Directory.Exists("cfg"))
            Directory.CreateDirectory("cfg");

        using (StreamWriter sw = new StreamWriter("cfg/config.txt"))
        {
            sw.WriteLine("mic:"+inputDeviceBox.SelectedItem.ToString());
            sw.WriteLine("vcable:" + selectedCableInput.FriendlyName);
            sw.WriteLine("playdevice:" + selectedPlayback.FriendlyName);
            sw.WriteLine("pvol:" + Common.playbackVolume);
            sw.WriteLine("sttenabled:"+Common.sttEnabled);
            sw.WriteLine("sttmodel:" + Common.sherpaSTTmodel);
            sw.WriteLine("sttrealtime:" + Common.isSttRealtime);
            sw.WriteLine("micenabled:" + Common.enableMicToCable);
            sw.WriteLine("mvol:"+Common.micVolume);
            sw.WriteLine("lipsync:"+this.lipsyncCheckbox.IsChecked.ToString());
            sw.WriteLine("voice:" +Common.voice);
            sw.WriteLine("invSpecialKey:" + invokeSpecialKey.SelectedItem);
            sw.WriteLine("invKey:" + invokeKey.SelectedItem);
            sw.WriteLine("vttkey:" + Common.vttKey);
        }

    }

    public void DownloadModelsHandler(object sender, RoutedEventArgs args)
    {

        if (dlModelsWnd!=null)
        {
            dlModelsWnd.Close();
        }
        dlModelsWnd = new DownloadModelsWindow();
        dlModelsWnd.Show();

    }

    public void DownloadSoundsHandler(object sender, RoutedEventArgs args)
    {
        new Thread(()=>{ 
            DownloadManager.DownloadSounds();
        }).Start();
    }


    private void DownloadManager_Output(object myObject, DownloadManager.OutputEventArgs myArgs)
    {
        Dispatcher.UIThread.InvokeAsync(() => this.outputCopy.Markdown = myArgs.text);

    }

    private void OutputText_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        this.outputCopy.Markdown = overlay.outputText.Markdown;
    }

    public void setupDevicesUI()
    {
        MMDeviceEnumerator enumerator;

        enumerator = new MMDeviceEnumerator();
        foreach (var endpoint in
                 enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            comboIn.Add(endpoint.FriendlyName);
            micIDs.Add(endpoint.ID);
            inDeviceList.Add(endpoint);
        }

        inputDeviceBox.Items.Add("i have no microphone");

        foreach (var i in comboIn)
        {
            inputDeviceBox.Items.Add(i);
        }

        

        enumerator = new MMDeviceEnumerator();
        foreach (var endpoint in
                 enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            comboOut.Add(endpoint.FriendlyName);
            speakerIDs.Add(endpoint.ID);
            outDeviceList.Add(endpoint);
        }
        foreach (var i in comboOut)
        {
            virtualCableBox.Items.Add(i);
            playbackDeviceBox.Items.Add(i);
        }
    }


    private void comboBoxInput_SelectedIndexChanged(object sender, EventArgs e)
    {
        foreach (var endpoint in inDeviceList)
        {
            if (endpoint.FriendlyName == inputDeviceBox.SelectedItem.ToString())
            {
                this.selectedMicrophone = endpoint;
                
            }
        }
    }

    private void virtualCableBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        foreach (var endpoint in outDeviceList)
        {
            if (endpoint.FriendlyName == virtualCableBox.SelectedItem.ToString())
            {
                this.selectedCableInput = endpoint;
                
            }
        }
    }

    private void playbackBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        foreach (var endpoint in outDeviceList)
        {
            if (endpoint.FriendlyName == playbackDeviceBox.SelectedItem.ToString())
            {
                this.selectedPlayback = endpoint;
            }
        }
    }

    private void modelBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (modelBox.SelectedItem!=null)
            Common.sherpaSTTmodel = modelBox.SelectedItem.ToString();
    }





}
