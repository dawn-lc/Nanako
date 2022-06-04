using Nanako.Utils;
using Konata.Core;
using Konata.Core.Events.Model;
using Konata.Core.Exceptions.Model;
using Konata.Core.Interfaces.Api;
using Konata.Core.Message;
using Konata.Core.Message.Model;
using Konata.Core.Events;
using System.Diagnostics;
using Konata.Core.Common;
using PuppeteerSharp;
using Spectre.Console;

namespace Nanako.Module;

public static class Command
{
    /// <summary>
    /// On friend message
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="eventSource"></param>
    internal static void OnFriendMessage(Bot bot, FriendMessageEvent eventSource)
    {
        if (Program.BotList.Any(p => p.Uin == eventSource.FriendUin)) return;
        if (Program.ChainTable[eventSource.Message.Sequence.ToString()] == null) Program.ChainTable.Add(eventSource.Message.Sequence.ToString(), eventSource.Message);
        foreach (var item in eventSource.Chain)
        {
            switch (item)
            {
                case TextChain Chain:
                    AnsiConsole.WriteLine("[{0}({1})]:<{2}({3})>{4}", bot.Name, bot.Uin, eventSource.GetType().Name, eventSource.FriendUin, Chain);
                    break;
                case ImageChain Chain:
                    AnsiConsole.WriteLine("[{0}({1})]:<{2}({3})>{4}[{5}]", bot.Name, bot.Uin, eventSource.GetType().Name, eventSource.FriendUin, Chain.FileHash, Chain.FileLength);
                    /*
                    if (!File.Exists($"Cache/Image/{Chain.FileName}"))
                    {
                        Task.Run(() => {
                            using FileStream file = new($"Cache/Image/{Chain.FileName}", FileMode.OpenOrCreate, FileAccess.Write);
                            file.SetLength(0);
                            Http.GetAsync(new(Chain.ImageUrl)).Result.Content.CopyToAsync(file);
                        });
                    }
                    */
                    break;
                default:
                    break;
            }
        }
        if (eventSource.Chain.First() is TextChain text && text != null && text.Content != null && text.Content.Trim() != "" && text.Content.Trim()[0] == '/')
        {
            OnCommand(bot, eventSource);
        }
        if (eventSource.Chain.Any(p => p is ReplyChain reply))
        {
            OnCommand(bot, eventSource);
        }
        ++Program.MessageCounter;
    }


    /// <summary>
    /// On group message
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="group"></param>
    internal static void OnGroupMessage(Bot bot, GroupMessageEvent eventSource)
    {
        if (Program.BotList.Find(p => p.Uin == eventSource.MemberUin) != null) return;
        if (Program.ChainTable[eventSource.Message.Sequence.ToString()] == null) Program.ChainTable.Add(eventSource.Message.Sequence.ToString(), eventSource.Message);
        foreach (var item in eventSource.Chain)
        {
            switch (item)
            {
                case TextChain Chain:
                    AnsiConsole.WriteLine("[{0}({1})]:<{2}({3})>{4}", bot.Name, bot.Uin, eventSource.GroupName, eventSource.GroupUin, Chain);
                    break;
                case ImageChain Chain:
                    AnsiConsole.WriteLine("[{0}({1})]:<{2}({3})>{4}[{5}]", bot.Name, bot.Uin, eventSource.GroupName, eventSource.GroupUin, Chain.FileName, eventSource.Message.Sequence);
                    /*
                    if (!File.Exists($"Cache/Image/{Chain.FileName}"))
                    {
                        Task.Run(async () => {
                            using FileStream file = new($"Cache/Image/{Chain.FileName}", FileMode.OpenOrCreate, FileAccess.Write);
                            file.SetLength(0);
                            await Http.GetAsync(new("https://gchat.qpic.cn" + Chain.ImageUrl)).Result.Content.CopyToAsync(file);
                        });
                    }
                    */
                    break;
                default:
                    break;
            }
        }
        if (eventSource.Chain.First() is TextChain text && text != null && text.Content != null && text.Content.Trim() != "" && text.Content.Trim()[0] == '/')
        {
            OnCommand(bot, eventSource);
        }
        if (eventSource.Chain.Any(p => p is ReplyChain reply) && eventSource.Chain.Any(p => p is AtChain at && at.AtUin == bot.Uin))
        {
            OnCommand(bot, eventSource);
        }
        ++Program.MessageCounter;
    }



