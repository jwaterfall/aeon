using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aeon
{
    public partial class Chunk : StaticBody3D
    {
        private MeshInstance3D _meshInstance;
        private MeshInstance3D _transparentMeshInstance;
        private CollisionShape3D _collisionShapeNode;

        public Vector2I ChunkPosition { get; private set; }
        public readonly Vector3I Dimensions = Configuration.CHUNK_DIMENSION;
        private ChunkManager _chunkManager;
        private ChunkMeshGenerator _chunkMeshGenerator;
        private ChunkDecorator _chunkDecorator;
        private ChunkData _chunkData = new StandardChunkData(Configuration.CHUNK_DIMENSION);
        private ChunkRGBLightData _chunkLightData = new(Configuration.CHUNK_DIMENSION);

        private Queue<(Vector3I, Vector3I, Vector3I)> _lightPropagationQueue = new();
        private Queue<(Vector3I, Vector3I)> _darknessPropagationQueue = new();
        private Queue<Vector3I> _lightRepairQueue = new();

        public bool IsGenerated { get; private set; } = false;
        public bool IsDecorated { get; private set; } = false;
        public bool IsRendered { get; private set; } = false;
        public bool IsDirty { get; set; } = false;

        public override void _Ready()
        {
            _chunkMeshGenerator = new ChunkMeshGenerator(this, _chunkManager);
            _chunkDecorator = new ChunkDecorator(this, _chunkManager);

            _meshInstance = new MeshInstance3D();
            AddChild(_meshInstance);

            _transparentMeshInstance = new MeshInstance3D();
            AddChild(_transparentMeshInstance);

            _collisionShapeNode = new CollisionShape3D();
            AddChild(_collisionShapeNode);
        }

        public void Generate(TerrainGenerator terrainGenerator, WorldPreset worldPreset)
        {
            for (int x = 0; x < Dimensions.X; x++)
            {
                for (int z = 0; z < Dimensions.Z; z++)
                {
                    int height = terrainGenerator.GetHeight(ChunkPosition * new Vector2I(Dimensions.X, Dimensions.Z) + new Vector2I(x, z));

                    for (int y = 0; y < Dimensions.Y; y++)
                    {
                        var globalPosition = GetWorldPosition(new Vector3I(x, y, z));
                        var blockType = terrainGenerator.GetBlockType(globalPosition, height);

                        SetBlock(new Vector3I(x, y, z), blockType);
                    }
                }
            }

            _chunkData.Optimize(this);

            IsGenerated = true;
        }

        public void Decorate(TerrainGenerator terrainGenerator, WorldPreset worldPreset)
        {
            _chunkDecorator.Decorate(terrainGenerator, worldPreset);

            _chunkData.Optimize(this);

            IsDecorated = true;
        }

        public void Render()
        {
            _chunkMeshGenerator.Generate();
            CallDeferred(nameof(SubmitMesh));
        }

        public void SubmitMesh()
        {
            _meshInstance.Mesh = _chunkMeshGenerator.Mesh;
            _transparentMeshInstance.Mesh = _chunkMeshGenerator.TransparentMesh;
            _collisionShapeNode.Shape = _chunkMeshGenerator.CollisionShape;

            _meshInstance.MaterialOverride = _chunkMeshGenerator.Material;
            _transparentMeshInstance.MaterialOverride = _chunkMeshGenerator.TransparentMaterial;

            IsRendered = true;
        }

        public Vector3I GetWorldPosition(Vector3I localPosition)
        {
            return localPosition + new Vector3I(ChunkPosition.X, 0, ChunkPosition.Y) * Dimensions;
        }

        public void SetChunkData(ChunkData chunkData)
        {
            _chunkData = chunkData;
        }

        public bool IsInChunk(Vector3I localPosition)
        {
            return localPosition.X >= 0 && localPosition.X < Dimensions.X &&
                   localPosition.Y >= 0 && localPosition.Y < Dimensions.Y &&
                   localPosition.Z >= 0 && localPosition.Z < Dimensions.Z;
        }

        private Vector3I[] GetNeighbors(Vector3I localPosition)
        {
            return new Vector3I[]
            {
                localPosition + new Vector3I(1, 0, 0),
                localPosition + new Vector3I(-1, 0, 0),
                localPosition + new Vector3I(0, 1, 0),
                localPosition + new Vector3I(0, -1, 0),
                localPosition + new Vector3I(0, 0, 1),
                localPosition + new Vector3I(0, 0, -1),
            };
        }

        public void Update()
        {
            if (IsDirty)
            {
                Render();
                IsDirty = false;
            }

            while (_darknessPropagationQueue.Count > 0)
            {
                var (localPosition, channel) = _darknessPropagationQueue.Dequeue();

                var existingChannelLightLevel = _chunkManager.GetLightLevel(GetWorldPosition(localPosition)) * channel;
                var newLightLevel = SetVectorWithChannel(localPosition, Vector3I.Zero, channel);

                _chunkManager.SetLightLevel(GetWorldPosition(localPosition), newLightLevel);

                foreach (var neighbor in GetNeighbors(localPosition))
                {
                    var neighborWorldPosition = GetWorldPosition(neighbor);
                    var neighborLightLevel = _chunkManager.GetLightLevel(neighborWorldPosition) * channel;
                    var neighborChannelLightLevel = neighborLightLevel * channel;

                    if (neighborChannelLightLevel > Vector3I.Zero && neighborChannelLightLevel < existingChannelLightLevel)
                    {
                        if (!_darknessPropagationQueue.Contains((neighbor, channel)))
                        {
                            _darknessPropagationQueue.Enqueue((neighbor, channel));
                        }
                    }
                    else if (neighborChannelLightLevel >= existingChannelLightLevel && !_lightPropagationQueue.Contains((neighbor, neighborChannelLightLevel, channel)))
                    {
                        _lightPropagationQueue.Enqueue((neighbor, neighborChannelLightLevel, channel));
                    }
                }
            }

            while (_lightPropagationQueue.Count > 0)
            {
                var (localPosition, lightLevel, channel) = _lightPropagationQueue.Dequeue();

                var newLightLevel = SetVectorWithChannel(localPosition, lightLevel, channel, true);

                _chunkManager.SetLightLevel(GetWorldPosition(localPosition), newLightLevel);

                var newChannelLightLevel = newLightLevel * channel;

                if (newChannelLightLevel <= Vector3I.Zero) continue;

                foreach (var neighbor in GetNeighbors(localPosition))
                {
                    var neighborWorldPosition = GetWorldPosition(neighbor);
                    var neighborLightLevel = _chunkManager.GetLightLevel(neighborWorldPosition) * channel;
                    var neighborBlockType = _chunkManager.GetBlock(neighborWorldPosition);

                    var newNeighborLightLevel = newChannelLightLevel - channel;

                    if (neighborBlockType.Transparent && neighborLightLevel < newNeighborLightLevel && !_lightPropagationQueue.Contains((neighbor, newNeighborLightLevel, channel)))
                    {
                        _lightPropagationQueue.Enqueue((neighbor, newNeighborLightLevel, channel));
                    }
                }
            }

            while (_lightRepairQueue.Count > 0)
            {
                var localPosition = _lightRepairQueue.Dequeue();
                var neighbors = GetNeighbors(localPosition);

                var channels = new Vector3I[]
                {
                    Vector3I.Right,
                    Vector3I.Up,
                    Vector3I.Back,
                };

                foreach (var neighbor in neighbors)
                {
                    foreach (var channel in channels)
                    {
                        var neighborWorldPosition = GetWorldPosition(neighbor);
                        var neighborChannelLightLevel = _chunkManager.GetLightLevel(neighborWorldPosition) * channel;

                        if (neighborChannelLightLevel > Vector3I.Zero)
                        {
                            _lightPropagationQueue.Enqueue((neighbor, neighborChannelLightLevel, channel));
                        }
                    }
                }
            }
        }

        private Vector3I SetVectorWithChannel(Vector3I localPosition, Vector3I value, Vector3I channel, bool keepMax = false)
        {
            var existingValue = _chunkManager.GetLightLevel(GetWorldPosition(localPosition));
            var existingChannelValue = (existingValue * channel);
            var newChannelValue = keepMax && existingChannelValue > (value * channel) ? existingChannelValue : (value * channel);
            var newValue = (existingValue - existingChannelValue) + newChannelValue;
            return newValue;
        }

        protected int GetIndex(Vector3I localPosition)
        {
            var dimensions = Dimensions;
            return (localPosition.Y * dimensions.Z * dimensions.X) + (localPosition.Z * dimensions.X) + localPosition.X;
        }

        public Vector3I GetLightLevel(Vector3I localPosition)
        {
            return _chunkLightData.Get(localPosition);
        }

        public void SetLightLevel(Vector3I localPosition, Vector3I lightLevel)
        {
            _chunkLightData.Set(localPosition, lightLevel);
            IsDirty = true;
        }

        public void Initialize(ChunkManager chunkManager, Vector2I chunkPosition)
        {
            _chunkManager = chunkManager;
            ChunkPosition = chunkPosition;

            Position = new Vector3I(chunkPosition.X, 0, chunkPosition.Y) * Dimensions;
        }

        public BlockType GetBlock(Vector3I localPosition)
        {
            return _chunkData.GetBlock(localPosition);
        }

        public void SetBlock(Vector3I localPosition, BlockType blockType, bool optimize = false, BlockType replaces = null)
        {
            if (!IsInChunk(localPosition)) return;
            if (replaces != null && _chunkData.GetBlock(localPosition) != replaces) return;

            var existingBlock = _chunkData.GetBlock(localPosition);

            if (existingBlock == blockType) return;

            _chunkData.SetBlock(this, localPosition, blockType);

            var channels = new Vector3I[]
            {
                Vector3I.Right,
                Vector3I.Up,
                Vector3I.Back,
            };

            foreach (var channel in channels)
            {
                if ((existingBlock.LightOutput * channel) > Vector3I.Zero)
                {
                    _darknessPropagationQueue.Enqueue((localPosition, channel));
                    _lightRepairQueue.Enqueue(localPosition);
                }

                if (blockType.LightOutput * channel > Vector3I.Zero)
                {
                    _lightPropagationQueue.Enqueue((localPosition, blockType.LightOutput * channel, channel));
                }
            }

            if (optimize)
            {
                _chunkData.Optimize(this);
            }
        }

        public void BreakBlock(Vector3I localPosition)
        {
            SetBlock(localPosition, BlockTypes.Instance.Get("air"), true);
        }

        public void PlaceBlock(Vector3I localPosition, BlockType blockType)
        {
            SetBlock(localPosition, blockType, true);
        }
    }
}
