using MandarinJuiceCore.Infrastructure;
using Mi5hmasH.GameLaunchers.Steam.Types;

namespace MandarinJuiceCore.GamingPlatformsFactory.Platforms;

[GamingPlatformType(GamingPlatformEnum.Steam)]
public sealed class Steam : Default
{
    public const string StoreBaseUrl = "https://store.steampowered.com/app";
    
    // -------------------------------
    // PUBLIC API
    // -------------------------------
    /// <summary>
    /// Opens the Steam store page for the specified App ID in the default web browser.
    /// </summary>
    public override void OpenStoreProductPage() => $"{StoreBaseUrl}/{AppId}".OpenUrl();
    
    /// <summary>
    /// Parses the User ID input and output properties into standardized 64-bit Steam ID formats, applying the specified variant transformation.
    /// </summary>
    /// <returns>The parsed 64-bit Steam ID for the input property.</returns>
    public override ulong GetParsedUserIdInput() => ParseUserId(UserIdInput);
    
    /// <summary>
    /// Parses the User ID input and output properties into standardized 64-bit Steam ID formats, applying the specified variant transformation.
    /// </summary>
    /// <returns>The parsed 64-bit Steam ID for the output property.</returns>
    public override ulong GetParsedUserIdOutput() => ParseUserId(UserIdOutput);

    /// <summary>
    /// Parses the provided User ID input into a standardized 64-bit Steam ID format, applying the specified variant transformation.
    /// </summary>
    /// <param name="userId">The User ID to parse.</param>
    /// <returns>The parsed 64-bit Steam ID.</returns>
    /// <exception cref="FormatException">Thrown if the provided User ID is not a valid Steam ID.</exception>
    public override ulong ParseUserId(string userId)
    {
        var steamId = new SteamId();
        return !steamId.Set(userId)
            ? throw new FormatException("The provided User ID is not a valid Steam ID.")
            : ParseUserIdInternal(steamId);
    }
    
    /// <summary>
    /// Parses the provided User ID input into a standardized 64-bit Steam ID format, applying the specified variant transformation.
    /// </summary>
    /// <param name="id">The User ID to parse.</param>
    /// <returns>The parsed 64-bit Steam ID.</returns>
    public override ulong ParseUserId(uint id) => ParseUserIdInternal(id);
    
    /// <summary>
    /// Parses the provided User ID input into a standardized 64-bit Steam ID format, applying the specified variant transformation.
    /// </summary>
    /// <param name="id">The User ID to parse.</param>
    /// <returns>The parsed 64-bit Steam ID.</returns>
    public override ulong ParseUserId(ulong id) => ParseUserIdInternal(id);

    // -------------------------------
    // INTERNAL LOGIC
    // -------------------------------
    private ulong ParseUserIdInternal(SteamId steamId) 
        => ApplyVariant(steamId.AccountId, steamId.GetSteamId64(), ParseVariant);

    private ulong ParseUserIdInternal(uint id)
        => ApplyVariant(id, id, ParseVariant);

    private ulong ParseUserIdInternal(ulong id)
        => ApplyVariant((uint)id, id, ParseVariant);

    // -------------------------------
    // VARIANT LOGIC
    // -------------------------------
    private static ulong ApplyVariant(uint uintId, ulong ulongId, uint variant)
    {
        return variant switch
        {
            0 => ulongId,
            1 => ~uintId | 0xFFFFFFFF00000000,
            2 => ~ulongId,
            3 => ~ObfuscateSteamId(ulongId),
            _ => throw new NotSupportedException($"The Steam ID variant '{variant}' is not supported.")
        };
    }

    private static ulong ObfuscateSteamId(ulong steamId)
    {
        var sid = steamId ^ 0x1A3B5C7DD0C2B4A8;
        return ((sid >> 32) & 0xFF) |
               (((sid >> 40) & 0xFF) << 8) |
               (((sid >> 48) & 0xFF) << 16) |
               (((sid >> 56) & 0xFF) << 24) |
               ((sid & 0xFF) << 32) |
               (((sid >> 8) & 0xFF) << 40) |
               (((sid >> 16) & 0xFF) << 48) |
               (((sid >> 24) & 0xFF) << 56);
    }
}