    public static async void OnCommand(Bot bot, ProtocolEvent eventSource)
    {
        switch (eventSource)
        {
            case GroupMessageEvent Message:  
                if (Message.Chain.Any(p => p is ReplyChain reply) && Message.Chain.Any(p => p is AtChain at && at.AtUin == bot.Uin) && Message.Chain.Any(p => p is TextChain text && text.Content.Trim() != ""))
                {
                   var Command = Message.Chain.Where(p => p is TextChain text && text.Content.Trim() != "").First() as TextChain;
                   switch (Command.Content.Trim())
                    {
                        case "搜图":
                        case "searchimage":
                            var test = Program.ChainTable[Util.GetArgs(Message.Chain.GetChain<ReplyChain>().ToString())["seq"]] as MessageStruct;
                            if (Message.Chain.GetChain<ReplyChain>() is ReplyChain reply && Program.ChainTable[Util.GetArgs(reply.ToString())["seq"]] is MessageStruct replySource && replySource != null && replySource.Chain.FindChain<ImageChain>().Any() && replySource.Chain.FindChain<ImageChain>().First() is ImageChain imageChain)
                            {
                                bot.SendGroupMessage(Message.GroupUin, Text("开始搜索, 请稍后..."));
                                bot.SendGroupMessage(Message.GroupUin, await ImageSearch.Search(imageChain));
                            }
                            else
                            {
                                bot.SendGroupMessage(Message.GroupUin, Text("没有找到回复的消息记录"));
                            }
                            break;
                        default:
                            bot.SendGroupMessage(Message.GroupUin, Text("未知指令"));
                            break;
                    }
                }
                else if (Message.Chain.First() is TextChain GroupChain)
                {
                    var Command = new Commands<string>(GroupChain.Content.Trim());
                    if (!await bot.SendGroupMessage(Message.GroupUin, Command[0][1..].ToLower() switch
                    {
                        "help" => OnCommandHelp(GroupChain),
                        "ping" => OnCommandPing(GroupChain),
                        "status" => OnCommandStatus(GroupChain),
                        "echo" => OnCommandEcho(GroupChain, Message.Chain),
                        "searchimage" => await ImageSearch.Search(Message.Chain.FindChain<ImageChain>().First()),
                        "搜图" => await ImageSearch.Search(Message.Chain.FindChain<ImageChain>().First()),
                        "eval" => await GetPermAsync(bot, Message.GroupUin, Message.MemberUin) > RoleType.Member ? OnCommandEval(Message.Chain) : Text("No permission to use this command."),
                        "member" => (await bot.GetGroupMemberInfo(Message.GroupUin, Message.MemberUin)).Role > RoleType.Member ? await OnCommandMemberInfo(bot, Message) : Text("No permission to use this command."),
                        "mute" => await GetPermAsync(bot, Message.GroupUin, Message.MemberUin) > RoleType.Member && await GetBotPermAsync(bot, Message.GroupUin) > RoleType.Member ? await OnCommandMuteMember(bot, Message) : Text("No permission to use this command."),
                        "title" => await GetPermAsync(bot, Message.GroupUin, Message.MemberUin) > RoleType.Member && await GetBotPermAsync(bot, Message.GroupUin) > RoleType.Member ? await OnCommandSetTitle(bot, Message) : Text("No permission to use this command."),
                        _ => Text("Unknown command")
                    }))
                    {
                        Console.WriteLine("发送失败! ");
                    }
                }
                break;
            case FriendMessageEvent Message:
                if (Message.Chain.Any(p => p is ReplyChain reply) && Message.Chain.Any(p => p is TextChain text && text.Content.Trim() != ""))
                {
                    var Command = Message.Chain.Where(p => p is TextChain text && text.Content.Trim() != "").First() as TextChain;
                    switch (Command.Content.Trim())
                    {
                        case "搜图":
                        case "searchimage":
                            try
                            {
                                if (Program.ChainTable[Util.GetArgs(Message.Chain.FindChain<ReplyChain>().First().ToString())["seq"]] is MessageStruct reply && reply != null && reply.Chain.FindChain<ImageChain>().Any() && reply.Chain.FindChain<ImageChain>().First() is ImageChain imageChain)
                                {
                                    bot.SendFriendMessage(Message.FriendUin, Text("开始搜索, 请稍后..."));
                                    bot.SendFriendMessage(Message.FriendUin, await ImageSearch.Search(imageChain));
                                }
                                else
                                {
                                    bot.SendFriendMessage(Message.FriendUin, Text("没有找到回复的消息记录"));
                                }
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("没有找到回复的消息记录");
                            }
                            break;
                        default:
                            bot.SendFriendMessage(Message.FriendUin, Text("未知指令"));
                            break;
                    }
                }
                else if (Message.Chain.First() is TextChain FriendChain)
                {
                    var Command = new Commands<string>(FriendChain.Content.Trim());
                    if (!await bot.SendFriendMessage(Message.FriendUin, Command[0][1..].ToLower() switch
                    {
                        "help" => OnCommandHelp(FriendChain),
                        "ping" => OnCommandPing(FriendChain),
                        "status" => OnCommandStatus(FriendChain),
                        "echo" => OnCommandEcho(FriendChain, Message.Chain),
                        "searchimage" => await ImageSearch.Search(Message.Chain.FindChain<ImageChain>().First()),
                        "搜图" => await ImageSearch.Search(Message.Chain.FindChain<ImageChain>().First()),
                        "addbot" => Message.FriendUin == Program.Config.Owner ? await OnCommandAddBot(Command[1], Command[2]) : Text("No permission to use this command."),
                        "captcha" => Message.FriendUin == Program.Config.Owner ? OnCommandCaptcha(Command[1], Command[2], Command[3]) : Text("No permission to use this command."),
                        "startcaptcha" => Message.FriendUin == Program.Config.Owner ? await OnCommandStartCaptchaAsync(Command[1], Command[2]) : Text("No permission to use this command."),
                        "enablebot" => Message.FriendUin == Program.Config.Owner ? OnCommandEnableBot(Command[1]) : Text("No permission to use this command."),
                        "disablebot" => Message.FriendUin == Program.Config.Owner ? OnCommandDisableBot(Command[1]) : Text("No permission to use this command."),
                        _ => Text("Unknown command")
                    }))
                    {
                        Console.WriteLine("发送失败! ");
                    }
                }
                break;
            default:
                break;
        }
    }

