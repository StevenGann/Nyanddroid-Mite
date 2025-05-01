using System;

namespace NyandroidMite
{
    /// <summary>
    /// Provides data for the ButtonStatesReceived event.
    /// </summary>
    public class ButtonStatesEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the array of button states.
        /// </summary>
        /// <value>An array of boolean values where true indicates a pressed button.</value>
        public bool[] ButtonStates { get; }

        /// <summary>
        /// Initializes a new instance of the ButtonStatesEventArgs class.
        /// </summary>
        /// <param name="buttonStates">The array of button states to be passed with the event.</param>
        public ButtonStatesEventArgs(bool[] buttonStates)
        {
            ButtonStates = buttonStates;
        }
    }

    /// <summary>
    /// Provides data for the AnalogValuesReceived event.
    /// </summary>
    public class AnalogValuesEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the array of analog values.
        /// </summary>
        /// <value>An array of integer values representing analog sensor readings.</value>
        public int[] AnalogValues { get; }

        /// <summary>
        /// Initializes a new instance of the AnalogValuesEventArgs class.
        /// </summary>
        /// <param name="analogValues">The array of analog values to be passed with the event.</param>
        public AnalogValuesEventArgs(int[] analogValues)
        {
            AnalogValues = analogValues;
        }
    }
} 