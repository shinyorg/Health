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

    // Health Connect rejects a ReadRecordsRequest pageSize outside 1-5000
    const int MAX_PAGE_SIZE = 5000;

    // androidx.activity.result.contract.ActivityResultContracts.RequestMultiplePermissions
    const string ACTION_REQUEST_PERMISSIONS = "androidx.activity.result.contract.action.REQUEST_PERMISSIONS";
    const string EXTRA_PERMISSIONS = "androidx.activity.result.contract.extra.PERMISSIONS";

    TaskCompletionSource<bool>? permissionTcs;


    public bool IsAvailable
    {
        get
        {
            var status = HealthConnectClient.GetSdkStatus(platform.AppContext);
            return status == HealthConnectClient.SdkAvailable;
        }
    }


    IHealthConnectClient GetClient()
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Health Connect is not available on this device. Ensure the Health Connect app is installed.");

        return IHealthConnectClient.GetOrCreate(platform.AppContext);
    }


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

        var contract = new HealthPermissionsRequestContract();
        var intent = contract.CreateIntentImpl(platform.AppContext, neededPermissions);

        if (intent.Action == ACTION_REQUEST_PERMISSIONS)
        {
            // Android 14+ ships Health Connect as part of the platform, so the contract delegates to
            // AndroidX's RequestMultiplePermissions - a pseudo-intent that only the AndroidX activity
            // result registry understands, no activity can handle it.  Health permissions are plain
            // runtime permissions there, so hand it to Shiny.
            var runtimePermissions = intent.GetStringArrayExtra(EXTRA_PERMISSIONS) ?? neededPermissions.ToArray();
            await platform.RequestPermissions(runtimePermissions).ConfigureAwait(false);
        }
        else
        {
            // pre-Android 14 the permissions belong to the Health Connect APK, which exposes its own
            // permission activity
            var activity = platform.CurrentActivity
                ?? throw new InvalidOperationException("No current activity available. Ensure permissions are requested after the activity has been created.");

            permissionTcs = new TaskCompletionSource<bool>();
            activity.StartActivityForResult(intent, REQUEST_CODE);
            await permissionTcs.Task.ConfigureAwait(false);
        }

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

                    var response = changesResponse.JavaCast<ChangesResponse>();
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
        DataType.MenstruationFlow => Java.Lang.Class.FromType(typeof(MenstruationFlowRecord)),
        DataType.BloodGlucose => Java.Lang.Class.FromType(typeof(BloodGlucoseRecord)),
        DataType.BodyTemperature => Java.Lang.Class.FromType(typeof(BodyTemperatureRecord)),
        DataType.BasalBodyTemperature => Java.Lang.Class.FromType(typeof(BasalBodyTemperatureRecord)),
        DataType.RespiratoryRate => Java.Lang.Class.FromType(typeof(RespiratoryRateRecord)),
        DataType.Vo2Max => Java.Lang.Class.FromType(typeof(Vo2MaxRecord)),
        DataType.HeartRateVariability => Java.Lang.Class.FromType(typeof(HeartRateVariabilityRmssdRecord)),
        DataType.LeanBodyMass => Java.Lang.Class.FromType(typeof(LeanBodyMassRecord)),
        DataType.BasalEnergyBurned => Java.Lang.Class.FromType(typeof(BasalMetabolicRateRecord)),
        DataType.ActiveEnergyBurned => Java.Lang.Class.FromType(typeof(ActiveCaloriesBurnedRecord)),
        DataType.FloorsClimbed => Java.Lang.Class.FromType(typeof(FloorsClimbedRecord)),
        DataType.WheelchairPushes => Java.Lang.Class.FromType(typeof(WheelchairPushesRecord)),
        DataType.Speed => Java.Lang.Class.FromType(typeof(SpeedRecord)),
        DataType.Power => Java.Lang.Class.FromType(typeof(PowerRecord)),
        DataType.SexualActivity => Java.Lang.Class.FromType(typeof(SexualActivityRecord)),
        DataType.OvulationTest => Java.Lang.Class.FromType(typeof(OvulationTestRecord)),
        DataType.CervicalMucus => Java.Lang.Class.FromType(typeof(CervicalMucusRecord)),
        DataType.IntermenstrualBleeding => Java.Lang.Class.FromType(typeof(IntermenstrualBleedingRecord)),
        DataType.Workout => Java.Lang.Class.FromType(typeof(ExerciseSessionRecord)),
        DataType.Nutrition => Java.Lang.Class.FromType(typeof(NutritionRecord)),
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

            case DataType.MenstruationFlow when obj is MenstruationFlowRecord menstruation:
                var mTime = DateTimeOffset.FromUnixTimeMilliseconds(menstruation.Time.ToEpochMilli());
                return new MenstruationFlowResult(mTime, mTime, FromNativeFlow(menstruation.Flow));

            case DataType.BloodGlucose when obj is BloodGlucoseRecord glucose:
                return PointResult(dataType, glucose.Time, glucose.Level.MilligramsPerDeciliter);

            case DataType.BodyTemperature when obj is BodyTemperatureRecord temp:
                return PointResult(dataType, temp.Time, temp.Temperature.Celsius);

            case DataType.BasalBodyTemperature when obj is BasalBodyTemperatureRecord basalTemp:
                return PointResult(dataType, basalTemp.Time, basalTemp.Temperature.Celsius);

            case DataType.RespiratoryRate when obj is RespiratoryRateRecord resp:
                return PointResult(dataType, resp.Time, resp.Rate);

            case DataType.Vo2Max when obj is Vo2MaxRecord vo2:
                return PointResult(dataType, vo2.Time, vo2.Vo2MillilitersPerMinuteKilogram);

            case DataType.HeartRateVariability when obj is HeartRateVariabilityRmssdRecord hrv:
                return PointResult(dataType, hrv.Time, hrv.HeartRateVariabilityMillis);

            case DataType.LeanBodyMass when obj is LeanBodyMassRecord lean:
                return PointResult(dataType, lean.Time, lean.Mass.Kilograms);

            case DataType.BasalEnergyBurned when obj is BasalMetabolicRateRecord bmr:
                return PointResult(dataType, bmr.Time, bmr.BasalMetabolicRate.KilocaloriesPerDay);

            case DataType.ActiveEnergyBurned when obj is ActiveCaloriesBurnedRecord active:
                return new NumericHealthResult(
                    dataType,
                    DateTimeOffset.FromUnixTimeMilliseconds(active.StartTime.ToEpochMilli()),
                    DateTimeOffset.FromUnixTimeMilliseconds(active.EndTime.ToEpochMilli()),
                    active.Energy.Kilocalories
                );

            case DataType.FloorsClimbed when obj is FloorsClimbedRecord floors:
                return new NumericHealthResult(
                    dataType,
                    DateTimeOffset.FromUnixTimeMilliseconds(floors.StartTime.ToEpochMilli()),
                    DateTimeOffset.FromUnixTimeMilliseconds(floors.EndTime.ToEpochMilli()),
                    floors.Floors
                );

            case DataType.WheelchairPushes when obj is WheelchairPushesRecord pushes:
                return new NumericHealthResult(
                    dataType,
                    DateTimeOffset.FromUnixTimeMilliseconds(pushes.StartTime.ToEpochMilli()),
                    DateTimeOffset.FromUnixTimeMilliseconds(pushes.EndTime.ToEpochMilli()),
                    pushes.Count
                );

            case DataType.Speed when obj is SpeedRecord speed:
                var avgSpeed = speed.Samples.Count > 0 ? speed.Samples.Average(s => s.Speed.MetersPerSecond) : 0;
                return new NumericHealthResult(
                    dataType,
                    DateTimeOffset.FromUnixTimeMilliseconds(speed.StartTime.ToEpochMilli()),
                    DateTimeOffset.FromUnixTimeMilliseconds(speed.EndTime.ToEpochMilli()),
                    avgSpeed
                );

            case DataType.Power when obj is PowerRecord power:
                var avgPower = power.Samples.Count > 0 ? power.Samples.Average(s => s.Power.Watts) : 0;
                return new NumericHealthResult(
                    dataType,
                    DateTimeOffset.FromUnixTimeMilliseconds(power.StartTime.ToEpochMilli()),
                    DateTimeOffset.FromUnixTimeMilliseconds(power.EndTime.ToEpochMilli()),
                    avgPower
                );

            case DataType.SexualActivity when obj is SexualActivityRecord sexual:
                var saTime = DateTimeOffset.FromUnixTimeMilliseconds(sexual.Time.ToEpochMilli());
                return new SexualActivityResult(saTime, saTime, FromNativeProtection(sexual.ProtectionUsed));

            case DataType.OvulationTest when obj is OvulationTestRecord ovulation:
                var ovTime = DateTimeOffset.FromUnixTimeMilliseconds(ovulation.Time.ToEpochMilli());
                return new OvulationTestResult(ovTime, ovTime, FromNativeOvulation(ovulation.Result));

            case DataType.CervicalMucus when obj is CervicalMucusRecord mucus:
                var cmTime = DateTimeOffset.FromUnixTimeMilliseconds(mucus.Time.ToEpochMilli());
                return new CervicalMucusResult(cmTime, cmTime, FromNativeMucus(mucus.Appearance));

            case DataType.IntermenstrualBleeding when obj is IntermenstrualBleedingRecord bleeding:
                var ibTime = DateTimeOffset.FromUnixTimeMilliseconds(bleeding.Time.ToEpochMilli());
                return new IntermenstrualBleedingResult(ibTime, ibTime);

            case DataType.Workout when obj is ExerciseSessionRecord exercise:
                return ConvertExercise(exercise);

            case DataType.Nutrition when obj is NutritionRecord nutrition:
                return ConvertNutrition(nutrition);

            default:
                return null;
        }
    }


    static NumericHealthResult PointResult(DataType dataType, Instant time, double value)
    {
        var dto = DateTimeOffset.FromUnixTimeMilliseconds(time.ToEpochMilli());
        return new NumericHealthResult(dataType, dto, dto, value);
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

        var list = new List<BloodPressureResult>();
        foreach (var bucket in ToManagedList<AggregationResultGroupedByDuration>(response))
        {
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


    public Task<IList<NumericHealthResult>> GetBloodGlucose(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryInstantaneousRecords<BloodGlucoseRecord>(start, end, interval, DataType.BloodGlucose, r => r.Level.MilligramsPerDeciliter, r => r.Time, cancelToken);

    public Task<IList<NumericHealthResult>> GetBodyTemperature(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryInstantaneousRecords<BodyTemperatureRecord>(start, end, interval, DataType.BodyTemperature, r => r.Temperature.Celsius, r => r.Time, cancelToken);

    public Task<IList<NumericHealthResult>> GetBasalBodyTemperature(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryInstantaneousRecords<BasalBodyTemperatureRecord>(start, end, interval, DataType.BasalBodyTemperature, r => r.Temperature.Celsius, r => r.Time, cancelToken);

    public Task<IList<NumericHealthResult>> GetRespiratoryRate(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryInstantaneousRecords<RespiratoryRateRecord>(start, end, interval, DataType.RespiratoryRate, r => r.Rate, r => r.Time, cancelToken);

    public Task<IList<NumericHealthResult>> GetVo2Max(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryInstantaneousRecords<Vo2MaxRecord>(start, end, interval, DataType.Vo2Max, r => r.Vo2MillilitersPerMinuteKilogram, r => r.Time, cancelToken);

    public Task<IList<NumericHealthResult>> GetHeartRateVariability(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryInstantaneousRecords<HeartRateVariabilityRmssdRecord>(start, end, interval, DataType.HeartRateVariability, r => r.HeartRateVariabilityMillis, r => r.Time, cancelToken);

    public Task<IList<NumericHealthResult>> GetLeanBodyMass(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryInstantaneousRecords<LeanBodyMassRecord>(start, end, interval, DataType.LeanBodyMass, r => r.Mass.Kilograms, r => r.Time, cancelToken);

    public Task<IList<NumericHealthResult>> GetBasalEnergyBurned(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(start, end, interval, BasalMetabolicRateRecord.BasalCaloriesTotal!, DataType.BasalEnergyBurned, ExtractEnergy, cancelToken);

    public Task<IList<NumericHealthResult>> GetActiveEnergyBurned(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(start, end, interval, ActiveCaloriesBurnedRecord.ActiveCaloriesTotal!, DataType.ActiveEnergyBurned, ExtractEnergy, cancelToken);

    public Task<IList<NumericHealthResult>> GetFloorsClimbed(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(start, end, interval, FloorsClimbedRecord.FloorsClimbedTotal!, DataType.FloorsClimbed, ExtractNumber, cancelToken);

    public Task<IList<NumericHealthResult>> GetWheelchairPushes(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(start, end, interval, WheelchairPushesRecord.CountTotal!, DataType.WheelchairPushes, ExtractNumber, cancelToken);

    public Task<IList<NumericHealthResult>> GetSpeed(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(start, end, interval, SpeedRecord.SpeedAvg!, DataType.Speed, result =>
        {
            if (result is AndroidX.Health.Connect.Client.Units.Velocity v) return v.MetersPerSecond;
            if (result is Java.Lang.Number n) return n.DoubleValue();
            return 0;
        }, cancelToken);

    public Task<IList<NumericHealthResult>> GetPower(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
        => QueryAggregate(start, end, interval, PowerRecord.PowerAvg!, DataType.Power, result =>
        {
            if (result is AndroidX.Health.Connect.Client.Units.Power p) return p.Watts;
            if (result is Java.Lang.Number n) return n.DoubleValue();
            return 0;
        }, cancelToken);


    static double ExtractEnergy(Java.Lang.Object? result)
    {
        if (result is AndroidX.Health.Connect.Client.Units.Energy energy) return energy.Kilocalories;
        if (result is Java.Lang.Number n) return n.DoubleValue();
        return 0;
    }

    static double ExtractNumber(Java.Lang.Object? result)
    {
        if (result is Java.Lang.Long l) return l.LongValue();
        if (result is Java.Lang.Number n) return n.DoubleValue();
        return 0;
    }


    public async Task<IList<SexualActivityResult>> GetSexualActivity(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var records = await ReadRecords<SexualActivityRecord>(start, end).ConfigureAwait(false);
        return records.Select(r =>
        {
            var t = DateTimeOffset.FromUnixTimeMilliseconds(r.Time.ToEpochMilli());
            return new SexualActivityResult(t, t, FromNativeProtection(r.ProtectionUsed));
        }).ToList();
    }


    public async Task<IList<OvulationTestResult>> GetOvulationTests(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var records = await ReadRecords<OvulationTestRecord>(start, end).ConfigureAwait(false);
        return records.Select(r =>
        {
            var t = DateTimeOffset.FromUnixTimeMilliseconds(r.Time.ToEpochMilli());
            return new OvulationTestResult(t, t, FromNativeOvulation(r.Result));
        }).ToList();
    }


    public async Task<IList<CervicalMucusResult>> GetCervicalMucus(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var records = await ReadRecords<CervicalMucusRecord>(start, end).ConfigureAwait(false);
        return records.Select(r =>
        {
            var t = DateTimeOffset.FromUnixTimeMilliseconds(r.Time.ToEpochMilli());
            return new CervicalMucusResult(t, t, FromNativeMucus(r.Appearance));
        }).ToList();
    }


    public async Task<IList<IntermenstrualBleedingResult>> GetIntermenstrualBleeding(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var records = await ReadRecords<IntermenstrualBleedingRecord>(start, end).ConfigureAwait(false);
        return records.Select(r =>
        {
            var t = DateTimeOffset.FromUnixTimeMilliseconds(r.Time.ToEpochMilli());
            return new IntermenstrualBleedingResult(t, t);
        }).ToList();
    }


    public async Task<IList<WorkoutResult>> GetWorkouts(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var records = await ReadRecords<ExerciseSessionRecord>(start, end).ConfigureAwait(false);
        return records.Select(ConvertExercise).ToList();
    }


    public async Task<IList<NutritionResult>> GetNutrition(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var records = await ReadRecords<NutritionRecord>(start, end).ConfigureAwait(false);
        return records.Select(ConvertNutrition).ToList();
    }


    public async Task<IList<MenstruationFlowResult>> GetMenstruationFlow(DateTimeOffset start, DateTimeOffset end, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        var startInstant = Instant.OfEpochMilli(start.ToUnixTimeMilliseconds())!;
        var endInstant = Instant.OfEpochMilli(end.ToUnixTimeMilliseconds())!;

        var javaClass = Java.Lang.Class.FromType(typeof(MenstruationFlowRecord));
        var kClass = JvmClassMappingKt.GetKotlinClass(javaClass);
        var request = new ReadRecordsRequest(
            kClass,
            TimeRangeFilter.Between(startInstant, endInstant),
            new List<DataOrigin>(),
            true,
            MAX_PAGE_SIZE,
            null!
        );

        var response = await CallSuspendAsync(
            cont => client.ReadRecords(request, cont)
        ).ConfigureAwait(false);

        var readResponse = response.JavaCast<ReadRecordsResponse>();
        var list = new List<MenstruationFlowResult>();
        foreach (var item in readResponse.Records)
        {
            var record = (MenstruationFlowRecord)item!;
            var time = DateTimeOffset.FromUnixTimeMilliseconds(record.Time.ToEpochMilli());
            list.Add(new MenstruationFlowResult(time, time, FromNativeFlow(record.Flow)));
        }
        return list;
    }


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
            DataType.BloodGlucose => new BloodGlucoseRecord(startInstant, zoneOffset, metadata, AndroidX.Health.Connect.Client.Units.BloodGlucose.InvokeMilligramsPerDeciliter(result.Value), (int)BloodGlucoseRecord.SpecimenSourceUnknown, (int)AndroidX.Health.Connect.Client.Records.MealType.MealTypeUnknown, (int)BloodGlucoseRecord.RelationToMealUnknown),
            DataType.BodyTemperature => new BodyTemperatureRecord(startInstant, zoneOffset, metadata, AndroidX.Health.Connect.Client.Units.Temperature.InvokeCelsius(result.Value), 0),
            DataType.BasalBodyTemperature => new BasalBodyTemperatureRecord(startInstant, zoneOffset, metadata, AndroidX.Health.Connect.Client.Units.Temperature.InvokeCelsius(result.Value), 0),
            DataType.RespiratoryRate => new RespiratoryRateRecord(startInstant, zoneOffset, result.Value, metadata),
            DataType.Vo2Max => new Vo2MaxRecord(startInstant, zoneOffset, metadata, result.Value, (int)Vo2MaxRecord.MeasurementMethodOther),
            DataType.HeartRateVariability => new HeartRateVariabilityRmssdRecord(startInstant, zoneOffset, result.Value, metadata),
            DataType.LeanBodyMass => new LeanBodyMassRecord(startInstant, zoneOffset, AndroidX.Health.Connect.Client.Units.Mass.InvokeKilograms(result.Value), metadata),
            DataType.BasalEnergyBurned => new BasalMetabolicRateRecord(startInstant, zoneOffset, AndroidX.Health.Connect.Client.Units.Power.InvokeKilocaloriesPerDay(result.Value), metadata),
            DataType.ActiveEnergyBurned => new ActiveCaloriesBurnedRecord(startInstant, zoneOffset, endInstant, zoneOffset, AndroidX.Health.Connect.Client.Units.Energy.InvokeKilocalories(result.Value), metadata),
            DataType.FloorsClimbed => new FloorsClimbedRecord(startInstant, zoneOffset, endInstant, zoneOffset, result.Value, metadata),
            DataType.WheelchairPushes => new WheelchairPushesRecord(startInstant, zoneOffset, endInstant, zoneOffset, (long)result.Value, metadata),
            DataType.Speed => new SpeedRecord(startInstant, zoneOffset, endInstant, zoneOffset, new List<SpeedRecord.Sample> { new SpeedRecord.Sample(startInstant, AndroidX.Health.Connect.Client.Units.Velocity.InvokeMetersPerSecond(result.Value)) }, metadata),
            DataType.Power => new PowerRecord(startInstant, zoneOffset, endInstant, zoneOffset, new List<PowerRecord.Sample> { new PowerRecord.Sample(startInstant, AndroidX.Health.Connect.Client.Units.Power.InvokeWatts(result.Value)) }, metadata),
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


    public async Task Write(MenstruationFlowResult result, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        var instant = Instant.OfEpochMilli(result.Start.ToUnixTimeMilliseconds())!;
        var zoneOffset = ZoneOffset.OfTotalSeconds((int)result.Start.Offset.TotalSeconds)!;

        var record = new MenstruationFlowRecord(
            instant,
            zoneOffset,
            Metadata.UnknownRecordingMethod(),
            ToNativeFlow(result.Flow)
        );

        await InsertRecord(client, (Java.Lang.Object)record).ConfigureAwait(false);
    }


    public async Task Write(SexualActivityResult result, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        var instant = Instant.OfEpochMilli(result.Start.ToUnixTimeMilliseconds())!;
        var zoneOffset = ZoneOffset.OfTotalSeconds((int)result.Start.Offset.TotalSeconds)!;
        var record = new SexualActivityRecord(instant, zoneOffset, Metadata.UnknownRecordingMethod(), ToNativeProtection(result.Protection));
        await InsertRecord(client, (Java.Lang.Object)record).ConfigureAwait(false);
    }


    public async Task Write(OvulationTestResult result, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        var instant = Instant.OfEpochMilli(result.Start.ToUnixTimeMilliseconds())!;
        var zoneOffset = ZoneOffset.OfTotalSeconds((int)result.Start.Offset.TotalSeconds)!;
        var record = new OvulationTestRecord(instant, zoneOffset, ToNativeOvulation(result.Outcome), Metadata.UnknownRecordingMethod());
        await InsertRecord(client, (Java.Lang.Object)record).ConfigureAwait(false);
    }


    public async Task Write(CervicalMucusResult result, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        var instant = Instant.OfEpochMilli(result.Start.ToUnixTimeMilliseconds())!;
        var zoneOffset = ZoneOffset.OfTotalSeconds((int)result.Start.Offset.TotalSeconds)!;
        var record = new CervicalMucusRecord(instant, zoneOffset, Metadata.UnknownRecordingMethod(), ToNativeMucus(result.Appearance), (int)CervicalMucusRecord.SensationUnknown);
        await InsertRecord(client, (Java.Lang.Object)record).ConfigureAwait(false);
    }


    public async Task Write(IntermenstrualBleedingResult result, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        var instant = Instant.OfEpochMilli(result.Start.ToUnixTimeMilliseconds())!;
        var zoneOffset = ZoneOffset.OfTotalSeconds((int)result.Start.Offset.TotalSeconds)!;
        var record = new IntermenstrualBleedingRecord(instant, zoneOffset, Metadata.UnknownRecordingMethod());
        await InsertRecord(client, (Java.Lang.Object)record).ConfigureAwait(false);
    }


    public async Task Write(WorkoutResult result, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        var startInstant = Instant.OfEpochMilli(result.Start.ToUnixTimeMilliseconds())!;
        var endInstant = Instant.OfEpochMilli(result.End.ToUnixTimeMilliseconds())!;
        var zoneOffset = ZoneOffset.OfTotalSeconds((int)result.Start.Offset.TotalSeconds)!;
        var record = new ExerciseSessionRecord(
            startInstant, zoneOffset, endInstant, zoneOffset,
            Metadata.UnknownRecordingMethod(),
            ToNativeExerciseType(result.Workout),
            result.Title ?? ""
        );
        await InsertRecord(client, (Java.Lang.Object)record).ConfigureAwait(false);
    }


    public async Task Write(NutritionResult result, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        var startInstant = Instant.OfEpochMilli(result.Start.ToUnixTimeMilliseconds())!;
        var endInstant = Instant.OfEpochMilli(result.End.ToUnixTimeMilliseconds())!;
        var zoneOffset = ZoneOffset.OfTotalSeconds((int)result.Start.Offset.TotalSeconds)!;

        AndroidX.Health.Connect.Client.Units.Mass? Grams(double? v)
            => v is double x ? AndroidX.Health.Connect.Client.Units.Mass.InvokeGrams(x) : null;
        var energy = result.EnergyKilocalories is double kcal
            ? AndroidX.Health.Connect.Client.Units.Energy.InvokeKilocalories(kcal)
            : null;

        var record = new NutritionRecord(
            startInstant, zoneOffset, endInstant, zoneOffset,
            Metadata.UnknownRecordingMethod(),
            null,                          // biotin
            null,                          // caffeine
            null,                          // calcium
            energy,                        // energy
            null,                          // energyFromFat
            null,                          // chloride
            Grams(result.CholesterolGrams),// cholesterol
            null,                          // chromium
            null,                          // copper
            Grams(result.FiberGrams),      // dietaryFiber
            null,                          // folate
            null,                          // folicAcid
            null,                          // iodine
            null,                          // iron
            null,                          // magnesium
            null,                          // manganese
            null,                          // molybdenum
            null,                          // monounsaturatedFat
            null,                          // niacin
            null,                          // pantothenicAcid
            null,                          // phosphorus
            null,                          // polyunsaturatedFat
            null,                          // potassium
            Grams(result.ProteinGrams),    // protein
            null,                          // riboflavin
            null,                          // saturatedFat
            null,                          // selenium
            Grams(result.SodiumGrams),     // sodium
            Grams(result.SugarGrams),      // sugar
            null,                          // thiamin
            Grams(result.CarbohydratesGrams), // totalCarbohydrate
            Grams(result.TotalFatGrams),   // totalFat
            null,                          // transFat
            null,                          // unsaturatedFat
            null,                          // vitaminA
            null,                          // vitaminB12
            null,                          // vitaminB6
            null,                          // vitaminC
            null,                          // vitaminD
            null,                          // vitaminE
            null,                          // vitaminK
            null,                          // zinc
            result.Name,                   // name
            ToNativeMealType(result.Meal)  // mealType
        );
        await InsertRecord(client, (Java.Lang.Object)record).ConfigureAwait(false);
    }


    async Task<IList<TRecord>> ReadRecords<TRecord>(DateTimeOffset start, DateTimeOffset end) where TRecord : Java.Lang.Object
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
            MAX_PAGE_SIZE,
            null!
        );

        var response = await CallSuspendAsync(cont => client.ReadRecords(request, cont)).ConfigureAwait(false);
        var readResponse = response.JavaCast<ReadRecordsResponse>();
        var list = new List<TRecord>();
        foreach (var item in readResponse.Records)
            list.Add((TRecord)item!);
        return list;
    }


    static WorkoutResult ConvertExercise(ExerciseSessionRecord r)
    {
        var start = DateTimeOffset.FromUnixTimeMilliseconds(r.StartTime.ToEpochMilli());
        var end = DateTimeOffset.FromUnixTimeMilliseconds(r.EndTime.ToEpochMilli());
        return new WorkoutResult(start, end, FromNativeExerciseType(r.ExerciseType), null, null, r.Title);
    }


    static NutritionResult ConvertNutrition(NutritionRecord r)
    {
        var start = DateTimeOffset.FromUnixTimeMilliseconds(r.StartTime.ToEpochMilli());
        var end = DateTimeOffset.FromUnixTimeMilliseconds(r.EndTime.ToEpochMilli());
        return new NutritionResult(
            start, end,
            FromNativeMealType(r.MealType),
            r.Name,
            r.Energy?.Kilocalories,
            r.Protein?.Grams,
            r.TotalCarbohydrate?.Grams,
            r.TotalFat?.Grams,
            r.DietaryFiber?.Grams,
            r.Sugar?.Grams,
            r.Sodium?.Grams,
            r.Cholesterol?.Grams
        );
    }


    static SexualActivityProtection FromNativeProtection(int protection)
    {
        if (protection == (int)SexualActivityRecord.ProtectionUsedProtected) return SexualActivityProtection.Protected;
        if (protection == (int)SexualActivityRecord.ProtectionUsedUnprotected) return SexualActivityProtection.Unprotected;
        return SexualActivityProtection.Unspecified;
    }

    static int ToNativeProtection(SexualActivityProtection protection) => protection switch
    {
        SexualActivityProtection.Protected => (int)SexualActivityRecord.ProtectionUsedProtected,
        SexualActivityProtection.Unprotected => (int)SexualActivityRecord.ProtectionUsedUnprotected,
        _ => (int)SexualActivityRecord.ProtectionUsedUnknown
    };


    static OvulationTestOutcome FromNativeOvulation(int result)
    {
        if (result == (int)OvulationTestRecord.ResultPositive) return OvulationTestOutcome.Positive;
        if (result == (int)OvulationTestRecord.ResultNegative) return OvulationTestOutcome.Negative;
        if (result == (int)OvulationTestRecord.ResultHigh) return OvulationTestOutcome.High;
        return OvulationTestOutcome.Inconclusive;
    }

    static int ToNativeOvulation(OvulationTestOutcome outcome) => outcome switch
    {
        OvulationTestOutcome.Positive => (int)OvulationTestRecord.ResultPositive,
        OvulationTestOutcome.Negative => (int)OvulationTestRecord.ResultNegative,
        OvulationTestOutcome.High => (int)OvulationTestRecord.ResultHigh,
        _ => (int)OvulationTestRecord.ResultInconclusive
    };


    static CervicalMucusAppearance FromNativeMucus(int appearance)
    {
        if (appearance == (int)CervicalMucusRecord.AppearanceDry) return CervicalMucusAppearance.Dry;
        if (appearance == (int)CervicalMucusRecord.AppearanceSticky) return CervicalMucusAppearance.Sticky;
        if (appearance == (int)CervicalMucusRecord.AppearanceCreamy) return CervicalMucusAppearance.Creamy;
        if (appearance == (int)CervicalMucusRecord.AppearanceWatery) return CervicalMucusAppearance.Watery;
        if (appearance == (int)CervicalMucusRecord.AppearanceEggWhite) return CervicalMucusAppearance.EggWhite;
        return CervicalMucusAppearance.Unspecified;
    }

    static int ToNativeMucus(CervicalMucusAppearance appearance) => appearance switch
    {
        CervicalMucusAppearance.Dry => (int)CervicalMucusRecord.AppearanceDry,
        CervicalMucusAppearance.Sticky => (int)CervicalMucusRecord.AppearanceSticky,
        CervicalMucusAppearance.Creamy => (int)CervicalMucusRecord.AppearanceCreamy,
        CervicalMucusAppearance.Watery => (int)CervicalMucusRecord.AppearanceWatery,
        CervicalMucusAppearance.EggWhite => (int)CervicalMucusRecord.AppearanceEggWhite,
        _ => (int)CervicalMucusRecord.AppearanceUnknown
    };


    static MealType FromNativeMealType(int mealType)
    {
        if (mealType == (int)AndroidX.Health.Connect.Client.Records.MealType.MealTypeBreakfast) return MealType.Breakfast;
        if (mealType == (int)AndroidX.Health.Connect.Client.Records.MealType.MealTypeLunch) return MealType.Lunch;
        if (mealType == (int)AndroidX.Health.Connect.Client.Records.MealType.MealTypeDinner) return MealType.Dinner;
        if (mealType == (int)AndroidX.Health.Connect.Client.Records.MealType.MealTypeSnack) return MealType.Snack;
        return MealType.Unknown;
    }

    static int ToNativeMealType(MealType mealType) => mealType switch
    {
        MealType.Breakfast => (int)AndroidX.Health.Connect.Client.Records.MealType.MealTypeBreakfast,
        MealType.Lunch => (int)AndroidX.Health.Connect.Client.Records.MealType.MealTypeLunch,
        MealType.Dinner => (int)AndroidX.Health.Connect.Client.Records.MealType.MealTypeDinner,
        MealType.Snack => (int)AndroidX.Health.Connect.Client.Records.MealType.MealTypeSnack,
        _ => (int)AndroidX.Health.Connect.Client.Records.MealType.MealTypeUnknown
    };


    static WorkoutType FromNativeExerciseType(int t)
    {
        if (t == (int)ExerciseSessionRecord.ExerciseTypeRunning || t == (int)ExerciseSessionRecord.ExerciseTypeRunningTreadmill) return WorkoutType.Running;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeWalking) return WorkoutType.Walking;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeHiking) return WorkoutType.Hiking;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeBiking || t == (int)ExerciseSessionRecord.ExerciseTypeBikingStationary) return WorkoutType.Cycling;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeSwimmingPool || t == (int)ExerciseSessionRecord.ExerciseTypeSwimmingOpenWater) return WorkoutType.Swimming;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeRowing || t == (int)ExerciseSessionRecord.ExerciseTypeRowingMachine) return WorkoutType.Rowing;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeElliptical) return WorkoutType.Elliptical;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeStairClimbing || t == (int)ExerciseSessionRecord.ExerciseTypeStairClimbingMachine) return WorkoutType.StairClimbing;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeStrengthTraining || t == (int)ExerciseSessionRecord.ExerciseTypeWeightlifting) return WorkoutType.StrengthTraining;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeHighIntensityIntervalTraining) return WorkoutType.HighIntensityIntervalTraining;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeYoga) return WorkoutType.Yoga;
        if (t == (int)ExerciseSessionRecord.ExerciseTypePilates) return WorkoutType.Pilates;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeTennis) return WorkoutType.Tennis;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeBasketball) return WorkoutType.Basketball;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeSoccer) return WorkoutType.Soccer;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeBaseball) return WorkoutType.Baseball;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeGolf) return WorkoutType.Golf;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeBoxing) return WorkoutType.Boxing;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeMartialArts) return WorkoutType.MartialArts;
        if (t == (int)ExerciseSessionRecord.ExerciseTypeDancing) return WorkoutType.Dancing;
        return WorkoutType.Other;
    }

    static int ToNativeExerciseType(WorkoutType type) => type switch
    {
        WorkoutType.Running => (int)ExerciseSessionRecord.ExerciseTypeRunning,
        WorkoutType.Walking => (int)ExerciseSessionRecord.ExerciseTypeWalking,
        WorkoutType.Hiking => (int)ExerciseSessionRecord.ExerciseTypeHiking,
        WorkoutType.Cycling => (int)ExerciseSessionRecord.ExerciseTypeBiking,
        WorkoutType.Swimming => (int)ExerciseSessionRecord.ExerciseTypeSwimmingPool,
        WorkoutType.Rowing => (int)ExerciseSessionRecord.ExerciseTypeRowing,
        WorkoutType.Elliptical => (int)ExerciseSessionRecord.ExerciseTypeElliptical,
        WorkoutType.StairClimbing => (int)ExerciseSessionRecord.ExerciseTypeStairClimbing,
        WorkoutType.StrengthTraining => (int)ExerciseSessionRecord.ExerciseTypeStrengthTraining,
        WorkoutType.HighIntensityIntervalTraining => (int)ExerciseSessionRecord.ExerciseTypeHighIntensityIntervalTraining,
        WorkoutType.Yoga => (int)ExerciseSessionRecord.ExerciseTypeYoga,
        WorkoutType.Pilates => (int)ExerciseSessionRecord.ExerciseTypePilates,
        WorkoutType.Tennis => (int)ExerciseSessionRecord.ExerciseTypeTennis,
        WorkoutType.Basketball => (int)ExerciseSessionRecord.ExerciseTypeBasketball,
        WorkoutType.Soccer => (int)ExerciseSessionRecord.ExerciseTypeSoccer,
        WorkoutType.Baseball => (int)ExerciseSessionRecord.ExerciseTypeBaseball,
        WorkoutType.Golf => (int)ExerciseSessionRecord.ExerciseTypeGolf,
        WorkoutType.Boxing => (int)ExerciseSessionRecord.ExerciseTypeBoxing,
        WorkoutType.MartialArts => (int)ExerciseSessionRecord.ExerciseTypeMartialArts,
        WorkoutType.Dancing => (int)ExerciseSessionRecord.ExerciseTypeDancing,
        _ => (int)ExerciseSessionRecord.ExerciseTypeOtherWorkout
    };


    static MenstrualFlow FromNativeFlow(int flow)
    {
        if (flow == (int)MenstruationFlowRecord.FlowLight) return MenstrualFlow.Light;
        if (flow == (int)MenstruationFlowRecord.FlowMedium) return MenstrualFlow.Medium;
        if (flow == (int)MenstruationFlowRecord.FlowHeavy) return MenstrualFlow.Heavy;
        return MenstrualFlow.Unspecified;
    }


    // Health Connect has no "none" flow value; None and Unspecified both map to FLOW_UNKNOWN
    static int ToNativeFlow(MenstrualFlow flow) => flow switch
    {
        MenstrualFlow.Light => (int)MenstruationFlowRecord.FlowLight,
        MenstrualFlow.Medium => (int)MenstruationFlowRecord.FlowMedium,
        MenstrualFlow.Heavy => (int)MenstruationFlowRecord.FlowHeavy,
        _ => (int)MenstruationFlowRecord.FlowUnknown
    };


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

        var list = new List<NumericHealthResult>();
        foreach (var bucket in ToManagedList<AggregationResultGroupedByDuration>(response))
        {
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
            MAX_PAGE_SIZE,
            null!
        );

        var response = await CallSuspendAsync(
            cont => client.ReadRecords(request, cont)
        ).ConfigureAwait(false);

        var readResponse = response.JavaCast<ReadRecordsResponse>();
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
        DataType.Calories => ["android.permission.health.READ_TOTAL_CALORIES_BURNED"],
        DataType.Distance => ["android.permission.health.READ_DISTANCE"],
        DataType.Weight => ["android.permission.health.READ_WEIGHT"],
        DataType.Height => ["android.permission.health.READ_HEIGHT"],
        DataType.BodyFatPercentage => ["android.permission.health.READ_BODY_FAT"],
        DataType.RestingHeartRate => ["android.permission.health.READ_RESTING_HEART_RATE"],
        DataType.BloodPressure => ["android.permission.health.READ_BLOOD_PRESSURE"],
        DataType.OxygenSaturation => ["android.permission.health.READ_OXYGEN_SATURATION"],
        DataType.SleepDuration => ["android.permission.health.READ_SLEEP"],
        DataType.Hydration => ["android.permission.health.READ_HYDRATION"],
        DataType.MenstruationFlow => ["android.permission.health.READ_MENSTRUATION"],
        DataType.BloodGlucose => ["android.permission.health.READ_BLOOD_GLUCOSE"],
        DataType.BodyTemperature => ["android.permission.health.READ_BODY_TEMPERATURE"],
        DataType.BasalBodyTemperature => ["android.permission.health.READ_BASAL_BODY_TEMPERATURE"],
        DataType.RespiratoryRate => ["android.permission.health.READ_RESPIRATORY_RATE"],
        DataType.Vo2Max => ["android.permission.health.READ_VO2_MAX"],
        DataType.HeartRateVariability => ["android.permission.health.READ_HEART_RATE_VARIABILITY"],
        DataType.LeanBodyMass => ["android.permission.health.READ_LEAN_BODY_MASS"],
        DataType.BasalEnergyBurned => ["android.permission.health.READ_BASAL_METABOLIC_RATE"],
        DataType.ActiveEnergyBurned => ["android.permission.health.READ_ACTIVE_CALORIES_BURNED"],
        DataType.FloorsClimbed => ["android.permission.health.READ_FLOORS_CLIMBED"],
        DataType.WheelchairPushes => ["android.permission.health.READ_WHEELCHAIR_PUSHES"],
        DataType.Speed => ["android.permission.health.READ_SPEED"],
        DataType.Power => ["android.permission.health.READ_POWER"],
        DataType.SexualActivity => ["android.permission.health.READ_SEXUAL_ACTIVITY"],
        DataType.OvulationTest => ["android.permission.health.READ_OVULATION_TEST"],
        DataType.CervicalMucus => ["android.permission.health.READ_CERVICAL_MUCUS"],
        DataType.IntermenstrualBleeding => ["android.permission.health.READ_INTERMENSTRUAL_BLEEDING"],
        DataType.Workout => ["android.permission.health.READ_EXERCISE"],
        DataType.Nutrition => ["android.permission.health.READ_NUTRITION"],
        _ => throw new InvalidOperationException("Invalid DataType")
    };


    static string[] ToWritePermissionStrings(DataType dataType) => dataType switch
    {
        DataType.StepCount => ["android.permission.health.WRITE_STEPS"],
        DataType.HeartRate => ["android.permission.health.WRITE_HEART_RATE"],
        DataType.Calories => ["android.permission.health.WRITE_TOTAL_CALORIES_BURNED"],
        DataType.Distance => ["android.permission.health.WRITE_DISTANCE"],
        DataType.Weight => ["android.permission.health.WRITE_WEIGHT"],
        DataType.Height => ["android.permission.health.WRITE_HEIGHT"],
        DataType.BodyFatPercentage => ["android.permission.health.WRITE_BODY_FAT"],
        DataType.RestingHeartRate => ["android.permission.health.WRITE_RESTING_HEART_RATE"],
        DataType.BloodPressure => ["android.permission.health.WRITE_BLOOD_PRESSURE"],
        DataType.OxygenSaturation => ["android.permission.health.WRITE_OXYGEN_SATURATION"],
        DataType.SleepDuration => ["android.permission.health.WRITE_SLEEP"],
        DataType.Hydration => ["android.permission.health.WRITE_HYDRATION"],
        DataType.MenstruationFlow => ["android.permission.health.WRITE_MENSTRUATION"],
        DataType.BloodGlucose => ["android.permission.health.WRITE_BLOOD_GLUCOSE"],
        DataType.BodyTemperature => ["android.permission.health.WRITE_BODY_TEMPERATURE"],
        DataType.BasalBodyTemperature => ["android.permission.health.WRITE_BASAL_BODY_TEMPERATURE"],
        DataType.RespiratoryRate => ["android.permission.health.WRITE_RESPIRATORY_RATE"],
        DataType.Vo2Max => ["android.permission.health.WRITE_VO2_MAX"],
        DataType.HeartRateVariability => ["android.permission.health.WRITE_HEART_RATE_VARIABILITY"],
        DataType.LeanBodyMass => ["android.permission.health.WRITE_LEAN_BODY_MASS"],
        DataType.BasalEnergyBurned => ["android.permission.health.WRITE_BASAL_METABOLIC_RATE"],
        DataType.ActiveEnergyBurned => ["android.permission.health.WRITE_ACTIVE_CALORIES_BURNED"],
        DataType.FloorsClimbed => ["android.permission.health.WRITE_FLOORS_CLIMBED"],
        DataType.WheelchairPushes => ["android.permission.health.WRITE_WHEELCHAIR_PUSHES"],
        DataType.Speed => ["android.permission.health.WRITE_SPEED"],
        DataType.Power => ["android.permission.health.WRITE_POWER"],
        DataType.SexualActivity => ["android.permission.health.WRITE_SEXUAL_ACTIVITY"],
        DataType.OvulationTest => ["android.permission.health.WRITE_OVULATION_TEST"],
        DataType.CervicalMucus => ["android.permission.health.WRITE_CERVICAL_MUCUS"],
        DataType.IntermenstrualBleeding => ["android.permission.health.WRITE_INTERMENSTRUAL_BLEEDING"],
        DataType.Workout => ["android.permission.health.WRITE_EXERCISE"],
        DataType.Nutrition => ["android.permission.health.WRITE_NUTRITION"],
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


    /// <summary>
    /// Materializes a java.util.List returned through the suspend-call bridge.
    /// </summary>
    /// <remarks>
    /// <see cref="CallSuspendAsync"/> hands back an untyped <see cref="Java.Lang.Object"/>, so the runtime
    /// resolves the managed peer from the object's concrete Java class.  Health Connect returns Kotlin lists
    /// whose concrete class varies with element count (<c>java.util.Arrays$ArrayList</c> from
    /// <c>Arrays.asList</c>, <c>Collections$SingletonList</c>, Kotlin's <c>EmptyList</c>, ...).  None of those
    /// have a binding, so the runtime falls back to the nearest bound base - usually
    /// <see cref="Java.Util.AbstractList"/>, which implements <c>Java.Util.IList</c> but NOT
    /// <see cref="System.Collections.IList"/>.  Casting to the latter throws InvalidCastException, so wrap the
    /// JNI handle explicitly instead.
    /// </remarks>
    static List<T> ToManagedList<T>(Java.Lang.Object javaListObject) where T : Java.Lang.Object
    {
        var result = new List<T>();
        using var javaList = new JavaList(javaListObject.Handle, JniHandleOwnership.DoNotTransfer);
        foreach (var item in javaList)
        {
            if (item is Java.Lang.Object peer)
                result.Add(peer.JavaCast<T>());
        }
        return result;
    }


    static Task<Java.Lang.Object> CallSuspendAsync(Func<IContinuation, Java.Lang.Object?> suspendFunction)
    {
        var tcs = new TaskCompletionSource<Java.Lang.Object>();
        var continuation = new SuspendContinuation(tcs);
        var immediateResult = suspendFunction(continuation);

        if (immediateResult != null && !IsCoroutineSuspended(immediateResult))
            SuspendContinuation.Complete(tcs, immediateResult);

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

        public void ResumeWith(Java.Lang.Object result) => Complete(tcs, result);


        /// <summary>
        /// Completes <paramref name="tcs"/> from a Kotlin <c>Result</c>.
        /// </summary>
        /// <remarks>
        /// <c>Continuation.resumeWith</c> hands over a <c>kotlin.Result</c>.  Because <c>Result</c> is an
        /// inline class, a success arrives as the bare value, but a failure arrives boxed as
        /// <c>kotlin.Result$Failure</c> wrapping the Throwable.  Passing that through as if it were the
        /// result means the real Health Connect error is lost and callers fail later on something
        /// unrelated - an InvalidCastException, or a JNI abort when a method is invoked on it.
        /// </remarks>
        public static void Complete(TaskCompletionSource<Java.Lang.Object> tcs, Java.Lang.Object result)
        {
            try
            {
                var failure = TryGetFailure(result);
                if (failure == null)
                    tcs.TrySetResult(result);
                else
                    tcs.TrySetException(failure);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }


        static Exception? TryGetFailure(Java.Lang.Object? result)
        {
            if (result?.Class?.Name != "kotlin.Result$Failure")
                return null;

            var fieldId = JNIEnv.GetFieldID(result.Class.Handle, "exception", "Ljava/lang/Throwable;");
            var handle = fieldId == IntPtr.Zero ? IntPtr.Zero : JNIEnv.GetObjectField(result.Handle, fieldId);
            if (handle == IntPtr.Zero)
                return new InvalidOperationException("The Health Connect call failed but did not report a reason.");

            Exception? throwable = GetObject<Java.Lang.Throwable>(handle, JniHandleOwnership.TransferLocalRef);
            return throwable ?? new InvalidOperationException("The Health Connect call failed but did not report a reason.");
        }
    }
}
