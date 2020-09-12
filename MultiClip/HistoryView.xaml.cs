using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MultiClip.UI
{
    public partial class HistoryView : Window
    {
        private HistoryViewModel ViewModel
        {
            get { return (HistoryViewModel)DataContext; }
            set { DataContext = value; }
        }

        public HistoryView()
        {
            InitializeComponent();
            ViewModel = new HistoryViewModel();
            Closed += (s, e) => IsClosed = true;
        }

        private bool IsClosed = false;

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Hide();

            if (e.Key == Key.Return)
            {
                Operations.SetClipboardTo(ViewModel.Selected.Location);
                Hide();
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            ViewModel.Items.Clear();
            //Hide();
            CloseIfAny();

            // ClipboardMonitor.Restart(); // has nasty artifacts
        }

        private static HistoryView activeView;

        public static string PopupActionName = "<MultiClip.Show>";

        public static void Popup()
        {
            if (activeView == null || activeView.IsClosed)
                activeView = new HistoryView();

            try
            {
                activeView.ViewModel.Reset();
                activeView.History.SelectedIndex = 0;

                activeView.Show();

                //activeView.CentreOnActiveScreen();

                activeView.Activate();
            }
            catch (InvalidOperationException)
            {
                activeView = null;
            }
            catch (Exception)
            {
            }
        }

        public static void CloseIfAny()
        {
            try
            {
                if (activeView != null)
                    activeView.Close();
            }
            catch { }
        }

        private void History_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Operations.SetClipboardTo(ViewModel.Selected.Location);

            var wnd = sender.GetParent<Window>();
            if (wnd != null)
                wnd.Close();
        }

        private void History_Loaded(object sender, RoutedEventArgs e)
        {
            if (History.SelectedIndex == -1)
                History.SelectedIndex = 0;

            History.ItemContainerGenerator.StatusChanged += (s, a) =>
            {
                if (History.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    var index = History.SelectedIndex;
                    if (index >= 0)
                    {
                        var item = History.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
                        item?.Focus();
                    }
                }
            };
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Remove(History.SelectedItem as HistoryItemViewModel);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RemoveAll();
        }
    }
}