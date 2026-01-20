using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using FBGRentora.MVVM.ViewModels;

namespace FBGRentora.Services.Navigation
{
    /// <summary>
    /// Implementation of INavigationService using dependency injection.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Stack<BaseViewModel> _navigationStack = new();
        private BaseViewModel? _currentViewModel;

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
            if (_currentViewModel != null)
            {
                _navigationStack.Push(_currentViewModel);
            }

            _currentViewModel = viewModel;
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
