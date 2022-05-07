namespace Nanako.Utils
{
    public static class Env
    {
        public static string Path => Environment.CurrentDirectory;
        public static string Name => System.Reflection.Assembly.GetExecutingAssembly().GetName().Name.ToString();

    }
}
