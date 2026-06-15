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
                DataType.BloodGlucose => "Blood Glucose (mg/dL)",
                DataType.BodyTemperature => "Body Temperature (°C)",
                DataType.BasalBodyTemperature => "Basal Body Temp (°C)",
                DataType.RespiratoryRate => "Respiratory Rate",
                DataType.Vo2Max => "VO2 Max",
                DataType.HeartRateVariability => "HRV (ms)",
                DataType.LeanBodyMass => "Lean Body Mass (kg)",
                DataType.BasalEnergyBurned => "Basal Energy (kcal)",
                DataType.ActiveEnergyBurned => "Active Energy (kcal)",
                DataType.FloorsClimbed => "Floors Climbed",
                DataType.WheelchairPushes => "Wheelchair Pushes",
                DataType.Speed => "Speed (m/s)",
                DataType.Power => "Power (W)",
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
                DataType.BloodGlucose => await health.GetBloodGlucose(start, end, Interval.Hours),
                DataType.BodyTemperature => await health.GetBodyTemperature(start, end, Interval.Hours),
                DataType.BasalBodyTemperature => await health.GetBasalBodyTemperature(start, end, Interval.Hours),
                DataType.RespiratoryRate => await health.GetRespiratoryRate(start, end, Interval.Hours),
                DataType.Vo2Max => await health.GetVo2Max(start, end, Interval.Hours),
                DataType.HeartRateVariability => await health.GetHeartRateVariability(start, end, Interval.Hours),
                DataType.LeanBodyMass => await health.GetLeanBodyMass(start, end, Interval.Hours),
                DataType.BasalEnergyBurned => await health.GetBasalEnergyBurned(start, end, Interval.Hours),
                DataType.ActiveEnergyBurned => await health.GetActiveEnergyBurned(start, end, Interval.Hours),
                DataType.FloorsClimbed => await health.GetFloorsClimbed(start, end, Interval.Hours),
                DataType.WheelchairPushes => await health.GetWheelchairPushes(start, end, Interval.Hours),
                DataType.Speed => await health.GetSpeed(start, end, Interval.Hours),
                DataType.Power => await health.GetPower(start, end, Interval.Hours),
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
