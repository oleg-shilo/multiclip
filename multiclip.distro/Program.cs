using multiclip.distro.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace multiclip.distro
{
    class Program
    {
        static void Main(string[] args)
        {
            var assembly_data = Resources.multiclip.Select(x => (byte)--x).ToArray(); // unscrumble
            var ui = Assembly.Load(assembly_data);
            var server = Assembly.Load(Resources.multiclip_server);

            AppDomain.CurrentDomain.AssemblyResolve += (sender, e_args)
                => e_args.Name.StartsWith("multiclip,") ? ui :
                   e_args.Name.StartsWith("multiclip.server,") ? server :
                   null;

            AppDomain.CurrentDomain.ExecuteAssemblyByName(ui.GetName(), args);
        }
    }
}