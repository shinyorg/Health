using System.Collections.ObjectModel;

namespace Sample;


public record ObserveEntry(DateTimeOffset Timestamp, string Value, string TimeRange);


[ShellMap<ObservePage>(registerRoute: false)]
public partial class ObserveViewModel(IHealthService health) : ObservableObject
{
    CancellationTokenSource? cts;

    public List<string> DataTypes { get; } = Enum.GetNames<DataType>().ToList();

    [ObservableProperty]
    string selectedDataType = nameof(DataType.HeartRate);

    [ObservableProperty]
    bool isObserving;

    [ObservableProperty]
    string? errorText;

    [ObservableProperty]
    string statusText = "";

    [ObservableProperty]
    string resultsHeader = "Results (0)";

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    public ObservableCollection<ObserveEntry> Results { get; } = [];


    [RelayCommand]
    async Task StartAsync()
    {
        if (IsObserving)
            return;

        ErrorText = null;
        OnPropertyChanged(nameof(HasError));
        Results.Clear();
        ResultsHeader = "Results (0)";

        var dataType = Enum.Parse<DataType>(SelectedDataType);

        try
        {
            await health.RequestPermissions(dataType);
        }
        catch (Exception ex)
        {
            ErrorText = $"Permission error: {ex.Message}";
            OnPropertyChanged(nameof(HasError));
            return;
        }

        cts = new CancellationTokenSource();
        IsObserving = true;
        StatusText = $"Observing {SelectedDataType}...";

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var result in health.Observe(dataType, cancelToken: cts.Token))
                {
                    var entry = result switch
                    {
                        BloodPressureResult bp => new ObserveEntry(
                            DateTimeOffset.Now,
                            $"{bp.Systolic:N0}/{bp.Diastolic:N0} mmHg",
                            $"{bp.Start:HH:mm} - {bp.End:HH:mm}"
                        ),
                        NumericHealthResult nr => new ObserveEntry(
                            DateTimeOffset.Now,
                            $"{nr.Value:N2}",
                            $"{nr.Start:HH:mm} - {nr.End:HH:mm}"
                        ),
                        _ => new ObserveEntry(DateTimeOffset.Now, "Unknown", "")
                    };

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Results.Insert(0, entry);
                        ResultsHeader = $"Results ({Results.Count})";
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (NotSupportedException)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ErrorText = "Real-time observation is not supported on this platform.";
                    OnPropertyChanged(nameof(HasError));
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ErrorText = ex.Message;
                    OnPropertyChanged(nameof(HasError));
                });
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsObserving = false;
                    StatusText = "";
                });
            }
        });
    }


    [RelayCommand]
    void Stop()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }
}
