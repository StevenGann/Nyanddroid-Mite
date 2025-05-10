using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System;

namespace Common;

/// <summary>
/// Provides static methods for measuring and recording the performance of code sections using named timers.
/// </summary>
/// <remarks>
/// The <see cref="Performance"/> class allows you to start and stop named timers to measure the elapsed time of code sections.
/// Timings are stored in a static queue for later analysis. This class is thread-safe and intended for simple performance profiling.
/// </remarks>
public class Performance
{
    /// <summary>
    /// Stores the name and elapsed time (in milliseconds) of completed measurements.
    /// </summary>
    private static readonly ConcurrentQueue<(string name, double milliseconds)> _measurements = new();

    /// <summary>
    /// Stores active timers, indexed by a unique integer ID. Each entry contains the timer name and the associated <see cref="Stopwatch"/>.
    /// </summary>
    private static readonly ConcurrentDictionary<int, (string name, Stopwatch stopwatch)> _timers = new();

    /// <summary>
    /// The next unique timer ID.
    /// </summary>
    private static int _nextId = 0;

    /// <summary>
    /// The timeout for stopping a dangling measurement, in milliseconds.
    /// </summary>
    public static int TimeOut {get; set;} = 1000;

    /// <summary>
    /// Starts a new timer with the specified name.
    /// </summary>
    /// <param name="name">A descriptive name for the timer (e.g., the code section being measured).</param>
    /// <returns>
    /// An integer ID that uniquely identifies the started timer. This ID should be passed to <see cref="Stop(int)"/> to stop the timer.
    /// </returns>
    /// <remarks>
    /// Multiple timers can be started and stopped independently. Timer IDs are assigned sequentially and are thread-safe.
    /// </remarks>
    public static int Start(string name)
    {
        int id = Interlocked.Increment(ref _nextId);
        _timers[id] = (name, Stopwatch.StartNew());
        return id;
    }

    /// <summary>
    /// Stops the timer with the specified ID and records the elapsed time.
    /// </summary>
    /// <param name="id">The unique ID of the timer to stop, as returned by <see cref="Start(string)"/>.</param>
    /// <returns>
    /// The elapsed time in milliseconds for the stopped timer, or -1 if the timer ID does not exist or has already been stopped.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is thread-safe and can be called from any thread. If the timer with the specified ID is found and running, it is stopped,
    /// its elapsed time is recorded, and the value is returned. If the timer ID does not exist (e.g., it was already stopped or never started),
    /// the method returns -1 and does nothing else.
    /// </para>
    /// <para>
    /// The elapsed time (in milliseconds) and the timer's name are added to the measurements queue for later retrieval.
    /// </para>
    /// </remarks>
    public static double Stop(int id)
    {
        if (_timers.TryRemove(id, out var entry))
        {
            entry.stopwatch.Stop();
            _measurements.Enqueue((entry.name, entry.stopwatch.Elapsed.TotalMilliseconds));
            return entry.stopwatch.Elapsed.TotalMilliseconds;
        }
        return -1;
    }

    /// <summary>
    /// Generates a performance report and stops any timers that have been running for more than 1000 milliseconds.
    /// </summary>
    /// <returns>A <see cref="PerfReport"/> containing all completed measurements.</returns>
    public static PerfReport GetReport()
    {
        // List to hold IDs of timers that should be stopped
        var toStop = new List<int>();
        var now = DateTime.UtcNow;
        foreach (var kvp in _timers)
        {
            var id = kvp.Key;
            var stopwatch = kvp.Value.stopwatch;
            if (stopwatch.IsRunning && stopwatch.ElapsedMilliseconds > TimeOut)
            {
                toStop.Add(id);
            }
        }
        // Stop timers after collecting IDs to avoid modifying the dictionary during iteration
        foreach (var id in toStop)
        {
            Stop(id);
        }
        return new PerfReport(_measurements);
    }
}


