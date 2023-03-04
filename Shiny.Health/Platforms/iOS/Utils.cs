using System;
using Foundation;
using HealthKit;

namespace Shiny.Health;


internal static class Utils
{
    public static NSDateComponents ToNative(this Interval interval)
    {
        var native = new NSDateComponents();;

        switch (interval)
        {
            case Interval.Days:
                native.Day = 1;
                break;

            case Interval.Hours:
                native.Hour = 1;
                break;

            case Interval.Minutes:
                native.Minute = 1;
                break;
        }
        return native;
    }
}

