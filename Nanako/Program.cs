using System.Collections;
using Sentry;
using Spectre.Console;
using Konata.Core;
using Konata.Core.Common;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces;
using Konata.Core.Interfaces.Api;
using Konata.Core.Message;
using Nanako.Module;
using System.Text.Json.Serialization;
using System.Text.Json;
using Konata.Core.Message.Model;
using Nanako.Utils;
using PuppeteerSharp;
using System.Collections.Generic;

namespace Nanako;

public class Config
{
    public class BotConfig
    {
        public bool Enable { get; set; } = false;
        public Konata.Core.Common.BotConfig Config { get; set; } = new()
        {
            EnableAudio = true,
            TryReconnect = true,
            HighwayChunkSize = 8192,
        };
        [JsonIgnore]
        public BotDevice? Device { get; set; }
        [JsonIgnore]
        public BotKeyStore? KeyStore { get; set; }
    }
    public class PluginConfig
    {
        public bool Enable { get; set; } = false;
        public int Index { get; set; } = 0;
    }
    public Dictionary<uint, BotConfig> ConfigTable { get; set; } = new();
    public Dictionary<string, PluginConfig> PluginConfigTable { get; set; } = new();
    public uint Owner { get; set; }
    public bool Initialize { get; set; } = false;
}

public static class Program
{
    public static Config Config { get; set; } = new();
    public static List<Bot> BotList { get; set; } = new List<Bot>();
    public static Dictionary<uint, List<Permission>> PermissionTable { get; set; } = new();
    public static List<Plugin> Plugins { get; set; } = new();
    public static Hashtable ChainTable { get; set; } = Hashtable.Synchronized(new Hashtable());
    public static uint MessageCounter { get; set; } = 0;

