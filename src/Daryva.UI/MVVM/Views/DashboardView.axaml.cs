using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Daryva.MVVM.ViewModels;
using Daryva.MVVM.Views.Controls.Charts;

namespace Daryva.MVVM.Views;

public partial class DashboardView : UserControl
{
    private DashboardViewModel? _viewModel;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
    }

    // DonutChart.Segments needs a resolved IBrush per segment, which XAML can't produce from three
    // separate decimal bindings without a MultiBinding converter reaching into the resource tree --
    // simpler to build the list here, reading the same theme brushes the rest of the app already
    // uses via DynamicResource. Rebuilt whenever the ViewModel's breakdown changes; it does not
    // re-resolve if the user switches Light/Dark theme mid-session without also refreshing the
    // dashboard (a real, minor, and accepted limitation -- everything else on this page keeps
    // using DynamicResource directly and stays fully theme-reactive).
    private void AttachViewModel()
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as DashboardViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateRentCollectionSegments();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // RentCollectedPercent is always set last, alongside PaidAmount/PendingAmount/OverdueAmount,
        // in DashboardViewModel.UpdateDashboardProperties -- one property change signals the whole
        // breakdown is ready to rebuild.
        if (e.PropertyName == nameof(DashboardViewModel.RentCollectedPercent))
            UpdateRentCollectionSegments();
    }

    private void UpdateRentCollectionSegments()
    {
        if (_viewModel == null)
            return;

        var donut = this.FindControl<DonutChart>("RentCollectionDonut");
        if (donut == null)
            return;

        donut.Segments = new List<DonutChartSegment>
        {
            new("Paid", _viewModel.RentCollectedPaidAmount, ResolveBrush("StatusSuccessBrush", Brushes.Green)),
            new("Pending", _viewModel.RentCollectedPendingAmount, ResolveBrush("StatusWarningBrush", Brushes.Orange)),
            new("Overdue", _viewModel.RentCollectedOverdueAmount, ResolveBrush("StatusDangerBrush", Brushes.Red)),
        };
    }

    private IBrush ResolveBrush(string resourceKey, IBrush fallback)
    {
        return this.TryGetResource(resourceKey, ThemeVariant.Default, out var value) && value is IBrush brush
            ? brush
            : fallback;
    }
}
