using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace multiclip.schim
{
    internal class Program
    {
        static byte[] Scrumble(bool toScramble, string inFile)
        {
            var bytes = File.ReadAllBytes(inFile);
            for (int i = 0; i < bytes.Length; i++)
                if ((i % 2) == 0)
                    bytes[i] = (byte)(toScramble ? bytes[i] - 1 : bytes[i] + 1);
            return bytes;
        }

        static void Scrumble(bool toScramble, string inFile, string outFile)
            => File.WriteAllBytes(outFile, Scrumble(toScramble, inFile));

        static Assembly asm;

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Any())
            {
                bool toScramble = (args.First() == "-s");
                string inFile = args[1];
                string outFile = args[2];

                Scrumble(toScramble, inFile, outFile);
            }
            else
            {
                AppDomain.CurrentDomain.ResourceResolve += CurrentDomain_ResourceResolve;
                var scambledAsm = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "multiclip.ui");
                asm = Assembly.Load(Scrumble(toScramble: false, scambledAsm));
                var app = asm.GetType("MultiClip.UI.App").GetMethod("Main");
                app.Invoke(null, new object[0]);
            }
        }

        private static Assembly CurrentDomain_ResourceResolve(object sender, ResolveEventArgs args)
        {
            return asm;
        }
    }
}