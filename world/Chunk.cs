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
        private ChunkData _chunkData;
        private byte[] _lightData;

        public bool IsGenerated { get; private set; } = false;
        public bool IsRendered { get; private set; } = false;

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

            _chunkData = new StandardChunkData(Dimensions);
            _lightData = new byte[Dimensions.X * Dimensions.Y * Dimensions.Z];
        }

        public void GenerateBlocks(TerrainGenerator terrainGenerator, WorldPreset worldPreset)
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

            _chunkDecorator.Decorate(terrainGenerator, worldPreset);

            _chunkData.Optimize(this);
            CalculateLightLevels();

            IsGenerated = true;
        }

        public void Render()
        {
            _chunkMeshGenerator.Generate();
            CallDeferred(nameof(AfterRender));
        }

        public void AfterRender()
        {
            _meshInstance.Mesh = _chunkMeshGenerator.Mesh;
            _transparentMeshInstance.Mesh = _chunkMeshGenerator.TransparentMesh;
            _collisionShapeNode.Shape = _chunkMeshGenerator.CollisionShape;

            _chunkMeshGenerator.Material.SetShaderParameter("chunk_lighting_data", GetShaderLightData());
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

        private void CalculateLightLevels()
        {
            for (int i = 0; i < _lightData.Length; i++)
            {
                _lightData[i] = 0;
            }

            var lightsToPropagate = new Queue<Vector3I>();

            for (int x = 0; x < Dimensions.X; x++)
            {
                for (int z = 0; z < Dimensions.Z; z++)
                {
                    Vector3I localPosition = new Vector3I(x, Dimensions.Y - 1, z);
                    var blockType = _chunkData.GetBlock(localPosition);

                    if (blockType.Transparent)
                    {
                        lightsToPropagate.Enqueue(localPosition);
                        _lightData[GetIndex(localPosition)] = 15;
                    }
                }
            }

            while (lightsToPropagate.Count > 0)
            {
                var localPosition = lightsToPropagate.Dequeue();
                var lightLevel = _lightData[GetIndex(localPosition)];

                var downNeighbor = localPosition + Vector3I.Down;

                var neighbors = new[]
                {
                    downNeighbor,
                    localPosition + Vector3I.Up,
                    localPosition + Vector3I.Left,
                    localPosition + Vector3I.Right,
                    localPosition + Vector3I.Forward,
                    localPosition + Vector3I.Back
                };

                foreach (var neighbor in neighbors)
                {
                    if (!IsInChunk(neighbor)) continue;

                    var neighborLightLevel = _lightData[GetIndex(neighbor)];
                    var neighborBlockType = _chunkData.GetBlock(neighbor);
                    var newLightLevel = neighbor == downNeighbor ? lightLevel : lightLevel - 1;

                    if (neighborBlockType.Transparent && neighborLightLevel < newLightLevel)
                    {
                        _lightData[GetIndex(neighbor)] = (byte)(newLightLevel);
                        lightsToPropagate.Enqueue(neighbor);
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
            return _lightData[GetIndex(localPosition)];
        }

        private byte GetLightLevelOrNeighbors(Vector3I localPosition)
        {
            return IsInChunk(localPosition) ? GetLightLevel(localPosition) : _chunkManager.GetLightLevel(GetWorldPosition(localPosition));
        }

        private byte[] GetShaderLightData()
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
                        var lightLevel = GetLightLevelOrNeighbors(localPosition);

                        var offsetPosition = new Vector3I(x, y, z);
                        var index = (offsetPosition.Y * dimensions.Z * dimensions.X) + (offsetPosition.Z * dimensions.X) + offsetPosition.X;
                        data[index] = lightLevel;
                    }
                }
            }

            return data;
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

            _chunkData.SetBlock(this, localPosition, blockType);

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
