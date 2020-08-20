using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace MultiClip.UI
{
    public enum Modifiers : int
    {
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }

    public static class HotKeysMapping
    {
        static string hotKeysMapping = Path.Combine(Globals.DataDir, @"..\multiclip.hotkeys");
        static internal Dictionary<string, Action> EmbeddedHandlers = new Dictionary<string, Action>();

        public static void Bind(HotKeys engine, ToolStripMenuItem rootMenu)
        {
            Bind(engine, rootMenu, hotKeysMapping);
        }

        public static void Bind(HotKeys engine, ToolStripMenuItem rootMenu, string mappingFile)
        {
            if (engine.Started)
            {
                EnsureDefaults(mappingFile);

                if (File.Exists(mappingFile))
                {
                    TrayIcon.InvokeMenu.DropDownItems.Clear();

                    foreach (HotKeyBinding item in LoadFrom(mappingFile))
                    {
                        try
                        {
                            string[] tokens = item.HotKey.Split('+').Select(x => x.Trim()).ToArray();

                            Keys key = tokens.Last().ToKeys();

                            Modifiers modifiers = tokens.Take(tokens.Length - 1)
                                                        .ToModifiers();

                            var handlerName = item.Name;

                            Action handler;
                            if (EmbeddedHandlers.ContainsKey(handlerName)) //<MultiClip.Show>
                            {
                                handler = EmbeddedHandlers[handlerName];
                            }
                            else
                            {
                                string app = item.Application;
                                string args = item.Args;

                                handler = () =>
                                           {
                                               try
                                               {
                                                   Process.Start(app, args);
                                               }
                                               catch (Exception e)
                                               {
                                                   System.Windows.MessageBox.Show(e.Message, "MultiClip - " + handlerName);
                                               }
                                           };
                            }

                            rootMenu.DropDownItems.Add(handlerName, null, (s, e) => handler());
                            engine.Bind(modifiers, key, handler);
                        }
                        catch { }
                    }
                }
            }
        }

        public static bool Edit()
        {
            return Edit(hotKeysMapping);
        }

        public static bool Edit(string mappingFile)
        {
            try
            {
                mappingFile = Path.GetFullPath(mappingFile);

                EnsureDefaults(mappingFile);

                var timestamp = File.GetLastWriteTimeUtc(mappingFile);
                Process.Start("notepad.exe", mappingFile).WaitForExit();
                return timestamp != File.GetLastWriteTimeUtc(mappingFile);
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.ToString(), "MultiClip.UI");
                return false;
            }
        }

        public static string ToView(string mappingFile = null)
        {
            mappingFile = mappingFile ?? hotKeysMapping;

            if (File.Exists(mappingFile))
            {
                var result = new StringBuilder();

                foreach (HotKeyBinding item in LoadFrom(mappingFile))
                {
                    string hotKey = item.HotKey.ToReadableHotKey();

                    result.AppendLine(string.Format("{0}{2}\t - {1}",
                                                    hotKey,
                                                    item.Name,
                                                    (hotKey.Length < 8 ? "\t" : "")));
                }

                return result.ToString();
            }
            else
                return "Hot keys mapping was not loaded.";
        }

        static void EnsureDefaults(string mappingFile = null)
        {
            mappingFile = mappingFile ?? hotKeysMapping;

            if (!File.Exists(mappingFile))
                CreateDefaultMappingFile(mappingFile);
        }

        static string ConfigHeader =
@";<hotkey>
;  [name]
;  <application>[|argument0...[|argumentN]]
;  Example: notepad.exe|" + hotKeysMapping + @"
;---------------------------------------";

        static void CreateDefaultMappingFile(string mappingFile)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(mappingFile));
            File.WriteAllText(mappingFile, ConfigHeader +
@"
Ctrl+Oem3
  <MultiClip.Show>
Ctrl+Shift+T
  <MultiClip.ToPlainText>
Ctrl+Shift+K
  <MultiClip.ShowHotKeys>
Ctrl+Shift+Q
  <MultiClip.Reset>
;---------------------------------------");
        }

        public class HotKeyBinding
        {
            public string Name { get; set; }
            public string HotKey { get; set; }
            public string Application { get; set; }
            public string Args { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        public static List<HotKeyBinding> Load()
        {
            return LoadFrom(hotKeysMapping);
        }

        public static void Save(IEnumerable<HotKeyBinding> bindings)
        {
            try
            {
                var data = new StringBuilder();
                data.AppendLine(ConfigHeader);

                foreach (HotKeyBinding item in bindings)
                {
                    if (item.HotKey.IsNotEmpty())
                    {
                        data.AppendLine(item.HotKey);
                        data.AppendLine("  " + item.Name);
                        var command = string.Join("|", item.Application, item.Args).Trim();
                        if (!command.IsEmpty() && command != "|")
                            data.AppendLine("  " + command);
                        data.AppendLine();
                    }
                }

                File.WriteAllText(hotKeysMapping, data.ToString());
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show("Cannot save hot keys settings: " + e.Message, "MultiClip");
            }
        }

        public static List<HotKeyBinding> LoadFrom(string mappingFile)
        {
            var result = new List<HotKeyBinding>();

            if (!File.Exists(mappingFile))
                CreateDefaultMappingFile(mappingFile);

            var mapping = new Dictionary<string, List<string>>();

            string currentKey = null;
            try
            {
                foreach (var line in File.ReadAllLines(mappingFile).Where(x => !x.StartsWith(";") && !x.IsEmpty()))
                {
                    if (!line.StartsWith(" ")) //hot key
                    {
                        //e.g. Ctrl+Shift+K
                        currentKey = line.Replace("Ctrl", "Control");
                        mapping[currentKey] = new List<string>();
                    }
                    else
                    {
                        if (currentKey != null && mapping.ContainsKey(currentKey))
                            mapping[currentKey].Add(line.Trim());
                    }
                }

                foreach (string key in mapping.Keys)
                {
                    List<string> data = mapping[key];

                    string[] command = (data.Count > 1 ? data[1].Split(new[] { '|' }, 2) : new string[0]);

                    result.Add(new HotKeyBinding
                    {
                        HotKey = key,
                        Name = data[0],
                        Application = (command.FirstOrDefault() ?? "").Trim('"'),
                        Args = command.Skip(1).FirstOrDefault() ?? ""
                    });
                }
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.Message, "MultiClip");
            }

            return result;
        }
    }

    public class HotKeys
    {
        static HotKeys instance;

        static public HotKeys Instance
        {
            get
            {
                return instance ?? (instance = new HotKeys());
            }
        }

        Dictionary<int, List<Action>> handlers = new Dictionary<int, List<Action>>();

        [DllImport("User32.dll")]
        static extern bool RegisterHotKey([In] IntPtr hWnd, [In] int id, [In] uint fsModifiers, [In] uint vk);

        [DllImport("User32.dll")]
        static extern bool UnregisterHotKey([In] IntPtr hWnd, [In] int id);

        HwndSource source;
        Window wnd;

        HotKeys() //private
        {
        }

        void Init()
        {
            wnd = new Window
            {
                Width = 0,
                Height = 0,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            wnd.SourceInitialized += (s, e) =>
            {
                source = HwndSource.FromHwnd(wnd.GetSafeHandle());
                source.AddHook(HwndHook);
            };

            wnd.Loaded += (s, e) =>
            {
                wnd.MakeInvisible();
            };

            wnd.Closing += (s, e) =>
            {
                UnregisterAll();
                if (source != null)
                {
                    source.RemoveHook(HwndHook);
                    source = null;
                }
            };
        }

        public bool Started
        {
            get { return wnd != null && wnd.IsVisible; }
        }

        public void Start()
        {
            if (wnd == null)
                Init();

            wnd.Show();
        }

        public void Stop()
        {
            wnd.Close();
        }

        public int Bind(Modifiers modifiers, Keys key, Action handler)
        {
            if (source == null)
            {
                //throw new Exception("HotKey object isn't initialized yet. You need to call HotKeys.Start before setting any binding.");
                return 0;
            }
            else
            {
                int id = string.Format("{0}:{1}", key, modifiers).GetHashCode();

                if (RegisterHotKey(source.Handle, id, (uint)modifiers, (uint)key))
                {
                    if (!handlers.ContainsKey(id))
                        handlers[id] = new List<Action>();

                    handlers[id].Add(handler);

                    return id;
                }
                else
                    return -1;
            }
        }

        public void UnregisterAll()
        {
            if (Started)
            {
                var hWnd = wnd.GetSafeHandle();

                foreach (var hotKey in handlers.Keys)
                    UnregisterHotKey(hWnd, hotKey);

                handlers.Clear();
            }
        }

        //public static bool PauseAllHandlers = false;

        IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            switch (msg)
            {
                case WM_HOTKEY:
                    {
                        var id = wParam.ToInt32();
                        if (handlers.ContainsKey(id))
                        {
                            foreach (Action handler in handlers[id])
                                try { handler(); } catch { }
                            handled = true;
                            break;
                        }
                    }
                    break;
            }
            return IntPtr.Zero;
        }
    }
}