using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Shiny.Health;


/// <summary>
/// Unified cross-platform service for reading, writing, and observing health data
/// from Apple HealthKit (iOS) and Android Health Connect.
/// </summary>
public interface IHealthService
{
    /// <summary>
    /// Checks whether the platform health store is available on this device.
    /// On Android, returns true only if Health Connect is installed and the SDK status is OK.
    /// On iOS, always returns true (HealthKit is available on all supported devices).
    /// </summary>
    bool IsAvailable { get; }
    /// <summary>
    /// Observes health data changes in real time, yielding new samples as they are recorded.
    /// iOS uses push-based HKAnchoredObjectQuery; Android polls Health Connect change tokens.
    /// Only yields samples added after observation starts (forward-only).
    /// </summary>
    /// <param name="dataType">The health data type to observe.</param>
    /// <param name="pollingInterval">Polling interval for Android (default 5 seconds). Ignored on iOS.</param>
    /// <param name="cancelToken">Cancellation token to stop observation and free platform resources.</param>
    /// <returns>An async stream of health results as they are recorded.</returns>
    IAsyncEnumerable<HealthResult> Observe(
        DataType dataType,
        TimeSpan? pollingInterval = null,
        [EnumeratorCancellation] CancellationToken cancelToken = default
    );

    /// <summary>
    /// Requests read permissions for the specified data types.
    /// </summary>
    /// <param name="dataTypes">The health data types to request read access for.</param>
    /// <returns>A collection of tuples indicating which permissions were granted.</returns>
    Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params DataType[] dataTypes);

    /// <summary>
    /// Requests permissions with a uniform permission type applied to all specified data types.
    /// </summary>
    /// <param name="permissionType">The permission type (Read, Write, or ReadWrite) to request.</param>
    /// <param name="dataTypes">The health data types to request access for.</param>
    /// <returns>A collection of tuples indicating which permissions were granted.</returns>
    Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(PermissionType permissionType, params DataType[] dataTypes);

    /// <summary>
    /// Requests permissions with per-metric permission types in a single call.
    /// </summary>
    /// <param name="permissions">An array of tuples specifying the permission type and data type for each metric.</param>
    /// <returns>A collection of tuples indicating which permissions were granted.</returns>
    Task<IEnumerable<(DataType Type, bool Success)>> RequestPermissions(params (PermissionType Permission, DataType Type)[] permissions);

    /// <summary>
    /// Writes a numeric health result to the platform health store.
    /// </summary>
    /// <param name="result">The numeric health result to write (e.g., steps, weight, heart rate).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    Task Write(NumericHealthResult result, CancellationToken cancelToken = default);

    /// <summary>
    /// Writes a blood pressure result to the platform health store.
    /// </summary>
    /// <param name="result">The blood pressure result with systolic and diastolic values in mmHg.</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    Task Write(BloodPressureResult result, CancellationToken cancelToken = default);

    /// <summary>
    /// Gets calorie burn data (kcal) aggregated over the specified time range and interval.
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of numeric results representing calories burned per interval bucket.</returns>
    Task<IList<NumericHealthResult>> GetCalories(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Gets distance data (meters) aggregated over the specified time range and interval.
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of numeric results representing distance traveled per interval bucket.</returns>
    Task<IList<NumericHealthResult>> GetDistances(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Gets step count data aggregated over the specified time range and interval.
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of numeric results representing step counts per interval bucket.</returns>
    Task<IList<NumericHealthResult>> GetStepCounts(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Gets average heart rate data (bpm) over the specified time range and interval.
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of numeric results representing average heart rate per interval bucket.</returns>
    Task<IList<NumericHealthResult>> GetAverageHeartRate(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Gets weight data (kg) over the specified time range and interval.
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of numeric results representing average weight per interval bucket.</returns>
    Task<IList<NumericHealthResult>> GetWeight(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Gets height data (meters) over the specified time range and interval.
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of numeric results representing average height per interval bucket.</returns>
    Task<IList<NumericHealthResult>> GetHeight(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Gets body fat percentage data (0-100) over the specified time range and interval.
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of numeric results representing average body fat percentage per interval bucket.</returns>
    Task<IList<NumericHealthResult>> GetBodyFatPercentage(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Gets resting heart rate data (bpm) over the specified time range and interval.
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of numeric results representing average resting heart rate per interval bucket.</returns>
    Task<IList<NumericHealthResult>> GetRestingHeartRate(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Gets blood pressure data (mmHg) over the specified time range and interval.
    /// Returns BloodPressureResult with separate Systolic and Diastolic values.
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of blood pressure results with systolic and diastolic values per interval bucket.</returns>
    Task<IList<BloodPressureResult>> GetBloodPressure(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Gets oxygen saturation data (0-100%) over the specified time range and interval.
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of numeric results representing average oxygen saturation per interval bucket.</returns>
    Task<IList<NumericHealthResult>> GetOxygenSaturation(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Gets sleep duration data (hours) over the specified time range and interval.
    /// Filters for actual sleep states (excludes "in bed" and "awake" on iOS).
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of numeric results representing sleep duration in hours per interval bucket.</returns>
    Task<IList<NumericHealthResult>> GetSleepDuration(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );

    /// <summary>
    /// Gets hydration data (liters) aggregated over the specified time range and interval.
    /// </summary>
    /// <param name="start">The start of the query time range.</param>
    /// <param name="end">The end of the query time range.</param>
    /// <param name="interval">The bucketing interval (Minutes, Hours, or Days).</param>
    /// <param name="cancelToken">Optional cancellation token.</param>
    /// <returns>A list of numeric results representing water intake in liters per interval bucket.</returns>
    Task<IList<NumericHealthResult>> GetHydration(
        DateTimeOffset start,
        DateTimeOffset end,
        Interval interval,
        CancellationToken cancelToken = default
    );
}
