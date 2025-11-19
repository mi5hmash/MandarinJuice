using MandarinJuiceCore;
using MandarinJuiceCore.GameProfile;
using MandarinJuiceCore.GamingPlatforms;
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
    private IGamingPlatform _gamingPlatform = new Other();
    private readonly ITestOutputHelper _output;
    
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

    private void LoadGameProfile()
    {
        // Load GameProfile
        _gameProfileManager.SetEncryptor(Keychain.GpMagic);
        _gameProfileManager.Load(Properties.Resources.profileFile, "profile");
        // Copy Mandarin Seed to Deencryptor
        _core.Deencryptor.MandarinSeed = _gameProfileManager.GameProfile.MandarinSeed;
        // Set GamingPlatform
        _gamingPlatform = GamingPlatformHelper.GetGamingPlatform(_gameProfileManager.GameProfile.Platform);
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
        Directory.Delete(tempDir);

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
        Directory.Delete(tempDir);

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
        Directory.Delete(tempDir);

        // Assert
        Assert.True(testResult);
    }

    [Fact]
    public void DecryptFiles_DoesDecrypt()
    {
        // Arrange
        const string userId = "76561197960265729";
        LoadGameProfile();
        _gamingPlatform.UserIdInput = userId;
        var mandarinFile = new MandarinFile(_core.Deencryptor, MandarinFileFlavor.Default);
        mandarinFile.SetFileData(Properties.Resources.encryptedFile, true);

        // Act
        mandarinFile.DecryptFile(_gamingPlatform.GetParsedUserIdInput());
        var resultData = mandarinFile.Data.AsSpan();

        // Assert
        Assert.Equal((ReadOnlySpan<byte>)resultData, Properties.Resources.decryptedFile);
    }

    [Theory]
    [InlineData(MandarinFileFlavor.Default)]
    [InlineData(MandarinFileFlavor.Compressible)]
    public void EncryptFiles_DoesEncrypt(MandarinFileFlavor flavor)
    {
        // Arrange
        const string userId = "76561197960265729";
        LoadGameProfile();
        _gamingPlatform.UserIdInput = userId;
        _gamingPlatform.UserIdOutput = userId;
        var mandarinFile = new MandarinFile(_core.Deencryptor, flavor);
        mandarinFile.SetFileData(Properties.Resources.decryptedFile);

        // Act
        mandarinFile.EncryptFile(_gamingPlatform.GetParsedUserIdOutput());
        mandarinFile.DecryptFile(_gamingPlatform.GetParsedUserIdInput());
        var resultData = mandarinFile.Data.AsSpan();

        // Assert
        Assert.Equal((ReadOnlySpan<byte>)resultData, Properties.Resources.decryptedFile);
    }
}