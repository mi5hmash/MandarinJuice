using System.Reflection;
using MandarinJuiceCore.GamingPlatformsFactory.Platforms;

namespace MandarinJuiceCore.GamingPlatformsFactory;

public static class GamingPlatformRegistry
{
    private static readonly Dictionary<GamingPlatformEnum, Type> GamingPlatforms = [];
    public static readonly Dictionary<GamingPlatformEnum, string> GamingPlatformsFriendlyNames = [];

    static GamingPlatformRegistry()
    {
        var ns = typeof(Default).Namespace!;
        var baseType = typeof(IGamingPlatform);

        var elements = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Select(t => new
            {
                Type = t,
                Attribute = t.GetCustomAttribute<GamingPlatformTypeAttribute>(false)
            })
            .Where(x =>
                x.Type.Namespace != null &&
                x.Type.Namespace.StartsWith(ns) &&
                x.Attribute != null &&
                !x.Type.IsAbstract &&
                baseType.IsAssignableFrom(x.Type));

        foreach (var element in elements)
        {
            var type = element.Type;
            if (!GamingPlatforms.TryAdd(element.Attribute!.GamingPlatformType, type))
                throw new InvalidOperationException($"Duplicate GamingPlatform '{element.Attribute!.GamingPlatformType}' in {element.Type.FullName}");
            GamingPlatformsFriendlyNames[element.Attribute.GamingPlatformType] = element.Attribute.FriendlyName;
        }
    }
    private static T GetOrThrow<T>(Dictionary<GamingPlatformEnum, T> dict, GamingPlatformEnum id)
        => dict.TryGetValue(id, out var value)
            ? value
            : throw new NotSupportedException($"The gaming platform '{id}' is not supported.");

    public static IGamingPlatform GetGamingPlatform(GamingPlatformEnum gamingPlatformType)
    {
        var type = GetOrThrow(GamingPlatforms, gamingPlatformType);
        return (IGamingPlatform)Activator.CreateInstance(type)!;
    }
}