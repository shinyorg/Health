
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common;
using Android.Gms.Fitness;
using Android.Gms.Fitness.Data;
using Android.Gms.Fitness.Request;
using Java.Lang;
using Java.Util.Concurrent;
using Shiny.Hosting;

namespace Shiny.Health;


public class HealthService : IHealthService, IAndroidLifecycle.IOnActivityResult
{
    const int REQUEST_CODE = 8765;
    readonly AndroidPlatform platform;
    public HealthService(AndroidPlatform platform)
        => this.platform = platform;

    // for writing
    //var client = FitnessClass
    // .GetRecordingClient(
    //     act,
    //     GoogleSignIn.GetLastSignedInAccount(act)
    // );

    public IObservable<T> Monitor<T>(HealthMetric<T> metric) => Observable.Create<T>(ob =>
    {
        var act = this.platform.CurrentActivity;
        var listener = new DataPointListener(dp =>
        {
            var value = metric.FromNative(dp);
            ob.OnNext(value);
        });

        var client = FitnessClass.GetSensorsClient(act, GoogleSignIn.GetLastSignedInAccount(act));

        client
            .AddAsync(
                new SensorRequest.Builder()
                    .SetDataType(metric.DataType)
                    .SetSamplingRate(10, TimeUnit.Seconds)
                    .Build(),
                listener
            )
            .ContinueWith(x =>
            {
                if (x.Exception != null)
                    ob.OnError(x.Exception);
            });

        return () => client.Remove(listener);
    });


    public async Task<IEnumerable<HealthResult<T>>> Query<T>(
        HealthMetric<T> metric,
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancellationToken = default
    )
    {
        var timeUnit = interval.ToNative();
        var readRequest = new DataReadRequest.Builder()
            .Aggregate(metric.DataType, metric.AggregationDataType)
            .BucketByTime(1, interval.ToNative())
            .SetTimeRange(start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds(), timeUnit)
            .Build();

        var list = new List<HealthResult<T>>();
        var dataReadResponse = await FitnessClass
            .GetHistoryClient(this.platform.CurrentActivity, null)!
            .ReadDataAsync(readRequest)
            .ConfigureAwait(false);

        if (dataReadResponse.Buckets.Count > 0)
        {
            foreach (var bucket in dataReadResponse.Buckets)
            {
                foreach (var dataSet in bucket.DataSets)
                {
                    foreach (var dp in dataSet.DataPoints)
                    {
                        var dstart = DateTimeOffset.FromUnixTimeMilliseconds(dp.GetStartTime(TimeUnit.Milliseconds));
                        var dend = DateTimeOffset.FromUnixTimeMilliseconds(dp.GetEndTime(TimeUnit.Milliseconds));
                        var value = metric.FromNative(dp);
                        list.Add(new HealthResult<T>(start, end, value));
                    }
                }
            }
        }
        return list;
    }


    public Task<bool> IsAuthorized(params Permission[] permissions)
    {
        var result = false;
        if (this.IsGooglePlayServicesAvailable())
        {
            var options = this.ToFitnessOptions(permissions);
            result = GoogleSignIn.HasPermissions(
                GoogleSignIn.GetLastSignedInAccount(this.platform.CurrentActivity),
                options
            );
        }
        return Task.FromResult(result);
    }


    TaskCompletionSource<bool>? permissionRequest;
    public Task<bool> RequestPermission(params Permission[] permissions)
    {
        if (!this.IsGooglePlayServicesAvailable())
            return Task.FromResult(false);

        this.permissionRequest = new();
        //using var _ = cancelToken.Register(() => this.permissionRequest.TrySetCanceled());

        var options = this.ToFitnessOptions(permissions);
        GoogleSignIn.RequestPermissions(
            this.platform.CurrentActivity,
            REQUEST_CODE,
            GoogleSignIn.GetLastSignedInAccount(this.platform.AppContext),
            options
        );
        return this.permissionRequest.Task;
    }


    protected FitnessOptions ToFitnessOptions(Permission[] permissions)
    {
        var options = FitnessOptions.InvokeBuilder();


        foreach (var permission in permissions)
        {
            if (permission.Type == PermissionType.Read || permission.Type == PermissionType.Both)
                options.AddDataType(permission.Metric.DataType, FitnessOptions.AccessRead);

            if (permission.Type == PermissionType.Write || permission.Type == PermissionType.Both)
                options.AddDataType(permission.Metric.DataType, FitnessOptions.AccessWrite);
        }
            //switch (permission.Kind)
            //{
            //    case HealthInfoKind.Steps:
            //        options
            //            .AddDataType(DataType.TypeStepCountCumulative, direction)
            //            .AddDataType(DataType.TypeStepCountDelta, direction);
            //        break;

            //    case HealthInfoKind.Distances:
            //        options.AddDataType(DataType.TypeDistanceDelta, direction);
            //        break;

            //    case HealthInfoKind.Calories:
            //        options.AddDataType(DataType.TypeCaloriesExpended, direction);
            //        break;

            //    case HealthInfoKind.HeartRate:
            //        options.AddDataType(DataType.TypeHeartRateBpm, direction);
            //        break;
            //}
        //}
        return options.Build();
    }


    bool IsGooglePlayServicesAvailable()
    {
        var googleApi = GoogleApiAvailability.Instance;
        var status = googleApi.IsGooglePlayServicesAvailable(this.platform.CurrentActivity);

        return status == ConnectionResult.Success;
        //if (status != ConnectionResult.Success)
        //{
        //    if (googleApi.IsUserResolvableError(status))
        //    {
        //        googleApi.GetErrorDialog(this.platform.CurrentActivity, status, GOOGLE_PLAY_SERVICE_ERROR_DIALOG).show();
        //    }
        //    return false;
        //}
        //return true;
    }


    public void Handle(Activity activity, int requestCode, Result resultCode, Intent data)
    {
        if (requestCode == REQUEST_CODE)
            this.permissionRequest?.TrySetResult(resultCode == Result.Ok);
    }
}