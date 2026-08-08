using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Daryva.MVVM.ViewModels;
using Daryva.Services;

namespace Daryva.Services.Navigation
{
    /// <summary>
    /// Implementation of INavigationService using dependency injection.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Stack<BaseViewModel> _navigationStack = new();
        private BaseViewModel? _currentViewModel;
        private DateTime _lastNavigationAtUtc = DateTime.UtcNow;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public BaseViewModel? CurrentViewModel => _currentViewModel;

        public event EventHandler<BaseViewModel?>? CurrentViewModelChanged;

        public void NavigateTo<T>() where T : BaseViewModel
        {
            var viewModel = _serviceProvider.GetRequiredService<T>();
            NavigateTo(viewModel);
        }

        public void NavigateTo(BaseViewModel viewModel)
        {
            var now = DateTime.UtcNow;
            var sinceLastNavigation = now - _lastNavigationAtUtc;
            _lastNavigationAtUtc = now;
            AppLogger.Log("Navigation", $"{_currentViewModel?.GetType().Name ?? "(none)"} -> {viewModel.GetType().Name} " +
                $"(+{sinceLastNavigation.TotalMilliseconds:F0}ms since previous navigation)");

            if (_currentViewModel != null)
            {
                // The outgoing instance stops being "current" but a fresh transient instance of
                // the same type is what actually gets displayed if this page is visited again
                // (see NavigateTo<T>) -- so its own event subscriptions (org switch, etc.) must be
                // torn down here, otherwise it keeps reacting to app-wide events invisibly for the
                // rest of the session. See INavigationAware.
                if (_currentViewModel is INavigationAware navigationAware)
                    navigationAware.Cleanup();
                _currentViewModel.IsActive = false;
                _navigationStack.Push(_currentViewModel);
            }

            viewModel.IsActive = true;
            _currentViewModel = viewModel;
            OnCurrentViewModelChanged();
        }

        public void ClearStackAndCurrent()
        {
            _navigationStack.Clear();
            _currentViewModel = null;
            OnCurrentViewModelChanged();
        }

        public void NavigateBack()
        {
            if (_navigationStack.Count > 0)
            {
                _currentViewModel = _navigationStack.Pop();
                OnCurrentViewModelChanged();
            }
        }

        private void OnCurrentViewModelChanged()
        {
            CurrentViewModelChanged?.Invoke(this, _currentViewModel);
        }

        public T? GetViewModel<T>() where T : BaseViewModel
        {
            // Check current view model
            if (_currentViewModel is T current)
            {
                return current;
            }

            // Check navigation stack
            foreach (var vm in _navigationStack)
            {
                if (vm is T stackVm)
                {
                    return stackVm;
                }
            }

            return null;
        }
    }
}
