namespace Evently.Common.Infrastructure.Messaging;

// Ends a lane's idle wait in this instance as soon as a message for it is stored here; other instances find it at
// their next poll.
public abstract class LaneWakeUps(
    IEnumerable<string> lanes)
{
    private readonly Dictionary<string, SemaphoreSlim> _wakeUps =
        lanes.ToDictionary(lane => lane, _ => new SemaphoreSlim(0, 1));

    public void WakeUp(string lane)
    {
        SemaphoreSlim? wakeUp = _wakeUps.GetValueOrDefault(lane);

        if (wakeUp is null || wakeUp.CurrentCount > 0)
        {
            return;
        }

        try
        {
            wakeUp.Release();
        }
        catch (SemaphoreFullException)
        {
            // Another writer woke the lane between the check and the release.
        }
    }

    // True when the lane was woken; false when the wait ran out or the instance is stopping.
    public async Task<bool> WaitAsync(
        string lane,
        TimeSpan wait,
        CancellationToken stoppingToken)
    {
        using var timeout = new CancellationTokenSource(wait);

        using var timeoutOrStopping = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, stoppingToken);

        try
        {
            await _wakeUps[lane].WaitAsync(timeoutOrStopping.Token);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
