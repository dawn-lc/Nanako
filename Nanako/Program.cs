using Newtonsoft.Json;
using Konata.Core;
using Konata.Core.Common;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces;
using Konata.Core.Interfaces.Api;
using Konata.Core.Message;
using Nanako.Module;
using Nanako.Utils;
using Sentry;

namespace Nanako;

public class Config
{
    public class BotConfig
    {
        public uint BotId { get; set; }
        public bool Enable { get; set; }
        [JsonIgnore]
        public Konata.Core.Common.BotConfig? Config { get; set; }
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
    public Config()
    {

    }

}

public static class Program
{
    public static Config Config = GetConfig();
    public static List<Bot> BotList = new();
    public static uint messageCounter = 0;
    public static List<(Bot bot, CaptchaEvent eventSource)> NeedCaptchaBotList = new();

    private static T DeserializeFile<T>(string path) where T : new()
    {
        T? data;
        return File.Exists(path) ? (data = JsonConvert.DeserializeObject<T>(File.ReadAllText(path))) != null ? data : new T() : new T();
    }

    /// <summary>
    /// Get bot config
    /// </summary>
    /// <returns></returns>
    private static Config GetConfig()
    {
        Config? configData = null;
        try
        {
            if (!Directory.Exists("Account")) Directory.CreateDirectory("Account");
            if (!Directory.Exists("Data")) Directory.CreateDirectory("Data");
            if (!Directory.Exists("Cache")) Directory.CreateDirectory("Cache");
            configData = DeserializeFile<Config>("config.json");
            if (configData.ConfigList != null && configData.ConfigList.Count > 0)
            {
                foreach (var bot in configData.ConfigList)
                {
                    if (!Directory.Exists($"Cache/{bot.BotId}")) Directory.CreateDirectory($"Cache/{bot.BotId}");
                    BotKeyStore? KeyStore;
                    if ((KeyStore = DeserializeFile<BotKeyStore>($"Account/{bot.BotId}.json")) != null)
                    {
                        bot.KeyStore = KeyStore;
                    }
                    BotDevice? Device;
                    if ((Device = DeserializeFile<BotDevice>($"Data/{bot.BotId}_device.json")) != null)
                    {
                        bot.Device = Device;
                    }
                    BotConfig? Config;
                    if ((Config = DeserializeFile<BotConfig>($"Data/{bot.BotId}_config.json")) != null)
                    {
                        bot.Config = Config;
                    }
                }
                return configData;
            }
            else
            {
                throw new Exception("Robot configuration list not found!");
            }
        }
        catch (IOException ex)
        {
            SentrySdk.CaptureException(ex);
            Console.WriteLine(ex);
            Thread.Sleep(10000);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            configData = null;
            Console.WriteLine(ex.Message);
            Console.WriteLine("initializing...");
        }
        while (configData == null)
        {
            try
            {
                Console.Write("please type owner account:");
                configData = new Config()
                {
                    Owner = Convert.ToUInt32(Console.ReadLine()),
                    ConfigList = new List<Config.BotConfig>()
                };
            }
            catch (Exception)
            {
                Console.WriteLine("initializing...");
                configData = null;
            }
        }
        return configData;
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
                SentrySdk.CaptureException(new Exception("Bot config failed."));
                Console.WriteLine("Bot config failed.");
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
                        Console.WriteLine("Login failed, please type your password.");
                        Console.Write("Password: ");
                        config.KeyStore = new BotKeyStore(bot.KeyStore.Account.Uin.ToString(), Console.ReadLine());
                        BotList.Remove(bot);
                        BotList.Add(BotFather.Create(config.Config, config.Device, config.KeyStore));
                        await Autologin(BotList.Find(p => p.Uin == config.KeyStore.Account.Uin));
                    }
                    UpdateKeyStore(bot);
                }
            }
        }
        else
        {
            SentrySdk.CaptureException(new Exception("Login failed, bot is null."));
            Console.WriteLine("Login failed, bot is null.");
        }
    }

    /// <summary>
    /// Update KeyStore
    /// </summary>
    private static void UpdateKeyStore(Bot bot)
    {
        File.WriteAllText($"Account/{bot.Uin}.json", JsonConvert.SerializeObject(bot.KeyStore, Formatting.Indented));
    }
    /// <summary>
    /// Update Config 
    /// </summary>
    private static void UpdateConfig()
    {
        File.WriteAllText($"config.json", JsonConvert.SerializeObject(Config, Formatting.Indented));
    }
    /// <summary>
    /// Update Config 
    /// </summary>
    private static void UpdateConfig(uint bot, BotConfig botConfig)
    {
        File.WriteAllText($"Data/{bot}_config.json", JsonConvert.SerializeObject(botConfig, Formatting.Indented));
    }
    /// <summary>
    /// Update Device
    /// </summary>
    private static void UpdateDevice(uint bot, BotDevice botDevice)
    {
        File.WriteAllText($"Data/{bot}_device.json", JsonConvert.SerializeObject(botDevice, Formatting.Indented));
    }
    public static void EnableBot(Bot bot)
    {
        Config.BotConfig? botConfig;
        if ((botConfig = Config.ConfigList.Find(p => p.BotId == bot.Uin)) != null)
        {
            botConfig.Enable = true;
        }
        UpdateConfig();
        Autologin(bot);
    }
    public static void DisableBot(Bot bot)
    {
        Config.BotConfig? botConfig;
        if ((botConfig = Config.ConfigList.Find(p => p.BotId == bot.Uin)) != null)
        {
            botConfig.Enable = false;
        }
        UpdateConfig();
        bot.Logout();
    }
    public static async Task<string> AddBotAsync(string account, string password)
    {
        if (Config.ConfigList.FindAll(p => p.BotId.ToString() == account).Count == 0)
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
            BotList.Add(BotFather.Create(botConfig.Config, botConfig.Device, botConfig.KeyStore));
            await Autologin(BotList.Find(p => p.Uin.ToString() == account));
            UpdateConfig();
            return "Add bot complete.";
        }
        else
        {
            return "Bot " + account + " already exists!";
        }
    }
    private static void Initialization()
    {
        if (new DirectoryInfo("Account").GetFiles().Any())
        {
            if (Config.ConfigList==null) Config.ConfigList = new();
            foreach (var item in new DirectoryInfo("Account").GetFiles())
            {
                Config.BotConfig bot = new()
                {
                    Enable = true
                };
                BotKeyStore? BotKeyStore;
                if ((BotKeyStore = DeserializeFile<BotKeyStore>(item.FullName)) != null)
                {
                    bot.BotId = BotKeyStore.Account.Uin;
                    bot.KeyStore = BotKeyStore;
                }
                BotDevice? BotDevice;
                if ((BotDevice = DeserializeFile<BotDevice>($"Data/{bot.BotId}_device.json")) != null)
                {
                    bot.Device = BotDevice;
                }
                BotConfig? BotConfig;
                if ((BotConfig = DeserializeFile<BotConfig>($"Data/{bot.BotId}_config.json")) != null)
                {
                    bot.Config = BotConfig;
                }
                Config.ConfigList.Add(bot);
            }
            UpdateConfig();
            return;
        }
        Console.WriteLine("For bot first login, please " +
                          "type your account and password.");
        Console.Write("Account: ");
        var account = Console.ReadLine();
        Console.Write("Password: ");
        var password = Console.ReadLine();
        Config.BotConfig botConfig = new()
        {
            Enable = true,
            BotId = Convert.ToUInt32(account),
            Config = Config.GlobalConfig,
            Device = BotDevice.Default(),
            KeyStore = new BotKeyStore(account, password)
        };
        Config.ConfigList.Add(botConfig);
        UpdateConfig(botConfig.KeyStore.Account.Uin, botConfig.Config);
        UpdateDevice(botConfig.KeyStore.Account.Uin, botConfig.Device);
        UpdateConfig();
        BotList.Add(BotFather.Create(botConfig.Config, botConfig.Device, botConfig.KeyStore));
        
    }
    public static void Main(string[] args)
    {
        using (SentrySdk.Init(o =>
        {
            o.Dsn = "https://ab8d4959bf0440e1be8c1945a7ed8a28@o687854.ingest.sentry.io/6427927";
            o.TracesSampleRate = 1.0;
        }))
        {
            if (Config.ConfigList.Count < 1) Initialization();
            foreach (var bot in Config.ConfigList.Where(bot => bot.Config != null && bot.Device != null && bot.KeyStore != null))
            {
                BotList.Add(BotFather.Create(bot.Config, bot.Device, bot.KeyStore));
            }
            foreach (Bot bot in BotList)
            {
                Autologin(bot);
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
                            UpdateConfig();
                            return;
                        default:
                            Console.WriteLine("Unknown command");
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

