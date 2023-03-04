using System;
#if ANDROID
using Android.Gms.Fitness.Data;
#endif
#if IOS
using HealthKit;
#endif

namespace Shiny.Health;


public abstract class HealthMetric
{
#if ANDROID
    public abstract DataType AggregationDataType { get; }
    public abstract DataType DataType { get; }
#endif

#if IOS
    public abstract HKQuantityTypeIdentifier QuantityTypeIdentifier { get; }
    public abstract HKStatisticsOptions StatisticsOptions { get; }
#endif
}


public abstract class HealthMetric<T> : HealthMetric
{
#if ANDROID
    public abstract T FromNative(DataPoint dataPoint);
#endif
#if IOS
    public abstract T FromNative(HKStatistics result);
#endif
}