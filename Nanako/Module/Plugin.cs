using Konata.Core;
using Konata.Core.Events;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces.Api;
using Konata.Core.Message;
using Konata.Core.Message.Model;
using Nanako.Module;
using Nanako.Utils;
using System.Text.Json;
using System.Reflection;

namespace Nanako.Module
{
    public abstract class Plugin
    {
        public abstract string Name { get; }
        public abstract string Info { get; }
        public virtual async Task<bool> Process(Bot bot, BaseEvent e)
        {
            return await Task.FromResult(true);
        }
        public static MessageStruct? GetHistoryMessage(ReplyChain replyChain)
        {
            return Program.GetHistoryMessage(replyChain);
        }
        public static Bot? GetBot(Predicate<Bot> match)
        {
            return Program.BotList.Find(match);
        }
        public static bool CheckPermission(uint u, string p)
        {
            return Permissions.Check(u, p);
        }
        public static async Task<bool> EnableBot(Bot? bot, uint u)
        {
            if (Permissions.Check(u, "EnableBot")) return await Program.EnableBot(bot);
            return false;
        }
        public static async Task<bool> EnableBot(uint bot, uint u)
        {
            return await EnableBot(GetBot(p => p.Uin == bot), u);
        }
        public static async Task<bool> DisableBot(Bot? bot, uint u)
        {
            if (Permissions.Check(u, "DisableBot")) return await Program.DisableBot(bot);
            return false;
        }
        public static async Task<bool> DisableBot(uint bot, uint u)
        {
            return await DisableBot(GetBot(p => p.Uin == bot), u);
        }
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
