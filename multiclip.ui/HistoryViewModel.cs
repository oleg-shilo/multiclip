using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MultiClip.UI
{
    class HistoryViewModel
    {
        public static string DataDir = Globals.DataDir;

        public ObservableCollection<HistoryItemViewModel> Items { get; set; } = new ObservableCollection<HistoryItemViewModel>();

        public HistoryItemViewModel Selected { get; set; }

        public object PreviewImage { get; set; }

        public void Remove(HistoryItemViewModel item)
        {
            if (item != null)
            {
                Log.WriteLine("Removing history item 2: " + item.Location);
                item.Location.TryDeleteDir();
                Items.Remove(item);
            }
        }

        public void RemoveAll()
        {
            Items.ForEach(item =>
            {
                Log.WriteLine("Removing history item (3): " + item.Location);
                item.Location.TryDeleteDir();
            });
            Items.Clear();
        }

        public void Reset()
        {
            Items.Clear();
            var sw = new Stopwatch();
            foreach (string dir in Directory.GetDirectories(DataDir).Reverse())
            {
                sw.Start();

                Items.Add(HistoryItemViewModel.LoadFrom(dir));
                Debug.WriteLine(sw.Elapsed + " - " + dir);
                sw.Reset();
            }
        }
    }
}