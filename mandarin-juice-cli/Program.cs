using MandarinJuiceCore;
using MandarinJuiceCore.GameProfile;
using MandarinJuiceCore.GamingPlatforms;
using MandarinJuiceCore.Helpers;
using MandarinJuiceCore.Infrastructure;
using Mi5hmasH.AppInfo;
using Mi5hmasH.ConsoleHelper;
using Mi5hmasH.GameProfile;
using Mi5hmasH.Logger;
using Mi5hmasH.Logger.Models;
using Mi5hmasH.Logger.Providers;

#region SETUP

// CONSTANTS
const string breakLine = "---";

// Initialize APP_INFO
var appInfo = new MyAppInfo("mandarin-juice-cli");

// Initialize LOGGER
var logger = new SimpleLogger
{
    LoggedAppName = appInfo.Name
};
// Configure ConsoleLogProvider
var consoleLogProvider = new ConsoleLogProvider();
logger.AddProvider(consoleLogProvider);
// Configure FileLogProvider
var fileLogProvider = new FileLogProvider(MyAppInfo.RootPath, 2);
fileLogProvider.CreateLogFile();
logger.AddProvider(fileLogProvider);
// Add event handler for unhandled exceptions
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    if (e.ExceptionObject is not Exception exception) return;
    var logEntry = new LogEntry(SimpleLogger.LogSeverity.Critical, $"Unhandled Exception: {exception}");
    fileLogProvider.Log(logEntry);
    fileLogProvider.Flush();
};
// Flush log providers on process exit
AppDomain.CurrentDomain.ProcessExit += (_, _) => logger.Flush();

//Initialize ProgressReporter
var progressReporter = new ProgressReporter(new Progress<string>(Console.WriteLine), null);

// Initialize CORE
var core = new Core(logger, progressReporter);

// Initialize Gaming Platform
IGamingPlatform gamingPlatform;

// Print HEADER
ConsoleHelper.PrintHeader(appInfo, breakLine);

// Say HELLO
ConsoleHelper.SayHello(breakLine);

// Get ARGUMENTS from command line
#if DEBUG
// For debugging purposes, you can manually set the arguments...
if (args.Length < 1)
{
    // ...below
    const string localArgs = "-m TEST";
    args = ConsoleHelper.GetArgs(localArgs);
}
#endif
var arguments = ConsoleHelper.ReadArguments(args);
#if DEBUG
// Write the arguments to the console for debugging purposes
ConsoleHelper.WriteArguments(arguments);
Console.WriteLine(breakLine);
#endif

#endregion

#region MAIN

// Optional argument: doNotWait
var doNotWait = arguments.ContainsKey("-q");

// Show HELP if no arguments are provided or if -h is provided
if (arguments.Count == 0 || arguments.ContainsKey("-h"))
{
    PrintHelp();
    goto EXIT;
}

// Get MODE
arguments.TryGetValue("-m", out var mode);
switch (mode)
{
    case "encrypt" or "e":
        await EncryptAll();
        break;
    case "decrypt" or "d":
        await DecryptAll();
        break;
    case "resign" or "r":
        await ResignAll();
        break;
    case "bruteforce" or "b":
        await BruteforceFirst();
        break;
    default:
        throw new ArgumentException($"Unknown mode: '{mode}'.");
}

// EXIT the application
EXIT:
Console.WriteLine(breakLine); // print a break line
ConsoleHelper.SayGoodbye(breakLine);
if (!doNotWait) ConsoleHelper.PressAnyKeyToExit();
return;

#endregion

#region HELPERS

static void PrintHelp()
{
    const string userIdInput = "76561197960265729";
    const string userIdOutput = "76561197960265730";
    var inputPath = Path.Combine(".", "InputDirectory");
    var gameProfilePath = Path.Combine(".", "game_profile.bin");
    var exeName = Path.Combine(".", Path.GetFileName(Environment.ProcessPath) ?? "ThisExecutableFileName.exe");
    var helpMessage = $"""
                       Usage: {exeName} -m <mode> [options]

                       Modes:
                         -m d  Decrypt SaveData files
                         -m e  Encrypt SaveData files
                         -m r  Re-sign SaveData files
                         -m b  Bruteforce User ID for the first file in the input directory

                       Options:
                         -g <game_profile_path>  Path to the Game Profile file
                         -p <path>               Path to folder containing SaveData files
                         -u <user_id>            User ID (used in decrypt/encrypt modes)
                         -uI <old_id>            Original User ID (used in re-sign mode)
                         -uO <new_id>            New User ID (used in re-sign mode)
                         -q                      Don't wait for user input to exit after operation completes (auto-close)
                         -h                      Show this help message

                       Examples:
                         Decrypt:  {exeName} -m d -g "{gameProfilePath}" -p "{inputPath}" -u {userIdInput}
                         Encrypt:  {exeName} -m e -g "{gameProfilePath}" -p "{inputPath}" -u {userIdOutput}
                         Re-sign:  {exeName} -m r -g "{gameProfilePath}" -p "{inputPath}" -uI {userIdInput} -uO {userIdOutput}
                         Bruteforce:  {exeName} -m b -g "{gameProfilePath}" -p "{inputPath}"
                       """;
    Console.WriteLine(helpMessage);
}

