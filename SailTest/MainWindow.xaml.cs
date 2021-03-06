﻿using Pear.RiaServices.Client.DataComponent;
using System.Windows;

namespace SailTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var model = new SailVM();
            sail.DataContext = model;

            model.Redraw();
        }
    }
}
