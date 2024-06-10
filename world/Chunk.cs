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
        private ChunkLightData _chunkLightData = new(Configuration.CHUNK_DIMENSION);

        private Queue<(Vector3I, byte)> _lightPropagationQueue = new();
        private Queue<Vector3I> _darknessPropagationQueue = new();

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

        public void RefreshShaderLightData()
        {
            var dimensions = Dimensions + new Vector3I(2, 0, 2);

            var data = new byte[dimensions.X * dimensions.Y * dimensions.Z];

            for (int x = 0; x < dimensions.X; x++)
            {
                for (int y = 0; y < dimensions.Y; y++)
                {
                    for (int z = 0; z < dimensions.Z; z++)
                    {
                        var localPosition = new Vector3I(x - 1, y, z - 1);
                        var lightLevel = _chunkManager.GetLightLevel(GetWorldPosition(localPosition));

                        var offsetPosition = new Vector3I(x, y, z);
                        var index = (offsetPosition.Y * dimensions.Z * dimensions.X) + (offsetPosition.Z * dimensions.X) + offsetPosition.X;
                        data[index] = lightLevel;
                    }
                }
            }

            _chunkMeshGenerator.Material.SetShaderParameter("chunk_lighting_data", data);
        }

        public void SubmitMesh()
        {
            _meshInstance.Mesh = _chunkMeshGenerator.Mesh;
            _transparentMeshInstance.Mesh = _chunkMeshGenerator.TransparentMesh;
            _collisionShapeNode.Shape = _chunkMeshGenerator.CollisionShape;

            _meshInstance.MaterialOverride = _chunkMeshGenerator.Material;
            _transparentMeshInstance.MaterialOverride = _chunkMeshGenerator.TransparentMaterial;

            RefreshShaderLightData();

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
                RefreshShaderLightData();
                IsDirty = false;
            }

            while (_darknessPropagationQueue.Count > 0)
            {
                var localPosition = _darknessPropagationQueue.Dequeue();
                var worldPosition = GetWorldPosition(localPosition);
                var lightLevel = _chunkManager.GetLightLevel(worldPosition);

                _chunkManager.SetLightLevel(worldPosition, 0);

                foreach (var neighbor in GetNeighbors(localPosition))
                {
                    var neighborWorldPosition = GetWorldPosition(neighbor);
                    var neighborLightLevel = _chunkManager.GetLightLevel(neighborWorldPosition);

                    if (neighborLightLevel > 0 && neighborLightLevel < lightLevel)
                    {
                        if (!_darknessPropagationQueue.Contains(neighbor))
                        {
                            _darknessPropagationQueue.Enqueue(neighbor);
                        }
                    }
                    else
                    {
                        if (neighborLightLevel >= 1 && !_lightPropagationQueue.Contains((neighbor, neighborLightLevel)))
                        {
                            _lightPropagationQueue.Enqueue((neighbor, neighborLightLevel));
                        }
                    }
                }
            }

            while (_lightPropagationQueue.Count > 0)
            {
                var (localPosition, lightLevel) = _lightPropagationQueue.Dequeue();
                _chunkManager.SetLightLevel(GetWorldPosition(localPosition), lightLevel);
                
                var newLightLevel = (byte)(lightLevel - 1);
                if (newLightLevel <= 0) continue;

                foreach (var neighbor in GetNeighbors(localPosition))
                {
                    var neighborWorldPosition = GetWorldPosition(neighbor);
                    var neighborLightLevel = _chunkManager.GetLightLevel(neighborWorldPosition);
                    var neighborBlockType = _chunkManager.GetBlock(neighborWorldPosition);

                    if (neighborBlockType.Transparent && neighborLightLevel < newLightLevel)
                    {
                        _chunkManager.SetLightLevel(neighborWorldPosition, newLightLevel);
                        _lightPropagationQueue.Enqueue((neighbor, newLightLevel));
                    }
                }
            }
        }

        protected int GetIndex(Vector3I localPosition)
        {
            var dimensions = Dimensions;
            return (localPosition.Y * dimensions.Z * dimensions.X) + (localPosition.Z * dimensions.X) + localPosition.X;
        }

        public byte GetLightLevel(Vector3I localPosition)
        {
            return _chunkLightData.Get(localPosition);
        }

        public void SetLightLevel(Vector3I localPosition, byte lightLevel)
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

            if (existingBlock.LightOutput > 0)
            {
                _darknessPropagationQueue.Enqueue(localPosition);
            }

            if (blockType.LightOutput > 0)
            {
                _lightPropagationQueue.Enqueue((localPosition, blockType.LightOutput));
            }

            if (!blockType.Transparent)
            {
                _chunkLightData.Set(localPosition, 0);
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
