using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using static MultiClip.UI.HotKeysMapping;

namespace MultiClip.UI.Utils
{
    public class HotKeyEditorViewModel : NotifyPropertyChangedBase
    {
        public void DoSomeTest(RoutedEventArgs e)
        {
        }

        public static HotKeyEditorViewModel Load()
        {
            return new HotKeyEditorViewModel();
        }

        public HotKeyEditorViewModel()
        {
            Reset();
        }

        public void Add()
        {
            var newBinding = new HotKeyBinding { Name = "New Binding" };
            HotKeys.Add(newBinding);
            SelectedHotKey = newBinding;
        }

        public void Remove()
        {
            if (NonBuiltInCommand)
            {
                var index = HotKeys.IndexOf(SelectedHotKey);
                HotKeys.RemoveAt(index);
                index--;
                SelectedHotKey = HotKeys[Math.Max(index, 0)];
            }
        }

        void Reset()
        {
            HotKeys.Clear();

            foreach (HotKeyBinding item in HotKeysMapping.Load())
                HotKeys.Add(item);
        }

        public string EnteredHotKey { get; set; }

        public string CurrentItemHotKey { get; set; } 

        public bool NonBuiltInCommand
        {
            get { return !(SelectedHotKey?.Name ?? "").StartsWith("<"); }
        }

        public bool IsErrorSet { get { return !LastError.IsEmpty(); } }

        string lastError;

        public string LastError
        {
            get { return lastError; }

            set
            {
                lastError = value;
                OnPropertyChanged(() => this.LastError);
                OnPropertyChanged(() => this.IsErrorSet);
            }
        }

        public void UpdateCurrentSelection()
        {
            if (selectedHotKey != null)
            {
                if (!EnteredHotKey.IsEmpty())
                    selectedHotKey.HotKey = EnteredHotKey.ToMachineHotKey();
            }
        }

        HotKeyBinding selectedHotKey;

        public HotKeyBinding SelectedHotKey
        {
            get { return selectedHotKey; }

            set
            {
                if (selectedHotKey != value)
                    UpdateCurrentSelection();

                selectedHotKey = value;

                if (selectedHotKey != null)
                {
                    CurrentItemHotKey = selectedHotKey.HotKey.ToReadableHotKey();
                    LastError = null;
                }
                else
                {
                    CurrentItemHotKey =
                    LastError = null;
                }

               OnPropertyChanged(() => this.CurrentItemHotKey);
               OnPropertyChanged(() => this.SelectedHotKey);
               OnPropertyChanged(() => this.NonBuiltInCommand);
            }
        }

        public ObservableCollection<HotKeyBinding> HotKeys { get; set; } = new ObservableCollection<HotKeyBinding>();
    }
}