using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using AndroidX.Health.Connect.Client;
using AndroidX.Health.Connect.Client.Aggregate;
using AndroidX.Health.Connect.Client.Contracts;
using AndroidX.Health.Connect.Client.Records;
using AndroidX.Health.Connect.Client.Records.Metadata;
using AndroidX.Health.Connect.Client.Request;
using AndroidX.Health.Connect.Client.Time;
using AndroidX.Health.Connect.Client.Response;
using Java.Time;
using Kotlin.Coroutines;
using Kotlin.Jvm;
using Shiny.Hosting;

namespace Shiny.Health;


public class HealthService(AndroidPlatform platform) : IHealthService, IAndroidLifecycle.IOnActivityResult
{
    const int REQUEST_CODE = 8765;
    TaskCompletionSource<bool>? permissionTcs;


    IHealthConnectClient GetClient()
        => IHealthConnectClient.GetOrCreate(platform.AppContext);


    public async Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params DataType[] dataTypes)
    {
        var client = GetClient();
        var neededPermissions = dataTypes.SelectMany(ToPermissionStrings).Distinct().ToList();

        var granted = await GetGrantedPermissionsAsync(client).ConfigureAwait(false);
        if (neededPermissions.All(granted.Contains))
            return dataTypes.Select(x => (x, true));

        permissionTcs = new TaskCompletionSource<bool>();
        var contract = new HealthPermissionsRequestContract();
        var intent = contract.CreateIntentImpl(platform.AppContext, neededPermissions);
        platform.CurrentActivity.StartActivityForResult(intent, REQUEST_CODE);
        await permissionTcs.Task.ConfigureAwait(false);

        granted = await GetGrantedPermissionsAsync(client).ConfigureAwait(false);
        return dataTypes.Select(dt =>
        {
            var perms = ToPermissionStrings(dt);
            return (dt, perms.All(granted.Contains));
        });
    }


    public void Handle(Activity activity, int requestCode, Result resultCode, Intent data)
    {
        if (requestCode == REQUEST_CODE)
            permissionTcs?.TrySetResult(true);
    }


    public Task<IList<NumericHealthResult>> GetStepCounts(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(
            start, end, interval,
            StepsRecord.CountTotal!,
            DataType.StepCount,
            result =>
            {
                if (result is Java.Lang.Long l) return l.LongValue();
                if (result is Java.Lang.Number n) return n.DoubleValue();
                return 0;
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetAverageHeartRate(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(
            start, end, interval,
            HeartRateRecord.BpmAvg!,
            DataType.HeartRate,
            result =>
            {
                if (result is Java.Lang.Long l) return l.LongValue();
                if (result is Java.Lang.Number n) return n.DoubleValue();
                return 0;
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetCalories(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(
            start, end, interval,
            TotalCaloriesBurnedRecord.EnergyTotal!,
            DataType.Calories,
            result =>
            {
                if (result is AndroidX.Health.Connect.Client.Units.Energy energy)
                    return energy.Kilocalories;
                if (result is Java.Lang.Number n) return n.DoubleValue();
                return 0;
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetDistances(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(
            start, end, interval,
            DistanceRecord.DistanceTotal!,
            DataType.Distance,
            result =>
            {
                if (result is AndroidX.Health.Connect.Client.Units.Length length)
                    return length.Meters;
                if (result is Java.Lang.Number n) return n.DoubleValue();
                return 0;
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetWeight(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(
            start, end, interval,
            WeightRecord.WeightAvg!,
            DataType.Weight,
            result =>
            {
                if (result is AndroidX.Health.Connect.Client.Units.Mass mass)
                    return mass.Kilograms;
                if (result is Java.Lang.Number n) return n.DoubleValue();
                return 0;
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetHeight(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(
            start, end, interval,
            HeightRecord.HeightAvg!,
            DataType.Height,
            result =>
            {
                if (result is AndroidX.Health.Connect.Client.Units.Length length)
                    return length.Meters;
                if (result is Java.Lang.Number n) return n.DoubleValue();
                return 0;
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetBodyFatPercentage(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryInstantaneousRecords<BodyFatRecord>(
            start, end, interval,
            DataType.BodyFatPercentage,
            record => record.Percentage.Value,
            record => record.Time,
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetRestingHeartRate(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(
            start, end, interval,
            RestingHeartRateRecord.BpmAvg!,
            DataType.RestingHeartRate,
            result =>
            {
                if (result is Java.Lang.Long l) return l.LongValue();
                if (result is Java.Lang.Number n) return n.DoubleValue();
                return 0;
            },
            cancelToken
        );


    public async Task<IList<BloodPressureResult>> GetBloodPressure(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        var startInstant = Instant.OfEpochMilli(start.ToUnixTimeMilliseconds())!;
        var endInstant = Instant.OfEpochMilli(end.ToUnixTimeMilliseconds())!;
        var duration = ToDuration(interval);

        var metrics = new List<AggregateMetric>
        {
            BloodPressureRecord.SystolicAvg!,
            BloodPressureRecord.DiastolicAvg!
        };
        var emptyOrigins = new List<DataOrigin>();

        var request = new AggregateGroupByDurationRequest(
            metrics,
            TimeRangeFilter.Between(startInstant, endInstant),
            duration,
            emptyOrigins
        );

        var response = await CallSuspendAsync(
            cont => client.AggregateGroupByDuration(request, cont)
        ).ConfigureAwait(false);

        var javaList = (System.Collections.IList)response;
        var list = new List<BloodPressureResult>();
        foreach (var item in javaList)
        {
            var bucket = (AggregationResultGroupedByDuration)item!;
            var sysRaw = bucket.Result.Get(BloodPressureRecord.SystolicAvg!);
            var diaRaw = bucket.Result.Get(BloodPressureRecord.DiastolicAvg!);
            var systolic = ExtractPressure(sysRaw);
            var diastolic = ExtractPressure(diaRaw);
            var bucketStart = DateTimeOffset.FromUnixTimeMilliseconds(bucket.StartTime.ToEpochMilli());
            var bucketEnd = DateTimeOffset.FromUnixTimeMilliseconds(bucket.EndTime.ToEpochMilli());
            list.Add(new BloodPressureResult(bucketStart, bucketEnd, systolic, diastolic));
        }
        return list;
    }


    public Task<IList<NumericHealthResult>> GetOxygenSaturation(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryInstantaneousRecords<OxygenSaturationRecord>(
            start, end, interval,
            DataType.OxygenSaturation,
            record => record.Percentage.Value,
            record => record.Time,
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetSleepDuration(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(
            start, end, interval,
            SleepSessionRecord.SleepDurationTotal!,
            DataType.SleepDuration,
            result =>
            {
                if (result is Duration d) return d.ToMillis() / 3600000.0;
                if (result is Java.Lang.Long l) return l.LongValue() / 3600000.0;
                if (result is Java.Lang.Number n) return n.DoubleValue() / 3600000.0;
                return 0;
            },
            cancelToken
        );


    public Task<IList<NumericHealthResult>> GetHydration(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(
            start, end, interval,
            HydrationRecord.VolumeTotal!,
            DataType.Hydration,
            result =>
            {
                if (result is AndroidX.Health.Connect.Client.Units.Volume volume)
                    return volume.Liters;
                if (result is Java.Lang.Number n) return n.DoubleValue();
                return 0;
            },
            cancelToken
        );


    static double ExtractPressure(Java.Lang.Object? raw)
    {
        if (raw is AndroidX.Health.Connect.Client.Units.Pressure p)
            return p.MillimetersOfMercury;
        if (raw is Java.Lang.Number n)
            return n.DoubleValue();
        return 0;
    }


    static Duration ToDuration(Interval interval) => interval switch
    {
        Interval.Minutes => Duration.OfMinutes(1)!,
        Interval.Hours => Duration.OfHours(1)!,
        Interval.Days => Duration.OfDays(1)!,
        _ => throw new InvalidOperationException("Invalid interval")
    };


    async Task<IList<NumericHealthResult>> QueryAggregate(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        AggregateMetric metric,
        DataType dataType,
        Func<Java.Lang.Object?, double> extractValue,
        CancellationToken cancelToken)
    {
        var client = GetClient();
        var startInstant = Instant.OfEpochMilli(start.ToUnixTimeMilliseconds())!;
        var endInstant = Instant.OfEpochMilli(end.ToUnixTimeMilliseconds())!;
        var duration = ToDuration(interval);

        var metrics = new List<AggregateMetric> { metric };
        var emptyOrigins = new List<DataOrigin>();

        var request = new AggregateGroupByDurationRequest(
            metrics,
            TimeRangeFilter.Between(startInstant, endInstant),
            duration,
            emptyOrigins
        );

        var response = await CallSuspendAsync(
            cont => client.AggregateGroupByDuration(request, cont)
        ).ConfigureAwait(false);

        var javaList = (System.Collections.IList)response;
        var list = new List<NumericHealthResult>();
        foreach (var item in javaList)
        {
            var bucket = (AggregationResultGroupedByDuration)item!;
            var rawValue = bucket.Result.Get(metric);
            var value = extractValue(rawValue);
            var bucketStart = DateTimeOffset.FromUnixTimeMilliseconds(bucket.StartTime.ToEpochMilli());
            var bucketEnd = DateTimeOffset.FromUnixTimeMilliseconds(bucket.EndTime.ToEpochMilli());
            list.Add(new NumericHealthResult(dataType, bucketStart, bucketEnd, value));
        }
        return list;
    }


    async Task<IList<NumericHealthResult>> QueryInstantaneousRecords<TRecord>(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        DataType dataType,
        Func<TRecord, double> extractValue,
        Func<TRecord, Instant> extractTime,
        CancellationToken cancelToken) where TRecord : Java.Lang.Object
    {
        var client = GetClient();
        var startInstant = Instant.OfEpochMilli(start.ToUnixTimeMilliseconds())!;
        var endInstant = Instant.OfEpochMilli(end.ToUnixTimeMilliseconds())!;

        var javaClass = Java.Lang.Class.FromType(typeof(TRecord));
        var kClass = JvmClassMappingKt.GetKotlinClass(javaClass);
        var request = new ReadRecordsRequest(
            kClass,
            TimeRangeFilter.Between(startInstant, endInstant),
            new List<DataOrigin>(),
            true,
            10000,
            null!
        );

        var response = await CallSuspendAsync(
            cont => client.ReadRecords(request, cont)
        ).ConfigureAwait(false);

        var readResponse = (ReadRecordsResponse)response;
        var records = new List<(DateTimeOffset Time, double Value)>();
        foreach (var item in readResponse.Records)
        {
            var record = (TRecord)item!;
            var time = extractTime(record);
            var dto = DateTimeOffset.FromUnixTimeMilliseconds(time.ToEpochMilli());
            records.Add((dto, extractValue(record)));
        }

        var buckets = GenerateBuckets(start, end, interval);
        var list = new List<NumericHealthResult>();
        foreach (var (bucketStart, bucketEnd) in buckets)
        {
            var bucketRecords = records.Where(r => r.Time >= bucketStart && r.Time < bucketEnd).ToList();
            var value = bucketRecords.Count > 0 ? bucketRecords.Average(r => r.Value) : 0;
            list.Add(new NumericHealthResult(dataType, bucketStart, bucketEnd, value));
        }
        return list;
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


    static string[] ToPermissionStrings(DataType dataType) => dataType switch
    {
        DataType.StepCount => ["android.permission.health.READ_STEPS"],
        DataType.HeartRate => ["android.permission.health.READ_HEART_RATE"],
        DataType.Calories => ["android.permission.health.READ_TOTAL_ENERGY_BURNED"],
        DataType.Distance => ["android.permission.health.READ_DISTANCE"],
        DataType.Weight => ["android.permission.health.READ_WEIGHT"],
        DataType.Height => ["android.permission.health.READ_HEIGHT"],
        DataType.BodyFatPercentage => ["android.permission.health.READ_BODY_FAT"],
        DataType.RestingHeartRate => ["android.permission.health.READ_RESTING_HEART_RATE"],
        DataType.BloodPressure => ["android.permission.health.READ_BLOOD_PRESSURE"],
        DataType.OxygenSaturation => ["android.permission.health.READ_OXYGEN_SATURATION"],
        DataType.SleepDuration => ["android.permission.health.READ_SLEEP"],
        DataType.Hydration => ["android.permission.health.READ_HYDRATION"],
        _ => throw new InvalidOperationException("Invalid DataType")
    };


    async Task<HashSet<string>> GetGrantedPermissionsAsync(IHealthConnectClient client)
    {
        var result = await CallSuspendAsync(
            cont => client.PermissionController.GetGrantedPermissions(cont)
        ).ConfigureAwait(false);

        var set = new HashSet<string>();
        if (result is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                set.Add(item?.ToString()!);
        }
        return set;
    }


    static Task<Java.Lang.Object> CallSuspendAsync(Func<IContinuation, Java.Lang.Object?> suspendFunction)
    {
        var tcs = new TaskCompletionSource<Java.Lang.Object>();
        var continuation = new SuspendContinuation(tcs);
        var immediateResult = suspendFunction(continuation);

        if (immediateResult != null && !IsCoroutineSuspended(immediateResult))
            tcs.TrySetResult(immediateResult);

        return tcs.Task;
    }


    static bool IsCoroutineSuspended(Java.Lang.Object value)
    {
        return value.Class?.Name == "kotlin.coroutines.intrinsics.CoroutineSingletons";
    }


    sealed class SuspendContinuation : Java.Lang.Object, IContinuation
    {
        readonly TaskCompletionSource<Java.Lang.Object> tcs;

        public SuspendContinuation(TaskCompletionSource<Java.Lang.Object> tcs) => this.tcs = tcs;

        public ICoroutineContext Context => EmptyCoroutineContext.Instance;

        public void ResumeWith(Java.Lang.Object result)
        {
            try
            {
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }
    }
}
