using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Androidx.Health.Connect.Client.Request;
using Androidx.Health.Connect.Client.Time;
using Xamarin.AndroidX.Health.Connect.Client;
using Xamarin.AndroidX.Health.Connect.Client.Permission;
using Xamarin.AndroidX.Health.Connect.Client.Records;
//using Androidx.Health.Connect.Client.Aggregate.

namespace Shiny.Health;


public class HealthService : IHealthService
{
    readonly AndroidPlatform platform;
    readonly IHealthConnectClient client;

    public HealthService(AndroidPlatform platform)
    {
        this.platform = platform;
        //new HealthConnectClientSlim()
        this.client = HealthConnectClient.GetOrCreate(platform.AppContext);

        //this.client.ReadRecords()
        //this.client.PermissionController.
        //HealthPermission.ReadHeartRate;

        //new Xamarin.AndroidX.Health.Connect
        //val PERMISSIONS =
        //setOf(
        //  HealthPermission.getReadPermission(HeartRateRecord::class),
        //  HealthPermission.getWritePermission(HeartRateRecord::class),
        //  HealthPermission.getReadPermission(StepsRecord::class),
        //  HealthPermission.getWritePermission(StepsRecord::class)
        //)

    }


    public Task<IList<NumericHealthResult>> GetAverageHeartRate(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
    {
        TimeRangeFilter filter = null;
        //TimeRangeFilter.Between(start, end);

        new ReadRecordsRequest(HeartRateRecord.Sample, filter, null, true, 2000, "");
        //new ReadRecordsRequest(Kotlin.Reflect.KClassesImplKt.GetQualifiedOrSimpleName());
        //this.client.ReadRecords()


        return null;
    }

    public Task<IList<NumericHealthResult>> GetCalories(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
    {
        throw new NotImplementedException();
    }

    public AccessState GetCurrentStatus(DataType dataType)
    {
        var status = HealthConnectClient.GetSdkStatus(this.platform.AppContext);
        //return status switch
        //{
        //    HealthConnectClient.SdkUnavailableProviderUpdateRequired => AccessState.NotSupported,
        //    HealthConnectClient.SdkUnavailable => AccessState.NotSupported,
        //    HealthConnectClient.SdkAvailable => AccessState.Available
        //};
        return AccessState.NotSupported;
    }

    public Task<IList<NumericHealthResult>> GetDistances(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IList<NumericHealthResult>> GetStepCounts(DateTimeOffset start, DateTimeOffset end, Interval interval, CancellationToken cancelToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params DataType[] dataTypes)
    {
        throw new NotImplementedException();
    }
}