using MandarinJuiceCore.Infrastructure;
using Mi5hmasH.GameLaunchers.Steam.Types;

namespace MandarinJuiceCore.GamingPlatforms;

public class Steam : IGamingPlatform
{
    public string AppId { get; set; } = string.Empty;
    public string UserIdInput { get; set; } = string.Empty;
    public string UserIdOutput { get; set; } = string.Empty;
    public uint ParseVariant { get; set; } = 0;

    public const string StoreBaseUrl = "https://store.steampowered.com/app";
    public void OpenStoreProductPage() => $"{StoreBaseUrl}/{AppId}".OpenUrl();

    public ulong GetParsedUserIdInput() => ParseUserId(UserIdInput);
    public ulong GetParsedUserIdOutput() => ParseUserId(UserIdOutput);
    private ulong ParseUserId(string userId)
    {
        var steamId = new SteamId();
        var result = steamId.Set(userId);
        if (!result) throw new FormatException("The provided User ID is not a valid Steam ID.");
        return ParseVariant switch
        {
            0 => steamId.GetSteamId64(),
            1 => ~steamId.AccountId | 0xFFFFFFFF00000000,
            2 => ~steamId.GetSteamId64(),
            3 => ~GetObfuscatedSteamId64(steamId),
            _ => throw new NotSupportedException($"The Steam ID variant '{ParseVariant}' is not supported.")
        };

        static ulong GetObfuscatedSteamId64(SteamId steamId)
        {
            var notSteamId = steamId.GetSteamId64() ^ 0x1A3B5C7DD0C2B4A8;
            return ((notSteamId >> 32) & 0xFF) |
                   (((notSteamId >> 40) & 0xFF) << 8) |
                   (((notSteamId >> 48) & 0xFF) << 16) |
                   (((notSteamId >> 56) & 0xFF) << 24) |
                   ((notSteamId & 0xFF) << 32) |
                   (((notSteamId >> 8) & 0xFF) << 40) |
                   (((notSteamId >> 16) & 0xFF) << 48) |
                   (((notSteamId >> 24) & 0xFF) << 56);
        }
    }
}