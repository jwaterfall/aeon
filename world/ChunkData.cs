using Godot;
using System;

namespace Aeon
{
    public abstract class ChunkData
    {
        protected readonly Vector3I _dimensions;

        protected ChunkData(Vector3I dimensions)
        {
            _dimensions = dimensions;
        }

        protected int GetIndex(Vector3I localPosition)
        {
            return (localPosition.Y * _dimensions.Z * _dimensions.X) + (localPosition.Z * _dimensions.X) + localPosition.X;
        }

        public abstract BlockType GetBlock(Vector3I localPosition);
        public abstract void SetBlock(Chunk chunk, Vector3I localPosition, BlockType blockType);
        public abstract void Optimize(Chunk chunk);
    }

    class StandardChunkData : ChunkData
    {
        private byte[] _blocks;

        public StandardChunkData(Vector3I dimensions, byte[] blocks) : base(dimensions)
        {
            _blocks = blocks;
        }

        public StandardChunkData(Vector3I dimensions) : base(dimensions)
        {
            _blocks = new byte[dimensions.X * dimensions.Z * dimensions.Y];
        }

        public override BlockType GetBlock(Vector3I localPosition)
        {
            return BlockTypes.Instance.Get(_blocks[GetIndex(localPosition)]);
        }

        public override void SetBlock(Chunk chunk, Vector3I localPosition, BlockType blockType)
        {
            _blocks[GetIndex(localPosition)] = blockType.Id;
        }

        public override void Optimize(Chunk chunk)
        {
            var isSingleBlock = Array.TrueForAll(_blocks, blockId => blockId == _blocks[0]);

            if (isSingleBlock)
            {
                var block = BlockTypes.Instance.Get(_blocks[0]);
                chunk.SetChunkData(new SingleBlockChunkData(_dimensions, block));
            }
            else
            {
                bool canOptimize = true;
                byte[] layerBlockIds = new byte[_dimensions.Y];
                var blocksInLayer = _dimensions.X * _dimensions.Z;

                for (int y = 0; y < _dimensions.Y; y++)
                {
                    var layer = new byte[blocksInLayer];
                    Array.Copy(_blocks, y * blocksInLayer, layer, 0, blocksInLayer);

                    var firstBlockId = layer[0];
                    var isSameBlock = Array.TrueForAll(layer, blockId => blockId == firstBlockId);

                    if (isSameBlock)
                    {
                        layerBlockIds[y] = firstBlockId;
                    }
                    else
                    {
                        canOptimize = false;
                        break;
                    }
                }

                if (canOptimize)
                {
                    var blocks = Array.ConvertAll(layerBlockIds, blockId => BlockTypes.Instance.Get(blockId));
                    chunk.SetChunkData(new VerticalLayerChunkData(_dimensions, blocks));
                }
            }
        }
    }

    class VerticalLayerChunkData : ChunkData
    {
        private BlockType[] _layerBlocks;

        public VerticalLayerChunkData(Vector3I dimensions, BlockType[] layerBlocks) : base(dimensions)
        {
            this._layerBlocks = layerBlocks;
        }

        public override BlockType GetBlock(Vector3I localPosition)
        {
            return _layerBlocks[localPosition.Y];
        }

        public override void SetBlock(Chunk chunk, Vector3I localPosition, BlockType blockType)
        {
            var blocks = new byte[_dimensions.X * _dimensions.Z * _dimensions.Y];

            for (int y = 0; y < _dimensions.Y; y++)
            {
                var blockId = blockType.Id;
                for (int x = 0; x < _dimensions.X; x++)
                {
                    for (int z = 0; z < _dimensions.Z; z++)
                    {
                        blocks[y * _dimensions.Z * _dimensions.X + z * _dimensions.X + x] = blockId;
                    }
                }
            }

            blocks[GetIndex(localPosition)] = blockType.Id;

            chunk.SetChunkData(new StandardChunkData(_dimensions, blocks));
        }

        public override void Optimize(Chunk chunk)
        {
            // No point in optimizing from here as players can only place a single block at a time
        }
    }

    class SingleBlockChunkData : ChunkData
    {
        private BlockType _block;

        public SingleBlockChunkData(Vector3I dimensions, BlockType block) : base(dimensions)
        {
            _block = block;
        }

        public override BlockType GetBlock(Vector3I localPosition)
        {
            return _block;
        }

        public override void SetBlock(Chunk chunk, Vector3I localPosition, BlockType blockType)
        {
            var blocks = new byte[_dimensions.X * _dimensions.Z * _dimensions.Y];

            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i] = blockType.Id;
            }

            blocks[GetIndex(localPosition)] = blockType.Id;

            chunk.SetChunkData(new StandardChunkData(_dimensions, blocks));
        }

        public override void Optimize(Chunk chunk)
        {
            // Can't optimize further
        }
    }
}
