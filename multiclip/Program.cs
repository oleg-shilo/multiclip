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
        static void Scrumble(string inFile, string outFile)
        {
            var bytes = File.ReadAllBytes(inFile);
            for (int i = 0; i < bytes.Length; i++)
                if ((i % 2) == 0)
                    bytes[i] = (byte)(bytes[i] + 1);
            File.WriteAllBytes(outFile, bytes);
        }

        static void Unscrumble(string inFile, string outFile)
        {
            var bytes = File.ReadAllBytes(inFile);
            for (int i = 0; i < bytes.Length; i++)
                if ((i % 2) == 0)
                    bytes[i] = (byte)(bytes[i] - 1);
            File.WriteAllBytes(outFile, bytes);
        }

        [STAThread]
        static void Main(string[] args)
        {
            var file = @"D:\dev\Galos\multiclip.git\MultiClip\bin\Debug\multiclip.exe";
            var assemblyFile1 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "multiclip.exe");

            var assemblyFile = @"D:\dev\Galos\multiclip.git\MultiClip\bin\Debug\multiclip.exe";

            AppDomain.CurrentDomain.ExecuteAssembly(assemblyFile);
            try
            {
                // Scrumble();
            }
            catch { }
            {
            }
        }
    }
}