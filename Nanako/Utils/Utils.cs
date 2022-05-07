using System.Collections;

namespace Nanako.Utils
{

    class Commands<String>
    {
        private List<string> CommandList { get; set; }
        public Commands(string data)
        {
            CommandList = new List<string>();
            string e = "";
            string t = "";
            bool q = false;
            foreach (var s in data)
            {
                if (s == '\"')
                {
                    if (q)
                    {
                        q = false;
                        if (t != " ") CommandList.Add(t);
                        t = "";
                        continue;
                    }
                    q = true;
                    continue;
                }
                if (q)
                {
                    t += s;
                    continue;
                }
                if (s == ' ')
                {
                    if (e != "")
                    {
                        CommandList.Add(e);
                        e = "";
                    }
                }
                else
                {
                    e += s;
                }
            }
            CommandList.Add(e);
        }
        public string this[int i]
        {
            get { return CommandList[i]; }
            set { CommandList[i] = value; }
        }
    }
    internal static class Utils
    {
        public static double Bytes2MiB(this long bytes, int round) => Math.Round(bytes / 1048576.0, round);
    }
}
