using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
#if ANDROID
using Android.Gms.Fitness.Data;
using Android.Content;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common.Apis;
using Android.Gms.Fitness;
using Android.Gms.Fitness.Request;
using Android.Gms.Fitness.Result;
using Android.OS;
#endif
#if IOS
using HealthKit;
#endif

// TODO: make use of Units project?
namespace Shiny.Health;


// TODO: EnergyUnit on UnitsNet
public class CaloriesHealthMetric : HealthMetric<double>
{
    public static CaloriesHealthMetric Default { get; } = new();

#if ANDROID
    public override DataType AggregationDataType => DataType.AggregateCaloriesExpended;
    public override DataType DataType => DataType.TypeCaloriesExpended;

    public override double FromNative(DataPoint dataPoint)
    {
        var field = dataPoint.DataType.Fields.First();
        return dataPoint.GetValue(field).AsFloat();
    }
#endif

#if IOS
    public override HKQuantityTypeIdentifier QuantityTypeIdentifier => HKQuantityTypeIdentifier.ActiveEnergyBurned;
    public override HKStatisticsOptions StatisticsOptions => HKStatisticsOptions.CumulativeSum;
    public override double FromNative(HKStatistics result)
        => result.SumQuantity()?.GetDoubleValue(HKUnit.Kilocalorie) ?? 0;
#endif
}


// TODO: LengthUnit on UnitsNet
public class DistanceHealthMetric : HealthMetric<double>
{
    public static DistanceHealthMetric Default { get; } = new();

#if ANDROID
    public override DataType AggregationDataType => DataType.AggregateDistanceDelta;
    public override DataType DataType => DataType.TypeDistanceDelta;
    public override double FromNative(DataPoint dataPoint)
    {
        var field = dataPoint.DataType.Fields.First();
        return dataPoint.GetValue(field).AsFloat();
    }
#endif

#if IOS
    public override HKQuantityTypeIdentifier QuantityTypeIdentifier => HKQuantityTypeIdentifier.DistanceWalkingRunning;
    public override HKStatisticsOptions StatisticsOptions => HKStatisticsOptions.CumulativeSum;

    public override double FromNative(HKStatistics result)
        => result.SumQuantity()?.GetDoubleValue(HKUnit.Meter) ?? 0;
#endif
}


// TODO: DurationUnit on UnitsNet (per minute?)
public class HeartRateHealthMetric : HealthMetric<double>
{
    public static HeartRateHealthMetric Default { get; } = new();

#if ANDROID
    public override DataType AggregationDataType => DataType.AggregateHeartRateSummary;
    public override DataType DataType => DataType.TypeHeartRateBpm;

    public override double FromNative(DataPoint dataPoint)
    {
        var field = dataPoint.DataType.Fields.First();
        return dataPoint.GetValue(field).AsFloat();
    }
#endif

#if IOS
    public override HKQuantityTypeIdentifier QuantityTypeIdentifier => HKQuantityTypeIdentifier.HeartRate;
    public override HKStatisticsOptions StatisticsOptions => HKStatisticsOptions.DiscreteAverage;

    // this works for queries, not sure for notifications yet
    public override double FromNative(HKStatistics result)
        => result.AverageQuantity()?.GetDoubleValue(HKUnit.Count.UnitDividedBy(HKUnit.Minute)) ?? 0;
#endif
}


public class StepCountHealthMetric : HealthMetric<int>
{
    public static StepCountHealthMetric Default { get; } = new();

#if ANDROID
    public override DataType AggregationDataType => DataType.AggregateStepCountDelta;
    public override DataType DataType => DataType.TypeStepCountDelta;
    public override int FromNative(DataPoint dataPoint)
    {
        var field = dataPoint.DataType.Fields.First();
        return dataPoint.GetValue(field).AsInt();
    }
#endif

#if IOS
    public override HKQuantityTypeIdentifier QuantityTypeIdentifier => HKQuantityTypeIdentifier.StepCount;
    public override HKStatisticsOptions StatisticsOptions => HKStatisticsOptions.CumulativeSum;
    public override int FromNative(HKStatistics result)
        => Convert.ToInt32(result.SumQuantity()?.GetDoubleValue(HKUnit.Count) ?? 0);    
#endif
}


public class SleepAnalysisHealthMetric : HealthMetric<object>
{
#if ANDROID
    public override DataType AggregationDataType => throw new NotImplementedException();
    public override DataType DataType => throw new NotImplementedException();

    public override object FromNative(DataPoint dataPoint)
    {
        throw new NotImplementedException();
    }
    //    @RequiresApi(api = Build.VERSION_CODES.N)
    //    public void getSleepAnalysis(Context context, double startDate, double endDate, final Promise promise)
    //    {
    //        if (android.os.Build.VERSION.SDK_INT < android.os.Build.VERSION_CODES.N)
    //        {
    //            promise.reject(String.valueOf(FitnessError.ERROR_METHOD_NOT_AVAILABLE), "Method not available");
    //            return;
    //        }

    //        SessionReadRequest request = new SessionReadRequest.Builder()
    //                .readSessionsFromAllApps()
    //                .read(DataType.TYPE_ACTIVITY_SEGMENT)
    //                .setTimeInterval((long)startDate, (long)endDate, TimeUnit.MILLISECONDS)
    //                .build();

