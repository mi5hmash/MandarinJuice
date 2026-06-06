using System.Reflection;
using MandarinJuiceCore.GamingPlatformsFactory.Platforms;

namespace MandarinJuiceCore.GamingPlatformsFactory;

public static class GamingPlatformRegistry
{
    private static readonly Dictionary<GamingPlatformEnum, IGamingPlatform> GamingPlatforms = new();
    public static readonly Dictionary<GamingPlatformEnum, string> GamingPlatformsFriendlyNames = new();

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
            var instance = (IGamingPlatform)Activator.CreateInstance(element.Type)!;
            if (!GamingPlatforms.TryAdd(element.Attribute!.GamingPlatformType, instance))
                throw new InvalidOperationException($"Duplicate GamingPlatform '{element.Attribute!.GamingPlatformType}' in {element.Type.FullName}");
            GamingPlatformsFriendlyNames[element.Attribute.GamingPlatformType] = element.Attribute.FriendlyName;
        }
    }

    public static IGamingPlatform GetGamingPlatform(GamingPlatformEnum gamingPlatformType) 
        => !GamingPlatforms.TryGetValue(gamingPlatformType, out var platform) 
            ? throw new NotSupportedException($"The gaming platform '{gamingPlatformType}' is not supported.") 
            : platform;
}