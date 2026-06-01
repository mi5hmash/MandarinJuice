namespace MandarinJuiceCore.GamingPlatformsFactory;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GamingPlatformTypeAttribute(GamingPlatformEnum gamingPlatformType, string? friendlyName = null) : Attribute
{
    public GamingPlatformEnum GamingPlatformType { get; } = gamingPlatformType;
    public string FriendlyName { get; } = friendlyName ?? gamingPlatformType.ToString();
}