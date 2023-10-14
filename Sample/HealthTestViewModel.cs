using Shiny.Health;

namespace Sample;


public class HealthTestViewModel : ViewModel
{
    public HealthTestViewModel(
        BaseServices services,
        IHealthService health
    ) : base(services)
    {
        this.Start = DateTime.Now.AddDays(-1).Date;
        this.End = this.Start.ToEndOfDay();

        this.Load = ReactiveCommand.CreateFromTask(async () =>
        {
            var result = await health.RequestPermission(
                new Permission(DistanceHealthMetric.Default, PermissionType.Read),
                new Permission(CaloriesHealthMetric.Default, PermissionType.Read),
                new Permission(StepCountHealthMetric.Default, PermissionType.Read),
                new Permission(HeartRateHealthMetric.Default, PermissionType.Read)
            );
            if (!result)
            {
                await this.Dialogs.Alert("Failed permission check");
                return;
            }

            if (this.Start < this.End)
            {
                this.ErrorText = String.Empty;
            }
            else
            {
                this.ErrorText = "Start date must be greater than End date";
                this.Calories = 0;
                this.HeartRate = 0;
                this.Distance = 0;
                this.Steps = 0;
                return;
            }

            this.Distance = (await health.Query(DistanceHealthMetric.Default, this.Start, this.End, Interval.Days)).Sum(x => x.Value);
            this.Calories = (await health.Query(CaloriesHealthMetric.Default, this.Start, this.End, Interval.Days)).Sum(x => x.Value);            
            this.Steps = (await health.Query(StepCountHealthMetric.Default, this.Start, this.End, Interval.Days)).Sum(x => x.Value);
            this.HeartRate = (await health.Query(HeartRateHealthMetric.Default, this.Start, this.End, Interval.Days)).Average(x => x.Value);
        });
        this.BindBusyCommand(this.Load);
    }


    public ICommand Load { get; }
    [Reactive] public DateTimeOffset Start { get; set; }
    [Reactive] public DateTimeOffset End { get; set; }

    [Reactive] public string ErrorText { get; private set; }
    [Reactive] public int Steps { get; private set; }
    [Reactive] public double Calories { get; private set; }
    [Reactive] public double Distance { get; private set; }
    [Reactive] public double HeartRate { get; private set; }
}