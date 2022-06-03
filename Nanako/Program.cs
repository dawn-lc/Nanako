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
using Nanako.Utils;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Nanako;

public class Config
{
    public class BotConfig
    {
        public uint BotId { get; set; }
        public bool Enable { get; set; }
        public Konata.Core.Common.BotConfig? Config = GlobalConfig;
        [JsonIgnore]
        public BotDevice? Device { get; set; }
        [JsonIgnore]
        public BotKeyStore? KeyStore { get; set; }
    }
    public static Konata.Core.Common.BotConfig GlobalConfig = new()
    {
        EnableAudio = true,
        TryReconnect = true,
        HighwayChunkSize = 8192,
    };
    public List<BotConfig> ConfigList { get; set; }
    public uint Owner { get; set; }
    public bool Initialize { get; set; }
    public Config()
    {
        ConfigList = new();
        Initialize = false;
    }
}

public static class Program
{
    public static Config Config;
    public static List<Bot> BotList = new();
    public static Hashtable ChainTable = Hashtable.Synchronized(new Hashtable());
    public static uint messageCounter = 0;
    public static List<(Bot bot, CaptchaEvent eventSource)> NeedCaptchaBotList = new();

    private static async Task<T> DeserializeFile<T>(string path) where T : new()
    {
        T? data;
        return File.Exists(path) ? (data = JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path))) != null ? data : new T() : new T();
    }


    /// <summary>
    /// Update keystore
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
                AnsiConsole.MarkupLine("Bot配置文件错误！请重新添加该Bot。");
            }
            else
            {
                bot.OnCaptcha -= BotCaptcha;
                bot.OnBotOnline -= BotOnline;
                bot.OnBotOffline -= BotOffline;
                bot.OnFriendMessage -= Command.OnFriendMessage;
                bot.OnGroupMessage -= Command.OnGroupMessage;

                if (config.Enable)
                {
                    bot.OnCaptcha += BotCaptcha;
                    bot.OnBotOnline += BotOnline;
                    bot.OnBotOffline += BotOffline;
                    bot.OnFriendMessage += Command.OnFriendMessage;
                    bot.OnGroupMessage += Command.OnGroupMessage;
                    if (await bot.Login())
                    {
                        config.KeyStore = bot.KeyStore;
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("Bot 登录失败，请重新输入密码！");
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
            AnsiConsole.MarkupLine("Bot 登录失败，Bot不存在！");
        }
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
    public static void EnableBot(Bot bot)
    {
        Config.BotConfig? botConfig;
        if ((botConfig = Config.ConfigList.Find(p => p.BotId == bot.Uin)) != null)
        {
            botConfig.Enable = true;
        }
        UpdateConfig(Config);
        Autologin(bot);
    }
    public static void DisableBot(Bot bot)
    {
        Config.BotConfig? botConfig;
        if ((botConfig = Config.ConfigList.Find(p => p.BotId == bot.Uin)) != null)
        {
            botConfig.Enable = false;
        }
        UpdateConfig(Config);
        bot.Logout();
    }
    public static async Task<string> AddBotAsync(string account, string password)
    {
        if (Config.ConfigList.Any(p => p.BotId.ToString() == account))
        {
            return $"Bot {account} already exists!";
        }
        else
        {
            Config.BotConfig botConfig = new()
            {
                Enable = true,
                BotId = Convert.ToUInt32(account),
                Config = Config.GlobalConfig,
                Device = BotDevice.Default(),
                KeyStore = new BotKeyStore(account, password)
            };
            UpdateConfig(botConfig.KeyStore.Account.Uin, botConfig.Config);
            UpdateDevice(botConfig.KeyStore.Account.Uin, botConfig.Device);
            Config.ConfigList.Add(botConfig);
            UpdateConfig(Config);
            BotList.Add(BotFather.Create(botConfig.Config, botConfig.Device, botConfig.KeyStore));
            await Autologin(BotList.Find(p => p.Uin.ToString() == account));
            return "Add bot complete.";
        }
    }
    private static async Task<Config> Initialization(StatusContext ctx)
    {
        ctx.Status("检查工作目录...");
        if (!Directory.Exists("Account")) Directory.CreateDirectory("Account");
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
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine(ex.ToString());
            Thread.Sleep(10000);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(ex.ToString());
            Thread.Sleep(10000);
            Environment.Exit(1);
        }
        return configData;
    }
    static async Task Main(string[] args)
    {
        using (SentrySdk.Init(o =>
        {
            o.Dsn = "https://ab8d4959bf0440e1be8c1945a7ed8a28@o687854.ingest.sentry.io/6427927";
            o.TracesSampleRate = 1.0;
        }))
        {
            await AnsiConsole.Status().StartAsync("初始化...", async ctx =>
            {
                Config = await Initialization(ctx);
            });
            if (!Config.Initialize || !Config.ConfigList.Any())
            {
                AnsiConsole.MarkupLine("首次启动, 请输入所有者账号！");
                Config = new Config()
                {
                    Owner = AnsiConsole.Ask<uint>("[green]账号[/]:"),
                    ConfigList = new()
                };
                AnsiConsole.MarkupLine("启动框架需要添加首个Bot, 请输入机器人账号以及密码！");
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
            foreach (var bot in Config.ConfigList.Where(bot => bot.Config != null && bot.Device != null && bot.KeyStore != null))
            {
                BotList.Add(BotFather.Create(bot.Config, bot.Device, bot.KeyStore));
            }
            foreach (Bot bot in BotList)
            {
                await Autologin(bot);
            }
            while (true)
            {
                string? input = Console.ReadLine()?.Trim();
                if (input != null && input.Length > 0)
                {
                    var Command = new Commands<string>(input);
                    switch (Command[0][1..])
                    {
                        case "stop":
                            BotStop();
                            UpdateConfig(Config);
                            return;
                        default:
                            AnsiConsole.MarkupLine("未知命令!");
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
    public static void BotOnline(Bot bot, BotOnlineEvent eventSource)
    {
        Console.WriteLine("[{0}({1})]:<{2}>", bot.Name, bot.Uin, eventSource.GetType().Name);
    }
    public static void BotOffline(Bot bot, BotOfflineEvent eventSource)
    {
        Console.WriteLine("[{0}({1})]:<{2}>", bot.Name, bot.Uin, eventSource.GetType().Name);
    }
    public static async void BotCaptcha(Bot bot, CaptchaEvent eventSource)
    {
        Bot? MainBot = BotList.Find(p => p.IsOnline());
        switch (eventSource.Type)
        {
            case CaptchaEvent.CaptchaType.Sms:
                NeedCaptchaBotList.Add((bot, eventSource));
                await MainBot.SendFriendMessage(Config.Owner,
                    new MessageBuilder()
                    .Text($"[{bot.Uin}] 需要进行短信验证!\n")
                    .Text($"绑定手机号为: {eventSource.Phone} 请检查是否收到验证码!\n")
                    .Text($"收到验证码后请发送 /captcha SMS {bot.Uin} 验证码 进行验证.")
                );
                break;
            case CaptchaEvent.CaptchaType.Slider:
                NeedCaptchaBotList.Add((bot, eventSource));
                await MainBot.SendFriendMessage(Config.Owner,
                    new MessageBuilder()
                    .Text($"[{bot.Uin}] 需要进行滑块验证!\n")
                    .Text($"请选择验证方法:\n")
                    .Text($"自动获取验证结果 /startcaptcha Auto {bot.Uin}\n")
                    .Text($"手动提交验证结果 /startcaptcha Ticket {bot.Uin} \n")
                );
                break;
            default:
            case CaptchaEvent.CaptchaType.Unknown:
                await MainBot.SendFriendMessage(Config.Owner,
                    new MessageBuilder()
                    .Text($"[{bot.Uin}] 遇到了无法处理的验证模式!\n")
                    .Text($"请请等待后续功能更新。\n")
                );
                break;
        }
    }
}

