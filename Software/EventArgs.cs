using System;

namespace NyandroidMite
{
    public class ButtonStatesEventArgs : EventArgs
    {
        public bool[] ButtonStates { get; }

        public ButtonStatesEventArgs(bool[] buttonStates)
        {
            ButtonStates = buttonStates;
        }
    }

    public class AnalogValuesEventArgs : EventArgs
    {
        public int[] AnalogValues { get; }

        public AnalogValuesEventArgs(int[] analogValues)
        {
            AnalogValues = analogValues;
        }
    }
} 