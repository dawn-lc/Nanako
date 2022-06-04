using Konata.Core.Message;
using Konata.Core.Message.Model;
using Nanako.Utils;
using System.Text.Json;

namespace Nanako.Module
{
    internal class ImageSearch
    {
        public class ImageSearchResult
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

        public static bool lockSearchImage = true;
        public static async Task<MessageBuilder> Search(ImageChain chain)
        {
            Uri ImageUrl = chain.ImageUrl[..4] != "http" ? new("https://gchat.qpic.cn" + chain.ImageUrl) : new(chain.ImageUrl);
            try
            {
                if (lockSearchImage)
                {
                    ImageSearchResult? r = JsonSerializer.Deserialize<ImageSearchResult>(await Http.GetAsync(new Uri("https://saucenao.com/search.php?db=999&output_type=2&numres=3&api_key=&url=" + ImageUrl.AbsoluteUri)).Result.Content.ReadAsStringAsync());
                    if (r != null)
                    {
                        var T = new MessageBuilder();
                        if (r.header.status == 0)
                        {
                            foreach (var item in r.results.OrderByDescending(x => x.header.similarity))
                            {
                                T.Image(await Http.GetAsync(new(item.header.thumbnail)).Result.Content.ReadAsByteArrayAsync());
                                T.Text($"\n相似度:{item.header.similarity}%");
                                T.Text($"\n地址:{(item.data.ext_urls != null ? item.data.ext_urls[0] : "没有数据!")}\n\n");
                            }

                        }
                        if (r.header.long_remaining <= 20)
                        {
                            lockSearchImage = false;
                            T.Text("识图次数以达到上限, 请等待24小时后限制解除");
                        }
                        return T;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return new MessageBuilder().Text("没有找到有效的结果");
        }
    }
}
