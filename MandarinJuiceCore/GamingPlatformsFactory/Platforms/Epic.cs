using System.Runtime.InteropServices;
using MandarinJuiceCore.Helpers;
using MandarinJuiceCore.Infrastructure;
using Mi5hmasH.GameLaunchers.Epic.Types;

namespace MandarinJuiceCore.GamingPlatformsFactory.Platforms;

[GamingPlatformType(GamingPlatformEnum.Epic, "Epic Games Store")]
public sealed class Epic : Default
{
    private const string StoreBaseUrl = "https://store.epicgames.com/en/p";

    // -------------------------------
    // PUBLIC API
    // -------------------------------
    /// <summary>
    /// Opens the Epic Games store page for the specified App ID in the default web browser.
    /// </summary>
    public override void OpenStoreProductPage() => $"{StoreBaseUrl}/{AppId}".OpenUrl();
    
    /// <summary>
    /// Parses the User ID input and output properties into standardized 64-bit Epic ID formats, applying the specified variant transformation.
    /// </summary>
    /// <returns>The parsed 64-bit Epic ID for the input property.</returns>
    public override ulong GetParsedUserIdInput() => ParseUserId(UserIdInput);

    /// <summary>
    /// Parses the User ID input and output properties into standardized 64-bit Epic ID formats, applying the specified variant transformation.
    /// </summary>
    /// <returns>The parsed 64-bit Epic ID for the output property.</returns>
    public override ulong GetParsedUserIdOutput() => ParseUserId(UserIdOutput);

    /// <summary>
    /// Parses the provided User ID input into a standardized 64-bit Epic ID format, applying the specified variant transformation.
    /// </summary>
    /// <param name="userId">The User ID to parse.</param>
    /// <returns>The parsed 64-bit Epic ID.</returns>
    /// <exception cref="FormatException">Thrown if the provided User ID is not a valid Epic ID.</exception>
    public override ulong ParseUserId(string userId)
    {
        var epicId = new EpicId();
        return !epicId.TrySetEpicId(userId) 
            ? throw new FormatException("The provided User ID is not a valid Epic ID.") 
            : ParseUserIdInternal(epicId);
    }

    /// <summary>
    /// Parses the provided User ID input into a standardized 64-bit Epic ID format, applying the specified variant transformation.
    /// </summary>
    /// <param name="id">The User ID to parse.</param>
    /// <returns>The parsed 64-bit Epic ID.</returns>
    public override ulong ParseUserId(uint id) => ParseUserIdInternal(id);
    
    /// <summary>
    /// Parses the provided User ID input into a standardized 64-bit Epic ID format, applying the specified variant transformation.
    /// </summary>
    /// <param name="id">The User ID to parse.</param>
    /// <returns>The parsed 64-bit Epic ID.</returns>
    public override ulong ParseUserId(ulong id) => ParseUserIdInternal(id);

    // -------------------------------
    // INTERNAL LOGIC
    // -------------------------------
    private ulong ParseUserIdInternal(EpicId epicId)
    {
        var (uintHash, ulongHash) = ComputeHashes(epicId);
        return ApplyVariant(uintHash, ulongHash, ParseVariant);
    }

    private ulong ParseUserIdInternal(uint id) 
        => ApplyVariant(id, id, ParseVariant);

    private ulong ParseUserIdInternal(ulong id) 
        => ApplyVariant((uint)id, id, ParseVariant);

    // -------------------------------
    // VARIANT LOGIC
    // -------------------------------
    private static ulong ApplyVariant(uint uintHash, ulong ulongHash, uint variant)
    {
        return variant switch
        {
            0 => ulongHash,
            1 => ~uintHash | 0xFFFFFFFF00000000,
            2 => ~ulongHash,
            _ => throw new NotSupportedException($"The Epic ID variant '{variant}' is not supported.")
        };
    }

    // -------------------------------
    // EPIC ID HASHING
    // -------------------------------
    private static (uint fullHash, ulong ulongHash) ComputeHashes(EpicId epicId)
    {
        const uint seed = 0xFFFFFFFF;

        var wide = epicId.GetAsWideString().AsSpan();
        var asUint = MemoryMarshal.Cast<byte, uint>(wide);

        var hi = MandarinDeencryptor.Murmur3_32(asUint[..8], seed);
        var lo = MandarinDeencryptor.Murmur3_32(asUint[^8..], seed);

        var ulongHash = ((ulong)hi << 32) | lo;
        var fullHash = MandarinDeencryptor.Murmur3_32(asUint, seed);

        return (fullHash, ulongHash);
    }
}
