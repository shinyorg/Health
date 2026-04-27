using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using AndroidX.Health.Connect.Client;
using AndroidX.Health.Connect.Client.Aggregate;
using AndroidX.Health.Connect.Client.Changes;
using AndroidX.Health.Connect.Client.Contracts;
using AndroidX.Health.Connect.Client.Records;
using AndroidX.Health.Connect.Client.Records.Metadata;
using AndroidX.Health.Connect.Client.Request;
using AndroidX.Health.Connect.Client.Time;
using AndroidX.Health.Connect.Client.Response;
using Java.Time;
using Kotlin.Coroutines;
using Kotlin.Jvm;
using Android.Runtime;
using Shiny.Hosting;

namespace Shiny.Health;


public class HealthService(AndroidPlatform platform) : IHealthService, IAndroidLifecycle.IOnActivityResult
{
    const int REQUEST_CODE = 8765;
    TaskCompletionSource<bool>? permissionTcs;


    IHealthConnectClient GetClient()
        => IHealthConnectClient.GetOrCreate(platform.AppContext);


    public Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params DataType[] dataTypes)
        => RequestPermissions(PermissionType.Read, dataTypes);

    public Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(PermissionType permissionType, params DataType[] dataTypes)
        => RequestPermissions(dataTypes.Select(dt => (permissionType, dt)).ToArray());

    public async Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params (PermissionType Permission, DataType Type)[] permissions)
    {
        var client = GetClient();
        var neededPermissions = new List<string>();
        foreach (var (permissionType, dataType) in permissions)
        {
            if (permissionType.HasFlag(PermissionType.Read))
                neededPermissions.AddRange(ToReadPermissionStrings(dataType));
            if (permissionType.HasFlag(PermissionType.Write))
                neededPermissions.AddRange(ToWritePermissionStrings(dataType));
        }
        neededPermissions = neededPermissions.Distinct().ToList();

        var granted = await GetGrantedPermissionsAsync(client).ConfigureAwait(false);
        if (neededPermissions.All(granted.Contains))
            return permissions.Select(x => (x.Type, true));

        permissionTcs = new TaskCompletionSource<bool>();
        var contract = new HealthPermissionsRequestContract();
        var intent = contract.CreateIntentImpl(platform.AppContext, neededPermissions);
        platform.CurrentActivity.StartActivityForResult(intent, REQUEST_CODE);
        await permissionTcs.Task.ConfigureAwait(false);

        granted = await GetGrantedPermissionsAsync(client).ConfigureAwait(false);
        return permissions.Select(p =>
        {
            var perms = new List<string>();
            if (p.Permission.HasFlag(PermissionType.Read))
                perms.AddRange(ToReadPermissionStrings(p.Type));
            if (p.Permission.HasFlag(PermissionType.Write))
                perms.AddRange(ToWritePermissionStrings(p.Type));
            return (p.Type, perms.All(granted.Contains));
        });
    }


    public void Handle(Activity activity, int requestCode, Result resultCode, Intent data)
    {
        if (requestCode == REQUEST_CODE)
            permissionTcs?.TrySetResult(true);
    }


    public async IAsyncEnumerable<HealthResult> Observe(
        DataType dataType,
        TimeSpan? pollingInterval = null,
        [EnumeratorCancellation] CancellationToken cancelToken = default)
    {
        var interval = pollingInterval ?? TimeSpan.FromSeconds(5);
        var channel = Channel.CreateUnbounded<HealthResult>(new UnboundedChannelOptions { SingleWriter = true });
        var client = GetClient();
        var recordKClass = GetRecordKClass(dataType);

        _ = Task.Run(async () =>
        {
            try
            {
                var kClass = JvmClassMappingKt.GetKotlinClass(recordKClass);
                var tokenRequest = new ChangesTokenRequest(
                    new List<Kotlin.Reflect.IKClass> { kClass },
                    new List<DataOrigin>()
                );
                var tokenResponse = await CallSuspendAsync(
                    cont => client.GetChangesToken(tokenRequest, cont)
                ).ConfigureAwait(false);
                var token = tokenResponse.ToString()!;

                while (!cancelToken.IsCancellationRequested)
                {
                    await Task.Delay(interval, cancelToken).ConfigureAwait(false);

                    var changesResponse = await CallSuspendAsync(
                        cont => client.GetChanges(token, cont)
                    ).ConfigureAwait(false);

                    var response = (ChangesResponse)changesResponse;
                    foreach (var change in response.Changes)
                    {
                        if (change is UpsertionChange upsert)
                        {
                            var result = ConvertRecord(upsert.Record, dataType);
                            if (result != null)
                                channel.Writer.TryWrite(result);
                        }
                    }
                    token = response.NextChangesToken;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
                return;
            }
            channel.Writer.TryComplete();
        }, cancelToken);

        await foreach (var item in channel.Reader.ReadAllAsync(cancelToken).ConfigureAwait(false))
            yield return item;
    }


    static Java.Lang.Class GetRecordKClass(DataType dataType) => dataType switch
    {
        DataType.StepCount => Java.Lang.Class.FromType(typeof(StepsRecord)),
        DataType.HeartRate => Java.Lang.Class.FromType(typeof(HeartRateRecord)),
        DataType.Calories => Java.Lang.Class.FromType(typeof(TotalCaloriesBurnedRecord)),
        DataType.Distance => Java.Lang.Class.FromType(typeof(DistanceRecord)),
        DataType.Weight => Java.Lang.Class.FromType(typeof(WeightRecord)),
        DataType.Height => Java.Lang.Class.FromType(typeof(HeightRecord)),
        DataType.BodyFatPercentage => Java.Lang.Class.FromType(typeof(BodyFatRecord)),
        DataType.RestingHeartRate => Java.Lang.Class.FromType(typeof(RestingHeartRateRecord)),
        DataType.BloodPressure => Java.Lang.Class.FromType(typeof(BloodPressureRecord)),
        DataType.OxygenSaturation => Java.Lang.Class.FromType(typeof(OxygenSaturationRecord)),
        DataType.SleepDuration => Java.Lang.Class.FromType(typeof(SleepSessionRecord)),
        DataType.Hydration => Java.Lang.Class.FromType(typeof(HydrationRecord)),
        _ => throw new InvalidOperationException($"Unsupported data type: {dataType}")
    };


    static HealthResult? ConvertRecord(IRecord record, DataType dataType)
    {
        var obj = (Java.Lang.Object)record;
        switch (dataType)
        {
            case DataType.StepCount when obj is StepsRecord steps:
                return new NumericHealthResult(
                    dataType,
                    DateTimeOffset.FromUnixTimeMilliseconds(steps.StartTime.ToEpochMilli()),
                    DateTimeOffset.FromUnixTimeMilliseconds(steps.EndTime.ToEpochMilli()),
                    steps.Count
                );

            case DataType.HeartRate when obj is HeartRateRecord hr:
                var avgBpm = hr.Samples.Count > 0
                    ? hr.Samples.Average(s => s.BeatsPerMinute)
                    : 0;
                return new NumericHealthResult(
                    dataType,
                    DateTimeOffset.FromUnixTimeMilliseconds(hr.StartTime.ToEpochMilli()),
                    DateTimeOffset.FromUnixTimeMilliseconds(hr.EndTime.ToEpochMilli()),
                    avgBpm
                );

            case DataType.Calories when obj is TotalCaloriesBurnedRecord cal:
                return new NumericHealthResult(
                    dataType,
                    DateTimeOffset.FromUnixTimeMilliseconds(cal.StartTime.ToEpochMilli()),
                    DateTimeOffset.FromUnixTimeMilliseconds(cal.EndTime.ToEpochMilli()),
                    cal.Energy.Kilocalories
                );

            case DataType.Distance when obj is DistanceRecord dist:
                return new NumericHealthResult(
                    dataType,
                    DateTimeOffset.FromUnixTimeMilliseconds(dist.StartTime.ToEpochMilli()),
                    DateTimeOffset.FromUnixTimeMilliseconds(dist.EndTime.ToEpochMilli()),
                    dist.Distance.Meters
                );

            case DataType.Weight when obj is WeightRecord weight:
                var wTime = DateTimeOffset.FromUnixTimeMilliseconds(weight.Time.ToEpochMilli());
                return new NumericHealthResult(dataType, wTime, wTime, weight.Weight.Kilograms);

            case DataType.Height when obj is HeightRecord height:
                var hTime = DateTimeOffset.FromUnixTimeMilliseconds(height.Time.ToEpochMilli());
                return new NumericHealthResult(dataType, hTime, hTime, height.Height.Meters);

            case DataType.BodyFatPercentage when obj is BodyFatRecord bf:
                var bfTime = DateTimeOffset.FromUnixTimeMilliseconds(bf.Time.ToEpochMilli());
                return new NumericHealthResult(dataType, bfTime, bfTime, bf.Percentage.Value);

            case DataType.RestingHeartRate when obj is RestingHeartRateRecord rhr:
                var rhrTime = DateTimeOffset.FromUnixTimeMilliseconds(rhr.Time.ToEpochMilli());
                return new NumericHealthResult(dataType, rhrTime, rhrTime, rhr.BeatsPerMinute);

            case DataType.BloodPressure when obj is BloodPressureRecord bp:
                var bpTime = DateTimeOffset.FromUnixTimeMilliseconds(bp.Time.ToEpochMilli());
                return new BloodPressureResult(
                    bpTime, bpTime,
                    bp.Systolic.MillimetersOfMercury,
                    bp.Diastolic.MillimetersOfMercury
                );

            case DataType.OxygenSaturation when obj is OxygenSaturationRecord o2:
                var o2Time = DateTimeOffset.FromUnixTimeMilliseconds(o2.Time.ToEpochMilli());
                return new NumericHealthResult(dataType, o2Time, o2Time, o2.Percentage.Value);

            case DataType.SleepDuration when obj is SleepSessionRecord sleep:
                var sleepStart = DateTimeOffset.FromUnixTimeMilliseconds(sleep.StartTime.ToEpochMilli());
                var sleepEnd = DateTimeOffset.FromUnixTimeMilliseconds(sleep.EndTime.ToEpochMilli());
                var hours = (sleepEnd - sleepStart).TotalHours;
                return new NumericHealthResult(dataType, sleepStart, sleepEnd, hours);

            case DataType.Hydration when obj is HydrationRecord hydration:
                return new NumericHealthResult(
                    dataType,
                    DateTimeOffset.FromUnixTimeMilliseconds(hydration.StartTime.ToEpochMilli()),
                    DateTimeOffset.FromUnixTimeMilliseconds(hydration.EndTime.ToEpochMilli()),
                    hydration.Volume.Liters
                );

            default:
                return null;
        }
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


    public async Task Write(NumericHealthResult result, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        var startInstant = Instant.OfEpochMilli(result.Start.ToUnixTimeMilliseconds())!;
        var endInstant = Instant.OfEpochMilli(result.End.ToUnixTimeMilliseconds())!;
        var zoneOffset = ZoneOffset.OfTotalSeconds((int)result.Start.Offset.TotalSeconds)!;
        var metadata = Metadata.UnknownRecordingMethod();

        Java.Lang.Object record = result.DataType switch
        {
            DataType.StepCount => new StepsRecord(startInstant, zoneOffset, endInstant, zoneOffset, (long)result.Value, metadata),
            DataType.HeartRate => CreateHeartRateRecord(startInstant, zoneOffset, endInstant, (long)result.Value, metadata),
            DataType.Calories => new TotalCaloriesBurnedRecord(startInstant, zoneOffset, endInstant, zoneOffset, AndroidX.Health.Connect.Client.Units.Energy.InvokeKilocalories(result.Value), metadata),
            DataType.Distance => new DistanceRecord(startInstant, zoneOffset, endInstant, zoneOffset, AndroidX.Health.Connect.Client.Units.Length.InvokeMeters(result.Value), metadata),
            DataType.Weight => new WeightRecord(startInstant, zoneOffset, AndroidX.Health.Connect.Client.Units.Mass.InvokeKilograms(result.Value), metadata),
            DataType.Height => new HeightRecord(startInstant, zoneOffset, AndroidX.Health.Connect.Client.Units.Length.InvokeMeters(result.Value), metadata),
            DataType.BodyFatPercentage => new BodyFatRecord(startInstant, zoneOffset, new AndroidX.Health.Connect.Client.Units.Percentage(result.Value), metadata),
            DataType.RestingHeartRate => new RestingHeartRateRecord(startInstant, zoneOffset, (long)result.Value, metadata),
            DataType.OxygenSaturation => new OxygenSaturationRecord(startInstant, zoneOffset, new AndroidX.Health.Connect.Client.Units.Percentage(result.Value), metadata),
            DataType.SleepDuration => new SleepSessionRecord(startInstant, zoneOffset, endInstant, zoneOffset, metadata, null, null, new List<SleepSessionRecord.Stage>()),
            DataType.Hydration => new HydrationRecord(startInstant, zoneOffset, endInstant, zoneOffset, AndroidX.Health.Connect.Client.Units.Volume.InvokeLiters(result.Value), metadata),
            _ => throw new InvalidOperationException($"Unsupported data type for writing: {result.DataType}")
        };

        await InsertRecord(client, record).ConfigureAwait(false);
    }


    public async Task Write(BloodPressureResult result, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        var instant = Instant.OfEpochMilli(result.Start.ToUnixTimeMilliseconds())!;
        var zoneOffset = ZoneOffset.OfTotalSeconds((int)result.Start.Offset.TotalSeconds)!;

        var record = new BloodPressureRecord(
            instant,
            zoneOffset,
            Metadata.UnknownRecordingMethod(),
            AndroidX.Health.Connect.Client.Units.Pressure.InvokeMillimetersOfMercury(result.Systolic),
            AndroidX.Health.Connect.Client.Units.Pressure.InvokeMillimetersOfMercury(result.Diastolic),
            (int)BloodPressureRecord.BodyPositionUnknown,
            (int)BloodPressureRecord.MeasurementLocationUnknown
        );

        await InsertRecord(client, (Java.Lang.Object)record).ConfigureAwait(false);
    }


    static HeartRateRecord CreateHeartRateRecord(Instant start, ZoneOffset offset, Instant end, long bpm, Metadata metadata)
    {
        var samples = new List<HeartRateRecord.Sample> { new HeartRateRecord.Sample(start, bpm) };
        return new HeartRateRecord(start, offset, end, offset, samples, metadata);
    }


    async Task InsertRecord(IHealthConnectClient client, Java.Lang.Object record)
    {
        var records = new List<IRecord> { record.JavaCast<IRecord>() };
        await CallSuspendAsync(cont => client.InsertRecords(records, cont)).ConfigureAwait(false);
    }


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


    static string[] ToReadPermissionStrings(DataType dataType) => dataType switch
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


    static string[] ToWritePermissionStrings(DataType dataType) => dataType switch
    {
        DataType.StepCount => ["android.permission.health.WRITE_STEPS"],
        DataType.HeartRate => ["android.permission.health.WRITE_HEART_RATE"],
        DataType.Calories => ["android.permission.health.WRITE_TOTAL_ENERGY_BURNED"],
        DataType.Distance => ["android.permission.health.WRITE_DISTANCE"],
        DataType.Weight => ["android.permission.health.WRITE_WEIGHT"],
        DataType.Height => ["android.permission.health.WRITE_HEIGHT"],
        DataType.BodyFatPercentage => ["android.permission.health.WRITE_BODY_FAT"],
        DataType.RestingHeartRate => ["android.permission.health.WRITE_RESTING_HEART_RATE"],
        DataType.BloodPressure => ["android.permission.health.WRITE_BLOOD_PRESSURE"],
        DataType.OxygenSaturation => ["android.permission.health.WRITE_OXYGEN_SATURATION"],
        DataType.SleepDuration => ["android.permission.health.WRITE_SLEEP"],
        DataType.Hydration => ["android.permission.health.WRITE_HYDRATION"],
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
