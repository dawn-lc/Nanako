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

namespace Nanako;

public class Config
{
    public class BotConfig
    {
        public uint BotId { get; set; }
        public bool Enable { get; set; } = false;
        public Konata.Core.Common.BotConfig? Config { get; set; } = GlobalConfig;
        [JsonIgnore]
        public BotDevice? Device { get; set; }
        [JsonIgnore]
        public BotKeyStore? KeyStore { get; set; }
    }
    public static Konata.Core.Common.BotConfig GlobalConfig { get; set; } = new()
    {
        EnableAudio = true,
        TryReconnect = true,
        HighwayChunkSize = 8192,
    };
    public List<BotConfig> ConfigList { get; set; } = new();
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

    private static async Task<T> DeserializeFile<T>(string path) where T : new()
    {
        T? data;
        return File.Exists(path) ? (data = JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path))) != null ? data : new T() : new T();
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
        Config configData = await DeserializeFile<Config>("config.json");
        try
        {
            foreach (var bot in configData.ConfigList)
            {
                ctx.Status($"加载 Bot {bot.BotId} 配置文件...");
                bot.KeyStore = await DeserializeFile<BotKeyStore>($"Account/{bot.BotId}.json");
                bot.Device = await DeserializeFile<BotDevice>($"Data/{bot.BotId}_device.json");
                bot.Config = await DeserializeFile<BotConfig>($"Data/{bot.BotId}_config.json");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine(ex.ToString());
            Thread.Sleep(10000);
            Environment.Exit(1);
        }
        Config = configData;
        ctx.Status("加载权限表...");
        try
        {
            PermissionTable = await DeserializeFile<Dictionary<uint, List<Permission>>>("Data/permissions.json");
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine(ex.ToString());
            Thread.Sleep(10000);
            Environment.Exit(1);
        }
        ctx.Status("加载插件...");
        PluginFactory.LoadPlugins();
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
            if (!Config.Initialize || !Config.ConfigList.Any())
            {
                AnsiConsole.MarkupLine("[aqua]首次启动, 请输入所有者账号！[/]");
                Config = new Config()
                {
                    Owner = AnsiConsole.Ask<uint>("[green]账号[/]:"),
                    ConfigList = new()
                };
                AnsiConsole.MarkupLine("[aqua]启动框架需要添加首个Bot, 请输入机器人账号以及密码！[/]");
                AnsiConsole.MarkupLine("[yellow]注意![/]该Bot请务必保证已添加所有者账号为好友，否则将无法收到登录验证信息。");
                var account = AnsiConsole.Ask<uint>("[green]账号[/]:");
                var password = AnsiConsole.Prompt(new TextPrompt<string>("[green]密码[/]:").PromptStyle("red").Secret());
                Config.BotConfig botConfig = new()
                {
                    Enable = true,
                    BotId = account,
                    Config = Config.GlobalConfig,
                    Device = BotDevice.Default(),
                    KeyStore = new BotKeyStore(account.ToString(), password)
                };
                Config.ConfigList.Add(botConfig);
                UpdateConfig(botConfig.KeyStore.Account.Uin, botConfig.Config);
                UpdateDevice(botConfig.KeyStore.Account.Uin, botConfig.Device);
                Config.Initialize = true;
                UpdateConfig(Config);
            }
            for (int i = 0; i < Config.ConfigList.Count; i++)
            {
                Config.BotConfig bot = Config.ConfigList[i];
                BotList.Add(BotFather.Create(bot.Config, bot.Device, bot.KeyStore));
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
                    switch (AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("[lime]请选择执行的指令？[/]")
                            .PageSize(8)
                            .MoreChoicesText("[grey](上下方向键选择/翻页)[/]")
                            .AddChoices(new[]
                            {
                                 "添加Bot", "关闭菜单", "退出程序"
                            })
                    ))
                    {
                        case "添加Bot":
                            AnsiConsole.WriteLine(await AddBot() ? "添加成功": "添加失败");
                            break;
                        case "退出程序":
                            if(AnsiConsole.Confirm("确认退出?", false))
                            {
                                BotStop();
                                UpdateConfig(Config);
                                return;
                            }
                            break;
                        case "关闭菜单":
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
            var config = Config.ConfigList.Find(p => p.BotId == bot.KeyStore.Account.Uin);
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
                bot.OnGroupMessage -= OnGroupMessage;

                if (config.Enable)
                {
                    bot.OnCaptcha += OnBotCaptcha;
                    bot.OnBotOnline += OnBotOnline;
                    bot.OnBotOffline += OnBotOffline;
                    bot.OnFriendMessage += OnFriendMessage;
                    bot.OnGroupMessage += OnGroupMessage;
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
        if ((botConfig = Config.ConfigList.Find(p => p.BotId == bot.Uin)) != null)
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
        if ((botConfig = Config.ConfigList.Find(p => p.BotId == bot.Uin)) != null)
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
        if (Config.ConfigList.Any(p => p.BotId == account))
        {
            return false;
        }
        else
        {
            var password = AnsiConsole.Prompt(new TextPrompt<string>("[green]密码[/]:").PromptStyle("red").Secret());
            Config.BotConfig botConfig = new()
            {
                Enable = true,
                BotId = Convert.ToUInt32(account),
                Config = Config.GlobalConfig,
                Device = BotDevice.Default(),
                KeyStore = new BotKeyStore(account.ToString(), password)
            };
            UpdateConfig(botConfig.KeyStore.Account.Uin, botConfig.Config);
            UpdateDevice(botConfig.KeyStore.Account.Uin, botConfig.Device);
            Config.ConfigList.Add(botConfig);
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
    internal static void OnFriendMessage(Bot bot, FriendMessageEvent eventSource)
    {
        if (BotList.Any(p => p.Uin == eventSource.FriendUin)) return;
        if (ChainTable[eventSource.Message.Sequence.ToString()] == null) ChainTable.Add(eventSource.Message.Sequence.ToString(), eventSource.Message);
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
        foreach (var plugin in Plugins)
        {
            plugin.Process(bot, eventSource);
        }
        ++MessageCounter;
    }
    internal static void OnGroupMessage(Bot bot, GroupMessageEvent eventSource)
    {
        if (BotList.Find(p => p.Uin == eventSource.MemberUin) != null) return;
        if (ChainTable[eventSource.Message.Sequence.ToString()] == null) ChainTable.Add(eventSource.Message.Sequence.ToString(), eventSource.Message);

        foreach (var item in eventSource.Chain)
        {
            switch (item)
            {
                case TextChain Chain:
                    AnsiConsole.WriteLine("[{0}({1})]:<{2}({3})>{4}", bot.Name, bot.Uin, eventSource.GroupName, eventSource.GroupUin, Chain.Content.Trim());
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
            plugin.Process(bot, eventSource);
        }
        ++MessageCounter;
    }
}

