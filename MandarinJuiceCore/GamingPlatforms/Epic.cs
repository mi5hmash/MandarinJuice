using System.Runtime.InteropServices;
using MandarinJuiceCore.Helpers;
using MandarinJuiceCore.Infrastructure;
using Mi5hmasH.GameLaunchers.Epic.Types;

namespace MandarinJuiceCore.GamingPlatforms;

public class Epic : IGamingPlatform
{
    public string AppId { get; set; } = string.Empty;
    public string UserIdInput { get; set; } = string.Empty;
    public string UserIdOutput { get; set; } = string.Empty;
    public uint ParseVariant { get; set; } = 0;

    public const string StoreBaseUrl = "https://store.epicgames.com/en/p";
    public void OpenStoreProductPage() => $"{StoreBaseUrl}/{AppId}".OpenUrl();

    public ulong GetParsedUserIdInput() => ParseUserId(UserIdInput);
    public ulong GetParsedUserIdOutput() => ParseUserId(UserIdOutput);

    public ulong ParseUserId(string userId)
    {
        var epicId = new EpicId();
        var parseResult = epicId.TrySetEpicId(userId);
        if (parseResult)
        {
            var wideEpicId = epicId.GetAsWideString().AsSpan();
            var wideEpicIdAsUint = MemoryMarshal.Cast<byte, uint>(wideEpicId);
            var hashedEpicId = MandarinDeencryptor.Murmur3_32(wideEpicIdAsUint, 0xFFFFFFFF);
            return ParseUserIdInternal(hashedEpicId);
        }

        parseResult = ulong.TryParse(userId, out var parsedUserId);
        return parseResult 
            ? ParseUserIdInternal(parsedUserId) 
            : throw new FormatException("The provided User ID is not a valid Epic ID.");
    }
    
    public ulong ParseUserId(uint userId) 
        => ParseUserIdInternal(userId);

    public ulong ParseUserId(ulong userId)
        => ParseUserIdInternal(userId);

    private ulong ParseUserIdInternal(ulong epicId)
    {
        return ParseVariant switch
        {
            0 => epicId,
            1 => ~(uint)epicId | 0xFFFFFFFF00000000,
            2 => ~epicId,
            _ => throw new NotSupportedException($"The Epic ID variant '{ParseVariant}' is not supported.")
        };
    }
}