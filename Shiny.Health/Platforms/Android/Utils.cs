using System;
using Java.Util.Concurrent;

namespace Shiny.Health;


internal static class Utils
{
    public static TimeUnit ToNative(this Interval interval) => interval switch
    {
        Interval.Days => TimeUnit.Days,
        Interval.Hours => TimeUnit.Hours,
        Interval.Minutes => TimeUnit.Minutes
    };
}