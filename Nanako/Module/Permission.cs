namespace Nanako.Module
{
    public class Permission
    {
        public string Name { get; set; }
        public bool Flag { get; set; }
    }

    public static class Permissions
    {
        public static bool Check(uint u, string permission)
        {
            return Program.PermissionTable.TryGetValue(u, out List<Permission>? permissions) && permissions.Any(p => (p.Name == permission || p.Name == "*" ) && p.Flag);
        }
    }
}
