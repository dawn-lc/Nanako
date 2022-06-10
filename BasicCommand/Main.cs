using Konata.Core;
using Konata.Core.Common;
using Konata.Core.Events;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces.Api;
using Konata.Core.Message;
using Konata.Core.Message.Model;
using Nanako.Module;
using Nanako.Utils;

namespace BasicCommand
{
    public class EnableBot : Permission
    {
        public override string Node { get; set; } = "EnableBot";
        public override bool Flag { get; set; } = false;
    }
    public class DisableBot : Permission
    {
        public override string Node { get; set; } = "DisableBot";
        public override bool Flag { get; set; } = false;
    }
    public class Main : Plugin
    {
        public override string Name => "BasicCommand";

        public override string Info => "基本指令";

        public override List<Permission> PermissionList => new() { new EnableBot(), new DisableBot()};

        public override async Task<bool> Process(Bot bot, BaseEvent eventSource)
        {

            switch (eventSource)
            {
                case GroupMessageEvent Message:
                    if (Message.Chain.First() is TextChain GroupChain)
                    {
                        var Command = new Commands<string>(GroupChain.Content.ToLower());
                        if (Command[0][..1] == "/") await bot.SendGroupMessage(Message.GroupUin, Command[0][1..].ToLower() switch
                        {
                            "帮助" => OnCommandHelp(),
                            "ping" => OnCommandPing(),
                            "状态" => OnCommandStatus(),
                            _ => Text("未知命令")
                        });
                    } else if (Message.Chain.Any(p => p is AtChain at && at.AtUin == bot.Uin))
                    {
                        if (!Message.Chain.Any(p => p is TextChain text && text.Content.Trim() != "")) break;
                        var Command = Message.Chain.Where(p => p is TextChain text && text.Content.Trim() != "").First() as TextChain;
                        await bot.SendGroupMessage(Message.GroupUin, Command.Content.Trim().ToLower() switch
                        {
                            "帮助" => OnCommandHelp(),
                            "ping" => OnCommandPing(),
                            "状态" => OnCommandStatus(),
                            _ => Text("未知命令")
                        });
                    }
                    break;
                case FriendMessageEvent Message:
                    if (Message.Chain.First() is TextChain FriendChain)
                    {
                        var Command = new Commands<string>(FriendChain.Content.Trim());
                        if (Command[0][..1] == "/") await bot.SendFriendMessage(Message.FriendUin, Command[0][1..].ToLower() switch
                        {
                            "帮助" => OnCommandHelp(),
                            "ping" => OnCommandPing(),
                            "状态" => OnCommandStatus(),
                            "启用bot" => await EnableBot(Convert.ToUInt32(Command[1]), Message.FriendUin) ? Text("已启用") : Text("指令执行失败, 没有足够的权限执行该命令..."),
                            "禁用bot" => await DisableBot(Convert.ToUInt32(Command[1]), Message.FriendUin) ? Text("已禁用") : Text("指令执行失败, 没有足够的权限执行该命令..."),
                            _ => Text("未知命令")
                        });
                    }
                    else if (Message.Chain.Any(p => p is AtChain at && at.AtUin == bot.Uin))
                    {
                        if (!Message.Chain.Any(p => p is TextChain text && text.Content.Trim() != "")) break;
                        var Command = Message.Chain.Where(p => p is TextChain text && text.Content.Trim() != "").First() as TextChain;
                        await bot.SendFriendMessage(Message.FriendUin, Command.Content.Trim().ToLower() switch
                        {
                            "帮助" => OnCommandHelp(),
                            "ping" => OnCommandPing(),
                            "状态" => OnCommandStatus(),
                            _ => Text("未知命令")
                        });
                    }
                    break;
                default:
                    break;
            }
            return true;
        }
        

        /// <summary>
        /// Get target in group permission
        /// </summary>
        /// <param name="group"></param>
        /// <param name="target"></param>
        /// <returns>RoleType</returns>
        public static async Task<RoleType> GetPermAsync(Bot bot, uint group, uint target)
        {
            return (await bot.GetGroupMemberInfo(group, target)).Role;
        }

        /// <summary>
        /// Get bot in group permission
        /// </summary>
        /// <param name="bot"></param>
        /// <param name="group"></param>
        /// <returns>RoleType</returns>
        public static async Task<RoleType> GetBotPermAsync(Bot bot, uint group)
        {
            return await GetPermAsync(bot, group, bot.Uin);
        }

        /// <summary>
        /// On help
        /// </summary>
        /// <param name="chain"></param>
        /// <returns></returns>
        public static MessageBuilder OnCommandHelp()
            => new MessageBuilder()
                .Text("[基础指令]\n")
                .Text("/帮助   来点帮助!\n")
                .Text("/状态   让我康康你运行正不正常\n")
                .Text("/ping   Pong!\n");

        /// <summary>
        /// On status
        /// </summary>
        /// <param name="chain"></param>
        /// <returns></returns>
        public static MessageBuilder OnCommandStatus()
        {
            string botStatus = "";
            if (Nanako.Program.BotList.Count < 1)
            {
                botStatus += "Bot does not exist!\n";
            }
            else
            {
                foreach (var bot in GetBotList())
                {
                    botStatus += $"[{bot.Name}({bot.Uin})]    {(bot.IsOnline() ? "Online" : "Offline")}\n";
                }
            }


            return new MessageBuilder()
                 .Text($"[{Env.Name}]\n\n")

                 .Text($"[Build Info]\n")
                 .Text($"[branch:{BuildStamp.Branch}]\n")
                 .Text($"[commit:{BuildStamp.CommitHash[..12]}]\n")
                 .Text($"[version:{BuildStamp.Version}]\n")
                 .Text($"[{BuildStamp.BuildTime}]\n\n")
                 // System status
                 .Text($"Processed {GetMessageCounter} message(s)\n")

                 .Text($"[Bot List]\n")
                 .Text($"{botStatus}\n")

                 .Text("Powered By Konata.Core");
        }

        /// <summary>
        /// On ping me
        /// </summary>
        /// <param name="chain"></param>
        /// <returns></returns>
        public static MessageBuilder OnCommandPing()
            => Text("Pong!");

        /// <summary>
        /// Repeat
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static MessageBuilder OnRepeat(MessageChain message)
            => new(message);

        private static MessageBuilder Text(string text)
            => new MessageBuilder().Text(text);
    }
}