using Microsoft.UI.Dispatching;

namespace ModelBuddy.Services;

/// <summary>
/// Implementation of <see cref="IDispatcherService"/> using WinUI 3's DispatcherQueue.
/// </summary>
public sealed class DispatcherService : IDispatcherService
{
    private readonly DispatcherQueue _dispatcherQueue;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatcherService"/> class.
    /// </summary>
    /// <param name="dispatcherQueue">The dispatcher queue to use.</param>
    public DispatcherService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
    }

    /// <inheritdoc/>
    public bool HasThreadAccess => _dispatcherQueue.HasThreadAccess;

    /// <inheritdoc/>
    public bool TryEnqueue(Action action)
    {
        if (action is null)
        {
            return false;
        }

        return _dispatcherQueue.TryEnqueue(() => action());
    }
}
