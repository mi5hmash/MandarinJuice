using MandarinJuiceCore.GamingPlatformsFactory;
using MandarinJuiceCore.Helpers;
using MandarinJuiceCore.Infrastructure;
using MandarinJuiceCore.Models.DSSS.Mandarin;
using Mi5hmasH.Logger;
using Mi5hmasH.Progress;

namespace MandarinJuiceCore;

public sealed class Core(SimpleLogger logger, ProgressReporter progressReporter)
{
    /// <summary>
    /// Gets the MandarinDeencryptor instance used to decrypt Mandarin-encoded data.
    /// </summary>
    public MandarinDeencryptor Deencryptor { get; } = new();

    /// <summary>
    /// Holds a flavor of Mandarin file that should be used.
    /// </summary>
    public MandarinFileFlavorEnum MandarinFileFlavor { get; set; } = MandarinFileFlavorEnum.Default;

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
    /// Marks the progress reporting as complete by reporting 100% progress.
    /// </summary>
    /// <param name="progressTracker">The progress tracker used to report progress.</param>
    /// <param name="errorCounter">The error counter used to report errors.</param>
    private void LogAllTasksCompleted(ProgressTracker progressTracker, ErrorCounter errorCounter)
        => logger.LogInfo($"{progressTracker} All tasks completed. {errorCounter}");

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
        string[] filesToProcess;
        try { filesToProcess = SaveDataFileIo.GetFiles(inputDir); }
        catch (Exception ex)
        {
            logger.LogWarning(ex.Message);
            return;
        }
        // INITIALIZE PROGRESS TRACKER
        var progressTracker = new ProgressTracker(filesToProcess.Length);
        var errorCounter = new ErrorCounter(logger);
        // DECRYPT
        logger.LogInfo($"Decrypting [{progressTracker.Total}] files...");
        // User ID
        ulong userIdInput;
        try { userIdInput = gamingPlatform.GetParsedUserIdInput(); }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return;
        }
        // Create a new folder in OUTPUT directory
        var outputDir = Directories.GetNewOutputDirectory("decrypted");
        Directory.CreateDirectory(outputDir);
        // Setup parallel options
        var po = GetParallelOptions(cts);
        // Process files in parallel
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
                        errorCounter.AddError($"{progressTracker} Failed to read the [{fileName}] file: {ex}", group);
                        break;
                    }
                    // Process file data
                    var mandarinFile = new MandarinFile(Deencryptor, MandarinFileFlavor);
                    try
                    {
                        mandarinFile.SetFileData(data, true);
                        if (!mandarinFile.IsEncrypted)
                        {
                            errorCounter.AddWarning($"{progressTracker} The [{fileName}] file is not encrypted, skipping...", group);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCounter.AddError($"{progressTracker} Failed to process the [{fileName}] file data: {ex}", group);
                        break;
                    }
                    // Try to decrypt file data
                    try
                    {
                        logger.LogInfo($"{progressTracker} Decrypting the [{fileName}] file...", group);
                        mandarinFile.DecryptFile(userIdInput);
                    }
                    catch (Exception ex)
                    {
                        errorCounter.AddError($"{progressTracker} Failed to decrypt the file: {ex.Message}", group);
                        break;
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
                        errorCounter.AddError($"{progressTracker} Failed to save the file: {ex}", group);
                        break;
                    }
                    logger.LogInfo($"{progressTracker} Decrypted the [{fileName}] file.", group);
                    break;
                }
                progressTracker.Increment();
                progressReporter.Report(progressTracker.Percentage);
            });
            LogAllTasksCompleted(progressTracker, errorCounter);
        }
        catch (OperationCanceledException ex)
        {
            errorCounter.AddWarning(ex.Message);
        }
        finally
        {
            progressReporter.Complete();
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
        string[] filesToProcess;
        try { filesToProcess = SaveDataFileIo.GetFiles(inputDir); }
        catch (Exception ex)
        {
            logger.LogWarning(ex.Message);
            return;
        }
        // INITIALIZE PROGRESS TRACKER
        var progressTracker = new ProgressTracker(filesToProcess.Length);
        var errorCounter = new ErrorCounter(logger);
        // ENCRYPT
        logger.LogInfo($"Encrypting [{progressTracker.Total}] files...");
        // User ID
        ulong userIdOutput;
        try { userIdOutput = gamingPlatform.GetParsedUserIdOutput(); }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return;
        }
        // Create a new folder in OUTPUT directory
        var outputDir = Directories.GetNewOutputDirectory("encrypted").AddUserId(gamingPlatform.UserIdOutput);
        Directory.CreateDirectory(outputDir);
        // Setup parallel options
        var po = GetParallelOptions(cts);
        // Process files in parallel
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
                        errorCounter.AddError($"{progressTracker} Failed to read the [{fileName}] file: {ex}", group);
                        break;
                    }
                    // Process file data
                    var mandarinFile = new MandarinFile(Deencryptor, MandarinFileFlavor);
                    try
                    {
                        mandarinFile.SetFileData(data);
                        if (mandarinFile.IsEncrypted)
                        {
                            errorCounter.AddWarning($"{progressTracker} The [{fileName}] file is already encrypted, skipping...", group);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCounter.AddError($"{progressTracker} Failed to process the [{fileName}] file data: {ex}", group);
                        break;
                    }
                    // Try to encrypt file data
                    try
                    {
                        logger.LogInfo($"{progressTracker} Encrypting the [{fileName}] file...", group);
                        mandarinFile.EncryptFile(userIdOutput);
                    }
                    catch (Exception ex)
                    {
                        errorCounter.AddError($"{progressTracker} Failed to encrypt the file: {ex.Message}", group);
                        break;
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
                        errorCounter.AddError($"{progressTracker} Failed to save the file: {ex}", group);
                        break;
                    }
                    logger.LogInfo($"{progressTracker} Encrypted the [{fileName}] file.", group);
                    break;
                }
                progressTracker.Increment();
                progressReporter.Report(progressTracker.Percentage);
            });
            LogAllTasksCompleted(progressTracker, errorCounter);
        }
        catch (OperationCanceledException ex)
        {
            errorCounter.AddWarning(ex.Message);
        }
        finally
        {
            progressReporter.Complete();
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
        string[] filesToProcess;
        try { filesToProcess = SaveDataFileIo.GetFiles(inputDir); }
        catch (Exception ex)
        {
            logger.LogWarning(ex.Message);
            return;
        }
        // INITIALIZE PROGRESS TRACKER
        var progressTracker = new ProgressTracker(filesToProcess.Length);
        var errorCounter = new ErrorCounter(logger);
        // RE-SIGN
        logger.LogInfo($"Re-signing [{progressTracker.Total}] files...");
        // Input User ID
        ulong userIdInput;
        try { userIdInput = gamingPlatform.GetParsedUserIdInput(); }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return;
        }
        // Output User ID
        ulong userIdOutput;
        try { userIdOutput = gamingPlatform.GetParsedUserIdOutput(); }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return;
        }
        // Create a new folder in OUTPUT directory
        var outputDir = Directories.GetNewOutputDirectory("resigned").AddUserId(gamingPlatform.UserIdOutput);
        Directory.CreateDirectory(outputDir);
        // Setup parallel options
        var po = GetParallelOptions(cts);
        // Process files in parallel
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
                        errorCounter.AddError($"{progressTracker} Failed to read the [{fileName}] file: {ex}", group);
                        break;
                    }
                    // Process file data
                    var mandarinFile = new MandarinFile(Deencryptor, MandarinFileFlavor);
                    try { mandarinFile.SetFileData(data); }
                    catch (Exception ex)
                    {
                        errorCounter.AddError($"{progressTracker} Failed to process the [{fileName}] file data: {ex}", group);
                        break;
                    }
                    // Try to decrypt file data if encrypted
                    if (mandarinFile.IsEncrypted)
                    {
                        try
                        {
                            logger.LogInfo($"{progressTracker} Decrypting the [{fileName}] file...", group);
                            mandarinFile.DecryptFile(userIdInput);
                        }
                        catch (Exception ex)
                        {
                            errorCounter.AddError($"{progressTracker} Failed to decrypt the file: {ex.Message}", group);
                            break;
                        }
                    }
                    // Try to encrypt file data
                    try
                    {
                        logger.LogInfo($"{progressTracker} Encrypting the [{fileName}] file...", group);
                        mandarinFile.EncryptFile(userIdOutput);
                    }
                    catch (Exception ex)
                    {
                        errorCounter.AddError($"{progressTracker} Failed to encrypt the file: {ex.Message}", group);
                        break;
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
                        errorCounter.AddError($"{progressTracker} Failed to save the file: {ex}", group);
                        break;
                    }
                    logger.LogInfo($"{progressTracker} Re-signed the [{fileName}] file.", group);
                    break;
                }
                progressTracker.Increment();
                progressReporter.Report(progressTracker.Percentage);
            });
            LogAllTasksCompleted(progressTracker, errorCounter);
        }
        catch (OperationCanceledException ex)
        {
            errorCounter.AddWarning(ex.Message);
        }
        finally
        {
            progressReporter.Complete();
        }
    }

    /// <summary>
    /// Asynchronously brute-forces the UserID of an encrypted file by iterating through all possible UserID values and checking if the decrypted data matches the expected format for the specified gaming platform.
    /// </summary>
    /// <param name="inputPath">The path to the file or directory to process.</param>
    /// <param name="gamingPlatform">The gaming platform implementation used to parse and set the user ID during the brute-force operation.</param>
    /// <param name="cts">A cancellation token source that can be used to cancel the brute-force operation.</param>
    /// <returns>The UserID if found; otherwise, <see langword="null"/>.</returns>
    public async Task<uint?> BruteforceUserIdAsync(string inputPath, IGamingPlatform gamingPlatform,
        CancellationTokenSource cts)
        => await Task.Run(() => BruteforceUserId(inputPath, gamingPlatform, cts));

    /// <summary>
    /// Brute-forces the UserID of an encrypted file by iterating through all possible UserID values and checking if the decrypted data matches the expected format for the specified gaming platform.
    /// </summary>
    /// <param name="inputPath">The path to the file or directory to process.</param>
    /// <param name="gamingPlatform">The gaming platform implementation used to parse and set the user ID during the brute-force operation.</param>
    /// <param name="cts">A cancellation token source that can be used to cancel the brute-force operation.</param>
    /// <returns>The UserID if found; otherwise, <see langword="null"/>.</returns>
    public uint? BruteforceUserId(string inputPath, IGamingPlatform gamingPlatform, CancellationTokenSource cts)
    {
        uint? result = null;
        // GET FILE TO PROCESS AND READ IT
        string fileToProcess;
        byte[] data;
        try
        {
            fileToProcess = SaveDataFileIo.GetFile(inputPath);
            data = File.ReadAllBytes(fileToProcess);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return result;
        }
        var fileName = Path.GetFileName(fileToProcess);
        // Process file data
        var mandarinFile = new MandarinFile(Deencryptor, MandarinFileFlavor);
        try { mandarinFile.SetFileData(data); }
        catch (Exception ex)
        {
            logger.LogError($"Failed to process the [{fileName}] file data: {ex}");
            return result;
        }
        // Check if file is encrypted
        if (!mandarinFile.IsEncrypted)
        {
            logger.LogError($"File [{fileName}] is not encrypted so it there is nothing to brute-force.");
            return result;
        }
        // INITIALIZE PROGRESS TRACKER
        var progressTracker = new ProgressTracker(100);
        var errorCounter = new ErrorCounter(logger);
        // BRUTE-FORCE
        const uint step = uint.MaxValue / 100;
        // Get state and target mask
        mandarinFile.GetStateAndTargetMask(out var mandarinState, out var targetMask);
        // Setup parallel options
        var po = GetParallelOptions(cts);
        // Brute-force data in parallel
        try
        {
            Parallel.For(0, uint.MaxValue, po, (ctr, state) =>
            {
                while (true)
                {
                    if (ctr % step == 0)
                    {
                        progressReporter.Report($"{progressTracker} Brute-forcing UserID of {fileName}...", progressTracker.Percentage);
                        progressTracker.Increment();
                    }
                    var userId = (uint)ctr;
                    var parsedUserId = gamingPlatform.ParseUserId(userId) + mandarinState;
                    if (!MandarinDeencryptor.TryParsedUserId(parsedUserId, targetMask)) break;
                    gamingPlatform.UserIdInput = userId.ToString();
                    result = userId;
                    state.Stop();
                    break;
                }
            });
            if (result is null) 
                throw new Exception("UserID not found.");
            
            logger.LogInfo($"Found UserID: {result}.");
        }
        catch (OperationCanceledException ex)
        {
            errorCounter.AddWarning(ex.Message);
        }
        finally
        {
            progressReporter.Complete();
        }

        return result;
    }
}