namespace MandarinJuiceCore.GamingPlatforms;

public enum GamingPlatform
{
    Other = 0,
    Steam = 1
}

public static class GamingPlatformHelper
{
    public static IGamingPlatform GetGamingPlatform(GamingPlatform platform) => platform switch
    {
        GamingPlatform.Steam => new Steam(),
        GamingPlatform.Other => new Other(),
        _ => throw new NotSupportedException($"The gaming platform '{platform}' is not supported.")
    };
}