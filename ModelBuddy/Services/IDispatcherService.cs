namespace ModelBuddy.Services;

/// <summary>
/// Abstraction for dispatching work to the UI thread.
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// Executes the specified action on the UI thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>True if the action was enqueued successfully; otherwise, false.</returns>
    bool TryEnqueue(Action action);

    /// <summary>
    /// Gets a value indicating whether the current thread has access to the UI thread.
    /// </summary>
    bool HasThreadAccess { get; }
}
