using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MultiClip.UI.Utils
{
    /// <summary>
    /// Interaction logic for HotKeysView.xaml
    /// </summary>
    public partial class HotKeysView : Window
    {
        public class Item
        {
            public string Name;
            public Action Action;

            public override string ToString()
            {
                return Name;
            }
        }

        public HotKeysView()
        {
            InitializeComponent();

            var map = HotKeysMapping.ToKeyHandlersView();
            foreach (var key in map.Keys)
                mappingList.Items.Add(new Item { Name = key, Action = map[key] });
        }

        public static string PopupActionName = "<MultiClip.ShowHotKeys>";

        static public void Popup()
        {
            new HotKeysView().ShowDialog();
        }

        void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Return)
            {
                Close();
                if (e.Key == Key.Return)
                    (mappingList.SelectedItem as Item)?.Action();
            }
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
            mappingList.SelectedIndex = 0;
            (mappingList.ItemContainerGenerator
                        .ContainerFromItem(mappingList.SelectedItem) as ListBoxItem)?
                        .Focus();
        }

        private void mappingList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            (mappingList.SelectedItem as Item)?.Action();
        }
    }
}