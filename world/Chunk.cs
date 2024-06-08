using Godot;
using System;
using System.Collections.Generic;

namespace Aeon
{
    public partial class Chunk : StaticBody3D
    {
        private SurfaceTool _surfaceTool;
        private MeshInstance3D _meshInstance;
        private ArrayMesh _generatedMesh;

        private SurfaceTool _transparentSurfaceTool;
        private MeshInstance3D _transparentMeshInstance;
        private ArrayMesh _generatedTransparentMesh;

        private SurfaceTool _collisionSurfaceTool;
        private Shape3D _collisionShape;
        private CollisionShape3D _collisionShapeNode;

        public Vector3I ChunkPosition { get; private set; }
        private ChunkManager _chunkManager;
        private ChunkData _chunkData;

        public bool IsGenerated { get; private set; } = false;
        public bool IsRendered { get; private set; } = false;

        private static readonly Dictionary<Direction, Vector3I> FaceDirections = new()
        {
            { Direction.Up, Vector3I.Up },
            { Direction.Down, Vector3I.Down },
            { Direction.North, Vector3I.Left },
            { Direction.South, Vector3I.Right },
            { Direction.West, Vector3I.Forward },
            { Direction.East, Vector3I.Back }
        };

        private static readonly Dictionary<Direction, Direction> InverseDirections = new()
        {
            { Direction.Up, Direction.Down },
            { Direction.Down, Direction.Up },
            { Direction.North, Direction.South },
            { Direction.South, Direction.North },
            { Direction.West, Direction.East },
            { Direction.East, Direction.West }
        };

        public override void _Ready()
        {
            _meshInstance = new MeshInstance3D();
            AddChild(_meshInstance);

            _transparentMeshInstance = new MeshInstance3D();
            AddChild(_transparentMeshInstance);

            _collisionShapeNode = new CollisionShape3D();
            AddChild(_collisionShapeNode);

            _chunkData = new StandardChunkData(Configuration.CHUNK_DIMENSION);
        }

        public void GenerateBlocks(TerrainGenerator terrainGenerator, WorldPreset worldPreset)
        {
            for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
            {
                for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                {
                    int height = terrainGenerator.GetHeight(new Vector2I(ChunkPosition.X * Configuration.CHUNK_DIMENSION.X + x, ChunkPosition.Z * Configuration.CHUNK_DIMENSION.Z + z));

                    for (int y = 0; y < Configuration.CHUNK_DIMENSION.Y; y++)
                    {
                        Vector3I globalPosition = new Vector3I(ChunkPosition.X, ChunkPosition.Y, ChunkPosition.Z) * Configuration.CHUNK_DIMENSION + new Vector3I(x, y, z);
                        BlockType blockType = terrainGenerator.GetBlockType(globalPosition, height);

                        SetBlock(new Vector3I(x, y, z), blockType);
                    }
                }
            }

            GenerateOres(worldPreset, terrainGenerator);
            GenerateGrass(terrainGenerator);
            GenerateTrees(terrainGenerator);

            _chunkData.Optimize(this);
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
                        random.Next(0, Configuration.CHUNK_DIMENSION.X),
                        random.Next(0, Configuration.CHUNK_DIMENSION.Y),
                        random.Next(0, Configuration.CHUNK_DIMENSION.Z)
                    );

                    Vector3I globalStartPosition = GetGlobalPosition(localStartPosition);
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
            for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
            {
                for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                {
                    int height = terrainGenerator.GetHeight(new Vector2I(ChunkPosition.X * Configuration.CHUNK_DIMENSION.X + x, ChunkPosition.Z * Configuration.CHUNK_DIMENSION.Z + z));
                    int localHeight = height - ChunkPosition.Y * Configuration.CHUNK_DIMENSION.Y;
                    if (localHeight < -1 || localHeight > Configuration.CHUNK_DIMENSION.Y - 2) continue;

                    Vector3I blockBelowPosition = new Vector3I(x, localHeight, z);
                    var blockBelow = IsInChunk(blockBelowPosition) ? GetBlock(blockBelowPosition) : _chunkManager.GetBlock(GetGlobalPosition(blockBelowPosition));

                    if (blockBelow != null && (blockBelow.Name == "grass" || blockBelow.Name == "snow") && random.NextDouble() <= 0.2f)
                    {
                        SetBlock(new Vector3I(x, localHeight + 1, z), BlockTypes.Instance.Get("short_grass"));
                    }
                }
            }
        }

