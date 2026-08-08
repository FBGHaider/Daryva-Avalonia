namespace Daryva.Services.Navigation
{
    /// <summary>
    /// Implemented by page ViewModels that subscribe to app-wide events (e.g.
    /// IOrgContext.CurrentOrgChanged) in their constructor. Every sidebar click creates a brand
    /// new transient instance via NavigationService.NavigateTo -- without this hook, the outgoing
    /// instance's subscriptions are never removed, so it keeps reacting to those events for the
    /// rest of the app's life alongside the new instance. Left unchecked, this compounds every
    /// time a page is revisited and was the cause of the "many request errors" / lost tab data
    /// seen after switching organisations back and forth: each stale instance fired its own
    /// duplicate load call for every org switch.
    /// </summary>
    public interface INavigationAware
    {
        void Cleanup();
    }
}
