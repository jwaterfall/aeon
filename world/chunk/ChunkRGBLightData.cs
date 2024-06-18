using Godot;

namespace Aeon
{
    class ChunkRGBLightData
    {
        private Vector3I[] _data;
        private Vector3I _dimensions;

        public ChunkRGBLightData(Vector3I chunkDimensions)
        {
            // Allocate array for storing Vector3I values for each voxel
            _data = new Vector3I[chunkDimensions.X * chunkDimensions.Y * chunkDimensions.Z];
            _dimensions = chunkDimensions;
        }

        private int GetIndex(Vector3I localPosition)
        {
            // Compute flat array index from 3D coordinates
            return localPosition.Y * _dimensions.Z * _dimensions.X + localPosition.Z * _dimensions.X + localPosition.X;
        }

        public Vector3I Get(Vector3I localPosition)
        {
            int index = GetIndex(localPosition);
            return _data[index];
        }

        public void Set(Vector3I localPosition, Vector3I value)
        {
            int index = GetIndex(localPosition);
            _data[index] = value;
        }
    }
}
