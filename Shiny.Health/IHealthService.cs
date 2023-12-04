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
    /// <param name="permission"></param>
    /// <returns></returns>
    AccessState GetCurrentStatus(Permission permission);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="permissions"></param>
    /// <returns></returns>
    Task<bool> IsAuthorized(params Permission[] permissions);

    // TODO: I really need this to come back as a batch of approve/deny since Apple Health allows this
    /// <summary>
    /// 
    /// </summary>
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
    //IObservable<T> Monitor<T>(HealthMetric<T> metric);
}