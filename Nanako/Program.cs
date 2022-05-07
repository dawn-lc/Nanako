using Newtonsoft.Json;
using Konata.Core;
using Konata.Core.Common;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces;
using Konata.Core.Interfaces.Api;
using Nanako.Module;
using PuppeteerSharp;
using Nanako.Utils;
using System.Text;

namespace Nanako;

class Config
{
    public class BotConfig
    {
        public Konata.Core.Common.BotConfig Config { get; set; }
        public Konata.Core.Common.BotDevice Device { get; set; }
        public Konata.Core.Common.BotKeyStore KeyStore { get; set; }
    }
    public static Konata.Core.Common.BotConfig GlobalConfig { get; set; }

    public List<BotConfig> ConfigList = new List<BotConfig>();
    public Config()
    {
        GlobalConfig = new()
        {
            EnableAudio = true,
            TryReconnect = true,
            HighwayChunkSize = 8192,
        };
        ConfigList = new();
    }

}

public static class Program
{
    private static ReaderWriterLock ConfigReaderWriterLock = new ReaderWriterLock();
    private static Config Config;
    public static List<Bot> BotList;
    /// <summary>
    /// Get bot config
    /// </summary>
    /// <returns></returns>
    private static Config GetConfig()
    {
        if (File.Exists("config.json"))
        {
            try
            {
                var configData = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
                if (configData == null) throw new Exception("Null in config.json");
                return configData;
            }
            catch (Exception)
            {
                Console.WriteLine("Error in config.json, initialization");
            }
        }
        return new Config();
    }


    /// <summary>
    /// Update keystore
    /// </summary>
    /// <param name="bot"></param>
    /// <returns></returns>
    private static async void Autologin(Bot bot)
    {
        var config = Config.ConfigList.Find(p => p.KeyStore.Account.Uin == bot.KeyStore.Account.Uin);
        if (await bot.Login())
        {
            if (config == null)
            {
                Config.ConfigList.Add(new Config.BotConfig() { KeyStore = bot.KeyStore });
            }
            else
            {
                config.KeyStore = bot.KeyStore;
            }
            return;
        }
        if (config != null)
        {
            Console.WriteLine("Login failed, please type your password.");
            Console.Write("Password: ");
            config.KeyStore = new BotKeyStore(bot.KeyStore.Account.Uin.ToString(), Console.ReadLine());
            BotList.Remove(bot);
            BotList.Add(BotFather.Create(config.Config, config.Device, config.KeyStore));
            if (await BotList.Find(p => p.Uin == config.KeyStore.Account.Uin).Login())
            {
                Console.WriteLine("{0} Online", config.KeyStore.Account.Uin);
            }
            else
            {
                Console.WriteLine("{0} Login failed. Please check whether the password is correct.", config.KeyStore.Account.Uin);
            }
            UpdateConfig();
        }
        else
        {
            Console.WriteLine("没有找到这个机器人的配置文件!");
        }
    }
    /// <summary>
    /// Update Config
    /// </summary>
    private static void UpdateConfig()
    {
        ConfigReaderWriterLock.AcquireReaderLock(100);
        try
        {
            File.WriteAllText("config.json", JsonConvert.SerializeObject(Config,Formatting.Indented));
        }
        finally
        {
            ConfigReaderWriterLock.ReleaseReaderLock();
        }
    }
    public static string AddBot(string account, string password)
    {
        if (Config.ConfigList.FindAll(p => p.KeyStore.Account.Uin.ToString() == account).Count == 0)
        {
            Config.BotConfig botConfig = new();
            botConfig.Config = Config.GlobalConfig;
            botConfig.Device = BotDevice.Default();
            botConfig.KeyStore = new BotKeyStore(account, password);
            Config.ConfigList.Add(botConfig);
            BotList.Add(BotFather.Create(botConfig.Config, botConfig.Device, botConfig.KeyStore));
            UpdateConfig();
            return "Add bot complete.";
        }
        else
        {
            return "Bot " + account + " already exists!";
        }
    }
    public static async Task Main(string[] args)
    {

        Console.WriteLine("Starting...");
        Config = GetConfig();
        BotList = new List<Bot>();

        if (Config.ConfigList.Count < 1)
        {
            Config.BotConfig botConfig = new();
            Config.ConfigList.Add(botConfig);
        }
        foreach (var botConfig in Config.ConfigList)
        {
            if (botConfig.Config == null)
            {
                botConfig.Config = Config.GlobalConfig;
            }
            if (botConfig.Device == null)
            {
                botConfig.Device = BotDevice.Default();
            }
            if (botConfig.KeyStore == null)
            {
                Console.WriteLine("For bot first login, please " +
                          "type your account and password.");
                Console.Write("Account: ");
                var account = Console.ReadLine();
                Console.Write("Password: ");
                var password = Console.ReadLine();
                botConfig.KeyStore = new BotKeyStore(account, password);
            }
            BotList.Add(BotFather.Create(botConfig.Config, botConfig.Device, botConfig.KeyStore));
        }
        foreach (Bot bot in BotList)
        {
            bot.OnCaptcha += async (bot, eventSource) =>
            {
                switch (eventSource.Type)
                {
                    case CaptchaEvent.CaptchaType.Sms:
                        Console.WriteLine("需要进行短信验证!");
                        Console.WriteLine("绑定手机号为: {0} 请检查是否收到验证码!", eventSource.Phone);
                        Console.Write("CODE:");
                        if (bot.SubmitSmsCode(Console.ReadLine()))
                        {
                            Console.WriteLine("验证成功");
                        }
                        else
                        {
                            Console.WriteLine("验证失败");
                        }
                        break;
                    case CaptchaEvent.CaptchaType.Slider:
                        try
                        {
                            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("下载用于进行滑块验证的浏览器时出现问题, 请确认您是否能够访问https://storage.googleapis.com");
                            break;
                        }
                        Console.WriteLine("需要进行滑块验证, 请在打开的窗口内进行验证!");
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
                        (await browser.PagesAsync())[0].Response += async (page, e) => {

                            if (e.Response.Url.Contains("cap_union_new_verify"))
                            {
                                var Response = await e.Response.JsonAsync();
                                if (Response.Value<int>("errorCode") == 0)
                                {
                                    if (bot.SubmitSliderTicket(Response.Value<string>("ticket")))
                                    {
                                        Console.WriteLine("验证成功");
                                    }
                                    else
                                    {
                                        Console.WriteLine("验证失败");
                                    }
                                    await browser.CloseAsync();
                                }
                            }
                        };
                        break;
                    default:
                    case CaptchaEvent.CaptchaType.Unknown:
                        Console.WriteLine("无法处理的验证码!");
                        break;
                }
            };

            bot.OnBotOnline += (bot, eventSource) =>
            {
                Console.WriteLine("[{0}({1})]:<{2}>", bot.Name, bot.Uin, eventSource.GetType().Name);
                UpdateConfig();
            };
            bot.OnBotOffline += (bot, eventSource) =>
            {
                Console.WriteLine("[{0}({1})]:<{2}>", bot.Name, bot.Uin, eventSource.GetType().Name);
                //Autologin(bot);
            };
            {
                bot.OnFriendMessage += Command.OnFriendMessage;
                bot.OnGroupMessage += Command.OnGroupMessage;
            }
            Autologin(bot);
        }


        ConsoleKeyInfo cki = new ConsoleKeyInfo();
        do
        {
            if (Console.KeyAvailable)
            {
                cki = Console.ReadKey(false);
                switch (cki.Key)
                {
                    default:
                        break;
                }
            }
        }
        while (cki.Key != ConsoleKey.Q);

    }

}