    private static async Task<T> DeserializeJSONFile<T>(string path) where T : new()
    {
        T? data;
        return File.Exists(path) ? (data = JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path))) != null ? data : new T() : new T();
    }
    private static async Task<T> DeserializeXMLFile<T>(string path) where T : new()
    {
        T? data;
        return File.Exists(path) ? (data = XML.Deserialize<T>(await File.ReadAllTextAsync(path))) != null ? data : new T() : new T();
    }

    /// <summary>
    /// Update KeyStore
    /// </summary>
    private static void UpdateKeyStore(Bot bot)
    {
        File.WriteAllText($"Account/{bot.Uin}.json", JsonSerializer.Serialize(bot.KeyStore));
    }
    /// <summary>
    /// Update Config 
    /// </summary>
    private static void UpdateConfig(Config config)
    {
        File.WriteAllText($"config.json", JsonSerializer.Serialize(config));
    }
    /// <summary>
    /// Update Permissions 
    /// </summary>
    private static void UpdatePermissions()
    {
        List<PermissionItem> t = new();
        foreach (var item in PermissionTable)
        {
            t.Add(new() { User = item.Key, Permissions = item.Value });
        }
        File.WriteAllText($"Data/permissions.xml", XML.Serialize(t));
    }
    /// <summary>
    /// Update Config 
    /// </summary>
    private static void UpdateConfig(uint bot, BotConfig botConfig)
    {
        File.WriteAllText($"Data/{bot}_config.json", JsonSerializer.Serialize(botConfig));
    }
    /// <summary>
    /// Update Device
    /// </summary>
    private static void UpdateDevice(uint bot, BotDevice botDevice)
    {
        File.WriteAllText($"Data/{bot}_device.json", JsonSerializer.Serialize(botDevice));
    }
    private static async Task Initialization(StatusContext ctx)
    {
        ctx.Status("检查工作目录...");
        if (!Directory.Exists("Account")) Directory.CreateDirectory("Account");
        if (!Directory.Exists("Plugins")) Directory.CreateDirectory("Plugins");
        if (!Directory.Exists("Data")) Directory.CreateDirectory("Data");
        if (!Directory.Exists("Cache")) Directory.CreateDirectory("Cache");
        if (!Directory.Exists("Cache/Image")) Directory.CreateDirectory("Cache/Image");
        ctx.Status("加载配置文件...");
        Config configData = await DeserializeJSONFile<Config>("config.json");
        try
        {
            foreach (var botConfig in configData.ConfigTable)
            {
                ctx.Status($"加载 Bot {botConfig.Key} 配置文件...");
                botConfig.Value.KeyStore = await DeserializeJSONFile<BotKeyStore>($"Account/{botConfig.Key}.json");
                botConfig.Value.Device = await DeserializeJSONFile<BotDevice>($"Data/{botConfig.Key}_device.json");
                botConfig.Value.Config = await DeserializeJSONFile<BotConfig>($"Data/{botConfig.Key}_config.json");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine(ex.ToString());
            Thread.Sleep(10000);
            Environment.Exit(1);
        }
        Config = configData;
        ctx.Status("加载插件...");
        PluginFactory.LoadPlugins();
        List<Plugin> OrderPlugins = new ();
        foreach (var item in Config.PluginConfigTable.OrderBy(c => c.Value.Index))
        {
            if (item.Value.Enable)
            {
                Plugin? plugin;
                if ((plugin = Plugins.Find(p => p.Name == item.Key)) != null)
                {
                    OrderPlugins.Add(plugin);
                }
            }
            else
            {
                AnsiConsole.WriteLine($"插件 {item.Key} 已禁用，如需启用请退出后修改配置文件。");
            }
        }
        Plugins = OrderPlugins;
        ctx.Status("加载权限表...");
        try
        {
            List<PermissionItem> t = await DeserializeXMLFile<List<PermissionItem>>("Data/permissions.xml");
            foreach (var item in t)
            {
                PermissionTable.Add(item.User, item.Permissions);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine(ex.ToString());
            Thread.Sleep(10000);
            Environment.Exit(1);
        }
    }
    static async Task Main(string[] args)
    {
        using (SentrySdk.Init(o =>
        {
            o.Dsn = "https://ab8d4959bf0440e1be8c1945a7ed8a28@o687854.ingest.sentry.io/6427927";
            o.TracesSampleRate = 1.0;
        }))
        {
            await AnsiConsole.Status()
                .StartAsync("初始化...", async ctx => {
                    await Initialization(ctx);
                });
            AnsiConsole.Cursor.Hide();
            AnsiConsole.MarkupLine("[aqua]Tips：按Tab键选择指令[/]");
            if (!Config.Initialize || !Config.ConfigTable.Any())
            {
                AnsiConsole.MarkupLine("[aqua]首次启动, 请输入所有者账号！[/]");
                Config = new Config()
                {
                    Owner = AnsiConsole.Ask<uint>("[green]账号[/]:"),
                    ConfigTable = new()
                };
                PermissionTable.Add(Config.Owner, new() { new Permissions.Root() });
                UpdatePermissions();
                AnsiConsole.MarkupLine("[aqua]启动框架需要添加首个Bot, 请输入机器人账号以及密码！[/]");
                AnsiConsole.MarkupLine("[yellow]注意![/]该Bot请务必保证已添加所有者账号为好友，否则将无法收到重要消息。");
                var account = AnsiConsole.Ask<uint>("[green]账号[/]:");
                var password = AnsiConsole.Prompt(new TextPrompt<string>("[green]密码[/]:").PromptStyle("red").Secret());
                Config.BotConfig botConfig = new()
                {
                    Enable = true,
                    Device = BotDevice.Default(),
                    KeyStore = new BotKeyStore(account.ToString(), password)
                };
                Config.ConfigTable.Add(account, botConfig);
                UpdateConfig(botConfig.KeyStore.Account.Uin, botConfig.Config);
                UpdateDevice(botConfig.KeyStore.Account.Uin, botConfig.Device);
                UpdatePermissions();
                Config.Initialize = true;
                UpdateConfig(Config);
            }
            if (!PermissionTable.Any())
            {
                PermissionTable.Add(Config.Owner, new() { new Permissions.Root() });
            }
            foreach (var botConfig in Config.ConfigTable)
            {
                BotList.Add(BotFather.Create(botConfig.Value.Config, botConfig.Value.Device, botConfig.Value.KeyStore));
            }
            for (int i = 0; i < BotList.Count; i++)
            {
                Bot bot = BotList[i];
                await Autologin(bot);
            }
            while (true)
            {
                AnsiConsole.Cursor.Hide();
                if (Console.ReadKey().Key == ConsoleKey.Tab)
                {
                    switch (Prompt("[lime]请选择执行的指令？[/]", new[] { "添加Bot", "禁用Bot", "启用Bot", "关闭菜单", "退出程序" }))
                    {
                        case "添加Bot":
                            AnsiConsole.WriteLine(await AddBot() ? "添加成功": "添加失败");
                            break;
                        case "启用Bot":
                            await EnableBot(Prompt("请选择Bot:", Config.ConfigTable.Keys.ToArray()));
                            break;
                        case "禁用Bot":
                            await DisableBot(Prompt("请选择Bot:", Config.ConfigTable.Keys.ToArray()));
                            break;
                        case "关闭菜单":
                            break;
                        case "退出程序":
                            if (AnsiConsole.Confirm("确认退出?", false))
                            {
                                BotStop();
                                UpdatePermissions();
                                UpdateConfig(Config);
                                return;
                            }
                            break;
                        default:
                            AnsiConsole.MarkupLine("[red]未知命令![/]");
                            break;
                    }
                }
            }
        }
    }
    public static void BotStop()
    {
        foreach (var bot in BotList)
        {
            bot.Logout();
            bot.Dispose();
        }
    }

    public static T Prompt<T>(string title, T[] choices) where T : notnull => AnsiConsole.Prompt(new SelectionPrompt<T>().Title(title).AddChoices(choices));


    public static MessageStruct? GetHistoryMessage(ReplyChain replyChain)
    {
        return GetHistoryMessage(Util.GetArgs(replyChain.ToString())["seq"]);
    }
    public static MessageStruct? GetHistoryMessage(string Sequence)
    {
        return ChainTable[Sequence] as MessageStruct;
    }
    public static MessageStruct? GetHistoryMessage(uint Sequence)
    {
        return ChainTable[Sequence] as MessageStruct;
    }
    public static void OnBotOnline(Bot bot, BotOnlineEvent eventSource)
    {
        AnsiConsole.WriteLine($"[{bot.Name}({bot.Uin})]:<上线>");
    }
    public static void OnBotOffline(Bot bot, BotOfflineEvent eventSource)
    {
        AnsiConsole.WriteLine($"[{bot.Name}({bot.Uin})]:<下线>");
    }
    /// <summary>
    /// Auto login
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    private static async Task Autologin(Bot? bot)
    {
        if (bot != null)
        {
            var config = Config.ConfigTable[bot.KeyStore.Account.Uin];
            if (config == null)
            {
                await bot.Logout();
                bot.Dispose();
                BotList.Remove(bot);
                AnsiConsole.MarkupLine("[aqua]Bot配置文件错误！请重新添加该Bot。[/]");
            }
            else
            {
                bot.OnCaptcha -= OnBotCaptcha;
                bot.OnBotOnline -= OnBotOnline;
                bot.OnBotOffline -= OnBotOffline;
                bot.OnFriendMessage -= OnFriendMessage;
                bot.OnGroupMessage -= OnGroupMessageAsync;

                if (config.Enable)
                {
                    bot.OnCaptcha += OnBotCaptcha;
                    bot.OnBotOnline += OnBotOnline;
                    bot.OnBotOffline += OnBotOffline;
                    bot.OnFriendMessage += OnFriendMessage;
                    bot.OnGroupMessage += OnGroupMessageAsync;
                    if (await bot.Login())
                    {
                        config.KeyStore = bot.KeyStore;
                        UpdateKeyStore(bot);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[aqua]Bot 登录失败，请重新输入密码！[/]");
                        config.KeyStore = new BotKeyStore(bot.KeyStore.Account.Uin.ToString(), AnsiConsole.Prompt(new TextPrompt<string>("[green]密码[/]:").PromptStyle("red").Secret()));
                        BotList.Remove(bot);
                        BotList.Add(BotFather.Create(config.Config, config.Device, config.KeyStore));
                        await Autologin(BotList.Find(p => p.Uin == config.KeyStore.Account.Uin));
                    }
                }
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[aqua]Bot 登录失败，Bot不存在！[/]");
        }
    }
    public static async Task<bool> EnableBot(uint bot)
    {
        return await EnableBot(BotList.Find(p => p.Uin == bot));
    }
    public static async Task<bool> EnableBot(Bot? bot)
    {
        if (bot == null) return false;
        Config.BotConfig? botConfig;
        if ((botConfig = Config.ConfigTable[bot.Uin]) != null)
        {
            botConfig.Enable = true;
        }
        UpdateConfig(Config);
        await Autologin(bot);
        return true;
    }
    public static async Task<bool> DisableBot(uint bot)
    {
        return await DisableBot(BotList.Find(p => p.Uin == bot));
    }
    public static async Task<bool> DisableBot(Bot? bot)
    {
        if (bot == null) return false;
        Config.BotConfig? botConfig;
        if ((botConfig = Config.ConfigTable[bot.Uin]) != null)
        {
            botConfig.Enable = false;
        }
        UpdateConfig(Config);
        await bot.Logout();
        return true;
    }
    public static async Task<bool> AddBot()
    {
        var account = AnsiConsole.Ask<uint>("[green]账号[/]:");
        if (Config.ConfigTable.Any(p => p.Key == account))
        {
            return false;
        }
        else
        {
            var password = AnsiConsole.Prompt(new TextPrompt<string>("[green]密码[/]:").PromptStyle("red").Secret());
            Config.BotConfig botConfig = new()
            {
                Enable = true,
                Device = BotDevice.Default(),
                KeyStore = new BotKeyStore(account.ToString(), password)
            };
            UpdateConfig(botConfig.KeyStore.Account.Uin, botConfig.Config);
            UpdateDevice(botConfig.KeyStore.Account.Uin, botConfig.Device);
            Config.ConfigTable.Add(account, botConfig);
            UpdateConfig(Config);
            BotList.Add(BotFather.Create(botConfig.Config, botConfig.Device, botConfig.KeyStore));
            await Autologin(BotList.Find(p => p.Uin == account));
            return true;
        }
    }
    public static async void OnBotCaptcha(Bot bot, CaptchaEvent eventSource)
    {
        switch (eventSource.Type)
        {
            case CaptchaEvent.CaptchaType.Sms:
                AnsiConsole.WriteLine(bot.SubmitSmsCode(AnsiConsole.Ask<string>("[green]短信验证码[/]:")) ? "短信验证成功" : "短信验证失败");
                break;
            case CaptchaEvent.CaptchaType.Slider:
                AnsiConsole.WriteLine($"[{bot.Uin}] 需要进行滑块验证!");
                await SliderCaptcha(bot, eventSource);
                break;
            default:
            case CaptchaEvent.CaptchaType.Unknown:
                AnsiConsole.WriteLine($"[{bot.Uin}] 遇到了无法处理的验证模式!");
                AnsiConsole.WriteLine($"请请等待后续功能更新。");
                break;
        }
    }
    private static async Task SliderCaptcha(Bot OnCaptchaBot, CaptchaEvent OnCaptchaBotCaptcha)
    {
        AnsiConsole.WriteLine("请在打开的窗口内进行验证");
        try
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
        }
        catch (Exception)
        {
            AnsiConsole.WriteLine("下载用于进行滑块验证的浏览器时出现问题, 请确认您是否能够访问https://storage.googleapis.com");
            return;
        }
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions()
        {
            Headless = false,
            DefaultViewport = new ViewPortOptions(),
            Args = new string[] { "--disable-features=site-per-process", "--window-size=300,550", OnCaptchaBotCaptcha.SliderUrl }
        });
        (await browser.PagesAsync())[0].Response += async (page, e) =>
        {
            if (e.Response.Url.Contains("cap_union_new_verify"))
            {
                var Response = await e.Response.JsonAsync();
                if (Response.Value<int>("errorCode") == 0)
                {
                    if (OnCaptchaBot.SubmitSliderTicket(Response.Value<string>("ticket")))
                    {
                        AnsiConsole.WriteLine("验证成功");
                    }
                    else
                    {
                        AnsiConsole.WriteLine("验证失败");
                    }
                    await browser.CloseAsync();
                }
            }
        };
    }
    internal static async void OnFriendMessage(Bot bot, FriendMessageEvent eventSource)
    {
        if (BotList.Any(p => p.Uin == eventSource.FriendUin)) return;
        if (ChainTable[eventSource.Message.Sequence.ToString()] == null) ChainTable.Add(eventSource.Message.Sequence.ToString(), eventSource.Message);
        foreach (var item in eventSource.Chain)
        {
            switch (item)
            {
                case TextChain Chain:
                    if (Chain.Content.Trim().Length > 0)
                    {
                        AnsiConsole.WriteLine("[{0}({1})]:<{2}({3})>{4}", bot.Name, bot.Uin, eventSource.GetType().Name, eventSource.FriendUin, Chain.Content);
                    }
                    break;
                case ImageChain Chain:
                    AnsiConsole.WriteLine("[{0}({1})]:<{2}({3})>{4}[{5}]", bot.Name, bot.Uin, eventSource.GetType().Name, eventSource.FriendUin, Chain.FileHash, Chain.FileLength);
                    break;
                default:
                    break;
            }
        }
        foreach (var plugin in Plugins)
        {
            // 返回false代表插件要求后续插件不再处理该消息
            if (!await plugin.Process(bot, eventSource)) return;
        }
        ++MessageCounter;
    }
    internal static async void OnGroupMessageAsync(Bot bot, GroupMessageEvent eventSource)
    {
        if (BotList.Find(p => p.Uin == eventSource.MemberUin) != null) return;
        if (ChainTable[eventSource.Message.Sequence.ToString()] == null) ChainTable.Add(eventSource.Message.Sequence.ToString(), eventSource.Message);

        foreach (var item in eventSource.Chain)
        {
            switch (item)
            {
                case TextChain Chain:
                    if (Chain.Content.Trim().Length > 0)
                    {
                        AnsiConsole.WriteLine("[{0}({1})]:<{2}({3})>{4}", bot.Name, bot.Uin, eventSource.GroupName, eventSource.GroupUin, Chain.Content);
                    }
                    break;
                case ImageChain Chain:
                    AnsiConsole.WriteLine("[{0}({1})]:<{2}({3})>{4}[{5}]", bot.Name, bot.Uin, eventSource.GroupName, eventSource.GroupUin, Chain.FileName, eventSource.Message.Sequence);
                    break;
                default:
                    break;
            }
        }
        foreach (var plugin in Plugins)
        {
            if (!await plugin.Process(bot, eventSource)) return;
        }
        ++MessageCounter;
    }
}

