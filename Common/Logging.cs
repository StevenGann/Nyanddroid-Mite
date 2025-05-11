using System.Collections.Concurrent;

namespace Common;

/// <summary>
/// Provides thread-safe logging functionality with support for different log levels and message filtering.
/// </summary>
/// <remarks>
/// The <see cref="Logging"/> class implements a thread-safe logging system using a concurrent queue.
/// Messages can be logged at different severity levels and filtered based on a minimum log level threshold.
/// All operations are non-blocking and safe for use in multi-threaded applications.
/// </remarks>
public static class Logging
{
    /// <summary>
    /// Gets the current number of messages in the log queue.
    /// </summary>
    /// <value>
    /// The number of messages currently stored in the log queue.
    /// </value>
    public static int Count { get { return _logQueue.Count; } }

    public static bool ConsoleOutput { get; set; } = true;

    /// <summary>
    /// Thread-safe queue storing log messages with their associated levels.
    /// </summary>
    private static ConcurrentQueue<(Level level, string message)> _logQueue = new();


    /// <summary>
    /// Defines the severity levels for log messages.
    /// </summary>
    /// <remarks>
    /// Log levels are ordered from least to most severe:
    /// - Performance: Detailed timing and performance measurements
    /// - Debug: Detailed information for debugging
    /// - Info: General informational messages
    /// - Warning: Potential issues that don't prevent normal operation
    /// - Error: Serious issues that may affect functionality
    /// </remarks>
    public enum Level
    {
        /// <summary>
        /// Performance-related messages, typically used for timing and optimization.
        /// </summary>
        Performance,

        /// <summary>
        /// Debug-level messages containing detailed information for troubleshooting.
        /// </summary>
        Debug,

        /// <summary>
        /// Informational messages about normal application operation.
        /// </summary>
        Info,

        /// <summary>
        /// Warning messages indicating potential issues that don't prevent operation.
        /// </summary>
        Warning,

        /// <summary>
        /// Error messages indicating serious issues that may affect functionality.
        /// </summary>
        Error,
    }

    /// <summary>
    /// Gets or sets the minimum level for messages to be logged.
    /// </summary>
    /// <value>
    /// The current log level threshold. Messages with a level lower than this will be ignored.
    /// Defaults to <see cref="Level.Performance"/>.
    /// </value>
    public static Level LogLevel { get; set; } = Level.Performance;

    /// <summary>
    /// Logs a message with the specified severity level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="level">The severity level of the message. Defaults to <see cref="Level.Debug"/>.</param>
    /// <remarks>
    /// Messages are only logged if their level is greater than or equal to the current <see cref="LogLevel"/>.
    /// The operation is thread-safe and non-blocking.
    /// </remarks>
    public static void Log(string message, Level level = Level.Debug)
    {
        if (level < LogLevel) return;
        _logQueue.Enqueue(new(level, message));
        if (ConsoleOutput)
        {
            Console.WriteLine($"[{level}] {message}");
        }
    }

    /// <summary>
    /// Retrieves and removes all messages from the log queue that meet the minimum level requirement.
    /// </summary>
    /// <returns>
    /// A list of tuples containing the level and message for each log entry that meets the minimum level requirement.
    /// The list is ordered from oldest to newest message.
    /// </returns>
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    /// <item><description>Dequeues all messages from the log queue</description></item>
    /// <item><description>Filters out messages below the current <see cref="LogLevel"/></description></item>
    /// <item><description>Returns the filtered messages in a new list</description></item>
    /// </list>
    /// The operation is thread-safe and messages are processed atomically.
    /// After calling this method, the processed messages are removed from the queue.
    /// </remarks>
    public static List<(Level level, string message)> GetLog()
    {
        var result = new List<(Level level, string message)>();
        while (_logQueue.TryDequeue(out var entry))
        {
            if (entry.level >= LogLevel)
            {
                result.Add(entry);
            }
        }
        return result;
    }
}

