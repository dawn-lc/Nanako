using System.Collections;
using System.Linq;
using System.Net.Http.Headers;

namespace Nanako.Utils
{

    class Commands<String> : IEnumerable<string>
    {
        private List<string> CommandList { get; set; }
        public Commands(string data)
        {
            CommandList = new List<string>();
            bool q = false;
            foreach (var item in data.Split(' '))
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
    internal static class Http
    {
        static readonly TimeSpan DefaultTimeout = new(0,0,1,0);
        public static HttpClient Constructor(Dictionary<string, IEnumerable<string>>? head, TimeSpan? timeout)
        {
            HttpClient Client = new();
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
    internal static class Utils
    {
        public static double Bytes2MiB(this long bytes, int round) => Math.Round(bytes / 1048576.0, round);
    }
}