string GetValidatedInputRootPath()
{
    arguments.TryGetValue("-p", out var inputRootPath);
    if (File.Exists(inputRootPath)) inputRootPath = Path.GetDirectoryName(inputRootPath);
    return !Directory.Exists(inputRootPath)
        ? throw new DirectoryNotFoundException($"The provided path '{inputRootPath}' is not a valid directory or does not exist.")
        : inputRootPath;
}

void LoadGameProfile()
{
    arguments.TryGetValue("-g", out var gameProfilePath);
    if (string.IsNullOrEmpty(gameProfilePath))
        throw new ArgumentException("Game Profile file is missing.");
    var gameProfileManager = new GameProfileManager<MandarinGameProfile>();
    gameProfileManager.SetEncryptor(Keychain.GpMagic);
    gameProfileManager.Load(gameProfilePath);
    // Copy MandarinFileFlavor to Core
    core.MandarinFileFlavor = gameProfileManager.GameProfile.MandarinFileFlavor;
    // Configure Deencryptor
    core.Deencryptor.MandarinSeed = gameProfileManager.GameProfile.MandarinSeed;
    // Set GamingPlatform
    gamingPlatform = GamingPlatformHelper.GetGamingPlatform(gameProfileManager.GameProfile.Platform);
    gamingPlatform.ParseVariant = gameProfileManager.GameProfile.ParseVariant;
}

#endregion

#region MODES

async Task DecryptAll()
{
    var cts = new CancellationTokenSource();
    arguments.TryGetValue("-u", out var userId);
    if (string.IsNullOrEmpty(userId))
        throw new ArgumentException("Input User ID is missing.");
    var inputRootPath = GetValidatedInputRootPath();
    LoadGameProfile();
    // Set User ID
    gamingPlatform.UserIdInput = userId;
    // Decrypt Files
    await core.DecryptFilesAsync(inputRootPath, gamingPlatform, cts);
    cts.Dispose();
}

async Task EncryptAll()
{
    var cts = new CancellationTokenSource();
    arguments.TryGetValue("-u", out var userId);
    if (string.IsNullOrEmpty(userId))
        throw new ArgumentException("Output User ID is missing.");
    var inputRootPath = GetValidatedInputRootPath();
    LoadGameProfile();
    // Set User ID
    gamingPlatform.UserIdOutput = userId;
    // Encrypt Files
    await core.EncryptFilesAsync(inputRootPath, gamingPlatform, cts);
    cts.Dispose();
}

async Task ResignAll()
{
    var cts = new CancellationTokenSource();
    arguments.TryGetValue("-uI", out var userIdInput);
    if (string.IsNullOrEmpty(userIdInput))
        throw new ArgumentException("Input User ID is missing.");
    arguments.TryGetValue("-uO", out var userIdOutput);
    if (string.IsNullOrEmpty(userIdOutput))
        throw new ArgumentException("Output User ID is missing.");
    var inputRootPath = GetValidatedInputRootPath();
    LoadGameProfile();
    // Set User IDs
    gamingPlatform.UserIdInput = userIdInput;
    gamingPlatform.UserIdOutput = userIdOutput;
    // Re-sign Files
    await core.ResignFilesAsync(inputRootPath, gamingPlatform, cts);
    cts.Dispose();
}

async Task BruteforceFirst()
{
    var cts = new CancellationTokenSource();
    var inputRootPath = GetValidatedInputRootPath();
    LoadGameProfile();
    // Bruteforce first file in the input directory
    await core.BruteforceUserIdAsync(inputRootPath, gamingPlatform, cts);
    cts.Dispose();
}

#endregion