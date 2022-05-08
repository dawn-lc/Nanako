using System.Collections;

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
    internal static class Utils
    {
        public static double Bytes2MiB(this long bytes, int round) => Math.Round(bytes / 1048576.0, round);
    }
}
