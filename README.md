# Nyandroid-Mite

Nyandroid-Mite is an autonomous robot project consisting of two major components: Firmware and Software. The system is designed to provide a platform for robotics experimentation, featuring motor control, sensor integration, and high-level AI capabilities.

## System Architecture

### Hardware Components
- **Microcontroller**: DFRduino Romeo board (Arduino UNO Atmega328)
- **Single Board Computer**: Raspberry Pi
- **Communication**: USB Serial connection between Arduino and Raspberry Pi

### Software Components

#### 1. Firmware (Arduino)
The firmware runs on the DFRduino Romeo board and handles:
- Motor control for two DC motors via H-bridge
- Servo control (up to 14 servos)
- Button input reading (7 buttons total)
- Analog sensor reading (7 analog inputs)
- Serial communication with the Raspberry Pi

#### 2. Software (Raspberry Pi)
The software runs on the Raspberry Pi and provides:
- AI and decision making
- Computer vision processing
- LIDAR mapping and navigation
- High-level control of the robot
- Communication with the microcontroller

## Communication Protocol

### Command Format (Raspberry Pi → Arduino)
Commands are sent as comma-separated values in the format:
```
M1,M2,S1,P1,S2,P2,...
```
Where:
- `M1`, `M2`: Motor speeds (-255 to 255)
- `S1,P1`, `S2,P2`: Servo pin and position pairs (pin: 0-13, position: 0-255)

Example commands:
- `100,-100` - Set motor 1 to 100, motor 2 to -100
- `0,0,8,128,9,64` - Stop motors, set servo on pin 8 to position 128, servo on pin 9 to position 64

### Sensor Data Format (Arduino → Raspberry Pi)
Sensor data is sent in the format:
```
B:0000000 A:123,456,789,...
```
Where:
- `B:`: Followed by 7 button states (0 or 1)
- `A:`: Followed by 7 comma-separated analog values (0-1023)

The sensor data is sent:
1. Every second automatically
2. Immediately after processing a command

## Hardware Pin Configuration

### Motor Control
- Motor 1 Direction: Digital Pin 4
- Motor 1 PWM: Digital Pin 5
- Motor 2 PWM: Digital Pin 6
- Motor 2 Direction: Digital Pin 7

### Button Inputs
- S1-S5: Analog Pin A7 (multiplexed)
- S6: Digital Pin 2
- S7: Digital Pin 3

### Analog Inputs
- A0-A6: Available for sensor connections

### Servo Control
- Any digital pin (0-13) can be used for servo control
- Servos are automatically attached when first used

## Development Setup

### Firmware Development
1. Install Arduino IDE
2. Connect DFRduino Romeo board via USB
3. Open `Firmware/Firmware.ino` in Arduino IDE
4. Select correct board and port
5. Upload firmware

### Software Development
1. Install .NET Core on Raspberry Pi
2. Navigate to `Software` directory
3. Build and run the project

## Dependencies

### Firmware
- Arduino IDE
- Servo.h library (included with Arduino IDE)

### Software
- .NET Core
- Additional dependencies listed in `Software/nyandroid-mite.csproj`

## Notes
- The firmware uses internal pull-up resistors for button inputs
- Motor speeds are clamped to -255 to 255 range
- Servo positions are mapped from 0-255 to 0-180 degrees
- Analog values range from 0-1023 (10-bit ADC)
- Button states are inverted due to pull-up configuration (0 = pressed, 1 = not pressed)

## Troubleshooting
1. If motors don't respond:
   - Check motor connections
   - Verify H-bridge wiring
   - Ensure power supply is adequate

2. If servos don't move:
   - Verify servo connections
   - Check power supply
   - Ensure servo pins are correctly specified

3. If communication fails:
   - Verify USB connection
   - Check baud rate (9600)
   - Ensure correct COM port selection
