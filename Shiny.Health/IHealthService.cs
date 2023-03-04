using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiny.Health;


public interface IHealthService
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancelToken"></param>
    /// <param name="permissions"></param>
    /// <returns></returns>
    Task<bool> IsAuthorized(params Permission[] permissions);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancelToken"></param>
    /// <param name="permissions"></param>
    /// <returns></returns>
    Task<bool> RequestPermission(params Permission[] permissions);

    /// <summary>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="metric"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="interval"></param>
    /// <param name="cancelToken"></param>
    /// <returns></returns>
    Task<IEnumerable<HealthResult<T>>> Query<T>(
        HealthMetric<T> metric,
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval = Interval.Days,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="metric"></param>
    /// <returns></returns>
    IObservable<T> Monitor<T>(HealthMetric<T> metric);
}