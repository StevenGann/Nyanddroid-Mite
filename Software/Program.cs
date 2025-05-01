using System;
using System.Threading;

namespace NyandroidMite
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Nyandroid Mite CLI");
            Console.WriteLine("=================");
            
            using var firmwareManager = new FirmwareManager();
            
            try
            {
                Console.WriteLine("Connecting to Nyandroid Mite...");
                firmwareManager.Connect();
                Console.WriteLine("Connected successfully!");

                // Subscribe to events
                firmwareManager.ButtonStatesReceived += (sender, e) =>
                {
                    Console.WriteLine($"\nButton States: {string.Join(", ", e.ButtonStates)}");
                };

                firmwareManager.AnalogValuesReceived += (sender, e) =>
                {
                    Console.WriteLine($"\nAnalog Values: {string.Join(", ", e.AnalogValues)}");
                };

                // Main menu loop
                while (true)
                {
                    Console.WriteLine("\nMenu:");
                    Console.WriteLine("1. Send motor commands");
                    Console.WriteLine("2. Send servo commands");
                    Console.WriteLine("3. Send combined motor and servo commands");
                    Console.WriteLine("4. Exit");
                    Console.Write("\nSelect an option: ");

                    if (!int.TryParse(Console.ReadLine(), out int choice))
                    {
                        Console.WriteLine("Invalid input. Please enter a number.");
                        continue;
                    }

                    switch (choice)
                    {
                        case 1:
                            SendMotorCommands(firmwareManager);
                            break;
                        case 2:
                            SendServoCommands(firmwareManager);
                            break;
                        case 3:
                            SendCombinedCommands(firmwareManager);
                            break;
                        case 4:
                            Console.WriteLine("Exiting...");
                            return;
                        default:
                            Console.WriteLine("Invalid option. Please try again.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void SendMotorCommands(FirmwareManager firmwareManager)
        {
            Console.Write("Enter motor 1 speed (-255 to 255): ");
            if (!int.TryParse(Console.ReadLine(), out int motor1Speed))
            {
                Console.WriteLine("Invalid input for motor 1 speed.");
                return;
            }

            Console.Write("Enter motor 2 speed (-255 to 255): ");
            if (!int.TryParse(Console.ReadLine(), out int motor2Speed))
            {
                Console.WriteLine("Invalid input for motor 2 speed.");
                return;
            }

            firmwareManager.SendCommand(motor1Speed, motor2Speed);
            Console.WriteLine("Motor commands sent successfully!");
        }

        static void SendServoCommands(FirmwareManager firmwareManager)
        {
            Console.Write("Enter number of servos to control: ");
            if (!int.TryParse(Console.ReadLine(), out int servoCount) || servoCount <= 0)
            {
                Console.WriteLine("Invalid number of servos.");
                return;
            }

            var servos = new (int pin, int position)[servoCount];
            for (int i = 0; i < servoCount; i++)
            {
                Console.Write($"Enter servo {i + 1} pin number: ");
                if (!int.TryParse(Console.ReadLine(), out int pin))
                {
                    Console.WriteLine("Invalid pin number.");
                    return;
                }

                Console.Write($"Enter servo {i + 1} position (0-180): ");
                if (!int.TryParse(Console.ReadLine(), out int position))
                {
                    Console.WriteLine("Invalid position.");
                    return;
                }

                servos[i] = (pin, position);
            }

            firmwareManager.SendCommand(0, 0, servos);
            Console.WriteLine("Servo commands sent successfully!");
        }

        static void SendCombinedCommands(FirmwareManager firmwareManager)
        {
            Console.Write("Enter motor 1 speed (-255 to 255): ");
            if (!int.TryParse(Console.ReadLine(), out int motor1Speed))
            {
                Console.WriteLine("Invalid input for motor 1 speed.");
                return;
            }

            Console.Write("Enter motor 2 speed (-255 to 255): ");
            if (!int.TryParse(Console.ReadLine(), out int motor2Speed))
            {
                Console.WriteLine("Invalid input for motor 2 speed.");
                return;
            }

            Console.Write("Enter number of servos to control: ");
            if (!int.TryParse(Console.ReadLine(), out int servoCount) || servoCount <= 0)
            {
                Console.WriteLine("Invalid number of servos.");
                return;
            }

            var servos = new (int pin, int position)[servoCount];
            for (int i = 0; i < servoCount; i++)
            {
                Console.Write($"Enter servo {i + 1} pin number: ");
                if (!int.TryParse(Console.ReadLine(), out int pin))
                {
                    Console.WriteLine("Invalid pin number.");
                    return;
                }

                Console.Write($"Enter servo {i + 1} position (0-180): ");
                if (!int.TryParse(Console.ReadLine(), out int position))
                {
                    Console.WriteLine("Invalid position.");
                    return;
                }

                servos[i] = (pin, position);
            }

            firmwareManager.SendCommand(motor1Speed, motor2Speed, servos);
            Console.WriteLine("Combined commands sent successfully!");
        }
    }
} 