#region References

using System;
using Raspberry.IO.InterIntegratedCircuit;
using Raspberry.Timers;
using Raspberry.IO.Components.Sensors.Pressure.Bmp085;

#endregion

namespace Raspberry.IO.Components.Sensors.Pressure.Bmp280
{
    /// <summary>
    /// Represents an I2C connection to a Bmp280 barometer / thermometer.
    /// </summary>
    public class Bmp280I2cConnection
    {
        protected struct CalibrationData
        {
            public ushort dig_T1;
            public short dig_T2;
            public short dig_T3;

            public ushort dig_P1;
            public short dig_P2;
            public short dig_P3;
            public short dig_P4;
            public short dig_P5;
            public short dig_P6;
            public short dig_P7;
            public short dig_P8;
            public short dig_P9;

            public byte dig_H1;
            public short dig_H2;
            public byte dig_H3;
            public short dig_H4;
            public short dig_H5;
            public sbyte dig_H6;
        }

        #region Fields

        private readonly I2cDeviceConnection connection;

        private static readonly TimeSpan lowDelay = TimeSpan.FromMilliseconds(5);
        private static readonly TimeSpan highDelay = TimeSpan.FromMilliseconds(14);
        private static readonly TimeSpan highestDelay = TimeSpan.FromMilliseconds(26);
        private static readonly TimeSpan defaultDelay = TimeSpan.FromMilliseconds(8);

        protected CalibrationData calibration = new CalibrationData();
        protected int t_fine;
        #endregion

        #region Instance Management

        /// <summary>
        /// Initializes a new instance of the <see cref="Bmp085I2cConnection"/> class.
        /// </summary>
        /// <param name="connection">The connection.</param>
        public Bmp280I2cConnection(I2cDeviceConnection connection)
        {
            this.connection = connection;
            Initialize();
        }

        #endregion

        #region Properties

        public const int DefaultAddress = 0x77;

        #endregion

        #region Methods

        /// <summary>
        /// Gets the pressure.
        /// </summary>
        /// <returns>The pressure.</returns>
        public UnitsNet.Pressure GetPressure()
        {
            return GetData().Pressure;
        }

        /// <summary>
        /// Gets the temperature.
        /// </summary>
        /// <returns>The temperature.</returns>
        public UnitsNet.Temperature GetTemperature()
        {
            // Do not use GetData here since it would imply useless I/O and computation.
            var rawTemperature = GetRawTemperature();

            return UnitsNet.Temperature.FromDegreesCelsius(Math.Round(rawTemperature / 100D, 2));
        }

        /// <summary>
        /// Gets the data.
        /// </summary>
        /// <returns>The data.</returns>
        public BmpData GetData()
        {
            var rawPressure = GetRawPressure();
            var rawTemperature = (t_fine * 5 + 128) >> 8;
            
            return new BmpData
            {
                Pressure = UnitsNet.Pressure.FromPascals(Math.Round(rawPressure / 256D, 2)),
                Temperature = UnitsNet.Temperature.FromDegreesCelsius(Math.Round(rawTemperature / 100D, 2))
            };
        }

        #endregion

        #region Private Helpers

        private static class Interop
        {
            public const byte REGISTER_DIG_T1 = 0x88;
            public const byte REGISTER_DIG_T2 = 0x8A;
            public const byte REGISTER_DIG_T3 = 0x8C;

            public const byte REGISTER_DIG_P1 = 0x8E;
            public const byte REGISTER_DIG_P2 = 0x90;
            public const byte REGISTER_DIG_P3 = 0x92;
            public const byte REGISTER_DIG_P4 = 0x94;
            public const byte REGISTER_DIG_P5 = 0x96;
            public const byte REGISTER_DIG_P6 = 0x98;
            public const byte REGISTER_DIG_P7 = 0x9A;
            public const byte REGISTER_DIG_P8 = 0x9C;
            public const byte REGISTER_DIG_P9 = 0x9E;

            public const byte REGISTER_DIG_H1 = 0xA1;
            public const byte REGISTER_DIG_H2 = 0xE1;
            public const byte REGISTER_DIG_H3 = 0xE3;
            public const byte REGISTER_DIG_H4 = 0xE4;
            public const byte REGISTER_DIG_H5 = 0xE5;
            public const byte REGISTER_DIG_H6 = 0xE7;

            public const byte REGISTER_CHIPID = 0xD0;
            public const byte REGISTER_VERSION = 0xD1;
            public const byte REGISTER_SOFTRESET = 0xE0;

            public const byte REGISTER_CAL26 = 0xE1;  // R calibration stored in 0xE1-0xF0

