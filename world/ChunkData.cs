using Godot;
using System;

namespace Aeon
{
    internal class ChunkData
    {
        private byte[] _blocks;
        public readonly Vector3I Dimensions;

        public ChunkData(Vector3I dimensions)
        {
            _blocks = new byte[dimensions.X * dimensions.Y * dimensions.Z];
            Dimensions = dimensions;
        }

        public BlockType GetBlock(Vector3I localPosition)
        {
            return BlockTypes.Instance.Get(_blocks[GetIndex(localPosition, Dimensions)]);
        }

        public void SetBlock(Vector3I localPosition, BlockType blockType)
        {
            _blocks[GetIndex(localPosition, Dimensions)] = blockType.Id;
        }

        private int GetIndex(Vector3I localPosition, Vector3I dimensions)
        {
            return localPosition.X + dimensions.X * (localPosition.Y + dimensions.Y * localPosition.Z);
        }
    }
}
