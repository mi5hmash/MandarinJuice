using MandarinJuiceCore.GamingPlatforms;
using MandarinJuiceCore.Helpers;
using MandarinJuiceCore.Infrastructure;
using MandarinJuiceCore.Models.DSSS.Mandarin;
using Mi5hmasH.Logger;

namespace MandarinJuiceCore;

public class Core(SimpleLogger logger, ProgressReporter progressReporter)
{
    /// <summary>
    /// Gets the MandarinDeencryptor instance used to decrypt Mandarin-encoded data.
    /// </summary>
    public MandarinDeencryptor Deencryptor { get; } = new();

    /// <summary>
    /// Holds a flavor of Mandarin file that should be used.
    /// </summary>
    public MandarinFileFlavor MandarinFileFlavor { get; set; } = MandarinFileFlavor.Default;

    /// <summary>
    /// Creates a new ParallelOptions instance configured with the specified cancellation token and an optimal degree of parallelism for the current environment.
    /// </summary>
    /// <param name="cts">The CancellationTokenSource whose token will be used to support cancellation of parallel operations.</param>
    /// <returns>A ParallelOptions object initialized with the provided cancellation token and a maximum degree of parallelism based on the number of available processors.</returns>
    private static ParallelOptions GetParallelOptions(CancellationTokenSource cts)
        => new()
        {
            CancellationToken = cts.Token,
            MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1)
        };
    
    /// <summary>
    /// Asynchronously decrypts all files in the specified input directory using the provided gaming platform context.
    /// </summary>
    /// <param name="inputDir">The path to the directory containing the files to decrypt.</param>
    /// <param name="gamingPlatform">An implementation of IGamingPlatform that provides platform-specific logic.</param>
    /// <param name="cts">A CancellationTokenSource used to cancel the decryption operation. If cancellation is requested, the operation will terminate early.</param>
    /// <returns>A Task that represents the asynchronous decryption operation.</returns>
    public async Task DecryptFilesAsync(string inputDir, IGamingPlatform gamingPlatform, CancellationTokenSource cts)
        => await Task.Run(() => DecryptFiles(inputDir, gamingPlatform, cts));

    /// <summary>
    /// Decrypts all encrypted files in the specified input directory and saves the decrypted files to a new output directory.
    /// </summary>
    /// <param name="inputDir">The path to the directory containing the files to be decrypted. Only files with the expected encrypted file extension are processed.</param>
    /// <param name="gamingPlatform">An implementation of IGamingPlatform that provides platform-specific logic.</param>
    /// <param name="cts">A CancellationTokenSource that can be used to cancel the decryption operation before completion.</param>
    public void DecryptFiles(string inputDir, IGamingPlatform gamingPlatform, CancellationTokenSource cts)
    {
        // GET FILES TO PROCESS
        var filesToProcess = Directory.GetFiles(inputDir, $"*{MandarinFile.FileExtension}", SearchOption.TopDirectoryOnly);
        if (filesToProcess.Length == 0) return;
        // DECRYPT
        logger.LogInfo($"Decrypting [{filesToProcess.Length}] files...");
        // Create a new folder in OUTPUT directory
        var outputDir = Directories.GetNewOutputDirectory("decrypted");
        Directory.CreateDirectory(outputDir);
        // User ID
        var userIdInput = gamingPlatform.GetParsedUserIdInput();
        // Setup parallel options
        var po = GetParallelOptions(cts);
        // Process files in parallel
        var progress = 0;
        try
        {
            Parallel.For((long)0, filesToProcess.Length, po, (ctr, _) =>
            {
                while (true)
                {
                    var fileName = Path.GetFileName(filesToProcess[ctr]);
                    var group = $"Task {ctr}";

                    // Try to read file data
                    byte[] data;
                    try { data = File.ReadAllBytes(filesToProcess[ctr]); }
                    catch (Exception ex)
                    {
                        logger.LogError($"[{progress}/{filesToProcess.Length}] Failed to read the [{fileName}] file: {ex}", group);
                        break; // Skip to the next file
                    }
                    // Process file data
                    var mandarinFile = new MandarinFile(Deencryptor, MandarinFileFlavor);
                    try
                    {
                        mandarinFile.SetFileData(data, true);
                        if (!mandarinFile.IsEncrypted)
                        {
                            logger.LogWarning($"[{progress}/{filesToProcess.Length}] The [{fileName}] file is not encrypted, skipping...", group);
                            break; // Skip to the next file
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"[{progress}/{filesToProcess.Length}] Failed to process the [{fileName}] file data: {ex}", group);
                        break; // Skip to the next file
                    }
                    // Try to decrypt file data
                    try
                    {
                        logger.LogInfo($"[{progress}/{filesToProcess.Length}] Decrypting the [{fileName}] file...", group);
                        mandarinFile.DecryptFile(userIdInput);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Failed to decrypt the file: {ex.Message}", group);
                        break; // Skip to the next file
                    }
                    // Try to save the decrypted file data
                    try
                    {
                        var outputFilePath = Path.Combine(outputDir, fileName);
                        ReadOnlySpan<byte> outputData = mandarinFile.Data.AsSpan();
                        File.WriteAllBytes(outputFilePath, outputData);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Failed to save the file: {ex}", group);
                        break; // Skip to the next file
                    }
                    logger.LogInfo($"[{progress}/{filesToProcess.Length}] Decrypted the [{fileName}] file.", group);
                    break;
                }
                Interlocked.Increment(ref progress);
                progressReporter.Report((int)((double)progress / filesToProcess.Length * 100));
            });
            logger.LogInfo($"[{progress}/{filesToProcess.Length}] All tasks completed.");
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex.Message);
        }
        finally
        {
            // Ensure progress is set to 100% at the end
            progressReporter.Report(100);
        }
    }

    /// <summary>
    /// Asynchronously encrypts all files in the specified input directory using the provided gaming platform context.
    /// </summary>
    /// <param name="inputDir">The path to the directory containing files to encrypt.</param>
    /// <param name="gamingPlatform">An implementation of IGamingPlatform that provides platform-specific logic.</param>
    /// <param name="cts">A CancellationTokenSource that can be used to cancel the encryption operation.</param>
    /// <returns>A task that represents the asynchronous encryption operation.</returns>
    public async Task EncryptFilesAsync(string inputDir, IGamingPlatform gamingPlatform, CancellationTokenSource cts)
        => await Task.Run(() => EncryptFiles(inputDir, gamingPlatform, cts));

    /// <summary>
    /// Encrypts all eligible files in the specified input directory using the current encryption settings.
    /// </summary>
    /// <param name="inputDir">The path to the directory containing files to be encrypted.</param>
    /// <param name="gamingPlatform">An implementation of IGamingPlatform that provides platform-specific logic.</param>
    /// <param name="cts">A CancellationTokenSource that can be used to cancel the encryption operation before completion.</param>
    public void EncryptFiles(string inputDir, IGamingPlatform gamingPlatform, CancellationTokenSource cts)
    {
        // GET FILES TO PROCESS
        var filesToProcess = Directory.GetFiles(inputDir, $"*{MandarinFile.FileExtension}", SearchOption.TopDirectoryOnly);
        if (filesToProcess.Length == 0) return;
        // ENCRYPT
        logger.LogInfo($"Encrypting [{filesToProcess.Length}] files...");
        // Create a new folder in OUTPUT directory
        var outputDir = Directories.GetNewOutputDirectory("encrypted").AddUserId(gamingPlatform.UserIdOutput);
        Directory.CreateDirectory(outputDir);
        // User ID
        var userIdOutput = gamingPlatform.GetParsedUserIdOutput();
        // Setup parallel options
        var po = GetParallelOptions(cts);
        // Process files in parallel
        var progress = 0;
        try
        {
            Parallel.For((long)0, filesToProcess.Length, po, (ctr, _) =>
            {
                while (true)
                {
                    var fileName = Path.GetFileName(filesToProcess[ctr]);
                    var group = $"Task {ctr}";

                    // Try to read file data
                    byte[] data;
                    try { data = File.ReadAllBytes(filesToProcess[ctr]); }
                    catch (Exception ex)
                    {
                        logger.LogError($"[{progress}/{filesToProcess.Length}] Failed to read the [{fileName}] file: {ex}", group);
                        break; // Skip to the next file
                    }
                    // Process file data
                    var mandarinFile = new MandarinFile(Deencryptor, MandarinFileFlavor);
                    try
                    {
                        mandarinFile.SetFileData(data);
                        if (mandarinFile.IsEncrypted)
                        {
                            logger.LogWarning($"[{progress}/{filesToProcess.Length}] The [{fileName}] file is already encrypted, skipping...", group);
                            break; // Skip to the next file
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"[{progress}/{filesToProcess.Length}] Failed to process the [{fileName}] file data: {ex}", group);
                        break; // Skip to the next file
                    }
                    // Try to encrypt file data
                    try
                    {
                        logger.LogInfo($"[{progress}/{filesToProcess.Length}] Encrypting the [{fileName}] file...", group);
                        mandarinFile.EncryptFile(userIdOutput);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Failed to encrypt the file: {ex.Message}", group);
                        break; // Skip to the next file
                    }
                    // Try to save the encrypted file data
                    try
                    {
                        var outputFilePath = Path.Combine(outputDir, fileName);
                        var outputData = mandarinFile.GetFileData();
                        File.WriteAllBytes(outputFilePath, outputData);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Failed to save the file: {ex}", group);
                        break; // Skip to the next file
                    }
                    logger.LogInfo($"[{progress}/{filesToProcess.Length}] Encrypted the [{fileName}] file.", group);
                    break;
                }
                Interlocked.Increment(ref progress);
                progressReporter.Report((int)((double)progress / filesToProcess.Length * 100));
            });
            logger.LogInfo($"[{progress}/{filesToProcess.Length}] All tasks completed.");
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex.Message);
        }
        finally
        {
            // Ensure progress is set to 100% at the end
            progressReporter.Report(100);
        }
    }

    /// <summary>
    /// Asynchronously re-signs all files in the specified input directory using the provided gaming platform context.
    /// </summary>
    /// <param name="inputDir">The path to the directory containing the files to be re-signed.</param>
    /// <param name="gamingPlatform">An implementation of IGamingPlatform that provides platform-specific logic.</param>
    /// <param name="cts">A CancellationTokenSource that can be used to cancel the operation before completion.</param>
    /// <returns>A task that represents the asynchronous re-signing operation.</returns>
    public async Task ResignFilesAsync(string inputDir, IGamingPlatform gamingPlatform, CancellationTokenSource cts)
        => await Task.Run(() => ResignFiles(inputDir, gamingPlatform, cts));

    /// <summary>
    /// Re-signs all files in the specified input directory by updating their user ID and encrypting them if necessary, then saves the processed files to a new output directory associated with the output user ID specified in the <paramref name="gamingPlatform"/>.
    /// </summary>
    /// <param name="inputDir">The path to the directory containing the files to be re-signed.</param>
    /// <param name="gamingPlatform">An implementation of IGamingPlatform that provides platform-specific logic.</param>
    /// <param name="cts">A CancellationTokenSource used to cancel the operation if needed. If cancellation is requested, the process will terminate early.</param>
    public void ResignFiles(string inputDir, IGamingPlatform gamingPlatform, CancellationTokenSource cts)
    {
        // GET FILES TO PROCESS
        var filesToProcess = Directory.GetFiles(inputDir, $"*{MandarinFile.FileExtension}", SearchOption.TopDirectoryOnly);
        if (filesToProcess.Length == 0) return;
        // RE-SIGN
        logger.LogInfo($"Re-signing [{filesToProcess.Length}] files...");
        // Create a new folder in OUTPUT directory
        var outputDir = Directories.GetNewOutputDirectory("resigned").AddUserId(gamingPlatform.UserIdOutput);
        Directory.CreateDirectory(outputDir);
        // User IDs
        var userIdInput = gamingPlatform.GetParsedUserIdInput();
        var userIdOutput = gamingPlatform.GetParsedUserIdOutput();
        // Setup parallel options
        var po = GetParallelOptions(cts);
        // Process files in parallel
        var progress = 0;
        try
        {
            Parallel.For((long)0, filesToProcess.Length, po, (ctr, _) =>
            {
                while (true)
                {
                    var fileName = Path.GetFileName(filesToProcess[ctr]);
                    var group = $"Task {ctr}";

                    // Try to read file data
                    byte[] data;
                    try { data = File.ReadAllBytes(filesToProcess[ctr]); }
                    catch (Exception ex)
                    {
                        logger.LogError($"[{progress}/{filesToProcess.Length}] Failed to read the [{fileName}] file: {ex}", group);
                        break; // Skip to the next file
                    }
                    // Process file data
                    var mandarinFile = new MandarinFile(Deencryptor, MandarinFileFlavor);
                    try { mandarinFile.SetFileData(data); }
                    catch (Exception ex)
                    {
                        logger.LogError($"[{progress}/{filesToProcess.Length}] Failed to process the [{fileName}] file data: {ex}", group);
                        break; // Skip to the next file
                    }
                    // Try to decrypt file data if encrypted
                    if (mandarinFile.IsEncrypted)
                    {
                        try
                        {
                            logger.LogInfo($"[{progress}/{filesToProcess.Length}] Decrypting the [{fileName}] file...", group);
                            mandarinFile.DecryptFile(userIdInput);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError($"Failed to decrypt the file: {ex.Message}", group);
                            break; // Skip to the next file
                        }
                    }
                    // Try to encrypt file data
                    try
                    {
                        logger.LogInfo($"[{progress}/{filesToProcess.Length}] Encrypting the [{fileName}] file...", group);
                        mandarinFile.EncryptFile(userIdOutput);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Failed to encrypt the file: {ex.Message}", group);
                        break; // Skip to the next file
                    }
                    // Try to save the encrypted file data
                    try
                    {
                        var outputFilePath = Path.Combine(outputDir, fileName);
                        var outputData = mandarinFile.GetFileData();
                        File.WriteAllBytes(outputFilePath, outputData);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Failed to save the file: {ex}", group);
                        break; // Skip to the next file
                    }
                    logger.LogInfo($"[{progress}/{filesToProcess.Length}] Re-signed the [{fileName}] file.", group);
                    break;
                }
                Interlocked.Increment(ref progress);
                progressReporter.Report((int)((double)progress / filesToProcess.Length * 100));
            });
            logger.LogInfo($"[{progress}/{filesToProcess.Length}] All tasks completed.");
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex.Message);
        }
        finally
        {
            // Ensure progress is set to 100% at the end
            progressReporter.Report(100);
        }
    }
}