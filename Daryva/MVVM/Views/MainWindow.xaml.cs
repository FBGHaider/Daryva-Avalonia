using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Daryva.MVVM.ViewModels;

namespace Daryva
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        /// <param name="viewModel">The ViewModel for this view.</param>
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            // Set window icon programmatically as fallback
            SetWindowIcon();
            
            // Handle window closing to cleanup
            Closing += MainWindow_Closing;
            
            // Update maximize button icon based on window state
            StateChanged += MainWindow_StateChanged;
            
            // Set up rounded corners clipping
            Loaded += MainWindow_Loaded;
            UpdateMaximizeButton();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cleanup ViewModel resources
            if (DataContext is MainWindowViewModel mainViewModel)
            {
                // Cleanup current ViewModel if it has cleanup
                if (mainViewModel.CurrentViewModel is DashboardViewModel dashboardVm)
                {
                    dashboardVm.Cleanup();
                }
                
                mainViewModel.Cleanup();
            }
        }

        private void SetWindowIcon()
        {
            try
            {
                // Try Assets/Logo/logo.png first (as requested), then other options
                var iconPaths = new[]
                {
                    ("Assets/Logo", "logo.png"),
                    ("Assets/Logo", "FBG_App_Icon_MAX.ico"),
                    ("Assets/Logo", "FBG_App_Icon.ico"),
                    ("Assets/Logo", "main_logo.ico"),
                    ("Assets", "main_logo.ico"),
                    ("Assets", "main_logo.png"),
                    ("Assets/Logo", "main_logo.png"),
                    ("Assets/Logo", "main_logo_transparent.ico"),
                    ("Assets/Logo", "main_logo_transparent.png")
                };
                
                foreach (var (folder, iconFile) in iconPaths)
                {
                    try
                    {
                        // Try pack URI first
                        var packUri = new Uri($"pack://application:,,,/{folder}/{iconFile}", UriKind.Absolute);
                        var bitmap = new BitmapImage(packUri);
                        Icon = bitmap;
                        return; // Success, exit
                    }
                    catch
                    {
                        try
                        {
                            // Fallback: Try relative path from executable directory
                            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                            var exeDir = Path.GetDirectoryName(exePath);
                            var iconPath = Path.Combine(exeDir ?? "", folder, iconFile);
                            
                            if (File.Exists(iconPath))
                            {
                                var bitmap = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                                Icon = bitmap;
                                return; // Success, exit
                            }
                        }
                        catch
                        {
                            // Continue to next file
                        }
                    }
                }
            }
            catch
            {
                // If all else fails, window will use default icon
            }
        }
        
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Double-click to maximize/restore
                MaximizeButton_Click(sender, e);
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Drag window - only if not maximized
                if (WindowState != WindowState.Maximized)
                {
                    try
                    {
                        DragMove();
                    }
                    catch
                    {
                        // Ignore drag errors (can happen during rapid movements)
                    }
                }
            }
        }
        
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateMaximizeButton();
        }
        
        private void UpdateMaximizeButton()
        {
            if (MaximizeButton != null)
            {
                if (WindowState == WindowState.Maximized)
                    MaximizeButton.Content = "\uE923"; // Restore icon
                else
                    MaximizeButton.Content = "\uE922"; // Maximize icon
            }
        }
        
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Create rounded corner clipping for the main border
            var mainBorder = this.FindName("MainBorder") as System.Windows.Controls.Border;
            if (mainBorder != null)
            {
                UpdateRoundedCorners(mainBorder);
                
                // Update on size change
                SizeChanged += (s, args) => UpdateRoundedCorners(mainBorder);
            }
        }
        
        private void UpdateRoundedCorners(System.Windows.Controls.Border border)
        {
            if (border != null && border.ActualWidth > 0 && border.ActualHeight > 0)
            {
                var rect = new System.Windows.Rect(0, 0, border.ActualWidth, border.ActualHeight);
                var geometry = new System.Windows.Media.RectangleGeometry(rect, 12, 12); // Windows 11 style corners
                border.Clip = geometry;
            }
        }
    }
}