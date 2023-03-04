using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventKit;
using Foundation;
using HealthKit;
using Microsoft.Extensions.Options;

namespace Shiny.Health;


public class HealthService : IHealthService
{
    public IObservable<T> Monitor<T>(HealthMetric<T> metric) => Observable.Create<T>(ob =>
    {
        var predicate = HKQuery.GetPredicateForSamples(NSDate.Now, null, HKQueryOptions.None);
        var qty = HKQuantityType.Create(metric.QuantityTypeIdentifier)!;

        var query = new HKAnchoredObjectQuery(
            qty,
            predicate,
            null,
            0,
            new HKAnchoredObjectUpdateHandler((qry, sample, deleted, anchor, e) =>
            {
                if (e != null)
                {

                }
                else
                {
                    foreach (var s in sample)
                    {
                        if (s is HKQuantitySample qtys)
                        {
                            //var value = metric.FromNative(qtys);
                            //ob.OnNext(null)
                        }
                    }
                }
                //.Quantity.GetDoubleValue
                //sample[0].Source;
                //sample[0].SourceRevision
                //sample[0].Uuid
                //sample[0].StartDate
                //sample[0].EndDate
            })
        );
        var store = new HKHealthStore();
        store.ExecuteQuery(query);

        return () =>
        {
            store.StopQuery(query);
            store.Dispose();
        };
    });


    public async Task<IEnumerable<HealthResult<T>>> Query<T>(HealthMetric<T> metric, DateTimeOffset start, DateTimeOffset end, Interval interval = Interval.Days, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<IEnumerable<HealthResult<T>>>();
        var calendar = NSCalendar.CurrentCalendar;

        var anchorComponents = calendar.Components(
            NSCalendarUnit.Day | NSCalendarUnit.Month | NSCalendarUnit.Year,
            (NSDate)start.LocalDateTime
        );
        anchorComponents.Hour = 0;
        var anchorDate = calendar.DateFromComponents(anchorComponents);
        var qtyType = HKQuantityType.Create(metric.QuantityTypeIdentifier)!;

        var query = new HKStatisticsCollectionQuery(
            qtyType,
            null,
            metric.StatisticsOptions,
            anchorDate,
            interval.ToNative()
        );
        query.InitialResultsHandler = (qry, results, err) =>
        {
            if (err != null)
            {
                tcs.TrySetException(new InvalidOperationException(err.Description));
            }
            else
            {
                var list = new List<HealthResult<T>>();

                results.EnumerateStatistics(
                    (NSDate)start.LocalDateTime,
                    (NSDate)end.LocalDateTime,
                    (result, stop) =>
                    {
                        try
                        {
                            var value = metric.FromNative(result);
                            if (value != null)
                            {
                                var item = new HealthResult<T>(
                                    result.StartDate.ToDateTime(),
                                    result.EndDate.ToDateTime(),
                                    value
                                );
                                list.Add(item);
                            }
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


    public async Task<bool> IsAuthorized(params Permission[] permissions)
    {
        if (!OperatingSystem.IsIOSVersionAtLeast(12))
            return false;

        if (!HKHealthStore.IsHealthDataAvailable)
            return false;

        var perms = ToSets(permissions);
        using var store = new HKHealthStore();
        var result = await store.GetRequestStatusForAuthorizationToShareAsync(perms.Share, perms.Read);
        return result == HKAuthorizationRequestStatus.Unnecessary;
    }


    public async Task<bool> RequestPermission(params Permission[] permissions)
    {
        if (!OperatingSystem.IsIOSVersionAtLeast(12))
            return false;

        if (!HKHealthStore.IsHealthDataAvailable)
            return false;

        var perms = ToSets(permissions);
        using var store = new HKHealthStore();
        var result = await store.RequestAuthorizationToShareAsync(perms.Share, perms.Read);

        if (result.Item2 != null)
            throw new InvalidOperationException(result.Item2.Description);

        return result.Item1;
    }


    static (NSSet<HKSampleType> Share, NSSet<HKObjectType> Read) ToSets(Permission[] permissions)
    {
        var share = new NSMutableSet<HKSampleType>();
        var read = new NSMutableSet<HKObjectType>();

        foreach (var permission in permissions)
        {
            var qtyType = HKQuantityType.Create(permission.Metric.QuantityTypeIdentifier)!;
            if (permission.Type == PermissionType.Read || permission.Type == PermissionType.Both)
                read.Add(qtyType);

            if (permission.Type == PermissionType.Write || permission.Type == PermissionType.Both)
                share.Add(qtyType);
        }
        // TODO: throw if no read permissions?
        return (
            new NSSet<HKSampleType>(share.ToArray()),
            new NSSet<HKObjectType>(read.ToArray())
        );
    }
}

//first, create a predicate and set the endDate and option to nil/none 

//    //Then we create a sample type which is HKQuantityTypeIdentifierHeartRate
//    HKSampleType * object = [HKSampleType quantityTypeForIdentifier: HKQuantityTypeIdentifierHeartRate];

//    //ok, now, create a HKAnchoredObjectQuery with all the mess that we just created.
//    heartQuery = [[HKAnchoredObjectQuery alloc] initWithType: object predicate:Predicate anchor:0 limit: 0 resultsHandler: ^(HKAnchoredObjectQuery * query, NSArray < HKSample *> *sampleObjects, NSArray < HKDeletedObject *> *deletedObjects, HKQueryAnchor * newAnchor, NSError * error) {

//        if (!error && sampleObjects.count > 0)
//        {
//            HKQuantitySample* sample = (HKQuantitySample*)[sampleObjects objectAtIndex: 0];
//            HKQuantity* quantity = sample.quantity;
//            NSLog(@"%f", [quantity doubleValueForUnit:[HKUnit unitFromString: @"count/min"]]);
//        }
//        else
//        {
//            NSLog(@"query %@", error);
//        }

//    }];

//    //wait, it's not over yet, this is the update handler
//    [heartQuery setUpdateHandler:^(HKAnchoredObjectQuery* query, NSArray<HKSample*>* SampleArray, NSArray<HKDeletedObject*>* deletedObjects, HKQueryAnchor* Anchor, NSError* error) {

// if (!error && SampleArray.count > 0) {
//    HKQuantitySample* sample = (HKQuantitySample*)[SampleArray objectAtIndex: 0];
//    HKQuantity* quantity = sample.quantity;
//    NSLog(@"%f", [quantity doubleValueForUnit:[HKUnit unitFromString:@"count/min"]]);
// }else{
//    NSLog(@"query %@", error);
// }
//}];

////now excute query and wait for the result showing up in the log. Yeah!
//[healthStore executeQuery:heartQuery] ;
//}