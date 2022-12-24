static class TypeHelper
{
    public static IEnumerable<Type> GetSubTypes(Type root)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
                                      .SelectMany(s => s.GetTypes())
                                      .Where(p => root.IsAssignableFrom(p) && !p.Equals(root));
    }
}