using System;
using Android.Gms.Fitness.Data;
using Android.Gms.Fitness.Request;

namespace Shiny.Health;


public class DataPointListener : Java.Lang.Object, IOnDataPointListener
{
    readonly Action<DataPoint> onDataPoint;
    public DataPointListener(Action<DataPoint> onDataPoint) => this.onDataPoint = onDataPoint;
    public void OnDataPoint(DataPoint dataPoint) => this.onDataPoint.Invoke(dataPoint);
}

