using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Foundation;
using HealthKit;

namespace Shiny.Health;


public class HealthService : IHealthService
{
    public bool IsAvailable => HKHealthStore.IsHealthDataAvailable;


    public async IAsyncEnumerable<HealthResult> Observe(
        DataType dataType,
        TimeSpan? pollingInterval = null,
        [EnumeratorCancellation] CancellationToken cancelToken = default)
    {
        var channel = Channel.CreateUnbounded<HealthResult>(new UnboundedChannelOptions { SingleWriter = false });
        var store = new HKHealthStore();

        var sampleType = ToNativeSampleType(dataType);
        var predicate = HKQuery.GetPredicateForSamples(
            (NSDate)DateTime.UtcNow,
            null,
            HKQueryOptions.StrictStartDate
        );

        HKAnchoredObjectUpdateHandler handler = (q, addedObjects, deletedObjects, newAnchor, error) =>
        {
            if (error != null)
            {
                channel.Writer.TryComplete(new InvalidOperationException(error.Description));
                return;
            }

            if (addedObjects != null)
            {
                foreach (var sample in addedObjects)
                {
                    var result = ConvertSample(sample, dataType);
                    if (result != null)
                        channel.Writer.TryWrite(result);
                }
            }
        };

        var query = new HKAnchoredObjectQuery(
            sampleType,
            predicate,
            HKQueryAnchor.Create(0),
            0,
            handler
        );
        query.UpdateHandler = handler;

        var registration = cancelToken.Register(() =>
        {
            store.StopQuery(query);
            channel.Writer.TryComplete();
            store.Dispose();
        });

        store.ExecuteQuery(query);

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancelToken).ConfigureAwait(false))
                yield return item;
        }
        finally
        {
            store.StopQuery(query);
            channel.Writer.TryComplete();
            store.Dispose();
            registration.Dispose();
        }
    }


    static HKSampleType ToNativeSampleType(DataType dataType) => dataType switch
    {
        DataType.SleepDuration => HKCategoryType.Create(HKCategoryTypeIdentifier.SleepAnalysis)!,
        DataType.MenstruationFlow => HKCategoryType.Create(HKCategoryTypeIdentifier.MenstrualFlow)!,
        DataType.BloodPressure => HKCorrelationType.Create(HKCorrelationTypeIdentifier.BloodPressure)!,
        DataType.SexualActivity => HKCategoryType.Create(HKCategoryTypeIdentifier.SexualActivity)!,
        DataType.OvulationTest => HKCategoryType.Create(HKCategoryTypeIdentifier.OvulationTestResult)!,
        DataType.CervicalMucus => HKCategoryType.Create(HKCategoryTypeIdentifier.CervicalMucusQuality)!,
        DataType.IntermenstrualBleeding => HKCategoryType.Create(HKCategoryTypeIdentifier.IntermenstrualBleeding)!,
        DataType.Workout => HKObjectType.WorkoutType,
        DataType.Nutrition => HKCorrelationType.Create(HKCorrelationTypeIdentifier.Food)!,
        _ => HKQuantityType.Create(ToNativeType(dataType))!
    };


    static HealthResult? ConvertSample(HKSample sample, DataType dataType)
    {
        var start = (DateTimeOffset)sample.StartDate.ToDateTime();
        var end = (DateTimeOffset)sample.EndDate.ToDateTime();

        if (dataType == DataType.BloodPressure && sample is HKCorrelation correlation)
        {
            var sysType = HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureSystolic)!;
            var diaType = HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureDiastolic)!;
            var sysSample = correlation.GetObjects(sysType).OfType<HKQuantitySample>().FirstOrDefault();
            var diaSample = correlation.GetObjects(diaType).OfType<HKQuantitySample>().FirstOrDefault();

            if (sysSample == null || diaSample == null)
                return null;

            var systolic = sysSample.Quantity.GetDoubleValue(HKUnit.MillimeterOfMercury);
            var diastolic = diaSample.Quantity.GetDoubleValue(HKUnit.MillimeterOfMercury);
            return new BloodPressureResult(start, end, systolic, diastolic);
        }

        if (dataType == DataType.SleepDuration && sample is HKCategorySample catSample)
        {
            // Filter for asleep states (exclude InBed=0 and Awake=2)
            if (catSample.Value == 0 || catSample.Value == 2)
                return null;

            var hours = (end - start).TotalHours;
            return new NumericHealthResult(DataType.SleepDuration, start, end, hours);
        }

        if (dataType == DataType.MenstruationFlow && sample is HKCategorySample menstrualSample)
        {
            var flow = FromNativeFlow((HKCategoryValueMenstrualFlow)(long)menstrualSample.Value);
            return new MenstruationFlowResult(start, end, flow, ReadCycleStart(menstrualSample));
        }

        if (dataType == DataType.SexualActivity && sample is HKCategorySample saSample)
            return new SexualActivityResult(start, end, ReadProtection(saSample));

        if (dataType == DataType.OvulationTest && sample is HKCategorySample ovSample)
            return new OvulationTestResult(start, end, FromNativeOvulation((HKCategoryValueOvulationTestResult)(long)ovSample.Value));

        if (dataType == DataType.CervicalMucus && sample is HKCategorySample cmSample)
            return new CervicalMucusResult(start, end, FromNativeMucus((HKCategoryValueCervicalMucusQuality)(long)cmSample.Value));

        if (dataType == DataType.IntermenstrualBleeding && sample is HKCategorySample)
            return new IntermenstrualBleedingResult(start, end);

        if (dataType == DataType.Workout && sample is HKWorkout workoutSample)
            return ConvertWorkout(workoutSample);

        if (dataType == DataType.Nutrition && sample is HKCorrelation foodSample)
            return ConvertFood(foodSample);

        if (sample is HKQuantitySample qtySample)
        {
            var unit = GetUnit(dataType);
            var value = qtySample.Quantity.GetDoubleValue(unit);

            if (dataType is DataType.BodyFatPercentage or DataType.OxygenSaturation)
                value *= 100;

            return new NumericHealthResult(dataType, start, end, value);
        }

        return null;
    }


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


    Task<IList<NumericHealthResult>> QueryAverage(DataType dataType, DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken)
        => this.Query(
            ToNativeType(dataType),
            HKStatisticsOptions.DiscreteAverage,
            start, end, interval,
            result => new NumericHealthResult(
                dataType,
                result.StartDate.ToDateTime(),
                result.EndDate.ToDateTime(),
                result.AverageQuantity()?.GetDoubleValue(GetUnit(dataType)) ?? 0
            ),
            cancelToken
        );

    Task<IList<NumericHealthResult>> QuerySum(DataType dataType, DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken)
        => this.Query(
            ToNativeType(dataType),
            HKStatisticsOptions.CumulativeSum,
            start, end, interval,
            result => new NumericHealthResult(
                dataType,
                result.StartDate.ToDateTime(),
                result.EndDate.ToDateTime(),
                result.SumQuantity()?.GetDoubleValue(GetUnit(dataType)) ?? 0
            ),
            cancelToken
        );

    public Task<IList<NumericHealthResult>> GetBloodGlucose(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAverage(DataType.BloodGlucose, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetBodyTemperature(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAverage(DataType.BodyTemperature, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetBasalBodyTemperature(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAverage(DataType.BasalBodyTemperature, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetRespiratoryRate(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAverage(DataType.RespiratoryRate, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetVo2Max(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAverage(DataType.Vo2Max, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetHeartRateVariability(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAverage(DataType.HeartRateVariability, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetLeanBodyMass(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAverage(DataType.LeanBodyMass, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetBasalEnergyBurned(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QuerySum(DataType.BasalEnergyBurned, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetActiveEnergyBurned(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QuerySum(DataType.ActiveEnergyBurned, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetFloorsClimbed(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QuerySum(DataType.FloorsClimbed, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetWheelchairPushes(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QuerySum(DataType.WheelchairPushes, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetSpeed(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAverage(DataType.Speed, start, end, interval, cancelToken);

    public Task<IList<NumericHealthResult>> GetPower(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAverage(DataType.Power, start, end, interval, cancelToken);


    public async Task<IList<MenstruationFlowResult>> GetMenstruationFlow(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var tcs = new TaskCompletionSource<HKSample[]>();
        var catType = HKCategoryType.Create(HKCategoryTypeIdentifier.MenstrualFlow)!;
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

        var list = new List<MenstruationFlowResult>();
        foreach (var sample in samples.OfType<HKCategorySample>())
        {
            var flow = FromNativeFlow((HKCategoryValueMenstrualFlow)(long)sample.Value);
            list.Add(new MenstruationFlowResult(
                (DateTimeOffset)sample.StartDate.ToDateTime(),
                (DateTimeOffset)sample.EndDate.ToDateTime(),
                flow,
                ReadCycleStart(sample)
            ));
        }
        return list;
    }


    public Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params DataType[] dataTypes)
        => RequestPermissions(PermissionType.Read, dataTypes);

    public Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(PermissionType permissionType, params DataType[] dataTypes)
        => RequestPermissions(dataTypes.Select(dt => (permissionType, dt)).ToArray());

    public async Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params (PermissionType Permission, DataType Type)[] permissions)
    {
        var share = new NSMutableSet<HKSampleType>();
        var read = new NSMutableSet<HKObjectType>();

        foreach (var (permissionType, dataType) in permissions)
        {
            foreach (var type in GetSampleTypes(dataType))
            {
                if (permissionType.HasFlag(PermissionType.Read))
                    read.Add(type);
                if (permissionType.HasFlag(PermissionType.Write))
                    share.Add(type);
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
        foreach (var (_, dataType) in permissions)
        {
            var good = GetCurrentStatus(dataType) == AccessState.Available;
            list.Add((dataType, good));
        }
        return list;
    }


    public async Task Write(NumericHealthResult result, CancellationToken cancelToken = default)
    {
        using var store = new HKHealthStore();

        if (result.DataType == DataType.SleepDuration)
        {
            var catType = HKCategoryType.Create(HKCategoryTypeIdentifier.SleepAnalysis)!;
            var sample = HKCategorySample.FromType(
                catType,
                (nint)3, // AsleepUnspecified
                (NSDate)result.Start.LocalDateTime,
                (NSDate)result.End.LocalDateTime
            );
            var sleepResult = await store.SaveObjectAsync(sample).ConfigureAwait(false);
            if (!sleepResult.Item1)
                throw new InvalidOperationException(sleepResult.Item2?.LocalizedDescription ?? "Failed to save sleep data");
            return;
        }

        var native = ToNativeType(result.DataType);
        var qtyType = HKQuantityType.Create(native)!;
        var unit = GetUnit(result.DataType);
        var value = result.DataType is DataType.BodyFatPercentage or DataType.OxygenSaturation
            ? result.Value / 100.0
            : result.Value;

        var quantity = HKQuantity.FromQuantity(unit, value);
        var quantitySample = HKQuantitySample.FromType(
            qtyType,
            quantity,
            (NSDate)result.Start.LocalDateTime,
            (NSDate)result.End.LocalDateTime
        );
        var saveResult = await store.SaveObjectAsync(quantitySample).ConfigureAwait(false);
        if (!saveResult.Item1)
            throw new InvalidOperationException(saveResult.Item2?.LocalizedDescription ?? "Failed to save health data");
    }


    public async Task Write(BloodPressureResult result, CancellationToken cancelToken = default)
    {
        using var store = new HKHealthStore();
        var unit = HKUnit.MillimeterOfMercury;

        var sysType = HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureSystolic)!;
        var diaType = HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureDiastolic)!;

        var sysSample = HKQuantitySample.FromType(
            sysType,
            HKQuantity.FromQuantity(unit, result.Systolic),
            (NSDate)result.Start.LocalDateTime,
            (NSDate)result.End.LocalDateTime
        );
        var diaSample = HKQuantitySample.FromType(
            diaType,
            HKQuantity.FromQuantity(unit, result.Diastolic),
            (NSDate)result.Start.LocalDateTime,
            (NSDate)result.End.LocalDateTime
        );

        var correlationType = HKCorrelationType.Create(HKCorrelationTypeIdentifier.BloodPressure)!;
        var correlation = HKCorrelation.Create(
            correlationType,
            (NSDate)result.Start.LocalDateTime,
            (NSDate)result.End.LocalDateTime,
            new NSSet<HKSample>(sysSample, diaSample)
        );

        var saveResult = await store.SaveObjectAsync(correlation).ConfigureAwait(false);
        if (!saveResult.Item1)
            throw new InvalidOperationException(saveResult.Item2?.LocalizedDescription ?? "Failed to save blood pressure data");
    }


    public async Task Write(MenstruationFlowResult result, CancellationToken cancelToken = default)
    {
        using var store = new HKHealthStore();
        var catType = HKCategoryType.Create(HKCategoryTypeIdentifier.MenstrualFlow)!;

        // HealthKit requires the cycle-start metadata key on menstrual flow samples
        var metadata = new NSMutableDictionary
        {
            [HKMetadataKey.MenstrualCycleStart] = NSNumber.FromBoolean(result.IsCycleStart)
        };

        var sample = HKCategorySample.FromType(
            catType,
            (nint)(long)ToNativeFlow(result.Flow),
            (NSDate)result.Start.LocalDateTime,
            (NSDate)result.End.LocalDateTime,
            metadata
        );

        var saveResult = await store.SaveObjectAsync(sample).ConfigureAwait(false);
        if (!saveResult.Item1)
            throw new InvalidOperationException(saveResult.Item2?.LocalizedDescription ?? "Failed to save menstruation data");
    }


    public AccessState GetCurrentStatus(DataType dataType)
    {
        if (!OperatingSystem.IsIOSVersionAtLeast(12))
            return AccessState.NotSupported;

        if (!HKHealthStore.IsHealthDataAvailable)
            return AccessState.NotSupported;

        using var store = new HKHealthStore();

        var states = GetSampleTypes(dataType)
            .Select(t => ToAccessState(store.GetAuthorizationStatus(t)))
            .ToList();

        if (states.Count > 0 && states.All(s => s == AccessState.Available))
            return AccessState.Available;
        if (states.Any(s => s == AccessState.Denied))
            return AccessState.Denied;
        return AccessState.Unknown;
    }


    static readonly HKQuantityTypeIdentifier[] nutritionTypes =
    [
        HKQuantityTypeIdentifier.DietaryEnergyConsumed,
        HKQuantityTypeIdentifier.DietaryProtein,
        HKQuantityTypeIdentifier.DietaryCarbohydrates,
        HKQuantityTypeIdentifier.DietaryFatTotal,
        HKQuantityTypeIdentifier.DietaryFiber,
        HKQuantityTypeIdentifier.DietarySugar,
        HKQuantityTypeIdentifier.DietarySodium,
        HKQuantityTypeIdentifier.DietaryCholesterol
    ];


    static HKSampleType[] GetSampleTypes(DataType dataType) => dataType switch
    {
        DataType.SleepDuration => [HKCategoryType.Create(HKCategoryTypeIdentifier.SleepAnalysis)!],
        DataType.MenstruationFlow => [HKCategoryType.Create(HKCategoryTypeIdentifier.MenstrualFlow)!],
        DataType.BloodPressure =>
        [
            HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureSystolic)!,
            HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureDiastolic)!
        ],
        DataType.SexualActivity => [HKCategoryType.Create(HKCategoryTypeIdentifier.SexualActivity)!],
        DataType.OvulationTest => [HKCategoryType.Create(HKCategoryTypeIdentifier.OvulationTestResult)!],
        DataType.CervicalMucus => [HKCategoryType.Create(HKCategoryTypeIdentifier.CervicalMucusQuality)!],
        DataType.IntermenstrualBleeding => [HKCategoryType.Create(HKCategoryTypeIdentifier.IntermenstrualBleeding)!],
        DataType.Workout => [HKObjectType.WorkoutType],
        DataType.Nutrition => nutritionTypes.Select(t => (HKSampleType)HKQuantityType.Create(t)!).ToArray(),
        _ => [HKQuantityType.Create(ToNativeType(dataType))!]
    };


    async Task<HKSample[]> QuerySamples(HKSampleType type, DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken)
    {
        var tcs = new TaskCompletionSource<HKSample[]>();
        var predicate = HKQuery.GetPredicateForSamples(
            (NSDate)start.LocalDateTime,
            (NSDate)end.LocalDateTime,
            HKQueryOptions.None
        );
        var query = new HKSampleQuery(type, predicate, 0, null, (q, results, error) =>
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
        return await tcs.Task.ConfigureAwait(false);
    }


    public async Task<IList<SexualActivityResult>> GetSexualActivity(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var samples = await QuerySamples(HKCategoryType.Create(HKCategoryTypeIdentifier.SexualActivity)!, start, end, cancelToken).ConfigureAwait(false);
        return samples
            .OfType<HKCategorySample>()
            .Select(s => new SexualActivityResult((DateTimeOffset)s.StartDate.ToDateTime(), (DateTimeOffset)s.EndDate.ToDateTime(), ReadProtection(s)))
            .ToList();
    }


    public async Task<IList<OvulationTestResult>> GetOvulationTests(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var samples = await QuerySamples(HKCategoryType.Create(HKCategoryTypeIdentifier.OvulationTestResult)!, start, end, cancelToken).ConfigureAwait(false);
        return samples
            .OfType<HKCategorySample>()
            .Select(s => new OvulationTestResult((DateTimeOffset)s.StartDate.ToDateTime(), (DateTimeOffset)s.EndDate.ToDateTime(), FromNativeOvulation((HKCategoryValueOvulationTestResult)(long)s.Value)))
            .ToList();
    }


    public async Task<IList<CervicalMucusResult>> GetCervicalMucus(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var samples = await QuerySamples(HKCategoryType.Create(HKCategoryTypeIdentifier.CervicalMucusQuality)!, start, end, cancelToken).ConfigureAwait(false);
        return samples
            .OfType<HKCategorySample>()
            .Select(s => new CervicalMucusResult((DateTimeOffset)s.StartDate.ToDateTime(), (DateTimeOffset)s.EndDate.ToDateTime(), FromNativeMucus((HKCategoryValueCervicalMucusQuality)(long)s.Value)))
            .ToList();
    }


    public async Task<IList<IntermenstrualBleedingResult>> GetIntermenstrualBleeding(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var samples = await QuerySamples(HKCategoryType.Create(HKCategoryTypeIdentifier.IntermenstrualBleeding)!, start, end, cancelToken).ConfigureAwait(false);
        return samples
            .OfType<HKCategorySample>()
            .Select(s => new IntermenstrualBleedingResult((DateTimeOffset)s.StartDate.ToDateTime(), (DateTimeOffset)s.EndDate.ToDateTime()))
            .ToList();
    }


    public async Task<IList<WorkoutResult>> GetWorkouts(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var samples = await QuerySamples(HKObjectType.WorkoutType, start, end, cancelToken).ConfigureAwait(false);
        return samples.OfType<HKWorkout>().Select(ConvertWorkout).ToList();
    }


    public async Task<IList<NutritionResult>> GetNutrition(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var samples = await QuerySamples(HKCorrelationType.Create(HKCorrelationTypeIdentifier.Food)!, start, end, cancelToken).ConfigureAwait(false);
        return samples.OfType<HKCorrelation>().Select(ConvertFood).ToList();
    }


    public async Task Write(SexualActivityResult result, CancellationToken cancelToken = default)
    {
        using var store = new HKHealthStore();
        var catType = HKCategoryType.Create(HKCategoryTypeIdentifier.SexualActivity)!;
        var metadata = new NSMutableDictionary();
        if (result.Protection != SexualActivityProtection.Unspecified)
            metadata[HKMetadataKey.SexualActivityProtectionUsed] = NSNumber.FromBoolean(result.Protection == SexualActivityProtection.Protected);

        var sample = HKCategorySample.FromType(
            catType,
            (nint)0, // HKCategoryValueNotApplicable
            (NSDate)result.Start.LocalDateTime,
            (NSDate)result.End.LocalDateTime,
            metadata
        );
        await SaveOrThrow(store, sample, "sexual activity").ConfigureAwait(false);
    }


    public async Task Write(OvulationTestResult result, CancellationToken cancelToken = default)
    {
        using var store = new HKHealthStore();
        var catType = HKCategoryType.Create(HKCategoryTypeIdentifier.OvulationTestResult)!;
        var sample = HKCategorySample.FromType(
            catType,
            (nint)(long)ToNativeOvulation(result.Outcome),
            (NSDate)result.Start.LocalDateTime,
            (NSDate)result.End.LocalDateTime
        );
        await SaveOrThrow(store, sample, "ovulation test").ConfigureAwait(false);
    }


    public async Task Write(CervicalMucusResult result, CancellationToken cancelToken = default)
    {
        using var store = new HKHealthStore();
        var catType = HKCategoryType.Create(HKCategoryTypeIdentifier.CervicalMucusQuality)!;
        var sample = HKCategorySample.FromType(
            catType,
            (nint)(long)ToNativeMucus(result.Appearance),
            (NSDate)result.Start.LocalDateTime,
            (NSDate)result.End.LocalDateTime
        );
        await SaveOrThrow(store, sample, "cervical mucus").ConfigureAwait(false);
    }


    public async Task Write(IntermenstrualBleedingResult result, CancellationToken cancelToken = default)
    {
        using var store = new HKHealthStore();
        var catType = HKCategoryType.Create(HKCategoryTypeIdentifier.IntermenstrualBleeding)!;
        var sample = HKCategorySample.FromType(
            catType,
            (nint)0, // HKCategoryValueNotApplicable
            (NSDate)result.Start.LocalDateTime,
            (NSDate)result.End.LocalDateTime
        );
        await SaveOrThrow(store, sample, "intermenstrual bleeding").ConfigureAwait(false);
    }


    public async Task Write(WorkoutResult result, CancellationToken cancelToken = default)
    {
        using var store = new HKHealthStore();
        var duration = (result.End - result.Start).TotalSeconds;
        var energy = result.TotalEnergyKilocalories is double e
            ? HKQuantity.FromQuantity(HKUnit.Kilocalorie, e)
            : null;
        var distance = result.TotalDistanceMeters is double d
            ? HKQuantity.FromQuantity(HKUnit.Meter, d)
            : null;

        var workout = HKWorkout.Create(
            ToNativeWorkout(result.Workout),
            (NSDate)result.Start.LocalDateTime,
            (NSDate)result.End.LocalDateTime,
            duration,
            energy!,
            distance!,
            (NSDictionary)null!
        );
        await SaveOrThrow(store, workout, "workout").ConfigureAwait(false);
    }


    public async Task Write(NutritionResult result, CancellationToken cancelToken = default)
    {
        using var store = new HKHealthStore();
        var start = (NSDate)result.Start.LocalDateTime;
        var end = (NSDate)result.End.LocalDateTime;
        var gram = HKUnit.FromString("g");

        var samples = new List<HKSample>();
        void Add(HKQuantityTypeIdentifier id, HKUnit unit, double? value)
        {
            if (value is double v)
                samples.Add(HKQuantitySample.FromType(HKQuantityType.Create(id)!, HKQuantity.FromQuantity(unit, v), start, end));
        }

        Add(HKQuantityTypeIdentifier.DietaryEnergyConsumed, HKUnit.Kilocalorie, result.EnergyKilocalories);
        Add(HKQuantityTypeIdentifier.DietaryProtein, gram, result.ProteinGrams);
        Add(HKQuantityTypeIdentifier.DietaryCarbohydrates, gram, result.CarbohydratesGrams);
        Add(HKQuantityTypeIdentifier.DietaryFatTotal, gram, result.TotalFatGrams);
        Add(HKQuantityTypeIdentifier.DietaryFiber, gram, result.FiberGrams);
        Add(HKQuantityTypeIdentifier.DietarySugar, gram, result.SugarGrams);
        Add(HKQuantityTypeIdentifier.DietarySodium, gram, result.SodiumGrams);
        Add(HKQuantityTypeIdentifier.DietaryCholesterol, gram, result.CholesterolGrams);

        if (samples.Count == 0)
            return;

        var metadata = new NSMutableDictionary();
        if (!string.IsNullOrEmpty(result.Name))
            metadata[HKMetadataKey.FoodType] = new NSString(result.Name);

        var foodType = HKCorrelationType.Create(HKCorrelationTypeIdentifier.Food)!;
        var correlation = HKCorrelation.Create(foodType, start, end, new NSSet<HKSample>(samples.ToArray()), metadata);
        await SaveOrThrow(store, correlation, "nutrition").ConfigureAwait(false);
    }


    static async Task SaveOrThrow(HKHealthStore store, HKObject obj, string what)
    {
        var saveResult = await store.SaveObjectAsync(obj).ConfigureAwait(false);
        if (!saveResult.Item1)
            throw new InvalidOperationException(saveResult.Item2?.LocalizedDescription ?? $"Failed to save {what} data");
    }


    static WorkoutResult ConvertWorkout(HKWorkout w)
    {
        double? energy = w.TotalEnergyBurned?.GetDoubleValue(HKUnit.Kilocalorie);
        double? distance = w.TotalDistance?.GetDoubleValue(HKUnit.Meter);
        return new WorkoutResult(
            (DateTimeOffset)w.StartDate.ToDateTime(),
            (DateTimeOffset)w.EndDate.ToDateTime(),
            FromNativeWorkout(w.WorkoutActivityType),
            energy,
            distance,
            null
        );
    }


    static NutritionResult ConvertFood(HKCorrelation food)
    {
        var gram = HKUnit.FromString("g");
        double? Get(HKQuantityTypeIdentifier id, HKUnit unit)
        {
            var qt = HKQuantityType.Create(id)!;
            var sample = food.GetObjects(qt).OfType<HKQuantitySample>().FirstOrDefault();
            return sample?.Quantity.GetDoubleValue(unit);
        }

        string? name = food.WeakMetadata?[HKMetadataKey.FoodType] is NSString ns ? ns.ToString() : null;

        return new NutritionResult(
            (DateTimeOffset)food.StartDate.ToDateTime(),
            (DateTimeOffset)food.EndDate.ToDateTime(),
            MealType.Unknown,
            name,
            Get(HKQuantityTypeIdentifier.DietaryEnergyConsumed, HKUnit.Kilocalorie),
            Get(HKQuantityTypeIdentifier.DietaryProtein, gram),
            Get(HKQuantityTypeIdentifier.DietaryCarbohydrates, gram),
            Get(HKQuantityTypeIdentifier.DietaryFatTotal, gram),
            Get(HKQuantityTypeIdentifier.DietaryFiber, gram),
            Get(HKQuantityTypeIdentifier.DietarySugar, gram),
            Get(HKQuantityTypeIdentifier.DietarySodium, gram),
            Get(HKQuantityTypeIdentifier.DietaryCholesterol, gram)
        );
    }


    static SexualActivityProtection ReadProtection(HKCategorySample sample)
    {
        var value = sample.WeakMetadata?[HKMetadataKey.SexualActivityProtectionUsed];
        if (value is NSNumber num)
            return num.BoolValue ? SexualActivityProtection.Protected : SexualActivityProtection.Unprotected;
        return SexualActivityProtection.Unspecified;
    }


    static OvulationTestOutcome FromNativeOvulation(HKCategoryValueOvulationTestResult value) => value switch
    {
        HKCategoryValueOvulationTestResult.Negative => OvulationTestOutcome.Negative,
        HKCategoryValueOvulationTestResult.Positive => OvulationTestOutcome.Positive, // == LuteinizingHormoneSurge
        HKCategoryValueOvulationTestResult.EstrogenSurge => OvulationTestOutcome.High,
        _ => OvulationTestOutcome.Inconclusive
    };


    static HKCategoryValueOvulationTestResult ToNativeOvulation(OvulationTestOutcome outcome) => outcome switch
    {
        OvulationTestOutcome.Negative => HKCategoryValueOvulationTestResult.Negative,
        OvulationTestOutcome.Positive => HKCategoryValueOvulationTestResult.Positive,
        OvulationTestOutcome.High => HKCategoryValueOvulationTestResult.EstrogenSurge,
        _ => HKCategoryValueOvulationTestResult.Indeterminate
    };


    static CervicalMucusAppearance FromNativeMucus(HKCategoryValueCervicalMucusQuality value) => value switch
    {
        HKCategoryValueCervicalMucusQuality.Dry => CervicalMucusAppearance.Dry,
        HKCategoryValueCervicalMucusQuality.Sticky => CervicalMucusAppearance.Sticky,
        HKCategoryValueCervicalMucusQuality.Creamy => CervicalMucusAppearance.Creamy,
        HKCategoryValueCervicalMucusQuality.Watery => CervicalMucusAppearance.Watery,
        HKCategoryValueCervicalMucusQuality.EggWhite => CervicalMucusAppearance.EggWhite,
        _ => CervicalMucusAppearance.Unspecified
    };


    // HealthKit has no "unspecified" cervical mucus quality; Unspecified is written as Dry (the least-fertile valid value).
    static HKCategoryValueCervicalMucusQuality ToNativeMucus(CervicalMucusAppearance appearance) => appearance switch
    {
        CervicalMucusAppearance.Dry => HKCategoryValueCervicalMucusQuality.Dry,
        CervicalMucusAppearance.Sticky => HKCategoryValueCervicalMucusQuality.Sticky,
        CervicalMucusAppearance.Creamy => HKCategoryValueCervicalMucusQuality.Creamy,
        CervicalMucusAppearance.Watery => HKCategoryValueCervicalMucusQuality.Watery,
        CervicalMucusAppearance.EggWhite => HKCategoryValueCervicalMucusQuality.EggWhite,
        _ => HKCategoryValueCervicalMucusQuality.Dry
    };


    static WorkoutType FromNativeWorkout(HKWorkoutActivityType type) => type switch
    {
        HKWorkoutActivityType.Running => WorkoutType.Running,
        HKWorkoutActivityType.Walking => WorkoutType.Walking,
        HKWorkoutActivityType.Hiking => WorkoutType.Hiking,
        HKWorkoutActivityType.Cycling => WorkoutType.Cycling,
        HKWorkoutActivityType.Swimming => WorkoutType.Swimming,
        HKWorkoutActivityType.Rowing => WorkoutType.Rowing,
        HKWorkoutActivityType.Elliptical => WorkoutType.Elliptical,
        HKWorkoutActivityType.StairClimbing => WorkoutType.StairClimbing,
        HKWorkoutActivityType.TraditionalStrengthTraining => WorkoutType.StrengthTraining,
        HKWorkoutActivityType.FunctionalStrengthTraining => WorkoutType.StrengthTraining,
        HKWorkoutActivityType.HighIntensityIntervalTraining => WorkoutType.HighIntensityIntervalTraining,
        HKWorkoutActivityType.Yoga => WorkoutType.Yoga,
        HKWorkoutActivityType.Pilates => WorkoutType.Pilates,
        HKWorkoutActivityType.Tennis => WorkoutType.Tennis,
        HKWorkoutActivityType.Basketball => WorkoutType.Basketball,
        HKWorkoutActivityType.Soccer => WorkoutType.Soccer,
        HKWorkoutActivityType.Baseball => WorkoutType.Baseball,
        HKWorkoutActivityType.Golf => WorkoutType.Golf,
        HKWorkoutActivityType.Boxing => WorkoutType.Boxing,
        HKWorkoutActivityType.MartialArts => WorkoutType.MartialArts,
        HKWorkoutActivityType.Dance => WorkoutType.Dancing,
        HKWorkoutActivityType.CardioDance => WorkoutType.Dancing,
        HKWorkoutActivityType.SocialDance => WorkoutType.Dancing,
        _ => WorkoutType.Other
    };


    static HKWorkoutActivityType ToNativeWorkout(WorkoutType type) => type switch
    {
        WorkoutType.Running => HKWorkoutActivityType.Running,
        WorkoutType.Walking => HKWorkoutActivityType.Walking,
        WorkoutType.Hiking => HKWorkoutActivityType.Hiking,
        WorkoutType.Cycling => HKWorkoutActivityType.Cycling,
        WorkoutType.Swimming => HKWorkoutActivityType.Swimming,
        WorkoutType.Rowing => HKWorkoutActivityType.Rowing,
        WorkoutType.Elliptical => HKWorkoutActivityType.Elliptical,
        WorkoutType.StairClimbing => HKWorkoutActivityType.StairClimbing,
        WorkoutType.StrengthTraining => HKWorkoutActivityType.TraditionalStrengthTraining,
        WorkoutType.HighIntensityIntervalTraining => HKWorkoutActivityType.HighIntensityIntervalTraining,
        WorkoutType.Yoga => HKWorkoutActivityType.Yoga,
        WorkoutType.Pilates => HKWorkoutActivityType.Pilates,
        WorkoutType.Tennis => HKWorkoutActivityType.Tennis,
        WorkoutType.Basketball => HKWorkoutActivityType.Basketball,
        WorkoutType.Soccer => HKWorkoutActivityType.Soccer,
        WorkoutType.Baseball => HKWorkoutActivityType.Baseball,
        WorkoutType.Golf => HKWorkoutActivityType.Golf,
        WorkoutType.Boxing => HKWorkoutActivityType.Boxing,
        WorkoutType.MartialArts => HKWorkoutActivityType.MartialArts,
        WorkoutType.Dancing => HKWorkoutActivityType.Dance,
        _ => HKWorkoutActivityType.Other
    };


    static bool ReadCycleStart(HKCategorySample sample)
    {
        var value = sample.WeakMetadata?[HKMetadataKey.MenstrualCycleStart];
        return value is NSNumber num && num.BoolValue;
    }


    static MenstrualFlow FromNativeFlow(HKCategoryValueMenstrualFlow value) => value switch
    {
        HKCategoryValueMenstrualFlow.None => MenstrualFlow.None,
        HKCategoryValueMenstrualFlow.Light => MenstrualFlow.Light,
        HKCategoryValueMenstrualFlow.Medium => MenstrualFlow.Medium,
        HKCategoryValueMenstrualFlow.Heavy => MenstrualFlow.Heavy,
        _ => MenstrualFlow.Unspecified
    };


    static HKCategoryValueMenstrualFlow ToNativeFlow(MenstrualFlow flow) => flow switch
    {
        MenstrualFlow.None => HKCategoryValueMenstrualFlow.None,
        MenstrualFlow.Light => HKCategoryValueMenstrualFlow.Light,
        MenstrualFlow.Medium => HKCategoryValueMenstrualFlow.Medium,
        MenstrualFlow.Heavy => HKCategoryValueMenstrualFlow.Heavy,
        _ => HKCategoryValueMenstrualFlow.Unspecified
    };


    static AccessState ToAccessState(HKAuthorizationStatus status) => status switch
    {
        HKAuthorizationStatus.NotDetermined => AccessState.Unknown,
        HKAuthorizationStatus.SharingDenied => AccessState.Denied,
        HKAuthorizationStatus.SharingAuthorized => AccessState.Available,
        _ => AccessState.Unknown
    };


    static HKUnit GetUnit(DataType dataType) => dataType switch
    {
        DataType.StepCount => HKUnit.Count,
        DataType.HeartRate => HKUnit.Count.UnitDividedBy(HKUnit.Minute),
        DataType.Calories => HKUnit.Kilocalorie,
        DataType.Distance => HKUnit.Meter,
        DataType.Weight => HKUnit.FromString("kg"),
        DataType.Height => HKUnit.Meter,
        DataType.BodyFatPercentage => HKUnit.Percent,
        DataType.RestingHeartRate => HKUnit.Count.UnitDividedBy(HKUnit.Minute),
        DataType.OxygenSaturation => HKUnit.Percent,
        DataType.Hydration => HKUnit.Liter,
        DataType.BloodGlucose => HKUnit.FromString("mg/dL"),
        DataType.BodyTemperature => HKUnit.FromString("degC"),
        DataType.BasalBodyTemperature => HKUnit.FromString("degC"),
        DataType.RespiratoryRate => HKUnit.Count.UnitDividedBy(HKUnit.Minute),
        DataType.Vo2Max => HKUnit.FromString("ml/kg*min"),
        DataType.HeartRateVariability => HKUnit.FromString("ms"),
        DataType.LeanBodyMass => HKUnit.FromString("kg"),
        DataType.BasalEnergyBurned => HKUnit.Kilocalorie,
        DataType.ActiveEnergyBurned => HKUnit.Kilocalorie,
        DataType.FloorsClimbed => HKUnit.Count,
        DataType.WheelchairPushes => HKUnit.Count,
        DataType.Speed => HKUnit.Meter.UnitDividedBy(HKUnit.Second),
        DataType.Power => HKUnit.FromString("W"),
        _ => throw new InvalidOperationException("Invalid Type")
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
        DataType.BloodGlucose => HKQuantityTypeIdentifier.BloodGlucose,
        DataType.BodyTemperature => HKQuantityTypeIdentifier.BodyTemperature,
        DataType.BasalBodyTemperature => HKQuantityTypeIdentifier.BasalBodyTemperature,
        DataType.RespiratoryRate => HKQuantityTypeIdentifier.RespiratoryRate,
        DataType.Vo2Max => HKQuantityTypeIdentifier.VO2Max,
        DataType.HeartRateVariability => HKQuantityTypeIdentifier.HeartRateVariabilitySdnn,
        DataType.LeanBodyMass => HKQuantityTypeIdentifier.LeanBodyMass,
        DataType.BasalEnergyBurned => HKQuantityTypeIdentifier.BasalEnergyBurned,
        DataType.ActiveEnergyBurned => HKQuantityTypeIdentifier.ActiveEnergyBurned,
        DataType.FloorsClimbed => HKQuantityTypeIdentifier.FlightsClimbed,
        DataType.WheelchairPushes => HKQuantityTypeIdentifier.PushCount,
        DataType.Speed => HKQuantityTypeIdentifier.WalkingSpeed,
        DataType.Power => HKQuantityTypeIdentifier.CyclingPower,
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
