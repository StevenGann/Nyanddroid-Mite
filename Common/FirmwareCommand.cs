using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common
{
    /// <summary>
    /// Represents a command to control motors and servos for the Nyandroid Mite firmware.
    /// </summary>
    public class FirmwareCommand
    {
        /// <summary>
        /// Speed for motor 1 (-255 to 255).
        /// </summary>
        public int Motor1Speed { get; set; }

        /// <summary>
        /// Speed for motor 2 (-255 to 255).
        /// </summary>
        public int Motor2Speed { get; set; }

        /// <summary>
        /// List of servo commands, each with a pin and position.
        /// </summary>
        public List<ServoCommand> Servos { get; set; } = new();

        /// <summary>
        /// Serializes this command to a JSON string.
        /// </summary>
        public string ToJson() => JsonSerializer.Serialize(this);

        /// <summary>
        /// Deserializes a JSON string to a FirmwareCommand instance.
        /// </summary>
        public static FirmwareCommand FromJson(string json) => JsonSerializer.Deserialize<FirmwareCommand>(json) ?? throw new ArgumentException("Invalid JSON for FirmwareCommand");
    }

    /// <summary>
    /// Represents a single servo command (pin and position).
    /// </summary>
    public class ServoCommand
    {
        public int Pin { get; set; }
        public int Position { get; set; }
    }
} 