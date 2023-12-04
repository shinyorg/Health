using System;
using System.Collections.Generic;
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
using Shiny.Hosting;

namespace Shiny.Health;


public class HealthService : IHealthService //, IAndroidLifecycle.IOnActivityResult
{
    const int REQUEST_CODE = 8765;
    readonly AndroidPlatform platform;


    public HealthService(AndroidPlatform platform)
    {
        this.platform = platform;
    }

    // for writing
    //var client = FitnessClass
    // .GetRecordingClient(
    //     act,
    //     GoogleSignIn.GetLastSignedInAccount(act)
    // );

    //public IObservable<T> Monitor<T>(HealthMetric<T> metric) => Observable.Create<T>(ob =>
    //{
    //    var act = this.platform.CurrentActivity;
    //    var listener = new DataPointListener(dp =>
    //    {
    //        var value = metric.FromNative(dp);
    //        ob.OnNext(value);
    //    });

    //    var client = FitnessClass.GetSensorsClient(act, GoogleSignIn.GetLastSignedInAccount(act));

    //    client
    //        .AddAsync(
    //            new SensorRequest.Builder()
    //                .SetDataType(metric.DataType)
    //                .SetSamplingRate(10, TimeUnit.Seconds)
    //                .Build(),
    //            listener
    //        )
    //        .ContinueWith(x =>
    //        {
    //            if (x.Exception != null)
    //                ob.OnError(x.Exception);
    //        });

    //    return () => client.Remove(listener);
    //});


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


    void Google()
    {
        var apiClient = new GoogleApiClient.Builder(this.platform.CurrentActivity)
            .AddApi(FitnessClass.SENSORS_API)
            .UseDefaultAccount()
            .AddScope(FitnessClass.ScopeActivityRead)
            .AddConnectionCallbacks(bundle =>
            {
                //if (data.HasExtra(SIGNIN_STATUS))
                //{
                //    var status = (Statuses)data.GetParcelableExtra(SIGNIN_STATUS, Java.Lang.Class.FromType(typeof(Statuses)))!;
                //    switch (status.StatusCode)
                //    {
                //        case GoogleSignInStatusCodes.SignInCurrentlyInProgress:
                //        case GoogleSignInStatusCodes.Success:
                //            break;

                //        default:
                //            this.permissionRequest?.TrySetException(new InvalidOperationException("Google Fit Setup Issue: " + status));
                //            break;
                //    }
                //}
                //if (requestCode == REQUEST_CODE)
                //    this.permissionRequest?.TrySetResult(resultCode == Result.Ok);
            })
            .AddOnConnectionFailedListener(result =>
            {

            })
            //.AddScope(FitnessClass.ScopeActivityReadWrite) 
            .Build();

        apiClient.Connect();

        //.AddConnectionCallbacks()
    }
        /*
        public class MyActivity extends FragmentActivity
                     implements ConnectionCallbacks, OnConnectionFailedListener, OnDataPointListener {
                private static final int REQUEST_OAUTH = 1001;
                private GoogleApiClient mGoogleApiClient;

                @Override
                protected void onCreate(@Nullable Bundle savedInstanceState) {
                    super.onCreate(savedInstanceState);

                    // Create a Google Fit Client instance with default user account.
                    mGoogleApiClient = new GoogleApiClient.Builder(this)
                            .addApi(Fitness.SENSORS_API)  // Required for SensorsApi calls
                            // Optional: specify more APIs used with additional calls to addApi
                            .useDefaultAccount()
                            .addScope(Fitness.SCOPE_ACTIVITY_READ_WRITE)
                            .addConnectionCallbacks(this)
                            .addOnConnectionFailedListener(this)
                            .build();

                    mGoogleApiClient.connect();
                }

                @Override
                public void onConnected(Bundle connectionHint) {
                    // Connected to Google Fit Client.
                    Fitness.SensorsApi.add(
                            mGoogleApiClient,
                            new SensorRequest.Builder()
                                    .setDataType(DataType.STEP_COUNT_DELTA)
                                    .build(),
                            this);
                }

                @Override
                public void onDataPoint(DataPoint dataPoint) {
                    // Do cool stuff that matters.
                }

                @Override
                public void onConnectionSuspended(int cause) {
                    // The connection has been interrupted. Wait until onConnected() is called.
                }

                @Override
                public void onConnectionFailed(ConnectionResult result) {
                    // Error while connecting. Try to resolve using the pending intent returned.
                    if (result.getErrorCode() == FitnessStatusCodes.NEEDS_OAUTH_PERMISSIONS) {
                        try {
                            result.startResolutionForResult(this, REQUEST_OAUTH);
                        } catch (SendIntentException e) {
                        }
                    }
                }

                @Override
                public void onActivityResult(int requestCode, int resultCode, Intent data) {
                    if (requestCode == REQUEST_OAUTH && resultCode == RESULT_OK) {
                        mGoogleApiClient.connect();
                    }
                } 
         */
        //        .useDefaultAccount()
        //.addScope(Fitness.SCOPE_ACTIVITY_READ_WRITE)
   

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


    //public void Handle(Activity activity, int requestCode, Result resultCode, Intent data)
    //{
    //    if (data.HasExtra(SIGNIN_STATUS))
    //    {
    //        var status = (Statuses)data.GetParcelableExtra(SIGNIN_STATUS, Java.Lang.Class.FromType(typeof(Statuses)))!;
    //        switch (status.StatusCode)
    //        {
    //            case GoogleSignInStatusCodes.SignInCurrentlyInProgress:
    //            case GoogleSignInStatusCodes.Success:
    //                break;

    //            default:
    //                this.permissionRequest?.TrySetException(new InvalidOperationException("Google Fit Setup Issue: " + status));
    //                break;
    //        }
    //    }
    //    if (requestCode == REQUEST_CODE)
    //        this.permissionRequest?.TrySetResult(resultCode == Result.Ok);
    //}

    public AccessState GetCurrentStatus(Permission permission)
    {
        throw new NotImplementedException();
    }
}