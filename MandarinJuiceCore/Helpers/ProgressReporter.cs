namespace MandarinJuiceCore.Helpers;

/// <summary>
/// Provides a mechanism for reporting progress updates as text messages and integer values to external handlers.
/// </summary>
/// <param name="text">An optional progress handler that receives status text updates. If null, no text progress will be reported.</param>
/// <param name="value">An optional progress handler that receives progress notifications as integer values. If null, progress updates are not reported externally.</param>
public class ProgressReporter(IProgress<string>? text = null, IProgress<int>? value = null)
{
    /// <summary>
    /// Initializes a new instance of the ProgressReporter class with an optional text progress handler.
    /// </summary>
    /// <param name="text">An optional progress handler that receives status text updates. If null, no text progress will be reported.</param>
    public ProgressReporter(IProgress<string>? text = null) : this(text, null) {}

    /// <summary>
    /// Initializes a new instance of the ProgressReporter class with an optional progress value handler.
    /// </summary>
    /// <param name="value">An optional progress handler that receives progress notifications as integer values. If null, progress updates are not reported externally.</param>
    public ProgressReporter(IProgress<int>? value = null) : this(null, value) {}

    /// <summary>
    /// Reports message and progress.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="progress"></param>
    public void Report(string message, int progress)
    {
        text?.Report(message);
        value?.Report(progress);
    }

    /// <summary>
    /// Reports message.
    /// </summary>
    /// <param name="message"></param>
    public void Report(string message)
        => text?.Report(message);

    /// <summary>
    /// Reports progress.
    /// </summary>
    /// <param name="progress"></param>
    public void Report(int progress)
        => value?.Report(progress);
}