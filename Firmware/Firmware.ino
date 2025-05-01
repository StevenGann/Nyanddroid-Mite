/**
 * Nyandroid Mite Microcontroller Firmware
 * 
 * This firmware runs on a DFRduino Romeo board (Arduino UNO Atmega328) and provides:
 * - Button reading for S1-S7 (S1-S5 on analog A7, S6-S7 on digital pins 2-3)
 * - Analog reading for pins A0-A6
 * - H-bridge motor control for two motors
 * - Servo control on any digital pin
 * - Serial communication for receiving commands and sending sensor data
 * 
 * Serial Communication:
 * - Commands are received in format: M1,M2,S1,P1,S2,P2,...
 *   where M1,M2 are motor speeds (-255 to 255)
 *   and S1,P1 are servo pin and position pairs
 * - Sensor data is sent in format: B:0000000 A:123,456,789,...
 *   where B: is followed by 7 button states (0 or 1)
 *   and A: is followed by 7 analog values
 * 
 * Timing:
 * - Sensor data is sent every second
 * - Data is also sent immediately after processing a command
 */

#include <Servo.h>  // Required for servo control

// Pin Definitions
const int BUTTON_S6 = 2;    // Digital pin for button S6
const int BUTTON_S7 = 3;    // Digital pin for button S7
const int ANALOG_BUTTONS = A7;  // Analog pin for buttons S1-S5

// Motor Control Pins (H-bridge control)
const int MOTOR1_DIR = 4;   // Motor 1 direction control
const int MOTOR1_PWM = 5;   // Motor 1 speed control (PWM)
const int MOTOR2_PWM = 6;   // Motor 2 speed control (PWM)
const int MOTOR2_DIR = 7;   // Motor 2 direction control

// Servo objects and tracking
Servo servos[14];           // Array of servo objects (one per possible digital pin)
bool servoAttached[14] = {false};  // Track which pins have servos attached

// Timing control for periodic sensor data transmission
unsigned long lastSendTime = 0;     // Last time sensor data was sent
const unsigned long SEND_INTERVAL = 1000;  // Send interval in milliseconds (1 second)

/**
 * Setup function - runs once at startup
 * Initializes all hardware and communication
 */
void setup() {
  // Initialize serial communication at 9600 baud
  Serial.begin(9600);
  
  // Setup button pins with internal pull-up resistors
  pinMode(BUTTON_S6, INPUT_PULLUP);
  pinMode(BUTTON_S7, INPUT_PULLUP);
  
  // Setup motor control pins as outputs
  pinMode(MOTOR1_DIR, OUTPUT);
  pinMode(MOTOR1_PWM, OUTPUT);
  pinMode(MOTOR2_PWM, OUTPUT);
  pinMode(MOTOR2_DIR, OUTPUT);
  
  // Initialize motors to stop
  setMotors(0, 0);
}

/**
 * Main loop function - runs continuously
 * Handles serial communication and periodic sensor data transmission
 */
void loop() {
  // Check for incoming serial commands
  if (Serial.available() > 0) {
    String command = Serial.readStringUntil('\n');
    processCommand(command);
    // Immediately send back sensor data after processing command
    sendSensorData();
  }
  
  // Send sensor data every second
  unsigned long currentTime = millis();
  if (currentTime - lastSendTime >= SEND_INTERVAL) {
    sendSensorData();
    lastSendTime = currentTime;
  }
}

/**
 * Reads the state of all buttons (S1-S7)
 * @param buttonStates Array to store button states (true = pressed)
 */
