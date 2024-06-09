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
        private ChunkData _chunkData;
        private byte[] _lightData;

        public bool IsGenerated { get; private set; } = false;
        public bool IsRendered { get; private set; } = false;

        public override void _Ready()
        {
            _chunkMeshGenerator = new ChunkMeshGenerator(this, _chunkManager);

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

            GenerateOres(worldPreset, terrainGenerator);
            GenerateGrass(terrainGenerator);
            GenerateTrees(terrainGenerator);

            _chunkData.Optimize(this);
            CalculateLightLevels();

            IsGenerated = true;
        }

        private void GenerateOres(WorldPreset worldPreset, TerrainGenerator terrainGenerator)
        {
            var random = new Random();
            foreach (var ore in worldPreset.Ores)
            {
                for (int i = 0; i < ore.Frequency; i++)
                {
                    Vector3I localStartPosition = new Vector3I(
                        random.Next(0, Dimensions.X),
                        random.Next(0, Dimensions.Y),
                        random.Next(0, Dimensions.Z)
                    );

                    Vector3I globalStartPosition = GetWorldPosition(localStartPosition);
                    if (globalStartPosition.Y < ore.MinHeight || globalStartPosition.Y > ore.MaxHeight) continue;

                    var veinPositions = new List<Vector3I>();
                    for (int j = 0; j < ore.Size; j++)
                    {
                        Vector3I localPosition = veinPositions.Count == 0 ? localStartPosition : veinPositions[j - 1] + new Vector3I(random.Next(-1, 2), random.Next(-1, 2), random.Next(-1, 2));
                        veinPositions.Add(localPosition);

                        if (IsInChunk(localPosition))
                        {
                            SetBlock(localPosition, BlockTypes.Instance.Get(ore.Block), false, BlockTypes.Instance.Get("stone"));
                        }
                    }
                }
            }
        }

        private void GenerateGrass(TerrainGenerator terrainGenerator)
        {
            var random = new Random();
            for (int x = 0; x < Dimensions.X; x++)
            {
                for (int z = 0; z < Dimensions.Z; z++)
                {
                    int height = terrainGenerator.GetHeight(ChunkPosition * new Vector2I(Dimensions.X, Dimensions.Z) + new Vector2I(x, z));
                    if (height < 0 || height > Dimensions.Y - 2) continue;

                    Vector3I blockBelowPosition = new Vector3I(x, height, z);
                    var blockBelow = IsInChunk(blockBelowPosition) ? GetBlock(blockBelowPosition) : _chunkManager.GetBlock(GetWorldPosition(blockBelowPosition));

                    if (blockBelow != null && (blockBelow.Name == "grass" || blockBelow.Name == "snow") && random.NextDouble() <= 0.2f)
                    {
                        SetBlock(new Vector3I(x, height + 1, z), BlockTypes.Instance.Get("short_grass"));
                    }
                }
            }
        }

        private void GenerateTrees(TerrainGenerator terrainGenerator)
        {
            var random = new Random();
            for (int x = 0; x < Dimensions.X; x++)
            {
                for (int z = 0; z < Dimensions.Z; z++)
                {
                    if (random.NextDouble() > 0.02f) continue;

                    int height = terrainGenerator.GetHeight(ChunkPosition * new Vector2I(Dimensions.X, Dimensions.Z) + new Vector2I(x, z));
                    if (height < 0 || height > Dimensions.Y - 2) continue;

                    var blockBelow = _chunkData.GetBlock(new Vector3I(x, height, z));
                    if (blockBelow.Name != "grass" && blockBelow.Name != "snow") continue;

                    int trunkHeight = random.Next(5, 10);
                    SetBlock(new Vector3I(x, height, z), BlockTypes.Instance.Get("dirt"));
                    GenerateTreeTrunk(height, trunkHeight, x, z);
                    GenerateTreeLeaves(height, trunkHeight, x, z);
                }
            }
        }

        private void GenerateTreeTrunk(int localHeight, int trunkHeight, int x, int z)
        {
            for (int y = 1; y <= trunkHeight; y++)
            {
                SetBlock(new Vector3I(x, localHeight + y, z), BlockTypes.Instance.Get("spruce_log"));
            }
        }

        private void GenerateTreeLeaves(int localHeight, int trunkHeight, int x, int z)
        {
            var random = new Random();
            int brimHeight = random.Next(1, 3);
            int brimWidth = 2;

            for (int y = 1; y <= brimHeight; y++)
            {
                for (int xOffset = -brimWidth; xOffset <= brimWidth; xOffset++)
                {
                    for (int zOffset = -brimWidth; zOffset <= brimWidth; zOffset++)
                    {
                        SetBlock(new Vector3I(x + xOffset, localHeight + trunkHeight + y, z + zOffset), BlockTypes.Instance.Get("spruce_leaves"));
                    }
                }
            }

            int topHeight = random.Next(1, 2);
            int topWidth = 1;

            for (int y = 1; y <= topHeight; y++)
            {
                for (int xOffset = -topWidth; xOffset <= topWidth; xOffset++)
                {
                    for (int zOffset = -topWidth; zOffset <= topWidth; zOffset++)
                    {
                        SetBlock(new Vector3I(x + xOffset, localHeight + trunkHeight + brimHeight + y, z + zOffset), BlockTypes.Instance.Get("spruce_leaves"));
                    }
                }
            }
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

        private void SetBlock(Vector3I localPosition, BlockType blockType, bool optimize = false, BlockType replaces = null)
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
