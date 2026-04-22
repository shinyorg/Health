using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Foundation;
using HealthKit;

namespace Shiny.Health;


public class HealthService : IHealthService
{
    public Task<IList<NumericHealthResult>> GetAverageHeartRate(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => this.Query(
            HKQuantityTypeIdentifier.HeartRate,
            HKStatisticsOptions.DiscreteAverage,
            start,
            end,
            interval,
            result =>
            {
                var avg = result.AverageQuantity()?.GetDoubleValue(HKUnit.Count.UnitDividedBy(HKUnit.Minute)) ?? 0;
                return new NumericHealthResult(
                    DataType.HeartRate,
                    result.StartDate.ToDateTime(),
                    result.EndDate.ToDateTime(),
                    avg
                );
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetCalories(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => this.Query(
            HKQuantityTypeIdentifier.ActiveEnergyBurned,
            HKStatisticsOptions.CumulativeSum,
            start,
            end,
            interval,
            result =>
            {
                var sum = result.SumQuantity()?.GetDoubleValue(HKUnit.Kilocalorie) ?? 0;
                return new NumericHealthResult(
                    DataType.Calories,
                    result.StartDate.ToDateTime(),
                    result.EndDate.ToDateTime(),
                    sum
                );
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetDistances(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => this.Query(
            HKQuantityTypeIdentifier.DistanceWalkingRunning,
            HKStatisticsOptions.CumulativeSum,
            start,
            end,
            interval,
            result =>
            {
                var sum = result.SumQuantity()?.GetDoubleValue(HKUnit.Meter) ?? 0;
                return new NumericHealthResult(
                    DataType.Distance,
                    result.StartDate.ToDateTime(),
                    result.EndDate.ToDateTime(),
                    sum
                );
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetStepCounts(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => this.Query(
            HKQuantityTypeIdentifier.StepCount,
            HKStatisticsOptions.CumulativeSum,
            start,
            end,
            interval,
            result =>
            {
                var sum = result.SumQuantity()?.GetDoubleValue(HKUnit.Count) ?? 0;
                return new NumericHealthResult(
                    DataType.StepCount,
                    result.StartDate.ToDateTime(),
                    result.EndDate.ToDateTime(),
                    sum
                );
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetWeight(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => this.Query(
            HKQuantityTypeIdentifier.BodyMass,
            HKStatisticsOptions.DiscreteAverage,
            start,
            end,
            interval,
            result =>
            {
                var avg = result.AverageQuantity()?.GetDoubleValue(HKUnit.FromString("kg")) ?? 0;
                return new NumericHealthResult(
                    DataType.Weight,
                    result.StartDate.ToDateTime(),
                    result.EndDate.ToDateTime(),
                    avg
                );
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetHeight(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => this.Query(
            HKQuantityTypeIdentifier.Height,
            HKStatisticsOptions.DiscreteAverage,
            start,
            end,
            interval,
            result =>
            {
                var avg = result.AverageQuantity()?.GetDoubleValue(HKUnit.Meter) ?? 0;
                return new NumericHealthResult(
                    DataType.Height,
                    result.StartDate.ToDateTime(),
                    result.EndDate.ToDateTime(),
                    avg
                );
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetBodyFatPercentage(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => this.Query(
            HKQuantityTypeIdentifier.BodyFatPercentage,
            HKStatisticsOptions.DiscreteAverage,
            start,
            end,
            interval,
            result =>
            {
                var avg = result.AverageQuantity()?.GetDoubleValue(HKUnit.Percent) ?? 0;
                return new NumericHealthResult(
                    DataType.BodyFatPercentage,
                    result.StartDate.ToDateTime(),
                    result.EndDate.ToDateTime(),
                    avg * 100
                );
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetRestingHeartRate(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => this.Query(
            HKQuantityTypeIdentifier.RestingHeartRate,
            HKStatisticsOptions.DiscreteAverage,
            start,
            end,
            interval,
            result =>
            {
                var avg = result.AverageQuantity()?.GetDoubleValue(HKUnit.Count.UnitDividedBy(HKUnit.Minute)) ?? 0;
                return new NumericHealthResult(
                    DataType.RestingHeartRate,
                    result.StartDate.ToDateTime(),
                    result.EndDate.ToDateTime(),
                    avg
                );
            },
            cancelToken
        );


    public async Task<IList<BloodPressureResult>> GetBloodPressure(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
    {
        var systolicTask = this.Query(
            HKQuantityTypeIdentifier.BloodPressureSystolic,
            HKStatisticsOptions.DiscreteAverage,
            start,
            end,
            interval,
            result => (
                Start: (DateTimeOffset)result.StartDate.ToDateTime(),
                End: (DateTimeOffset)result.EndDate.ToDateTime(),
                Value: result.AverageQuantity()?.GetDoubleValue(HKUnit.MillimeterOfMercury) ?? 0
            ),
            cancelToken
        );

        var diastolicTask = this.Query(
            HKQuantityTypeIdentifier.BloodPressureDiastolic,
            HKStatisticsOptions.DiscreteAverage,
            start,
            end,
            interval,
            result => (
                Start: (DateTimeOffset)result.StartDate.ToDateTime(),
                End: (DateTimeOffset)result.EndDate.ToDateTime(),
                Value: result.AverageQuantity()?.GetDoubleValue(HKUnit.MillimeterOfMercury) ?? 0
            ),
            cancelToken
        );

        var systolic = await systolicTask.ConfigureAwait(false);
        var diastolic = await diastolicTask.ConfigureAwait(false);

        var results = new List<BloodPressureResult>();
        for (int i = 0; i < Math.Min(systolic.Count, diastolic.Count); i++)
        {
            results.Add(new BloodPressureResult(
                systolic[i].Start,
                systolic[i].End,
                systolic[i].Value,
                diastolic[i].Value
            ));
        }
        return results;
    }


    public Task<IList<NumericHealthResult>> GetOxygenSaturation(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => this.Query(
            HKQuantityTypeIdentifier.OxygenSaturation,
            HKStatisticsOptions.DiscreteAverage,
            start,
            end,
            interval,
            result =>
            {
                var avg = result.AverageQuantity()?.GetDoubleValue(HKUnit.Percent) ?? 0;
                return new NumericHealthResult(
                    DataType.OxygenSaturation,
                    result.StartDate.ToDateTime(),
                    result.EndDate.ToDateTime(),
                    avg * 100
                );
            },
            cancelToken
        );


    public async Task<IList<NumericHealthResult>> GetSleepDuration(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
    {
        var tcs = new TaskCompletionSource<HKSample[]>();
        var catType = HKCategoryType.Create(HKCategoryTypeIdentifier.SleepAnalysis)!;
        var predicate = HKQuery.GetPredicateForSamples(
            (NSDate)start.LocalDateTime,
            (NSDate)end.LocalDateTime,
            HKQueryOptions.None
        );

        var query = new HKSampleQuery(catType, predicate, 0, null, (q, results, error) =>
        {
            if (error != null)
                tcs.TrySetException(new InvalidOperationException(error.Description));
            else
                tcs.TrySetResult(results ?? Array.Empty<HKSample>());
        });

        using var store = new HKHealthStore();
        using var ct = cancelToken.Register(() =>
        {
            tcs.TrySetCanceled();
            store.StopQuery(query);
        });

        store.ExecuteQuery(query);
        var samples = await tcs.Task.ConfigureAwait(false);

        // Filter for asleep states (exclude InBed=0 and Awake=2)
        var asleepSamples = samples
            .OfType<HKCategorySample>()
            .Where(s => s.Value != 0 && s.Value != 2)
            .ToList();

        var buckets = GenerateBuckets(start, end, interval);
        var list = new List<NumericHealthResult>();

        foreach (var (bucketStart, bucketEnd) in buckets)
        {
            double totalHours = 0;
            foreach (var sample in asleepSamples)
            {
                var sampleStart = (DateTimeOffset)sample.StartDate.ToDateTime();
                var sampleEnd = (DateTimeOffset)sample.EndDate.ToDateTime();

                var overlapStart = sampleStart < bucketStart ? bucketStart : sampleStart;
                var overlapEnd = sampleEnd > bucketEnd ? bucketEnd : sampleEnd;

                if (overlapStart < overlapEnd)
                    totalHours += (overlapEnd - overlapStart).TotalHours;
            }
            list.Add(new NumericHealthResult(DataType.SleepDuration, bucketStart, bucketEnd, totalHours));
        }
        return list;
    }


    public Task<IList<NumericHealthResult>> GetHydration(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => this.Query(
            HKQuantityTypeIdentifier.DietaryWater,
            HKStatisticsOptions.CumulativeSum,
            start,
            end,
            interval,
            result =>
            {
                var sum = result.SumQuantity()?.GetDoubleValue(HKUnit.Liter) ?? 0;
                return new NumericHealthResult(
                    DataType.Hydration,
                    result.StartDate.ToDateTime(),
                    result.EndDate.ToDateTime(),
                    sum
                );
            },
            cancelToken
        );


    public async Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params DataType[] dataTypes)
    {
        var share = new NSMutableSet<HKSampleType>();
        var read = new NSMutableSet<HKObjectType>();

        foreach (var dataType in dataTypes)
        {
            if (dataType == DataType.SleepDuration)
            {
                read.Add(HKCategoryType.Create(HKCategoryTypeIdentifier.SleepAnalysis)!);
            }
            else if (dataType == DataType.BloodPressure)
            {
                read.Add(HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureSystolic)!);
                read.Add(HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureDiastolic)!);
            }
            else
            {
                var native = ToNativeType(dataType);
                var qtyType = HKQuantityType.Create(native)!;
                read.Add(qtyType);
            }
        }

        using var store = new HKHealthStore();
        var tuple = await store.RequestAuthorizationToShareAsync(
            new NSSet<HKSampleType>(share.ToArray()),
            new NSSet<HKObjectType>(read.ToArray())
        );
        if (!tuple.Item1)
            throw new InvalidOperationException(tuple.Item2.LocalizedDescription);

        var list = new List<(DataType, bool)>();
        foreach (var dataType in dataTypes)
        {
            var good = GetCurrentStatus(dataType) == AccessState.Available;
            list.Add((dataType, good));
        }
        return list;
    }


    public AccessState GetCurrentStatus(DataType dataType)
    {
        if (!OperatingSystemShim.IsIOSVersionAtLeast(12))
            return AccessState.NotSupported;

        if (!HKHealthStore.IsHealthDataAvailable)
            return AccessState.NotSupported;

        using var store = new HKHealthStore();

        if (dataType == DataType.SleepDuration)
        {
            var catType = HKCategoryType.Create(HKCategoryTypeIdentifier.SleepAnalysis)!;
            var status = store.GetAuthorizationStatus(catType);
            return ToAccessState(status);
        }

        if (dataType == DataType.BloodPressure)
        {
            var sysStatus = store.GetAuthorizationStatus(HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureSystolic)!);
            var diaStatus = store.GetAuthorizationStatus(HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureDiastolic)!);
            var sysAccess = ToAccessState(sysStatus);
            var diaAccess = ToAccessState(diaStatus);
            if (sysAccess == AccessState.Available && diaAccess == AccessState.Available)
                return AccessState.Available;
            if (sysAccess == AccessState.Denied || diaAccess == AccessState.Denied)
                return AccessState.Denied;
            return AccessState.Unknown;
        }

        var native = ToNativeType(dataType);
        var type = HKQuantityType.Create(native)!;
        return ToAccessState(store.GetAuthorizationStatus(type));
    }


    static AccessState ToAccessState(HKAuthorizationStatus status) => status switch
    {
        HKAuthorizationStatus.NotDetermined => AccessState.Unknown,
        HKAuthorizationStatus.SharingDenied => AccessState.Denied,
        HKAuthorizationStatus.SharingAuthorized => AccessState.Available,
        _ => AccessState.Unknown
    };


    static HKQuantityTypeIdentifier ToNativeType(DataType dataType) => dataType switch
    {
        DataType.StepCount => HKQuantityTypeIdentifier.StepCount,
        DataType.HeartRate => HKQuantityTypeIdentifier.HeartRate,
        DataType.Calories => HKQuantityTypeIdentifier.ActiveEnergyBurned,
        DataType.Distance => HKQuantityTypeIdentifier.DistanceWalkingRunning,
        DataType.Weight => HKQuantityTypeIdentifier.BodyMass,
        DataType.Height => HKQuantityTypeIdentifier.Height,
        DataType.BodyFatPercentage => HKQuantityTypeIdentifier.BodyFatPercentage,
        DataType.RestingHeartRate => HKQuantityTypeIdentifier.RestingHeartRate,
        DataType.OxygenSaturation => HKQuantityTypeIdentifier.OxygenSaturation,
        DataType.Hydration => HKQuantityTypeIdentifier.DietaryWater,
        _ => throw new InvalidOperationException("Invalid Type")
    };


    async Task<IList<T>> Query<T>(
        HKQuantityTypeIdentifier quantityTypeIdentifier,
        HKStatisticsOptions statsOption,
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        Func<HKStatistics, T?> transform,
        CancellationToken cancellationToken
    )
    {
        var tcs = new TaskCompletionSource<IList<T>>();
        var calendar = NSCalendar.CurrentCalendar;

        var anchorComponents = calendar.Components(
            NSCalendarUnit.Day | NSCalendarUnit.Month | NSCalendarUnit.Year,
            (NSDate)start.LocalDateTime
        );
        anchorComponents.Hour = 0;
        var anchorDate = calendar.DateFromComponents(anchorComponents);
        var qtyType = HKQuantityType.Create(quantityTypeIdentifier)!;

        var query = new HKStatisticsCollectionQuery(
            qtyType,
            null,
            statsOption,
            anchorDate,
            ToNative(interval)
        );
        query.InitialResultsHandler = (qry, results, err) =>
        {
            if (err != null)
            {
                tcs.TrySetException(new InvalidOperationException(err.Description));
            }
            else
            {
                var list = new List<T>();

                results.EnumerateStatistics(
                    (NSDate)start.LocalDateTime,
                    (NSDate)end.LocalDateTime,
                    (result, stop) =>
                    {
                        try
                        {
                            var value = transform(result);
                            if (value != null)
                                list.Add(value);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    }
                );
                tcs.TrySetResult(list);
            }
        };

        using var store = new HKHealthStore();
        using var ct = cancellationToken.Register(() =>
        {
            tcs.TrySetCanceled();
            store.StopQuery(query);
        });

        store.ExecuteQuery(query);
        var result = await tcs.Task.ConfigureAwait(false);
        return result;
    }


    static List<(DateTimeOffset Start, DateTimeOffset End)> GenerateBuckets(DateTimeOffset start, DateTimeOffset end, Interval interval)
    {
        var buckets = new List<(DateTimeOffset, DateTimeOffset)>();
        var current = start;
        while (current < end)
        {
            var next = interval switch
            {
                Interval.Minutes => current.AddMinutes(1),
                Interval.Hours => current.AddHours(1),
                Interval.Days => current.AddDays(1),
                _ => throw new InvalidOperationException("Invalid interval")
            };
            if (next > end) next = end;
            buckets.Add((current, next));
            current = next;
        }
        return buckets;
    }


    static NSDateComponents ToNative(Interval interval)
    {
        var native = new NSDateComponents();

        switch (interval)
        {
            case Interval.Days:
                native.Day = 1;
                break;

            case Interval.Hours:
                native.Hour = 1;
                break;

            case Interval.Minutes:
                native.Minute = 1;
                break;
        }
        return native;
    }
}
