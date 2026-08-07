using Material.Icons;

namespace Daryva.MVVM.Models
{
    /// <summary>
    /// Represents a navigation item in the main navigation rail.
    /// </summary>
    public class NavigationItem
    {
        /// <summary>
        /// Gets or sets the title of the navigation item.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the icon for the navigation item.
        /// </summary>
        public MaterialIconKind Icon { get; set; }

        /// <summary>
        /// Gets or sets the type of the ViewModel associated with this navigation item.
        /// </summary>
        public Type? ViewModelType { get; set; }
    }
}
