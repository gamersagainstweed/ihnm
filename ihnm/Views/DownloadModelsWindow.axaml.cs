using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ihnm.Enums;
using ihnm.Managers;
using SharpCompress.Common;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static ihnm.Managers.DownloadManager;

namespace ihnm;

public partial class DownloadModelsWindow : Window
{

    List<modelEntry> entries = new List<modelEntry>();

    List<string> models = new List<string>();


    int mbSum = 0;

    public DownloadModelsWindow()
    {
        InitializeComponent();
        this.Height = 700;
        this.Width = 700;

        this.SizeChanged += DownloadModelsWindow_SizeChanged;

        DownloadManager.Output += DownloadManager_Output;
        DownloadManager.DownloadComplete += DownloadManager_DownloadComplete;

        this.menuGrid.RowDefinitions[2].Height = GridLength.Parse((this.Height - 200).ToString());

        this.ttsClick(null,null);
    }

    private void DownloadModelsWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        this.menuGrid.RowDefinitions[2].Height = GridLength.Parse((this.Height - 200).ToString());
    }

    private void DownloadManager_DownloadComplete(object? sender, System.EventArgs e)
    {
        Common.downloadInProcess = false;
    }

    public void selectAll(object sender, RoutedEventArgs args)
    {
        foreach (var entry in entries)
        {
            entry.box.IsChecked = true;
        }
    }

    public void ttsClick(object sender, RoutedEventArgs args)
    {
        modelsGrid.Children.Clear();
        entries = new List<modelEntry>();
        mbSum = 0;

        TextBlock ttsMdls = new TextBlock() { Text = "Text-to-speech models" };
        ttsMdls.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        modelsGrid.Children.Add(ttsMdls);

        foreach (sherpaTTSmodel model in DownloadManager.sherpaTTSmodels)
        {
            string langStr = "";

            foreach (EnumLanguage lang in model.languages)
            {
                langStr += lang.ToString() + ", ";
            }

            langStr = langStr.Substring(0, langStr.Length - 2);

            TextBlock mdlName = new TextBlock()
            {
                Text = model.id  + " (" + model.filesize.ToString() + "MB)" + " " +
            "(" + model.speakers.ToString() + " voices)" + " (" + langStr + ")"
            };
            mdlName.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            CheckBox mdlBox = new CheckBox();

            Grid entry = new Grid() { };
            entry.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            entry.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            entry.Children.Add(mdlName);
            entry.Children.Add(mdlBox);
            Grid.SetColumn(mdlName, 1);

            entries.Add(new modelEntry() { box = mdlBox, model = model.id });

            modelsGrid.Children.Add(entry);

            mbSum += model.filesize;

        }

        this.selectAllBtn.Content = "Select all" + " (" + ((double)mbSum / 1024).ToString("f2") + "GB) ";

    }

    public void sttClick(object sender, RoutedEventArgs args)
    {
        modelsGrid.Children.Clear();
        entries = new List<modelEntry>();
        mbSum = 0;

        TextBlock sttMdls = new TextBlock() { Text = "Speech-to-text models" };
        sttMdls.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        modelsGrid.Children.Add(sttMdls);

        foreach (sherpaSTTmodel model in DownloadManager.sherpaSTTmodels)
        {
            string langStr = "";

            foreach (EnumLanguage lang in model.languages)
            {
                langStr += lang.ToString() + ", ";
            }

            langStr = langStr.Substring(0, langStr.Length - 2);

            TextBlock mdlName = new TextBlock()
            {
                Text = model.id  + " (" + model.filesize.ToString() + "MB)" + " (" + langStr + ")"
            };
            mdlName.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            CheckBox mdlBox = new CheckBox();

            Grid entry = new Grid() { };
            entry.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            entry.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            entry.Children.Add(mdlName);
            entry.Children.Add(mdlBox);
            Grid.SetColumn(mdlName, 1);

            entries.Add(new modelEntry() { box = mdlBox, model = model.id });

            modelsGrid.Children.Add(entry);

            mbSum += model.filesize;
        }

        this.selectAllBtn.Content = "Select all" + " (" + ((double)mbSum / 1024).ToString("f2") + "GB) ";


    }

    public void vadClick(object sender, RoutedEventArgs args)
    {
        modelsGrid.Children.Clear();
        entries = new List<modelEntry>();
        mbSum = 0;

        TextBlock vadMdls = new TextBlock() { Text = "Voice activity detection models" };
        vadMdls.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        modelsGrid.Children.Add(vadMdls);

        foreach (sherpaVADmodel model in DownloadManager.sherpaVADmodels)
        {

            TextBlock mdlName = new TextBlock()
            {
                Text = model.id + " (" + model.filesize.ToString() + "MB)"
            };
            mdlName.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            CheckBox mdlBox = new CheckBox();

            Grid entry = new Grid() { };
            entry.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            entry.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            entry.Children.Add(mdlName);
            entry.Children.Add(mdlBox);
            Grid.SetColumn(mdlName, 1);

            entries.Add(new modelEntry() { box = mdlBox, model = model.id });

            modelsGrid.Children.Add(entry);

            mbSum += model.filesize;
        }

        this.selectAllBtn.Content = "Select all" + " (" + ((double)mbSum / 1024).ToString("f2") + "GB) ";

    }

    private void DownloadManager_Output(object myObject, OutputEventArgs myArgs)
    {
        Dispatcher.UIThread.InvokeAsync(() => this.output.Text = myArgs.text);
    }

    public void DownloadHandler(object sender, RoutedEventArgs args)
    {
        if (Common.downloadInProcess==false)
        {

            foreach (modelEntry entr in entries)
            {
                if (entr.box.IsChecked==true)
                {
                    models.Add(entr.model);
                }
            }


            
            new Thread(() =>
            {
                DownloadManager.DownloadModels(models);

            }).Start();
            Common.downloadInProcess = true;
        }
    }


    class modelEntry
    {
        public string model;
        public CheckBox box;
    }

}