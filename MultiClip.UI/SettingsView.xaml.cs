using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace MultiClip.UI
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : Window
    {
        SettingsViewModel viewModel;

        public static bool EnsureDefaults()
        {
            return SettingsViewModel.EnsureDefaults();
        }

        static SettingsView activeView;

        public static void Popup()
        {
            if (activeView == null)
            {
                try
                {
                    HotKeys.Instance.UnregisterAll();

                    (activeView = new SettingsView()).ShowDialog();
                }
                catch { }
                finally
                {
                    activeView = null;

                    HotKeysMapping.Bind(HotKeys.Instance);
                }
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

        public SettingsView()
        {
            InitializeComponent();

            viewModel = SettingsViewModel.Load();
            AutoBinder.BindOnLoad(this, viewModel);
        }

        void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
                Close();
        }

        void Window_Closed(object sender, EventArgs e)
        {
            viewModel.Process();
            viewModel.Save();
            HotKeysMapping.Save(HotKeyEditor.viewModel.HotKeys);
        }

        void EditHotKeys_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            viewModel.ClearHistory();
        }

        void HotKeyEditorHide_Click(object sender, RoutedEventArgs e)
        {
            HotKeyEditor.viewModel.UpdateCurrentSelection(); //to capture changes if any
            HotKeysMapping.Save(HotKeyEditor.viewModel.HotKeys);
            viewModel.RefreshHotKeysView();
        }

        void PurgeHistory_Click(object sender, RoutedEventArgs e)
        {
            PurgeHistory.IsEnabled = false;
            this.Dispatcher.InUIThread(() =>
            {
                viewModel.PurgeHistory();
                PurgeHistory.IsEnabled = true;
            });
        }
    }
}