        private void GenerateTrees(TerrainGenerator terrainGenerator)
        {
            var random = new Random();
            for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
            {
                for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                {
                    if (random.NextDouble() > 0.02f) continue;

                    int height = terrainGenerator.GetHeight(new Vector2I(ChunkPosition.X * Configuration.CHUNK_DIMENSION.X + x, ChunkPosition.Z * Configuration.CHUNK_DIMENSION.Z + z));
                    int localHeight = height - ChunkPosition.Y * Configuration.CHUNK_DIMENSION.Y;
                    if (localHeight < 0 || localHeight >= Configuration.CHUNK_DIMENSION.Y) continue;

                    var blockBelow = _chunkData.GetBlock(new Vector3I(x, localHeight, z));
                    if (blockBelow.Name != "grass" && blockBelow.Name != "snow") continue;

                    int trunkHeight = random.Next(5, 10);
                    SetBlock(new Vector3I(x, localHeight, z), BlockTypes.Instance.Get("dirt"));
                    GenerateTreeTrunk(localHeight, trunkHeight, x, z);
                    GenerateTreeLeaves(localHeight, trunkHeight, x, z);
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

        private Vector3I GetGlobalPosition(Vector3I localPosition)
        {
            return new Vector3I(ChunkPosition.X, ChunkPosition.Y, ChunkPosition.Z) * Configuration.CHUNK_DIMENSION + localPosition;
        }

        public void SetChunkData(ChunkData chunkData)
        {
            _chunkData = chunkData;
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

        private bool IsInChunk(Vector3I localPosition)
        {
            return localPosition.X >= 0 && localPosition.X < Configuration.CHUNK_DIMENSION.X &&
                   localPosition.Y >= 0 && localPosition.Y < Configuration.CHUNK_DIMENSION.Y &&
                   localPosition.Z >= 0 && localPosition.Z < Configuration.CHUNK_DIMENSION.Z;
        }

        public BlockType GetBlock(Vector3I localPosition)
        {
            return _chunkData.GetBlock(localPosition);
        }

        public void Render()
        {
            InitializeSurfaceTools();

            for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
            {
                for (int y = 0; y < Configuration.CHUNK_DIMENSION.Y; y++)
                {
                    for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                    {
                        RenderBlock(new Vector3I(x, y, z));
                    }
                }
            }

            CommitSurfaceTools();
            CallDeferred(nameof(AfterRender));
        }

        private void InitializeSurfaceTools()
        {
            _surfaceTool = new SurfaceTool();
            _surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
            _surfaceTool.SetSmoothGroup(uint.MaxValue);

            _transparentSurfaceTool = new SurfaceTool();
            _transparentSurfaceTool.Begin(Mesh.PrimitiveType.Triangles);
            _transparentSurfaceTool.SetSmoothGroup(uint.MaxValue);

            _collisionSurfaceTool = new SurfaceTool();
            _collisionSurfaceTool.Begin(Mesh.PrimitiveType.Triangles);
            _collisionSurfaceTool.SetSmoothGroup(uint.MaxValue);
        }

        private void CommitSurfaceTools()
        {
            _surfaceTool.GenerateNormals(false);
            _generatedMesh = _surfaceTool.Commit();

            _transparentSurfaceTool.GenerateNormals(false);
            _generatedTransparentMesh = _transparentSurfaceTool.Commit();

            _collisionSurfaceTool.GenerateNormals(false);
            _collisionShape = _collisionSurfaceTool.Commit().CreateTrimeshShape();

            _surfaceTool.Dispose();
            _transparentSurfaceTool.Dispose();
            _collisionSurfaceTool.Dispose();

            _surfaceTool = null;
            _transparentSurfaceTool = null;
            _collisionSurfaceTool = null;
        }

        public void AfterRender()
        {
            _meshInstance.Mesh = _generatedMesh;
            _transparentMeshInstance.Mesh = _generatedTransparentMesh;
            _collisionShapeNode.Shape = _collisionShape;

            _meshInstance.MaterialOverride = BlockTextures.Instance.material;
            _transparentMeshInstance.MaterialOverride = BlockTextures.Instance.transparentMaterial;

            IsRendered = true;
        }

        private Vector3I GetWorldPosition(Vector3I localPosition)
        {
            return localPosition + ChunkPosition * Configuration.CHUNK_DIMENSION;
        }

        private bool ShouldRender(Vector3I localPosition, Direction faceToCheck, BlockType sourceBlockType)
        {
            var blockType = !IsInChunk(localPosition)
                ? _chunkManager.GetBlock(GetWorldPosition(localPosition))
                : GetBlock(localPosition);

            return blockType.Occludes.Contains(faceToCheck) == false || (blockType.Transparent && (blockType != sourceBlockType || !blockType.CullsSelf));
        }

        private void RenderBlock(Vector3I localPosition)
        {
            var blockType = _chunkData.GetBlock(localPosition);

            if (blockType.Name == "air") return;

            foreach (var face in blockType.Faces)
            {
                if (face.OccludedBy.HasValue)
                {
                    var directionOfBlockToCheck = FaceDirections[face.OccludedBy.Value];
                    var faceToCheck = InverseDirections[face.OccludedBy.Value];

                    if (!ShouldRender(localPosition + directionOfBlockToCheck, faceToCheck, blockType))
                    {
                        continue;
                    }
                }

                RenderFace(face, localPosition, blockType);
            }
        }

        private void RenderFace(Face face, Vector3I localPosition, BlockType blockType)
        {
            var uvOffset = face.TextureAtlasOffset / BlockTextures.Instance.size;
            var height = 1.0f / BlockTextures.Instance.size.Y;
            var width = 1.0f / BlockTextures.Instance.size.X;

            var uva = uvOffset + new Vector2(face.UV[0] * width, face.UV[1] * height);
            var uvb = uvOffset + new Vector2(face.UV[0] * width, face.UV[3] * height);
            var uvc = uvOffset + new Vector2(face.UV[2] * width, face.UV[3] * height);

            var st = blockType.Transparent ? _transparentSurfaceTool : _surfaceTool;

            var a = face.Vertices[0] + localPosition;
            var b = face.Vertices[1] + localPosition;
            var c = face.Vertices[2] + localPosition;

            st.AddTriangleFan(new[] { a, b, c }, new[] { uva, uvb, uvc });

            if (blockType.HasCollision)
            {
                _collisionSurfaceTool.AddTriangleFan(new[] { a, b, c }, new[] { uva, uvb, uvc });
            }

            if (face.Vertices.Count == 4)
            {
                var uvd = uvOffset + new Vector2(face.UV[2] * width, face.UV[1] * height);
                var d = face.Vertices[3] + localPosition;

                st.AddTriangleFan(new[] { a, c, d }, new[] { uva, uvc, uvd });

                if (blockType.HasCollision)
                {
                    _collisionSurfaceTool.AddTriangleFan(new[] { a, c, d }, new[] { uva, uvc, uvd });
                }
            }
        }

        public void Initialize(ChunkManager chunkManager, Vector3I chunkPosition)
        {
            _chunkManager = chunkManager;
            ChunkPosition = chunkPosition;

            Position = chunkPosition * Configuration.CHUNK_DIMENSION;
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
