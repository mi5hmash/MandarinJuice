using MandarinJuiceCore;
using MandarinJuiceCore.GameProfile;
using MandarinJuiceCore.GamingPlatformsFactory;
using MandarinJuiceCore.Helpers;
using MandarinJuiceCore.Infrastructure;
using MandarinJuiceCore.Models.DSSS.Mandarin;
using Mi5hmasH.GameProfile;
using Mi5hmasH.Logger;

namespace QualityControl.xUnit;

public sealed class MandarinJuiceCoreTests : IDisposable
{
    private readonly Core _core;
    private readonly GameProfileManager<MandarinGameProfile> _gameProfileManager = new();
    private IGamingPlatform _gamingPlatform = GamingPlatformRegistry.GetGamingPlatform(GamingPlatformEnum.Other);
    private readonly ITestOutputHelper _output;

    private const string SteamId = "76561197960265729";
    private const string EpicId = "1234567890abcdef1234567890abcdef";

    public MandarinJuiceCoreTests(ITestOutputHelper output)
    {
        _output = output;
        _output.WriteLine("SETUP");

        // Setup
        var logger = new SimpleLogger();
        var progressReporter = new ProgressReporter(null, null);
        _core = new Core(logger, progressReporter);
    }

    public void Dispose()
    {
        _output.WriteLine("CLEANUP");
    }

    private void LoadGameProfile(string profileData)
    {
        // Load GameProfile
        _gameProfileManager.SetEncryptor(Keychain.GpMagic);
        _gameProfileManager.Load(profileData, "profile");
        // Copy Mandarin Seed to Deencryptor
        _core.Deencryptor.MandarinSeed = _gameProfileManager.GameProfile.MandarinSeed;
        // Set GamingPlatform
        _gamingPlatform = GamingPlatformRegistry.GetGamingPlatform(_gameProfileManager.GameProfile.Platform);
        _gamingPlatform.AppId = _gameProfileManager.GameProfile.AppId ?? "0";
        _gamingPlatform.ParseVariant = _gameProfileManager.GameProfile.ParseVariant;
    }

    [Fact]
    public async Task DecryptFilesAsync_DoesNotThrow_WhenNoFiles()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var testResult = true;

        // Act
        try
        {
            await _core.DecryptFilesAsync(tempDir, _gamingPlatform, cts);
        }
        catch
        {
            testResult = false;
        }
        Directory.Delete(tempDir, true);

        // Assert
        Assert.True(testResult);
    }

    [Fact]
    public async Task EncryptFilesAsync_DoesNotThrow_WhenNoFiles()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var testResult = true;

        // Act
        try
        {
            await _core.EncryptFilesAsync(tempDir, _gamingPlatform, cts);
        }
        catch
        {
            testResult = false;
        }
        Directory.Delete(tempDir, true);

        // Assert
        Assert.True(testResult);
    }

    [Fact]
    public async Task ResignFilesAsync_DoesNotThrow_WhenNoFiles()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var testResult = true;

        // Act
        try
        {
            await _core.ResignFilesAsync(tempDir, _gamingPlatform, cts);
        }
        catch
        {
            testResult = false;
        }
        Directory.Delete(tempDir, true);

        // Assert
        Assert.True(testResult);
    }

    public static IEnumerable<object[]> DecryptFileTheories =>
    [
        ["V1_STEAM",Properties.Resources.profileFileSteamV1, Properties.Resources.encryptedFileSteamV1, SteamId],
        ["V2_STEAM",Properties.Resources.profileFileSteamV2, Properties.Resources.encryptedFileSteamV2, SteamId],
        ["V3_STEAM",Properties.Resources.profileFileSteamV3, Properties.Resources.encryptedFileSteamV3, SteamId],
        ["V1_EPIC", Properties.Resources.profileFileEpicV1, Properties.Resources.encryptedFileEpicV1, EpicId],
        ["V2_EPIC",Properties.Resources.profileFileEpicV2, Properties.Resources.encryptedFileEpicV2, EpicId]
    ];

    [Theory]
    [MemberData(nameof(DecryptFileTheories))]
    public void DecryptFiles_DoesDecrypt(string variant, string profileData, byte[] data, string userId)
    {
        // Arrange
        _output.WriteLine(variant);
        LoadGameProfile(profileData);
        _gamingPlatform.UserIdInput = userId;
        var mandarinFile = new MandarinFile(_core.Deencryptor, MandarinFileFlavorEnum.Default);
        mandarinFile.SetFileData(data, true);

        // Act
        mandarinFile.DecryptFile(_gamingPlatform.GetParsedUserIdInput());
        var resultData = mandarinFile.Data.AsSpan();

        // Assert
        Assert.Equal(Properties.Resources.decryptedFile, (ReadOnlySpan<byte>)resultData);
    }

    [Theory]
    [InlineData(MandarinFileFlavorEnum.Default)]
    [InlineData(MandarinFileFlavorEnum.Compressible)]
    public void EncryptFiles_DoesEncrypt(MandarinFileFlavorEnum flavor)
    {
        // Arrange
        LoadGameProfile(Properties.Resources.profileFileSteamV1);
        _gamingPlatform.UserIdInput = SteamId;
        _gamingPlatform.UserIdOutput = SteamId;
        var mandarinFile = new MandarinFile(_core.Deencryptor, flavor);
        mandarinFile.SetFileData(Properties.Resources.decryptedFile);

        // Act
        mandarinFile.EncryptFile(_gamingPlatform.GetParsedUserIdOutput());
        mandarinFile.DecryptFile(_gamingPlatform.GetParsedUserIdInput());
        var resultData = mandarinFile.Data.AsSpan();

        // Assert
        Assert.Equal(Properties.Resources.decryptedFile, (ReadOnlySpan<byte>)resultData);
    }
}