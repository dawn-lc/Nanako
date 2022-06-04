using System.Collections;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;

namespace Nanako.Utils
{
    class Commands<String> : IEnumerable<string>
    {
        private List<string> CommandList { get; set; }
        public Commands(string data)
        {
            if (data.Trim().Length < 0) return;
            CommandList = new List<string>();
            bool q = false;
            foreach (var item in data.Trim().Split(' '))
            {
                if (item[0]== '\"')
                {
                    q = true;
                    CommandList.Add(item[^1] != '\"' ? item[1..] : ((q = false) ? item[1..^1] : item[1..^1]));
                }
                else if (item[^1] == '\"')
                {
                    q = false;
                    CommandList[^1] += $" {item[..^1]}";
                }
                else
                {
                    if (q)
                    {
                        CommandList[^1] += $" {item}";
                    }
                    else
                    {
                        CommandList.Add(item);
                    }
                }
            }
        }
        public string this[int i]
        {
            get { return CommandList[i]; }
            set { CommandList[i] = value; }
        }

        public IEnumerator<string> GetEnumerator()
        {
            return CommandList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    public static class Http
    {
        static readonly TimeSpan DefaultTimeout = new(0,0,1,0);
        public static HttpClient Constructor(Dictionary<string, IEnumerable<string>>? head, TimeSpan? timeout)
        {
            HttpClientHandler clientHandler = new();
            clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
            HttpClient Client = new(clientHandler);
            Client.DefaultRequestHeaders.Clear();
            Client.Timeout = timeout ?? DefaultTimeout;
            if (head != null)
            {
                foreach (var item in head)
                {
                    Client.DefaultRequestHeaders.Add(item.Key, item.Value);
                }
            }
            return Client;
        }
        public static async Task<HttpResponseMessage> GetAsync(Uri url, Dictionary<string, IEnumerable<string>>? head = null, TimeSpan? timeout = null)
        {
            return await Constructor(head, timeout).GetAsync(url);
        }
        public static async Task<HttpResponseMessage> PostAsync(Uri url, HttpContent content, Dictionary<string, IEnumerable<string>>? head = null, TimeSpan? timeout = null)
        {
            return await Constructor(head, timeout).PostAsync(url, content);
        }
        public static async Task<HttpResponseMessage> PatchAsync(Uri url, HttpContent content, Dictionary<string, IEnumerable<string>>? head = null, TimeSpan? timeout = null)
        {
            return await Constructor(head, timeout).PatchAsync(url, content);
        }
        public static async Task<HttpResponseMessage> PutAsync(Uri url, HttpContent content, Dictionary<string, IEnumerable<string>>? head = null, TimeSpan? timeout = null)
        {
            return await Constructor(head, timeout).PutAsync(url, content);
        }
        public static HttpRequestMessage CreateRequest(Uri url, HttpMethod method, HttpContent? content)
        {
            return new HttpRequestMessage()
            {
                RequestUri = url,
                Method = method,
                Content = content,
            };
        }
        public static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage Request, Dictionary<string, IEnumerable<string>>? head = null, TimeSpan? timeout = null)
        {
            return await Constructor(head, timeout).SendAsync(Request);
        }
    }
    internal static class Util
    {
        /// <summary>
        /// Get arguments of a code string
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetArgs(string code)
        {
            var kvpair = new Dictionary<string, string>();

            // Split with a comma
            // [KQ:x,x=1,y=2] will becomes
            // "KQ:x" "x=1" "y=2"
            var split = code[..^1].Split(',');
            {
                // Split every kvpair with an equal
                // "KQ:x" ignored
                // "x=1" will becomes "x" "1"
                for (var i = 1; i < split.Length; ++i)
                {
                    var eqpair = split[i].Split('=');
                    if (eqpair.Length < 2) continue;
                    {
                        kvpair.Add(eqpair[0],
                            string.Join("=", eqpair[1..]));
                    }
                }

                return kvpair;
            }
        }
        public static long Epoch(this DateTime time)
        => (time.Ticks - 621355968000000000) / 10000;
        /// <summary>
        /// 利用反射来判断对象是否包含某个属性
        /// </summary>
        /// <param name="instance">object</param>
        /// <param name="propertyName">需要判断的属性</param>
        /// <returns>是否包含</returns>
        public static bool ContainProperty(this object instance, string propertyName)
        {
            if (instance != null && !string.IsNullOrEmpty(propertyName))
            {
                PropertyInfo _findedPropertyInfo = instance.GetType().GetProperty(propertyName);
                return (_findedPropertyInfo != null);
            }
            return false;
        }


        public static double Bytes2MiB(this long bytes, int round) => Math.Round(bytes / 1048576.0, round);
    }
}
