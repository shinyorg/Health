namespace Sample;


[ShellMap<WritePage>("Write")]
public partial class WriteViewModel(
    IHealthService health
) : ObservableObject
{
    [ObservableProperty]
    bool isBusy;

    [ObservableProperty]
    string? errorText;

    [ObservableProperty]
    bool showSuccess;

    [ObservableProperty]
    string? valueText;

    [ObservableProperty]
    string? systolicText;

    [ObservableProperty]
    string? diastolicText;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);
    public bool IsNotBusy => !IsBusy;
    public bool IsNumericType => SelectedDataType != DataType.BloodPressure;
    public bool IsBloodPressureType => SelectedDataType == DataType.BloodPressure;

    public List<DataType> DataTypes { get; } =
    [
        DataType.StepCount,
        DataType.HeartRate,
        DataType.Calories,
        DataType.Distance,
        DataType.Weight,
        DataType.Height,
        DataType.BodyFatPercentage,
        DataType.RestingHeartRate,
        DataType.BloodPressure,
        DataType.OxygenSaturation,
        DataType.SleepDuration,
        DataType.Hydration
    ];

    DataType selectedDataType = DataType.StepCount;
    public DataType SelectedDataType
    {
        get => selectedDataType;
        set
        {
            if (SetProperty(ref selectedDataType, value))
            {
                OnPropertyChanged(nameof(IsNumericType));
                OnPropertyChanged(nameof(IsBloodPressureType));
                OnPropertyChanged(nameof(UnitLabel));
            }
        }
    }

    public string UnitLabel => SelectedDataType switch
    {
        DataType.StepCount => "steps",
        DataType.HeartRate => "bpm",
        DataType.Calories => "kcal",
        DataType.Distance => "meters",
        DataType.Weight => "kg",
        DataType.Height => "meters",
        DataType.BodyFatPercentage => "%",
        DataType.RestingHeartRate => "bpm",
        DataType.OxygenSaturation => "%",
        DataType.SleepDuration => "hours",
        DataType.Hydration => "liters",
        _ => ""
    };


    [RelayCommand]
    async Task WriteAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(IsNotBusy));
            ErrorText = null;
            ShowSuccess = false;
            OnPropertyChanged(nameof(HasError));

            var now = DateTimeOffset.Now;

            if (SelectedDataType == DataType.BloodPressure)
            {
                if (!double.TryParse(SystolicText, out var systolic) ||
                    !double.TryParse(DiastolicText, out var diastolic))
                {
                    ErrorText = "Please enter valid systolic and diastolic values";
                    OnPropertyChanged(nameof(HasError));
                    return;
                }

                await health.RequestPermissions(PermissionType.Write, DataType.BloodPressure);
                await health.Write(new BloodPressureResult(now, now, systolic, diastolic));
            }
            else
            {
                if (!double.TryParse(ValueText, out var value))
                {
                    ErrorText = "Please enter a valid numeric value";
                    OnPropertyChanged(nameof(HasError));
                    return;
                }

                await health.RequestPermissions(PermissionType.Write, SelectedDataType);
                await health.Write(new NumericHealthResult(
                    SelectedDataType,
                    now.AddMinutes(-30),
                    now,
                    value
                ));
            }

            ShowSuccess = true;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }
}
