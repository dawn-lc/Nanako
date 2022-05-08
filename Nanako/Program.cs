using Newtonsoft.Json;
using Konata.Core;
using Konata.Core.Common;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces;
using Konata.Core.Interfaces.Api;
using Konata.Core.Message;
using Nanako.Module;
using PuppeteerSharp;
using Nanako.Utils;

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
    public Config(uint owner)
    {
        ConfigList = new();
        Owner = owner;
    }

}

public static class Program
{
    public static Config Config;
    public static List<Bot> BotList;
    public static uint messageCounter;
    public static List<(Bot bot, CaptchaEvent eventSource)> NeedCaptchaBotList;


    private static T? DeserializeFile<T>(string path)
    {
        if (File.Exists(path))
        {
            T? data;
            if ((data = JsonConvert.DeserializeObject<T>(File.ReadAllText(path))) != null)
            {
                return data;
            }
        }
        return default;
    }

    /// <summary>
    /// Get bot config
    /// </summary>
    /// <returns></returns>
    private static Config GetConfig()
    {
        if (!Directory.Exists("Account")) Directory.CreateDirectory("Account");
        if (!Directory.Exists("Data")) Directory.CreateDirectory("Data");
        try
        {
            Config? configData = DeserializeFile<Config>("config.json");
            if (configData != null)
            {
                if (configData.ConfigList != null && configData.ConfigList.Count > 0)
                {
                    foreach (var bot in configData.ConfigList)
                    {
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
                }
                return configData;
            }
        }
        catch (Exception)
        {
            Console.WriteLine("Error in config.json, initialization");
        }
        Console.Write("first start, please type owner account:");
        var config = new Config(Convert.ToUInt32(Console.ReadLine()));
        File.WriteAllText($"config.json", JsonConvert.SerializeObject(config, Formatting.Indented));
        return config;
    }


    /// <summary>
    /// Update keystore
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    private static async void Autologin(Bot? bot)
    {
        if (bot != null)
        {
            var config = Config.ConfigList.Find(p => p.BotId == bot.KeyStore.Account.Uin);
            if (config == null)
            {
                await bot.Logout();
                bot.Dispose();
                BotList.Remove(bot);
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
                        Autologin(BotList.Find(p => p.Uin == config.KeyStore.Account.Uin));
                    }
                    UpdateKeyStore(bot);
                }
            }
        }
        else
        {
            Console.WriteLine("Login failed, bot is null");
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
    public static string AddBot(string account, string password)
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
            Config.ConfigList.Add(botConfig);
            BotList.Add(BotFather.Create(botConfig.Config, botConfig.Device, botConfig.KeyStore));
            UpdateConfig(botConfig.KeyStore.Account.Uin, botConfig.Config);
            UpdateDevice(botConfig.KeyStore.Account.Uin, botConfig.Device);
            File.WriteAllText($"config.json", JsonConvert.SerializeObject(Config, Formatting.Indented));
            Autologin(BotList.Find(p => p.Uin.ToString() == account));
            return "Add bot complete.";
        }
        else
        {
            return "Bot " + account + " already exists!";
        }
    }
    public static void Main(string[] args)
    {
        Config = GetConfig();
        BotList = new List<Bot>();
        messageCounter = 0;
        NeedCaptchaBotList = new List<(Bot bot, CaptchaEvent eventSource)>();

        if (Config.ConfigList.Count < 1)
        {
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
            BotList.Add(BotFather.Create(botConfig.Config, botConfig.Device, botConfig.KeyStore));
            UpdateConfig(botConfig.KeyStore.Account.Uin, botConfig.Config);
            UpdateDevice(botConfig.KeyStore.Account.Uin, botConfig.Device);
            File.WriteAllText($"config.json", JsonConvert.SerializeObject(Config, Formatting.Indented));
        }
        foreach (var bot in Config.ConfigList.Where(bot=> bot.Config != null && bot.Device != null && bot.KeyStore != null))
        {
            BotList.Add(BotFather.Create(bot.Config, bot.Device, bot.KeyStore));
        }
        foreach (Bot bot in BotList)
        {
            Autologin(bot);
        }
        Console.WriteLine("OK");
        while (true)
        {
            string? input= Console.ReadLine();
            if (input != null && input.Length > 0)
            {
                var Command = new Commands<string>(input);
                switch (Command[0][1..])
                {
                    case "stop":
                        BotStop();
                        File.WriteAllText($"config.json", JsonConvert.SerializeObject(Config, Formatting.Indented));
                        return;
                    default:
                        Console.WriteLine("Unknown command");
                        break;
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
                    .Text($"收到验证码后请发送 /Captcha SMS {bot.Uin} 验证码 进行验证.")
                );
                break;
            case CaptchaEvent.CaptchaType.Slider:
                NeedCaptchaBotList.Add((bot, eventSource));
                await MainBot.SendFriendMessage(Config.Owner,
                    new MessageBuilder()
                    .Text($"[{bot.Uin}] 需要进行滑块验证!\n")
                    .Text($"请选择验证方法:\n")
                    .Text($"自动获取验证结果 /StartCaptcha Auto {bot.Uin}\n")
                    .Text($"手动提交验证结果 /StartCaptcha Ticket {bot.Uin} \n")
                );
                break;
            default:
            case CaptchaEvent.CaptchaType.Unknown:
                Console.WriteLine("无法处理的验证码!");
                break;
        }
    }
}

