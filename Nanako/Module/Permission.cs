
using System.Reflection;
using System.Xml.Serialization;

namespace Nanako.Module
{
    public class PermissionItem
    {
        public uint User { get; set; }
        public List<Permission> Permissions { get; set; } = new(); 
    }

    public abstract class Permission
    {
        public abstract string Node { get; set; }
        public abstract bool Flag { get; set; }
    }

    public static class Permissions
    {
        public class Root : Permission
        {
            public override string Node { get; set; } = "*";
            public override bool Flag { get; set; } = true;
        }
        public static Type[] All { get; } = Assembly.GetCallingAssembly().GetTypes().Where(t => t.BaseType != null && t.BaseType.Name == typeof(Permission).Name).ToArray();
        public static bool Check(uint u, string node)
        {
            return Program.PermissionTable.TryGetValue(u, out List<Permission>? permissions) && permissions.Any(p => (p is Root || p.Node == node) && p.Flag);
        }
    }
}
