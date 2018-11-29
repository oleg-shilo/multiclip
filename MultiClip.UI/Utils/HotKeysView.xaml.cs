using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MultiClip.UI.Utils
{
    /// <summary>
    /// Interaction logic for HotKeysView.xaml
    /// </summary>
    public partial class HotKeysView : Window
    {
        public HotKeysView()
        {
            InitializeComponent();
            mapping.Text = HotKeysMapping.ToView();
        }

        static internal void Popup()
        {
            new HotKeysView().ShowDialog();
        }

        void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Return)
                Close();
        }

        void Window_Deactivated(object sender, EventArgs e)
        {
            try
            {
                Close();
            }
            catch { }
        }

        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // to ensure the keyboard input focus
            this.Activate();
        }
    }
}
