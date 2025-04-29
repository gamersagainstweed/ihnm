using Avalonia;
using Avalonia.Controls;
using System;

namespace ihnm.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.Height = 800;
        this.Width = 500;

        this.Closing += (s, e) =>
        {
            Environment.Exit(0);
        };


    }
}
