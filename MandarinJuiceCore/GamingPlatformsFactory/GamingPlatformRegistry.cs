using System.Reflection;
using MandarinJuiceCore.GamingPlatformsFactory.Platforms;

namespace MandarinJuiceCore.GamingPlatformsFactory;

public static class GamingPlatformRegistry
{
    private static readonly Dictionary<GamingPlatformEnum, IGamingPlatform> GamingPlatforms;
    public static readonly Dictionary<GamingPlatformEnum, string> GamingPlatformsFriendlyNames;

    static GamingPlatformRegistry()
    {
        GamingPlatforms = new Dictionary<GamingPlatformEnum, IGamingPlatform>();
        GamingPlatformsFriendlyNames = new Dictionary<GamingPlatformEnum, string>();

        var ns = typeof(Default).Namespace;
        var baseType = typeof(IGamingPlatform);
        const bool inherit = false;

        var elements = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => 
                t.Namespace == ns &&
                !t.IsAbstract &&
                baseType.IsAssignableFrom(t) &&
                t.GetCustomAttribute<GamingPlatformTypeAttribute>(inherit) != null)
            .Select(t => new
            {
                Type = t,
                Attribute = t.GetCustomAttribute<GamingPlatformTypeAttribute>(inherit)
            });

        foreach (var element in elements)
        {
            var instance = (IGamingPlatform)Activator.CreateInstance(element.Type)!;
            GamingPlatforms[element.Attribute!.GamingPlatformType] = instance;
            GamingPlatformsFriendlyNames[element.Attribute.GamingPlatformType] = element.Attribute.FriendlyName;
        }
    }

    public static IGamingPlatform GetGamingPlatform(GamingPlatformEnum gamingPlatformType) 
        => !GamingPlatforms.TryGetValue(gamingPlatformType, out var platform) 
            ? throw new NotSupportedException($"The gaming platform '{gamingPlatformType}' is not supported.") 
            : platform;
}