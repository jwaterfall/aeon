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
            _data = new byte[chunkDimensions.X * chunkDimensions.Y * chunkDimensions.Z];
            _dimensions = chunkDimensions;
        }

        private int GetIndex(Vector3I localPosition)
        {
            return localPosition.Y * _dimensions.Z * _dimensions.X + localPosition.Z * _dimensions.X + localPosition.X;
        }

        public byte Get(Vector3I localPosition)
        {
            return _data[GetIndex(localPosition)];
        }

        public void Set(Vector3I localPosition, byte value)
        {
            _data[GetIndex(localPosition)] = value;
        }
    }
}