            public const byte REGISTER_CONTROLHUMID = 0xF2;
            public const byte REGISTER_CONTROL = 0xF4;
            public const byte REGISTER_CONFIG = 0xF5;
            public const byte REGISTER_PRESSUREDATA = 0xF7;
            public const byte REGISTER_TEMPDATA = 0xFA;
            public const byte REGISTER_HUMIDDATA = 0xFD;
        }

        private void Initialize()
        {
            if (ReadByte(0xD0) != 0x58)
                throw new InvalidOperationException("Device is not a Bmp280 barometer");

            calibration.dig_T1 = ReadUInt16_LE(Interop.REGISTER_DIG_T1);
            calibration.dig_T2 = ReadInt16_LE(Interop.REGISTER_DIG_T2);
            calibration.dig_T3 = ReadInt16_LE(Interop.REGISTER_DIG_T3);

            calibration.dig_P1 = ReadUInt16_LE(Interop.REGISTER_DIG_P1);
            calibration.dig_P2 = ReadInt16_LE(Interop.REGISTER_DIG_P2);
            calibration.dig_P3 = ReadInt16_LE(Interop.REGISTER_DIG_P3);
            calibration.dig_P4 = ReadInt16_LE(Interop.REGISTER_DIG_P4);
            calibration.dig_P5 = ReadInt16_LE(Interop.REGISTER_DIG_P5);
            calibration.dig_P6 = ReadInt16_LE(Interop.REGISTER_DIG_P6);
            calibration.dig_P7 = ReadInt16_LE(Interop.REGISTER_DIG_P7);
            calibration.dig_P8 = ReadInt16_LE(Interop.REGISTER_DIG_P8);
            calibration.dig_P9 = ReadInt16_LE(Interop.REGISTER_DIG_P9);
        }

        private int GetRawTemperature()
        {
            int var1, var2;
            int adc_T = ReadInt24(Interop.REGISTER_TEMPDATA);

            adc_T >>= 4;

            var1 = ((((adc_T >> 3) - (calibration.dig_T1 << 1))) * (calibration.dig_T2)) >> 11;

            var2 = (((((adc_T >> 4) - (calibration.dig_T1)) *
                   ((adc_T >> 4) - (calibration.dig_T1))) >> 12) *
                 calibration.dig_T3) >> 14;

            t_fine = var1 + var2;

            return (t_fine * 5 + 128) >> 8;
        }

        private uint GetRawPressure()
        {
            GetTemperature(); // the pressure reading has a dependency of temperature
            
            long var1, var2, p;
            int adc_P = ReadInt24(Interop.REGISTER_PRESSUREDATA);

            adc_P >>= 4;

            var1 = (t_fine) - 128000;
            var2 = var1 * var1 * calibration.dig_P6;

            var2 = var2 + ((var1 * calibration.dig_P5) << 17);

            var2 = var2 + ((calibration.dig_P4) << 35);
            var1 = ((var1 * var1 * calibration.dig_P3) >> 8) + ((var1 * calibration.dig_P2) << 12);
            var1 = ((((1) << 47) + var1)) * (calibration.dig_P1) >> 33;

            if (var1 == 0)
            {
                return 0;  // avoid exception caused by division by zero
            }
            p = 1048576 - adc_P;
            p = (((p << 31) - var2) * 3125) / var1;
            var1 = ((calibration.dig_P9) * (p >> 13) * (p >> 13)) >> 25;
            var2 = ((calibration.dig_P8) * p) >> 19;

            p = ((p + var1 + var2) >> 8) + ((calibration.dig_P7) << 4);

            return (uint)p;
        }
        
        private byte ReadByte(byte address)
        {
            return ReadBytes(address, 1)[0];
        }

        private byte[] ReadBytes(byte address, int byteCount)
        {
            connection.WriteByte(address);
            return connection.Read(byteCount);
        }

        private ushort ReadUInt16(byte address)
        {
            var bytes = ReadBytes(address, 2);
            unchecked
            {
                return (ushort)((bytes[0] << 8) + bytes[1]);
            }
        }

        private ushort ReadUInt16_LE(byte address)
        {
            var bytes = ReadBytes(address, 2);
            unchecked
            {
                return (ushort)((bytes[1] << 8) + bytes[0]);
            }
        }

        private short ReadInt16(byte address) => (short)ReadUInt16(address);

        private short ReadInt16_LE(byte address) => (short)ReadUInt16_LE(address);

        int ReadInt24(byte address)
        {
            var result = ReadBytes(address, 3);
            return result[0] << 16 | result[1] << 8 | result[2];
        }

        private void WriteByte(byte address, byte data)
        {
            connection.Write(address, data);
        }

        #endregion
    }
}
