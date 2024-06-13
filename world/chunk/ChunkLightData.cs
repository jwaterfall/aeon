using Godot;
using System;

namespace Aeon
{
    class ChunkLightData
    {
        private byte[] _data;
        private Vector3I _dimensions;

        public ChunkLightData(Vector3I chunkDimensions)
        {
            _data = new byte[chunkDimensions.X * chunkDimensions.Y * chunkDimensions.Z / 2];
            _dimensions = chunkDimensions;
        }

        private int GetIndex(Vector3I localPosition)
        {
            return localPosition.Y * _dimensions.Z * _dimensions.X + localPosition.Z * _dimensions.X + localPosition.X;
        }

        public byte Get(Vector3I localPosition)
        {
            int index = GetIndex(localPosition);
            int byteIndex = index / 2;
            bool isLowerNibble = index % 2 == 0;

            if (isLowerNibble)
            {
                return (byte)(_data[byteIndex] & 0x0F);
            }
            else
            {
                return (byte)(_data[byteIndex] >> 4 & 0x0F);
            }
        }

        public void Set(Vector3I localPosition, byte value)
        {
            int index = GetIndex(localPosition);
            int byteIndex = index / 2;
            bool isLowerNibble = index % 2 == 0;

            if (isLowerNibble)
            {
                _data[byteIndex] = (byte)(_data[byteIndex] & 0xF0 | value & 0x0F);
            }
            else
            {
                _data[byteIndex] = (byte)(_data[byteIndex] & 0x0F | value << 4);
            }
        }
    }
}
