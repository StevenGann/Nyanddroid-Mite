using System.Collections.Concurrent;
using System.Text.Json;

namespace Common;

/// <summary>
/// Represents a performance report containing a collection of named timing measurements.
/// </summary>
/// <remarks>
/// The <see cref="PerfReport"/> class is typically constructed from a <see cref="ConcurrentQueue{T}"/> of measurement tuples
/// and provides serialization to JSON for reporting or analysis purposes.
/// </remarks>
public class PerfReport
{
    /// <summary>
    /// Gets or sets the list of measurements included in this report.
    /// </summary>
    public List<Measurement> Measurements { get; set; } = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="PerfReport"/> class from a queue of measurements.
    /// </summary>
    /// <param name="measurements">A <see cref="ConcurrentQueue{T}"/> containing tuples of measurement name and elapsed time in milliseconds.</param>
    /// <remarks>
    /// This constructor will dequeue all items from the provided queue and add them to the <see cref="Measurements"/> list.
    /// </remarks>
    public PerfReport(ConcurrentQueue<(string, double)> measurements)
    {
        while (measurements.TryDequeue(out var measurement))
        {
            Measurements.Add(new Measurement(measurement.Item1, measurement.Item2));
        }
    }

    /// <summary>
    /// Serializes this performance report to a JSON string.
    /// </summary>
    /// <returns>A JSON string representing the performance report and its measurements.</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }
}

/// <summary>
/// Represents a single named timing measurement.
/// </summary>
public class Measurement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Measurement"/> class with the specified name and elapsed time.
    /// </summary>
    /// <param name="name">The name or label of the measured code section.</param>
    /// <param name="time">The elapsed time in milliseconds for the measurement.</param>
    public Measurement(string name, double time)
    {
        this.Name = name;
        this.Time = time;
    }

    /// <summary>
    /// Gets or sets the name or label of the measured code section.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the elapsed time in milliseconds for the measurement.
    /// </summary>
    public double Time { get; set; }
}