    private static async Task<MessageBuilder> OnCommandStartCaptchaAsync(string Type, string Bot)
    {
        Bot? MainBot = Program.BotList.Find(p => p.IsOnline());
        var (bot, eventSource) = Program.NeedCaptchaBotList.Find(p => p.bot.Uin.ToString() == Bot);
        Bot? OnCaptchaBot = bot;
        CaptchaEvent? OnCaptchaBotCaptcha = eventSource;
        if (OnCaptchaBot != null)
        {
            switch (Type)
            {
                case "Auto":
                    try
                    {
                        await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
                    }
                    catch (Exception)
                    {
                        return new MessageBuilder().Text("下载用于进行滑块验证的浏览器时出现问题, 请确认您是否能够访问https://storage.googleapis.com");
                    }
                    var browser = await Puppeteer.LaunchAsync(new LaunchOptions()
                    {
                        Headless = false,
                        DefaultViewport = new ViewPortOptions(),
                        Args = new string[] {
                                "--disable-features=site-per-process",
                                "--window-size=300,550",
                                eventSource.SliderUrl
                            }
                    });
                    (await browser.PagesAsync())[0].Response += async (page, e) =>
                    {
                        if (e.Response.Url.Contains("cap_union_new_verify"))
                        {
                            var Response = await e.Response.JsonAsync();
                            if (Response.Value<int>("errorCode") == 0)
                            {
                                if (bot.SubmitSliderTicket(Response.Value<string>("ticket")))
                                {
                                    await MainBot.SendFriendMessage(Program.Config.Owner, new MessageBuilder().Text($"{bot.Uin} 验证成功\n"));
                                }
                                else
                                {
                                    await MainBot.SendFriendMessage(Program.Config.Owner, new MessageBuilder().Text($"{bot.Uin} 验证失败\n"));
                                }
                                await browser.CloseAsync();
                            }
                        }
                    };
                    return new MessageBuilder().Text("请在打开的窗口内进行验证");
                case "Ticket":
                    return new MessageBuilder()
                        .Text($"浏览器打开链接:{eventSource.SliderUrl}\n")
                        .Text($"在验证前请打开浏览器开发者工具, 验证完成后, 找到尾部为\"cap_union_new_verify\"的网络请求.\n")
                        .Text($"点击请求, 在响应面板中找到名为\"ticket\"的值, 完整复制并粘贴到下面的命令中.")
                        .Text($"发送 /captcha Slider {bot.Uin} 获取到的值 进行验证.");
                default:
                    return new MessageBuilder().Text("未知的验证码类型");
            }
        }
        return new MessageBuilder().Text("没有找到这个Bot");
    }
    private static MessageBuilder OnCommandCaptcha(string Type, string Bot, string Captcha)
    {
        Bot? OnCaptchaBot = Program.BotList.Find(b => !b.IsOnline() && b.Uin.ToString() == Bot);
        if (OnCaptchaBot != null)
        {
            return Type switch
            {
                "SMS" => OnCaptchaBot.SubmitSmsCode(Captcha) ? new MessageBuilder().Text("短信验证成功") : new MessageBuilder().Text("短信验证失败"),
                "Slider" => OnCaptchaBot.SubmitSliderTicket(Captcha) ? new MessageBuilder().Text("滑块验证成功") : new MessageBuilder().Text("滑块验证失败"),
                _ => new MessageBuilder().Text("未知的验证码类型"),
            };
        }
        return new MessageBuilder().Text("没有找到这个Bot");
    }

