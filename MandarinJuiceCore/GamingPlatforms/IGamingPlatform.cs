namespace MandarinJuiceCore.GamingPlatforms;

public interface IGamingPlatform
{
    string AppId { get; set; }
    string UserIdInput { get; set; }
    string UserIdOutput { get; set; }
    uint ParseVariant { get; set; }
    void OpenStoreProductPage();
    ulong GetParsedUserIdInput();
    ulong GetParsedUserIdOutput();
}