void readButtons(bool* buttonStates) {
  // Read analog buttons (S1-S5) from A7
  int analogValue = analogRead(ANALOG_BUTTONS);
  
  // Decode analog value to individual buttons using voltage thresholds
  // These thresholds are approximate and may need adjustment based on hardware
  buttonStates[0] = (analogValue < 50);   // S1
  buttonStates[1] = (analogValue >= 50 && analogValue < 200);   // S2
  buttonStates[2] = (analogValue >= 200 && analogValue < 400);  // S3
  buttonStates[3] = (analogValue >= 400 && analogValue < 600);  // S4
  buttonStates[4] = (analogValue >= 600 && analogValue < 800);  // S5
  
  // Read digital buttons (S6, S7)
  buttonStates[5] = !digitalRead(BUTTON_S6);  // S6 (inverted due to pull-up)
  buttonStates[6] = !digitalRead(BUTTON_S7);  // S7 (inverted due to pull-up)
}

/**
 * Reads all analog pins (A0-A6)
 * @param analogValues Array to store analog readings
 */
void readAnalogPins(int* analogValues) {
  for (int i = 0; i < 7; i++) {
    analogValues[i] = analogRead(i);
  }
}

/**
 * Sets the speed and direction of both motors
 * @param motor1Speed Speed for motor 1 (-255 to 255)
 * @param motor2Speed Speed for motor 2 (-255 to 255)
 */
void setMotors(int motor1Speed, int motor2Speed) {
  // Motor 1 control
  if (motor1Speed > 0) {
    digitalWrite(MOTOR1_DIR, HIGH);
    analogWrite(MOTOR1_PWM, motor1Speed);
  } else {
    digitalWrite(MOTOR1_DIR, LOW);
    analogWrite(MOTOR1_PWM, -motor1Speed);
  }
  
  // Motor 2 control
  if (motor2Speed > 0) {
    digitalWrite(MOTOR2_DIR, HIGH);
    analogWrite(MOTOR2_PWM, motor2Speed);
  } else {
    digitalWrite(MOTOR2_DIR, LOW);
    analogWrite(MOTOR2_PWM, -motor2Speed);
  }
}

/**
 * Sets the position of a servo on a specified pin
 * @param pin The digital pin the servo is connected to
 * @param position The desired position (0-255)
 */
void setServo(int pin, int position) {
  // Clamp position to valid range
  if (position < 0) position = 0;
  if (position > 255) position = 255;
  
  // Attach servo if not already attached
  if (!servoAttached[pin]) {
    servos[pin].attach(pin);
    servoAttached[pin] = true;
  }
  
  // Convert 0-255 to 0-180 degrees for servo control
  int angle = map(position, 0, 255, 0, 180);
  servos[pin].write(angle);
}

/**
 * Processes incoming serial commands
 * @param command The command string to process
 */
void processCommand(String command) {
  // Command format: M1,M2,S1,P1,S2,P2,...
  // M1,M2 are motor speeds (-255 to 255)
  // S1,P1 are servo pin and position pairs
  
  // Split command into parts
  int parts[20]; // Array to store command parts
  int partCount = 0;
  int lastIndex = 0;
  
  // Parse comma-separated values
  for (int i = 0; i < command.length(); i++) {
    if (command[i] == ',') {
      parts[partCount++] = command.substring(lastIndex, i).toInt();
      lastIndex = i + 1;
    }
  }
  parts[partCount++] = command.substring(lastIndex).toInt();
  
  // Process motor commands (first two values)
  if (partCount >= 2) {
    setMotors(parts[0], parts[1]);
  }
  
  // Process servo commands (remaining pairs)
  for (int i = 2; i < partCount; i += 2) {
    if (i + 1 < partCount) {
      setServo(parts[i], parts[i + 1]);
    }
  }
}

/**
 * Sends current sensor data over serial
 * Format: B:0000000 A:123,456,789,...
 */
void sendSensorData() {
  bool buttonStates[7];
  int analogValues[7];
  
  // Read all sensors
  readButtons(buttonStates);
  readAnalogPins(analogValues);
  
  // Send button states
  Serial.print("B:");
  for (int i = 0; i < 7; i++) {
    Serial.print(buttonStates[i] ? "1" : "0");
  }
  
  // Send analog values
  Serial.print(" A:");
  for (int i = 0; i < 7; i++) {
    Serial.print(analogValues[i]);
    if (i < 6) Serial.print(",");
  }
  
  Serial.println();
}
