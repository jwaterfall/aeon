using Godot;

namespace Aeon.World
{
    public partial class Chunk : StaticBody3D
    {
        private MeshInstance3D _meshInstance;
        private MeshInstance3D _transparentMeshInstance;
        private CollisionShape3D _collisionShapeNode;

        public Vector3I ChunkPosition { get; private set; }
        public readonly Vector3I Dimensions = Configuration.CHUNK_DIMENSION;
        private World _world;
        private ChunkMeshGenerator _chunkMeshGenerator;
        private ChunkLightManager _lightManager;
        private ChunkDecorator _chunkDecorator;
        private ChunkData _chunkData = new StandardChunkData(Configuration.CHUNK_DIMENSION);

        public bool IsGenerated { get; private set; } = false;
        public bool NeedsToBeRendered { get; set; } = true;

        public override void _Ready()
        {
            _chunkMeshGenerator = new ChunkMeshGenerator(this, _world);
            _lightManager = new ChunkLightManager(this, _world);
            _chunkDecorator = new ChunkDecorator(this, _world);

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
                    int height = terrainGenerator.GetHeight(new Vector2I(ChunkPosition.X, ChunkPosition.Z) * new Vector2I(Dimensions.X, Dimensions.Z) + new Vector2I(x, z));

                    for (int y = 0; y < Dimensions.Y; y++)
                    {
                        var globalPosition = GetWorldPosition(new Vector3I(x, y, z));
                        var blockType = terrainGenerator.GetBlockType(globalPosition, height);

                        SetBlock(new Vector3I(x, y, z), blockType);
                    }
                }
            }

            //_chunkDecorator.Decorate(terrainGenerator, worldPreset);

            //_lightManager.PropagateNeighborLight();
            //_lightManager.PropagateSkyLight();

            _chunkData.Optimize(this);

            IsGenerated = true;
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

            NeedsToBeRendered = false;
        }

        public Vector3I GetWorldPosition(Vector3I localPosition)
        {
            return localPosition + ChunkPosition * Dimensions;
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

        protected int GetIndex(Vector3I localPosition)
        {
            var dimensions = Dimensions;
            return (localPosition.Y * dimensions.Z * dimensions.X) + (localPosition.Z * dimensions.X) + localPosition.X;
        }

        public Vector3I GetLightLevel(Vector3I localPosition)
        {
            return _lightManager.GetLightLevel(localPosition);
        }

        public void SetLightLevel(Vector3I localPosition, Vector3I lightLevel)
        {
            _lightManager.SetLightLevel(localPosition, lightLevel);
            NeedsToBeRendered = true;
        }

        public byte GetSkyLightLevel(Vector3I localPosition)
        {
            return _lightManager.GetSkyLightLevel(localPosition);
        }

        public void SetSkyLightLevel(Vector3I localPosition, byte lightLevel)
        {
            _lightManager.SetSkyLightLevel(localPosition, lightLevel);
            NeedsToBeRendered = true;
        }

        public void Initialize(World world, Vector3I chunkPosition)
        {
            _world = world;
            ChunkPosition = chunkPosition;

            Position = new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z) * Dimensions;
        }

        public Block GetBlock(Vector3I localPosition)
        {
            return _chunkData.GetBlock(localPosition);
        }

        public void SetBlock(Vector3I localPosition, Block blockType, bool optimize = false, Block replaces = null)
        {
            if (!IsInChunk(localPosition)) return;
            if (replaces != null && _chunkData.GetBlock(localPosition) != replaces) return;

            var existingBlock = _chunkData.GetBlock(localPosition);

            if (existingBlock == blockType) return;

            _chunkData.SetBlock(this, localPosition, blockType);

            if (existingBlock.LightOutput != Vector3I.Zero)
            {
                _lightManager.RemoveLightSource(localPosition, existingBlock.LightOutput);
            }

            if (blockType.LightOutput != Vector3I.Zero)
            {
                _lightManager.AddLightSource(localPosition, blockType.LightOutput);
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

        public void PlaceBlock(Vector3I localPosition, Block blockType)
        {
            SetBlock(localPosition, blockType, true);
        }
    }
}
