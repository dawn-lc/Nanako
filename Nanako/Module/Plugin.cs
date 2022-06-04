using Konata.Core;
using Konata.Core.Events;
using System.Reflection;

namespace Nanako.Module
{
    public abstract class Plugin
    {
        public abstract string Name { get; }
        public abstract string Info { get; }
        public abstract Task<bool> Process(Bot bot, BaseEvent e);
    }

    public class PluginFactory
    {
        public static void LoadPlugins()
        {
            IEnumerable<FileInfo> files = new DirectoryInfo("Plugins").GetFiles().Where(p => p.Extension == ".dll");
            foreach (var file in files)
            {
                foreach (Type t in Assembly.LoadFile(file.FullName).GetExportedTypes())
                {
                    if (t.IsSubclassOf(typeof(Plugin)) && !t.IsAbstract && Activator.CreateInstance(t) is Plugin plugin)
                    {
                        Program.Plugins.Add(plugin);
                    }
                }
            }
        }
    }
}
