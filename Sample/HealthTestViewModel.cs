namespace Sample;


[ShellMap<HealthTestPage>(registerRoute: false)]
public partial class HealthTestViewModel(
    INavigator navigator,
    IHealthService health
) : ObservableObject, IPageLifecycleAware
{
    [ObservableProperty]
    DateTimeOffset start = DateTime.Now.AddDays(-1).Date;

    [ObservableProperty]
    DateTimeOffset end = new DateTimeOffset(DateTime.Now.Date).ToEndOfDay();

    [ObservableProperty]
    string? errorText;

    [ObservableProperty]
    bool isBusy;

    [ObservableProperty]
    double steps;

    [ObservableProperty]
    double calories;

    [ObservableProperty]
    double distance;

    [ObservableProperty]
    double heartRate;

    [ObservableProperty]
    double weight;

    [ObservableProperty]
    double height;

    [ObservableProperty]
    double bodyFatPercentage;

    [ObservableProperty]
    double restingHeartRate;

    [ObservableProperty]
    double systolic;

    [ObservableProperty]
    double diastolic;

    [ObservableProperty]
    double oxygenSaturation;

    [ObservableProperty]
    double sleepDuration;

    [ObservableProperty]
    double hydration;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);


    public void OnAppearing() => _ = LoadDataAsync();
    public void OnDisappearing() { }


    [RelayCommand]
    async Task LoadDataAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorText = null;
            OnPropertyChanged(nameof(HasError));

            await health.RequestPermissions(
                DataType.Calories,
                DataType.Distance,
                DataType.HeartRate,
                DataType.StepCount,
                DataType.Weight,
                DataType.Height,
                DataType.BodyFatPercentage,
                DataType.RestingHeartRate,
                DataType.BloodPressure,
                DataType.OxygenSaturation,
                DataType.SleepDuration,
                DataType.Hydration
            );

            if (Start >= End)
            {
                ErrorText = "Start date must be before end date";
                OnPropertyChanged(nameof(HasError));
                return;
            }

            Distance = (await health.GetDistances(Start, End, Interval.Days)).Sum(x => x.Value);
            Calories = (await health.GetCalories(Start, End, Interval.Days)).Sum(x => x.Value);
            Steps = (await health.GetStepCounts(Start, End, Interval.Days)).Sum(x => x.Value);

            var heartRateData = await health.GetAverageHeartRate(Start, End, Interval.Days);
            HeartRate = heartRateData.Any() ? heartRateData.Average(x => x.Value) : 0;

            var weightData = await health.GetWeight(Start, End, Interval.Days);
            Weight = weightData.Any() ? weightData.Average(x => x.Value) : 0;

            var heightData = await health.GetHeight(Start, End, Interval.Days);
            Height = heightData.Any() ? heightData.Average(x => x.Value) : 0;

            var bodyFatData = await health.GetBodyFatPercentage(Start, End, Interval.Days);
            BodyFatPercentage = bodyFatData.Any() ? bodyFatData.Average(x => x.Value) : 0;

            var restingHrData = await health.GetRestingHeartRate(Start, End, Interval.Days);
            RestingHeartRate = restingHrData.Any() ? restingHrData.Average(x => x.Value) : 0;

            var bpData = await health.GetBloodPressure(Start, End, Interval.Days);
            Systolic = bpData.Any() ? bpData.Average(x => x.Systolic) : 0;
            Diastolic = bpData.Any() ? bpData.Average(x => x.Diastolic) : 0;

            var o2Data = await health.GetOxygenSaturation(Start, End, Interval.Days);
            OxygenSaturation = o2Data.Any() ? o2Data.Average(x => x.Value) : 0;

            SleepDuration = (await health.GetSleepDuration(Start, End, Interval.Days)).Sum(x => x.Value);
            Hydration = (await health.GetHydration(Start, End, Interval.Days)).Sum(x => x.Value);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            IsBusy = false;
        }
    }


    [RelayCommand]
    async Task NavToList(string dataTypeName)
    {
        try
        {
            var type = Enum.Parse<DataType>(dataTypeName);
            await navigator.NavigateTo("List", args: [("Type", type)]);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            OnPropertyChanged(nameof(HasError));
        }
    }
}