    //        Fitness.getSessionsClient(context, GoogleSignIn.getLastSignedInAccount(context))
    //                .readSession(request)
    //                .addOnSuccessListener(new OnSuccessListener<SessionReadResponse>() {
    //                    @Override
    //                    public void onSuccess(SessionReadResponse response)
    //        {
    //            List<Object> sleepSessions = response.getSessions()
    //                .stream()
    //                .filter(new Predicate<Session>() {
    //                                @Override
    //                                public boolean test(Session s)
    //            {
    //                return s.getActivity().equals(FitnessActivities.SLEEP);
    //            }
    //        })
    //                            .collect(Collectors.toList());

    //        WritableArray sleep = Arguments.createArray();
    //        for (Object session : sleepSessions)
    //        {
    //            List<DataSet> dataSets = response.getDataSet((Session)session);
    //            for (DataSet dataSet : dataSets)
    //            {
    //                processSleep(dataSet, (Session)session, sleep);
    //            }
    //        }

    //        promise.resolve(sleep);
    //    }
    //})
    //                .addOnFailureListener(new OnFailureListener()
    //{
    //    @Override
    //                    public void onFailure(@NonNull Exception e)
    //    {
    //        promise.reject(e);
    //    }
    //});
    //    }

    //private void processSleep(DataSet dataSet, Session session, WritableArray map)
    //{

    //    for (DataPoint dp : dataSet.getDataPoints())
    //    {
    //        for (Field field : dp.getDataType().getFields())
    //        {
    //            WritableMap sleepMap = Arguments.createMap();
    //            sleepMap.putString("value", dp.getValue(field).asActivity());
    //            sleepMap.putString("sourceId", session.getIdentifier());
    //            sleepMap.putString("startDate", dateFormat.format(dp.getStartTime(TimeUnit.MILLISECONDS)));
    //            sleepMap.putString("endDate", dateFormat.format(dp.getEndTime(TimeUnit.MILLISECONDS)));
    //            map.pushMap(sleepMap);
    //        }
    //    }
    //}
#endif

#if IOS
    public override HKQuantityTypeIdentifier QuantityTypeIdentifier => throw new NotImplementedException();

    public override HKStatisticsOptions StatisticsOptions => throw new NotImplementedException();

    public override object FromNative(HKStatistics result)
    {
        throw new NotImplementedException();
    }

    //RCT_REMAP_METHOD(getSleepAnalysis,
    //                 withStartDate: (double) startDate

    //                 andEndDate: (double) endDate

    //                 withSleepAnalysisResolver:(RCTPromiseResolveBlock) resolve

    //                 andSleepAnalysisRejecter:(RCTPromiseRejectBlock) reject)
    //{

    //    if (!startDate)
    //    {
    //        NSError* error = [RCTFitness createErrorWithCode: ErrorDateNotCorrect andDescription: RCT_ERROR_DATE_NOT_CORRECT];
    //        [RCTFitness handleRejectBlock:reject error:error] ;
    //        return;
    //    }

    //    NSDate* sd = [RCTFitness dateFromTimeStamp: startDate / 1000];
    //    NSDate* ed = [RCTFitness dateFromTimeStamp: endDate / 1000];

    //    HKSampleType* sampleType = [HKSampleType categoryTypeForIdentifier: HKCategoryTypeIdentifierSleepAnalysis];
    //    NSPredicate* predicate = [HKQuery predicateForSamplesWithStartDate: sd endDate: ed options: HKQueryOptionNone];

    //    HKSampleQuery* query = [[HKSampleQuery alloc] initWithSampleType: sampleType predicate:predicate limit:0 sortDescriptors: nil resultsHandler:^(HKSampleQuery * query, NSArray * results, NSError * error) {
    //        if (error)
    //        {
    //            NSError* error = [RCTFitness createErrorWithCode: ErrorNoEvents andDescription: RCT_ERROR_NO_EVENTS];
    //            [RCTFitness handleRejectBlock:reject error:error] ;
    //            return;
    //        }

    //        NSMutableArray* data = [NSMutableArray arrayWithCapacity: 1];

    //    for (HKCategorySample* sample in results)
    //        {
    //            NSString* startDateString = [RCTFitness ISO8601StringFromDate: sample.startDate];
    //            NSString* endDateString = [RCTFitness ISO8601StringFromDate: sample.endDate];

    //            NSString* valueString;

    //            switch (sample.value)
    //            {
    //                case HKCategoryValueSleepAnalysisInBed:
    //                    valueString = @"INBED";
    //                    break;
    //                case HKCategoryValueSleepAnalysisAsleep:
    //                    valueString = @"ASLEEP";
    //                    break;
    //                default:
    //                    valueString = @"UNKNOWN";
    //                    break;
    //            }

    //            NSDictionary* elem = @{
    //                @"value" : valueString,
    //                @"sourceName" : [[[sample sourceRevision] source] name],
    //                @"sourceId" : [[[sample sourceRevision] source] bundleIdentifier],
    //                @"startDate" : startDateString,
    //                @"endDate" : endDateString,
    //        };

    //            [data addObject:elem] ;
    //        }

    //        dispatch_async(dispatch_get_main_queue(), ^{
    //            resolve(data);
    //        });
    //    }];

    //    [self.healthStore executeQuery:query] ;
    //}
#endif
}