    public static MessageBuilder OnCommandEnableBot(string bot)
    {
        Program.EnableBot(Program.BotList.Find(p => p.Uin.ToString() == bot));
        return new MessageBuilder().Text("OK");
    }
    public static MessageBuilder OnCommandDisableBot(string bot)
    {
        Program.DisableBot(Program.BotList.Find(p => p.Uin.ToString() == bot));
        return new MessageBuilder().Text("OK");
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
    /// On addbot
    /// </summary>
    /// <param name="chain"></param>
    /// <returns></returns>
    public static async Task<MessageBuilder> OnCommandAddBot(string account, string password) => Text(await Program.AddBotAsync(account, password));
    /// <summary>
    /// On help
    /// </summary>
    /// <param name="chain"></param>
    /// <returns></returns>
    public static MessageBuilder OnCommandHelp(TextChain chain)
        => new MessageBuilder()
            .Text("[Help]\n")
            .Text("/help   Print this bot help\n")
            .Text("/ping   Pong!\n")
            .Text("/status   Show bot status\n")
            .Text("/echo   Send a message");

   
    /// <summary>
    /// On status
    /// </summary>
    /// <param name="chain"></param>
    /// <returns></returns>
    public static MessageBuilder OnCommandStatus(TextChain chain)
    {
        string botStatus = "";
        if (Program.BotList.Count < 1)
        {
            botStatus += "Bot does not exist!\n";
        }
        else
        {
            foreach (var bot in Program.BotList)
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
            .Text($"Processed {Program.MessageCounter} message(s)\n")
            .Text($"GC Memory {GC.GetTotalAllocatedBytes().Bytes2MiB(2)} MiB " +
                  $"({Math.Round((double)GC.GetTotalAllocatedBytes() / GC.GetTotalMemory(false) * 100, 2)}%)\n")
            .Text($"Total Memory {Process.GetCurrentProcess().WorkingSet64.Bytes2MiB(2)} MiB\n\n")

            .Text($"[Bot List]\n")
            .Text($"{botStatus}\n")

            .Text("Powered By Konata.Core");
    }

    /// <summary>
    /// On ping me
    /// </summary>
    /// <param name="chain"></param>
    /// <returns></returns>
    public static MessageBuilder OnCommandPing(TextChain chain)
        => Text("Pong!");

    /// <summary>
    /// On message echo <br/>
    /// <b>Safer than MessageBuilder.Eval()</b>
    /// </summary>
    /// <param name="text"></param>
    /// <param name="chain"></param>
    /// <returns></returns>
    public static MessageBuilder OnCommandEcho(TextChain text, MessageChain chain)
        => new MessageBuilder(text.Content[5..].Trim()).Add(chain[1..]);

    /// <summary>
    /// On message eval
    /// </summary>
    /// <param name="chain"></param>
    /// <returns></returns>
    public static MessageBuilder OnCommandEval(MessageChain chain)
        => MessageBuilder.Eval(chain.ToString()[5..].TrimStart());

    /// <summary>
    /// On member info
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="group"></param>
    /// <returns></returns>
    public static async Task<MessageBuilder> OnCommandMemberInfo(Bot bot, GroupMessageEvent group)
    {
        // Get at
        var at = group.Chain.GetChain<AtChain>();
        if (at is null) return Text("Argument error");

        // Get group info
        var memberInfo = await bot.GetGroupMemberInfo(group.GroupUin, at.AtUin, true);
        if (memberInfo is null) return Text("No such member");

        return new MessageBuilder("[Member Info]\n")
            .Text($"Name: {memberInfo.Name}\n")
            .Text($"Join: {memberInfo.JoinTime}\n")
            .Text($"Role: {memberInfo.Role}\n")
            .Text($"Level: {memberInfo.Level}\n")
            .Text($"SpecTitle: {memberInfo.SpecialTitle}\n")
            .Text($"Nickname: {memberInfo.NickName}");
    }

    /// <summary>
    /// On mute
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="group"></param>
    /// <returns></returns>
    public static async Task<MessageBuilder> OnCommandMuteMember(Bot bot, GroupMessageEvent group)
    {
        // Get at
        var atChain = group.Chain.GetChain<AtChain>();
        if (atChain is null) return Text("Argument error");

        var time = 60U;
        var textChains = group.Chain
            .FindChain<TextChain>();
        {
            // Parse time
            if (textChains.Count is 2 &&
                uint.TryParse(textChains[1].Content, out var t))
            {
                time = t;
            }
        }

        try
        {
            if (await bot.GroupMuteMember(group.GroupUin, atChain.AtUin, time))
                return Text($"Mute member [{atChain.AtUin}] for {time} sec.");
            return Text("Unknown error.");
        }
        catch (OperationFailedException e)
        {
            return Text($"{e.Message} ({e.HResult})");
        }
    }

    /// <summary>
    /// Set title
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="group"></param>
    /// <returns></returns>
    public static async Task<MessageBuilder> OnCommandSetTitle(Bot bot, GroupMessageEvent group)
    {
        // Get at
        var atChain = group.Chain.GetChain<AtChain>();
        if (atChain is null) return Text("Argument error");

        var textChains = group.Chain
            .FindChain<TextChain>();
        {
            // Check argument
            if (textChains.Count is not 2) return Text("Argument error");

            try
            {
                if (await bot.GroupSetSpecialTitle(group.GroupUin, atChain.AtUin, textChains[1].Content, uint.MaxValue))
                    return Text($"Set special title for member [{atChain.AtUin}].");
                return Text("Unknown error.");
            }
            catch (OperationFailedException e)
            {
                return Text($"{e.Message} ({e.HResult})");
            }
        }
    }




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