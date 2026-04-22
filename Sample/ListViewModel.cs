using System.Collections.ObjectModel;

namespace Sample;


[ShellMap<ListPage>("List")]
public partial class ListViewModel(
    INavigator navigator,
    IHealthService health
) : ObservableObject, IQueryAttributable
{
    DataType type;

    [ObservableProperty]
    string? title;

    [ObservableProperty]
    DateTime dateStart = DateTime.Now.AddDays(-1);

    [ObservableProperty]
    TimeSpan timeStart;

    [ObservableProperty]
    DateTime dateEnd = DateTime.Now;

    [ObservableProperty]
    TimeSpan timeEnd;

    [ObservableProperty]
    bool isBusy;

    [ObservableProperty]
    ObservableCollection<NumericHealthResult> data = [];


    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Type", out var val) && val is DataType dt)
        {
            type = dt;
            Title = type switch
            {
                DataType.Calories => "Total Calories",
                DataType.Distance => "Total Distance",
                DataType.HeartRate => "Average Heart Rate",
                DataType.StepCount => "Total Steps",
                DataType.Weight => "Average Weight (kg)",
                DataType.Height => "Average Height (m)",
                DataType.BodyFatPercentage => "Body Fat %",
                DataType.RestingHeartRate => "Resting Heart Rate",
                DataType.OxygenSaturation => "O2 Saturation %",
                DataType.SleepDuration => "Sleep Duration (hrs)",
                DataType.Hydration => "Hydration (L)",
                _ => type.ToString()
            };
        }
    }


    [ObservableProperty]
    string? errorText;

    [RelayCommand]
    async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorText = null;
            var start = DateStart.Date.Add(TimeStart);
            var end = DateEnd.Date.Add(TimeEnd);

            IList<NumericHealthResult> results = type switch
            {
                DataType.StepCount => await health.GetStepCounts(start, end, Interval.Hours),
                DataType.HeartRate => await health.GetAverageHeartRate(start, end, Interval.Hours),
                DataType.Calories => await health.GetCalories(start, end, Interval.Hours),
                DataType.Distance => await health.GetDistances(start, end, Interval.Hours),
                DataType.Weight => await health.GetWeight(start, end, Interval.Hours),
                DataType.Height => await health.GetHeight(start, end, Interval.Hours),
                DataType.BodyFatPercentage => await health.GetBodyFatPercentage(start, end, Interval.Hours),
                DataType.RestingHeartRate => await health.GetRestingHeartRate(start, end, Interval.Hours),
                DataType.OxygenSaturation => await health.GetOxygenSaturation(start, end, Interval.Hours),
                DataType.SleepDuration => await health.GetSleepDuration(start, end, Interval.Hours),
                DataType.Hydration => await health.GetHydration(start, end, Interval.Hours),
                _ => []
            };

            Data = new ObservableCollection<NumericHealthResult>(results);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
