namespace Daryva.Services;

/// <summary>
/// Coalesces rapid repeated triggers into a single delayed action -- each call to Trigger cancels
/// any pending one and restarts the delay, so only the last trigger within a burst actually runs.
/// Built for IOrgContext.CurrentOrgChanged: switching organisation twice in quick succession used
/// to fire a full page data reload (several API calls each) for the first switch, then again for
/// the second, and rapid tab navigation on top of that could pile up enough concurrent requests to
/// trip the backend's rate limiter. Debouncing the reload itself means a quick flurry of switches
/// only ever reloads once, for the org the user actually settled on.
/// </summary>
public sealed class AsyncDebouncer
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cts;

    public AsyncDebouncer(TimeSpan delay)
    {
        _delay = delay;
    }

    public void Trigger(Action action)
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = RunAsync(action, cts.Token);
    }

    private async Task RunAsync(Action action, CancellationToken token)
    {
        try
        {
            await Task.Delay(_delay, token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (!token.IsCancellationRequested)
            action();
    }
}
