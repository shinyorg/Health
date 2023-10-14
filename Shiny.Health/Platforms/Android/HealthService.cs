using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Fitness;
using Android.Gms.Fitness.Request;
using Java.Util.Concurrent;
using Microsoft.Extensions.Logging;
using Shiny.Hosting;

namespace Shiny.Health;


public class HealthService : IHealthService, IAndroidLifecycle.IOnActivityResult
{
    const int REQUEST_CODE = 8765;
    readonly AndroidPlatform platform;
    readonly ILogger logger;


    public HealthService(AndroidPlatform platform, ILogger<HealthService> logger)
    {
        this.platform = platform;
        this.logger = logger;
    }

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
        var unixStart = Math.Abs(start.ToUnixTimeSeconds());
        var unixEnd = Math.Abs(end.ToUnixTimeSeconds());
        var readRequest = new DataReadRequest.Builder()
            .Aggregate(metric.DataType, metric.AggregationDataType)
            .BucketByTime(1, interval.ToNative())
            .SetTimeRange(unixStart, unixEnd, timeUnit)
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
                        list.Add(new HealthResult<T>(dstart, dend, value));
                    }
                }
            }
        }
        return list;
    }


    public Task<bool> IsAuthorized(params Permission[] permissions)
        => Task.FromResult(this.IsAuthorizedInternal(permissions));


    TaskCompletionSource<bool>? permissionRequest;
    public Task<bool> RequestPermission(params Permission[] permissions)
    {
        if (this.IsAuthorizedInternal(permissions))
            return Task.FromResult(true);

        this.permissionRequest = new();
        //using var _ = cancelToken.Register(() => this.permissionRequest.TrySetCanceled());

        //<uses-permission android:name="android.permission.ACTIVITY_RECOGNITION"/>
        var options = this.ToFitnessOptions(permissions);
        GoogleSignIn.RequestPermissions(
            this.platform.CurrentActivity,
            REQUEST_CODE,
            GoogleSignIn.GetLastSignedInAccount(this.platform.AppContext),
            options
        );
        return this.permissionRequest.Task;
    }


    protected bool IsAuthorizedInternal(params Permission[] permissions)
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
        return result;
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


    const string SIGNIN_STATUS = "googleSignInStatus";


    public void Handle(Activity activity, int requestCode, Result resultCode, Intent data)
    {
        if (data.HasExtra(SIGNIN_STATUS))
        {
            var status = (Statuses)data.GetParcelableExtra(SIGNIN_STATUS, Java.Lang.Class.FromType(typeof(Statuses)))!;
            this.logger.LogDebug("Google SignIn Status: " + status.Status.ToString());
        }
        if (requestCode == REQUEST_CODE)
            this.permissionRequest?.TrySetResult(resultCode == Result.Ok);
        //if (requestCode != REQUEST_CODE)
        //    return;

        //if (resultCode == Result.Ok)
        //{
        //    this.permissionRequest?.TrySetResult(true);
        //}
        //else if (data.HasExtra(SIGNIN_STATUS))
        //{
        //    var status = (Statuses)data.GetParcelableExtra(SIGNIN_STATUS, Java.Lang.Class.FromType(typeof(Statuses)))!;
        //    this.logger.LogDebug("Google SignIn Status: " + status.Status.ToString());

        //    switch (status.StatusCode)
        //    {
        //        case GoogleSignInStatusCodes.SignInCurrentlyInProgress:
        //            break;

        //        case GoogleSignInStatusCodes.Success:
        //            this.permissionRequest?.TrySetResult(true);
        //            break;

        //        default:
        //            this.permissionRequest?.TrySetResult(false);
        //            break;
        //    }
        //}
    }
}