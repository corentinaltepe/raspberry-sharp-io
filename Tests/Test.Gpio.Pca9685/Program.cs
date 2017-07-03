using System;
using System.Collections.Generic;
using System.Threading;
using Common.Logging;
using Raspberry.IO.Components.Controllers.Pca9685;
using Raspberry.IO.GeneralPurpose;
using Raspberry.IO.InterIntegratedCircuit;
using UnitsNet;
using Mono.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Test.Gpio.PCA9685
{
    /// <summary>
    /// Demonstrates a connection to the Pca9685 LED controller - used by the Adafruit 16-channel PWM/Servo Shield
    /// </summary>
    /// <remarks>
    /// 
    /// Ported from https://github.com/adafruit/Adafruit_Python_PCA9685/blob/master/examples/simpletest.py
    /// </remarks>
    class Program
    {
        private static ILogger<Program> log;

        static void Main(string[] args)
        {
            var options = ParseOptions(args);
            if (options == null)
                return;

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();

            var sp = serviceCollection.BuildServiceProvider();
            sp.GetRequiredService<LoggerFactory>().AddConsole().AddDebug();
            log = sp.GetRequiredService<ILogger<Program>>();

            log.LogInformation("-=Pca9685 Sample=-");
            log.LogInformation("Running {0}", System.Runtime.InteropServices.RuntimeInformation.OSDescription);
            log.LogInformation("Options:" + Environment.NewLine + options);

            var pulse = CalculatePulse(options.PwmFrequency, 50);
            log.LogInformation("Pulse={0}", pulse);

            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                log.LogWarning("Windows detected. Exiting");
                return;
            }

            log.LogInformation("Connecting...");

            try
            {
                using (var driver = new I2cDriver(options.SdaPin.ToProcessor(), options.SclPin.ToProcessor()))
                {
                    log.LogInformation("Creating device...");
                    var device = new Pca9685Connection(sp, driver.Connect(options.DeviceAddress));

                    log.LogInformation("Setting frequency...");
                    device.SetPwmUpdateRate(options.PwmFrequency);
                    while (!Console.KeyAvailable)
                    {
                        log.LogInformation("Set channel={0} to {1}", options.Channel, options.PwmOn);
                        device.SetPwm(options.Channel, 0, options.PwmOn);
                        Thread.Sleep(1000);
                        log.LogInformation("Set channel={0} to {1}", options.Channel, options.PwmOff);
                        device.SetPwm(options.Channel, 0, options.PwmOff);
                        Thread.Sleep(1000);
                    }
                    log.LogInformation("Key pressed. Exiting.");
                }
            }
            catch (InvalidOperationException e)
            {
                log.LogError(new EventId(0), e, "Failed to connect? Do you have a Pca9685 IC attached to the i2c line and powered on?");
            }
        }

        #region Private Helpers

        /// <summary>
        /// Ported but wasn't used in original? Ported from https://github.com/adafruit/Adafruit-Raspberry-Pi-Python-Code/blob/master/Adafruit_PWM_Servo_Driver/Servo_Example.py
        /// Not entirely sure what the result is meant to mean.
        /// </summary>
        private static int CalculatePulse(Frequency frequency, int pulse)
        {
            const int microSeconds = 1000000; // # 1,000,000 us per second

            var pulseLengthMicroSeconds = microSeconds / (int)frequency.Hertz; // # 60 Hz
            log.LogInformation("{0} uSecs per period", pulseLengthMicroSeconds);

            var microSecondsPerBit = pulseLengthMicroSeconds / 4096; // # 12 bits of resolution
            log.LogInformation("{0} uSecs per bit", microSecondsPerBit);

            return (pulse * 1000) / pulseLengthMicroSeconds;
        }

        private static Pca9685Options ParseOptions(IEnumerable<string> arguments)
        {
            var options = new Pca9685Options
            {
                SdaPin = ConnectorPin.P1Pin03,
                SclPin = ConnectorPin.P1Pin05,
                DeviceAddress = 0x40,
                PwmFrequency = Frequency.FromHertz(60),
                PwmOn = 150,
                PwmOff = 600
            };

            var optionSet = new OptionSet
            {
                {"c|Channel=", v => options.Channel = (PwmChannel) Enum.Parse(typeof (PwmChannel), v)},
                {"f|PwmFrequency=", v => options.PwmFrequency = Frequency.FromHertz(int.Parse(v))},
                {"b|PwmOn=", v => options.PwmOn = int.Parse(v)},
                {"e|PwmOff=", v => options.PwmOff = int.Parse(v)},
                {"h|?:", v => options.ShowHelp = true}
            };

            optionSet.Parse(arguments);

            if (options.ShowHelp)
            {
                Console.WriteLine("Options:");
                optionSet.WriteOptionDescriptions(Console.Out);

                return null;
            }

            return options;
        }

        #endregion
    }
}
