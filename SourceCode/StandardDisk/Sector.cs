using System;

namespace StandardDisk
{
    public struct Sector
    {
        internal Sector(uint sectorSize)
            : this()
        {
            Data = new byte[sectorSize];
        }

        internal byte[] Data { get; set; }

        internal int Length { get; private set; }

        internal byte ReadData(uint index)
        {
            if (index >= Data.Length)
                return 0;

            return Data[index];
        }

        internal void WriteData(byte[] data, int writeAtOffset, int readAtOffset)
        {
            bool emptyData = false;
            if (data.Length == 0)
            {
                emptyData = true;
                data = new byte[Data.Length];
            }

            Length = writeAtOffset; // The offset means that there is already data in this sector
            for (int i = 0; i < Data.Length; i++)
            {
                if (i + writeAtOffset >= Data.Length)
                    return; // No more space to write

                if (i + readAtOffset >= data.Length)
                {
                    // Nothing else to read: add zeros (in case there was other data before)
                    Data[i + writeAtOffset] = 0;
                    continue;
                }

                Data[i + writeAtOffset] = data[i + readAtOffset];
                if (!emptyData)
                    Length++;
            }
        }

        public override string ToString()
        {
            return String.Format("Sector length {0:n0}", Length);
        }
    }
}