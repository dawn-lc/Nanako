using Konata.Core;
using Konata.Core.Events;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces.Api;
using Konata.Core.Message;
using Konata.Core.Message.Model;
using Nanako.Module;
using Nanako.Utils;
using System.Text.Json;

namespace ImageSearch
{

    public class ImageSearch : Permission
    {
        public override string Node { get; set; } = "ImageSearch";
        public override bool Flag { get; set; } = true;
    }

    public class Main : Plugin
    {
        public override string Name => "ImageSearch";

        public override string Info => "调用SaucenaoAPI对图片进行搜索";

        private static bool lockSearchImage = true;
        private static string Dir { get; set; } = $"{Env.Path}\\Data\\ImageSearch";
        private static string Key { get; set; } = Directory.Exists(Dir) && File.Exists($"{Dir}\\API.txt") ? File.ReadAllText($"{Dir}\\API.txt") : Initialization();

        public override List<Permission> PermissionList => new() { new ImageSearch() };

        private class ImageSearchResult
        {
            public class Header
            {
                public string user_id { get; set; }

                public string account_type { get; set; }

                public string short_limit { get; set; }

                public string long_limit { get; set; }

                public int long_remaining { get; set; }

                public int short_remaining { get; set; }

                public int status { get; set; }

            }
            public class Results
            {
                public class Header
                {
                    public string similarity { get; set; }

                    public string thumbnail { get; set; }

                }
                public class Data
                {
                    public List<string> ext_urls { get; set; }
                }
                public Header header { get; set; }
                public Data data { get; set; }
            }
            public Header header { get; set; }
            public List<Results> results { get; set; }
        }

        private static string Initialization()
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText($"{Dir}/API.txt", "");
            return "";

        }
        private static async Task<MessageBuilder> ImageSearch(ImageChain chain)
        {
            var T = new MessageBuilder();
            Uri ImageUrl = new(chain.ImageUrl);
            if (lockSearchImage)
            {
                ImageSearchResult? r = JsonSerializer.Deserialize<ImageSearchResult>(await HTTP.GetAsync(new Uri($"https://saucenao.com/search.php?db=999&output_type=2&numres=3&api_key={Key}&url={ImageUrl.AbsoluteUri}")).Result.Content.ReadAsStringAsync());
                if (r != null)
                {
                    if (r.header.status == 0)
                    {
                        foreach (var item in r.results.OrderByDescending(x => x.header.similarity))
                        {
                            T.Image(await HTTP.GetAsync(new(item.header.thumbnail)).Result.Content.ReadAsByteArrayAsync());
                            T.Text($"\n相似度:{item.header.similarity}%");
                            T.Text($"\n地址:{(item.data.ext_urls != null ? item.data.ext_urls[0] : "没有数据!")}\n\n");
                        }
                    }
                    if (r.header.long_remaining <= 20)
                    {
                        lockSearchImage = false;
                        T.Text("识图次数以达到上限, 请等待24小时后限制解除");
                    }
                }
                else
                {
                    T.Text("没有任何结果");
                }
            }
            else
            {
                T.Text("识图次数以达到上限, 请等待24小时后限制解除");
            }
            return T;
        }
        public override async Task<bool> Process(Bot bot, BaseEvent e)
        {
            try
            {
                MessageStruct? ReplyMessage;
                switch (e)
                {
                    case GroupMessageEvent messageEvent:
                        if(CheckPermission(messageEvent.MemberUin, "ImageSearch"))
                        {
                            if (messageEvent.Chain.Any(p => p is AtChain at && at.AtUin == bot.Uin))
                            {
                                ReplyMessage = messageEvent.Chain.Any(p => p is ReplyChain) ? GetHistoryMessage(messageEvent.Chain.GetChain<ReplyChain>()) : null;
                                if (ReplyMessage != null && messageEvent.Chain.Any(p => p is TextChain text && text.Content.Trim() != "" && text.Content.Trim().Contains("搜图")))
                                {
                                    _ = bot.SendGroupMessage(messageEvent.GroupUin, await ImageSearch(ReplyMessage.Chain.GetChain<ImageChain>()));
                                    return false;
                                }
                            }
                        }
                        return true;
                    case FriendMessageEvent messageEvent:
                        if (CheckPermission(messageEvent.FriendUin, "ImageSearch"))
                        {
                            ReplyMessage = messageEvent.Chain.Any(p => p is ReplyChain) ? GetHistoryMessage(messageEvent.Chain.GetChain<ReplyChain>()) : null;
                            if (ReplyMessage != null && messageEvent.Chain.Any(p => p is TextChain text && text.Content.Trim() != "" && text.Content.Trim().Contains("搜图")))
                            {
                                _ = await bot.SendFriendMessage(messageEvent.FriendUin, await ImageSearch(ReplyMessage.Chain.GetChain<ImageChain>()));
                                return false;
                            }
                        }
                        return true;
                    default:
                        return true;
                }
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}