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

        private Queue<Vector3I> _lightPropagationQueue = new Queue<Vector3I>();
        private Queue<Vector3I> _darknessPropagationQueue = new Queue<Vector3I>();

        private Queue<Vector3I> _sunlightPropagationQueue = new Queue<Vector3I>();
        private byte[] _sunlightData = Enumerable.Repeat((byte)0, Configuration.CHUNK_DIMENSION.X * Configuration.CHUNK_DIMENSION.Y * Configuration.CHUNK_DIMENSION.Z).ToArray();

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

        public void Update()
        {
            if (IsDirty)
            {
                RefreshShaderLightData();
                IsDirty = false;
            }

            while (_darknessPropagationQueue.Count > 0)
            {
                GD.Print("Starting darkness propagation");
                var localPosition = _darknessPropagationQueue.Dequeue();
                var lightLevel = _chunkManager.GetLightLevel(GetWorldPosition(localPosition));

                var neighbors = new[]
                {
                    localPosition + Vector3I.Up,
                    localPosition + Vector3I.Down,
                    localPosition + Vector3I.Left,
                    localPosition + Vector3I.Right,
                    localPosition + Vector3I.Forward,
                    localPosition + Vector3I.Back
                };

                foreach (var neighbor in neighbors)
                {
                    var neigbourGlobalPosition = GetWorldPosition(neighbor);
                    var neighborLightLevel = _chunkManager.GetLightLevel(neigbourGlobalPosition);

                    if (neighborLightLevel > 0 && neighborLightLevel < lightLevel)
                    {
                        _darknessPropagationQueue.Enqueue(neighbor);
                    }
                    else
                    {
                        _lightPropagationQueue.Enqueue(neighbor);
                    }

                    _chunkManager.SetLightLevel(GetWorldPosition(localPosition), 0);
                }
            }

            while (_lightPropagationQueue.Count > 0)
            {
                var localPosition = _lightPropagationQueue.Dequeue();
                var lightLevel = _chunkManager.GetLightLevel(GetWorldPosition(localPosition));

                if (lightLevel <= 1) continue;

                var neighbors = new[]
                {
                    localPosition + Vector3I.Up,
                    localPosition + Vector3I.Down,
                    localPosition + Vector3I.Left,
                    localPosition + Vector3I.Right,
                    localPosition + Vector3I.Forward,
                    localPosition + Vector3I.Back
                };
                
                var newLightLevel = (byte)(lightLevel - 1);

                foreach (var neighbor in neighbors)
                {
                    var neigbourGlobalPosition = GetWorldPosition(neighbor);
                    var neighborLightLevel = _chunkManager.GetLightLevel(neigbourGlobalPosition);
                    var neighborBlockType = _chunkManager.GetBlock(neigbourGlobalPosition);

                    if (neighborBlockType.Transparent && neighborLightLevel < newLightLevel)
                    {
                        _chunkManager.SetLightLevel(neigbourGlobalPosition, newLightLevel);
                        _lightPropagationQueue.Enqueue(neighbor);
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

            var neigbors = new[]
            {
                localPosition + Vector3I.Up,
                localPosition + Vector3I.Down,
                localPosition + Vector3I.Left,
                localPosition + Vector3I.Right,
                localPosition + Vector3I.Forward,
                localPosition + Vector3I.Back
            };

            if (existingBlock.LightOutput > 0)
            {
                foreach (var neighbor in neigbors)
                {
                    var block = _chunkManager.GetBlock(GetWorldPosition(neighbor));

                    if (block.Transparent)
                    {
                        _darknessPropagationQueue.Enqueue(neighbor);
                    }
                }
            }

            if (blockType.LightOutput > 0)
            {

                foreach (var neighbor in neigbors)
                {
                    var block = _chunkManager.GetBlock(GetWorldPosition(neighbor));

                    if (block.Transparent)
                    {
                        _chunkManager.SetLightLevel(GetWorldPosition(neighbor), blockType.LightOutput);
                        _lightPropagationQueue.Enqueue(neighbor);
                    }
                }
            }

            if (!blockType.Transparent)
            {
                var index = GetIndex(localPosition);
                _chunkLightData.Set(localPosition, 0);
                _sunlightData[index] = 0;
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
