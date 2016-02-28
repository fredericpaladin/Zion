namespace StandardDisk
{
    public struct Block
    {
        private Sector[] _sectors;

        internal Block(uint sectorSize, uint sectorNum, uint address)
            : this()
        {
            Address = address;
            SectorSize = (int)sectorSize;
            _sectors = new Sector[sectorNum];

            for (uint i = 0; i < sectorNum; i++)
                _sectors[i] = new Sector(sectorSize);
        }

        internal uint Address { get; private set; }

        internal int SectorSize { get; private set; }

        internal int Length
        {
            get
            {
                int length = 0;
                for (int i = 0; i < _sectors.Length; i++)
                    length += _sectors[i].Length;

                return length;
            }
        }

        internal byte[] ReadData()
        {
            byte[] data = new byte[SectorSize * _sectors.Length];

            for (uint i = 0; i < _sectors.Length; i++)
            {
                for (uint offset = 0; offset < _sectors[i].Data.Length; offset++)
                    data[offset + (i * SectorSize)] = _sectors[i].ReadData(offset);
            }

            return data;
        }

        internal void WriteData(byte[] data, int writeAtOffset)
        {
            // Calculate the starting sector (i.e. if the offset is greater than the sector size, we cannot write at sector 0).
            int start = writeAtOffset / SectorSize;

            for (int i = start; i < _sectors.Length; i++)
            {
                int writeOffset = (i == start) ? writeAtOffset % SectorSize : 0;
                int readOffset = (i == start) ? 0 : (i * SectorSize) - writeAtOffset;
                _sectors[i].WriteData(data, writeOffset, readOffset);
            }
        }
    }
}