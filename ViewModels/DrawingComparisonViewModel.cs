using System.ComponentModel;
using System.Runtime.CompilerServices;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.ViewModels;

public sealed class DrawingComparisonViewModel : INotifyPropertyChanged
{
    private string _status = "Ready";
    private bool _isProcessing;
    private DrawingViewMode _viewMode = DrawingViewMode.Overlay;
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public bool IsProcessing { get => _isProcessing; set { _isProcessing = value; OnPropertyChanged(); } }
    public DrawingViewMode ViewMode { get => _viewMode; set { _viewMode = value; OnPropertyChanged(); } }
    public DifferenceResult? Difference { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
