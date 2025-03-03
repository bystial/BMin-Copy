﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VMS.TPS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ScriptWindow : Window
    {
        public ScriptWindow(ViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void Close_GUI(object sender, RoutedEventArgs e)
        {
            Close(); // this closes the script GUI
        }

    }
}
