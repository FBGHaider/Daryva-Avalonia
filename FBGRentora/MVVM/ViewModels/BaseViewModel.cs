using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FBGRentora.MVVM.ViewModels
{
    /// <summary>
    /// Base class for ViewModels that implements INotifyPropertyChanged.
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets the property value and raises PropertyChanged if the value has changed.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="field">The backing field for the property.</param>
        /// <param name="value">The new value for the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>True if the value was changed; otherwise, false.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Sets the property value and raises PropertyChanged if the value has changed, with additional action.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="field">The backing field for the property.</param>
        /// <param name="value">The new value for the property.</param>
        /// <param name="onChanged">Action to execute after the property has been set.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>True if the value was changed; otherwise, false.</returns>
        protected bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Sets the property value and raises PropertyChanged if the value has changed, with additional action that receives the new value.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="field">The backing field for the property.</param>
        /// <param name="value">The new value for the property.</param>
        /// <param name="onChanged">Action to execute after the property has been set, receiving the new value.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>True if the value was changed; otherwise, false.</returns>
        protected bool SetProperty<T>(ref T field, T value, Action<T> onChanged, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            T oldValue = field;
            field = value;
            onChanged?.Invoke(value);
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
