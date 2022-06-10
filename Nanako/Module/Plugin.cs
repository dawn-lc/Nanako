using Konata.Core;
using Konata.Core.Events;
using Konata.Core.Message;
using Konata.Core.Message.Model;
using Spectre.Console;
using System.Reflection;

namespace Nanako.Module
{
    public abstract class Plugin
    {
        public abstract string Name { get; } 
        public abstract string Info { get; }
        public abstract List<Permission> PermissionList { get; }
        public virtual async Task<bool> Process(Bot bot, BaseEvent e)
        {
            return await Task.FromResult(true);
        }
        public static MessageStruct? GetHistoryMessage(ReplyChain replyChain)
        {
            return Program.GetHistoryMessage(replyChain);
        }
        public static List<Bot> GetBotList(Predicate<Bot>? match = null)
        {
            if(match == null) return Program.BotList;
            return Program.BotList.FindAll(match);
        }
        public static uint GetMessageCounter()
        {
            return Program.MessageCounter;
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
                foreach (Type t in Assembly.LoadFrom(file.FullName).GetExportedTypes())
                {
                    if (t.IsSubclassOf(typeof(Plugin)) && !t.IsAbstract && Activator.CreateInstance(t) is Plugin plugin)
                    {
                        if (!Program.Config.PluginConfigTable.ContainsKey(plugin.Name))
                        {
                            Program.Config.PluginConfigTable.Add(plugin.Name, new());
                        }
                        Program.Plugins.Add(plugin);
                        AnsiConsole.WriteLine($"插件 {plugin.Name} 已加载");
                    }
                }
            }
        }
    }
}
