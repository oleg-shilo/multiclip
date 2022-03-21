using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace MultiClip.UI.Utils
{
    /// <summary>
    /// Interaction logic for HotKeyEditor.xaml
    /// </summary>
    public partial class HotKeyEditor : UserControl
    {
        public HotKeyEditorViewModel viewModel;

        public HotKeyEditor()
        {
            InitializeComponent();
            viewModel = HotKeyEditorViewModel.Load();
            AutoBinder.BindOnLoad(this, viewModel);

            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            HotKeys.SelectionChanged += HotKeys_SelectionChanged;

            viewModel.SelectedHotKey = viewModel.HotKeys.FirstOrDefault();
        }

        void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(viewModel.CurrentItemHotKey))
            {
                HotKeyValue.Text =
                hotkey = viewModel.CurrentItemHotKey.ToReadableHotKey();
            }
            else if (e.PropertyName == nameof(viewModel.IsErrorSet))
            {
                if (viewModel.IsErrorSet)
                    ShowError();
            }
        }

        void HotKeys_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HotKeys.Items.Refresh(); //will update the item title if the bound items are changed
            HotKeys.ScrollIntoView(HotKeys.SelectedItem);
        }

        string hotkey;

        void KeysView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var oldHotkey = hotkey;

            Key wpfKey = e.Key == Key.System ? e.SystemKey : e.Key;
            var formsKey = (System.Windows.Forms.Keys)KeyInterop.VirtualKeyFromKey(wpfKey);

            var key = formsKey.ToString();
            var modifiers = "";

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                modifiers += "Ctrl+";

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                modifiers += "Shift+";

            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                modifiers += "Alt+";

            if (modifiers == "" || key.EndsWith("ShiftKey") || key.EndsWith("ControlKey") || key.EndsWith("Menu"))
                hotkey = "";
            else
                hotkey = modifiers + key;

            hotkey = hotkey.ToReadableHotKey();

            if (!hotkey.IsEmpty())
                if (viewModel.HotKeys.Where(x => x != viewModel.SelectedHotKey).Any(x => x.HotKey.ToReadableHotKey() == hotkey))
                {
                    viewModel.LastError = "The hot key combination is already taken";
                    hotkey = "";
                }

            viewModel.EnteredHotKey =
            HotKeyValue.Text = hotkey;
        }

        void KeysView_TextChanged(object sender, TextChangedEventArgs e)
        {
            HotKeyValue.Text = hotkey;
        }

        void EditFile_Click(object sender, RoutedEventArgs e)
        {
            this.GetParent<Window>().Close();
            HotKeysMapping.Edit();
        }

        void ShowError_Click(object sender, RoutedEventArgs e)
        {
            ShowError();
        }

        void ShowError()
        {
            (FindResource("ShowError") as Storyboard)?.Begin();
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            HotKeys.Items.Refresh();
        }

        void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.InUIThread(100,  HotKeys.Items.Refresh);
        }
    }
}