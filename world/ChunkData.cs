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

    abstract class ChunkLayerData
    {
        protected readonly Vector2I _dimensions;
        protected readonly int _layerIndex;

        protected ChunkLayerData(Vector2I dimensions, int layerIndex)
        {
            _dimensions = dimensions;
            _layerIndex = layerIndex;
        }

        protected int GetIndex(Vector2I localPosition)
        {
            return (localPosition.Y * _dimensions.X) + localPosition.X;
        }

        public abstract BlockType GetBlock(Vector2I localPosition);
        public abstract void SetBlock(StandardChunkData chunkData, Vector2I localPosition, BlockType blockType);
        public abstract void Optimize(StandardChunkData chunkData);
    }


    class StandardChunkLayerData : ChunkLayerData
    {
        private byte[] _blocks;

        public StandardChunkLayerData(Vector2I dimensions, byte[] blocks, int layerIndex) : base(dimensions, layerIndex)
        {
            _blocks = blocks;
        }

        public StandardChunkLayerData(Vector2I dimensions, int layerIndex) : base(dimensions, layerIndex)
        {
            _blocks = new byte[dimensions.X * dimensions.Y];
        }

        public override BlockType GetBlock(Vector2I localPosition)
        {
            return BlockTypes.Instance.Get(_blocks[GetIndex(localPosition)]);
        }

        public override void SetBlock(StandardChunkData chunkData, Vector2I localPosition, BlockType blockType)
        {
            _blocks[GetIndex(localPosition)] = blockType.Id;
        }

        public override void Optimize(StandardChunkData chunkData)
        {
            //var isSingleBlock = Array.TrueForAll(_blocks, blockId => blockId == _blocks[0]);

            //if (isSingleBlock)
            //{
            //    var block = BlockTypes.Instance.Get(_blocks[0]);
            //    chunkData.SetLayerData(_dimensions.Y, new SingleBlockChunkLayerData(_dimensions, block, _layerIndex));
            //}
        }
    }


    class SingleBlockChunkLayerData : ChunkLayerData
    {
        private BlockType _block;

        public SingleBlockChunkLayerData(Vector2I dimensions, BlockType block, int layerIndex) : base(dimensions, layerIndex)
        {
            _block = block;
        }

        public override BlockType GetBlock(Vector2I localPosition)
        {
            return _block;
        }

        public override void SetBlock(StandardChunkData chunkData, Vector2I localPosition, BlockType blockType)
        {
            var blocks = new byte[_dimensions.X * _dimensions.Y];

            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i] = blockType.Id;
            }

            blocks[GetIndex(localPosition)] = blockType.Id;

            chunkData.SetLayerData(_layerIndex, new StandardChunkLayerData(_dimensions, blocks, _layerIndex));
        }

        public override void Optimize(StandardChunkData chunkData)
        {
            // Can't optimize further
        }
    }


    class StandardChunkData : ChunkData
    {
        private ChunkLayerData[] _layers;

        public StandardChunkData(Vector3I dimensions, ChunkLayerData[] layers) : base(dimensions)
        {
            _layers = layers;
        }

        public StandardChunkData(Vector3I dimensions) : base(dimensions)
        {
            _layers = new ChunkLayerData[dimensions.Y];

            for (int y = 0; y < dimensions.Y; y++)
            {
                _layers[y] = new StandardChunkLayerData(new Vector2I(dimensions.X, dimensions.Z), y);
            }
        }

        public override BlockType GetBlock(Vector3I localPosition)
        {
            return _layers[localPosition.Y].GetBlock(new Vector2I(localPosition.X, localPosition.Z));
        }

        public override void SetBlock(Chunk chunk, Vector3I localPosition, BlockType blockType)
        {
            _layers[localPosition.Y].SetBlock(this, new Vector2I(localPosition.X, localPosition.Z), blockType);
        }

        public void SetLayerData(int layerIndex, ChunkLayerData layerData)
        {
            _layers[layerIndex] = layerData;
        }

        public override void Optimize(Chunk chunk)
        {
            var areAllSingleBlockLayers = Array.TrueForAll(_layers, layer => layer is SingleBlockChunkLayerData);
            var firstLayerBlock = (_layers[0]).GetBlock(new Vector2I(0, 0));
            var isSingleBlock = areAllSingleBlockLayers &&
                Array.TrueForAll(_layers, layer => ((SingleBlockChunkLayerData)layer).GetBlock(new Vector2I(0, 0)) == firstLayerBlock);

            if (isSingleBlock)
            {
                chunk.SetChunkData(new SingleBlockChunkData(_dimensions, firstLayerBlock));
            }
            else
            {
                foreach (var layer in _layers)
                {
                    layer.Optimize(this);
                }
            }
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
            var chunkData = new StandardChunkData(_dimensions);
            var layers = new ChunkLayerData[_dimensions.Y];

            for (int y = 0; y < _dimensions.Y; y++)
            {
                layers[y] = new SingleBlockChunkLayerData(new Vector2I(_dimensions.X, _dimensions.Z), blockType, y);
            }

            var modifiedLayerBlocks = new byte[_dimensions.X * _dimensions.Z];

            for (int i = 0; i < modifiedLayerBlocks.Length; i++)
            {
                modifiedLayerBlocks[i] = blockType.Id;
            }

            modifiedLayerBlocks[(localPosition.Z * _dimensions.X) + localPosition.X] = blockType.Id;
            layers[localPosition.Y] = new StandardChunkLayerData(new Vector2I(_dimensions.X, _dimensions.Z), modifiedLayerBlocks, localPosition.Y);

            chunkData.SetLayerData(localPosition.Y, layers[localPosition.Y]);
        }

        public override void Optimize(Chunk chunk)
        {
            // Can't optimize further
        }
    }
}
