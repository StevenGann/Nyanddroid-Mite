using System;
using System.Threading;
using System.Numerics;

namespace NyandroidMite
{

    public interface ISensor2D
    {
        /// <summary>
        /// Configures the sensor's position and orientation relative to the robot's center
        /// </summary>
        /// <param name="offset">The 2D position offset from the robot's center</param>
        /// <param name="rotationRadians">The rotation offset in radians</param>
        void ConfigureSensor(Vector2 offset, float rotationRadians);

        /// <summary>
        /// Queries the sensor to get obstacle detection points
        /// </summary>
        /// <returns>Array of Vector2 points representing detected obstacles in robot's coordinate frame</returns>
        Vector2[] QuerySensor();

        /// <summary>
        /// Gets the current sensor configuration
        /// </summary>
        /// <returns>Tuple containing the current offset and rotation</returns>
        (Vector2 offset, float rotationRadians) GetConfiguration();
    }

}
