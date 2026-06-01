namespace MandarinJuiceCore.GamingPlatformsFactory;

public interface IGamingPlatform
{
    string AppId { get; set; }
    string UserIdInput { get; set; }
    string UserIdOutput { get; set; }
    uint ParseVariant { get; set; }
    void OpenStoreProductPage();
    ulong GetParsedUserIdInput();
    ulong GetParsedUserIdOutput();
    public ulong ParseUserId(uint userId);
    public ulong ParseUserId(ulong userId);
    public ulong ParseUserId(string userId);
}