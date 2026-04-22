using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shiny.Health;


public interface IHealthService
{
    Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params DataType[] dataTypes);

    Task<IList<NumericHealthResult>> GetCalories(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    Task<IList<NumericHealthResult>> GetDistances(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    Task<IList<NumericHealthResult>> GetStepCounts(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    Task<IList<NumericHealthResult>> GetAverageHeartRate(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    Task<IList<NumericHealthResult>> GetWeight(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    Task<IList<NumericHealthResult>> GetHeight(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    Task<IList<NumericHealthResult>> GetBodyFatPercentage(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    Task<IList<NumericHealthResult>> GetRestingHeartRate(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    Task<IList<BloodPressureResult>> GetBloodPressure(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    Task<IList<NumericHealthResult>> GetOxygenSaturation(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    Task<IList<NumericHealthResult>> GetSleepDuration(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    Task<IList<NumericHealthResult>> GetHydration(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );
}
