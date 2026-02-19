namespace Daryva.Services.Navigation
{
    /// <summary>
    /// Service for navigating between ViewModels in the application.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Navigates to the specified ViewModel type.
        /// </summary>
        void NavigateTo<T>() where T : MVVM.ViewModels.BaseViewModel;

        /// <summary>
        /// Navigates to the specified ViewModel instance.
        /// </summary>
        void NavigateTo(MVVM.ViewModels.BaseViewModel viewModel);

        /// <summary>
        /// Navigates back to the previous ViewModel.
        /// </summary>
        void NavigateBack();

        /// <summary>
        /// Gets the current ViewModel.
        /// </summary>
        MVVM.ViewModels.BaseViewModel? CurrentViewModel { get; }

        /// <summary>
        /// Event raised when the current ViewModel changes.
        /// </summary>
        event EventHandler<MVVM.ViewModels.BaseViewModel?>? CurrentViewModelChanged;

        /// <summary>
        /// Gets a ViewModel of the specified type from the navigation stack or current view.
        /// </summary>
        T? GetViewModel<T>() where T : MVVM.ViewModels.BaseViewModel;
    }
}
