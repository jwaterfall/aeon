using Godot;
using System;
using System.Collections.Generic;

namespace Aeon
{
    public partial class Chunk : StaticBody3D
    {
        private SurfaceTool surfaceTool = new SurfaceTool();
        private Mesh mesh;
        private MeshInstance3D meshInstance;

        private SurfaceTool transparentSurfaceTool = new SurfaceTool();
        private Mesh transparentMesh;
        private MeshInstance3D transparentMeshInstance;

        private SurfaceTool collisionSurfaceTool = new SurfaceTool();
        private Shape3D collisionShape;
        private CollisionShape3D collisionShapeNode;

        public Vector3I chunkPosition;
        private ChunkManager chunkManager;
        private ChunkData _chunkData;

        public bool generated = false;
        public bool rendered = false;

        private Dictionary<Direction, Vector3I> faceDirections = new()
        {
            { Direction.Up, Vector3I.Up },
            { Direction.Down, Vector3I.Down },
            { Direction.North, Vector3I.Left },
            { Direction.South, Vector3I.Right },
            { Direction.West, Vector3I.Forward },
            { Direction.East, Vector3I.Back }
        };

        private Dictionary<Direction, Direction> inverseDirections = new()
        {
            { Direction.Up, Direction.Down },
            { Direction.Down, Direction.Up },
            { Direction.North, Direction.South },
            { Direction.South, Direction.North },
            { Direction.West, Direction.East },
            { Direction.East, Direction.West },
        };

        public override void _Ready()
        {
            meshInstance = new();
            AddChild(meshInstance);

            transparentMeshInstance = new();
            AddChild(transparentMeshInstance);

            collisionShapeNode = new();
            AddChild(collisionShapeNode);

            _chunkData = new StandardChunkData(Configuration.CHUNK_DIMENSION);
        }

        public void GenerateBlocks(TerrainGenerator terrainGenerator, WorldPreset worldPreset)
        {
            for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
            {
                for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                {
                    var height = terrainGenerator.GetHeight(new Vector2I(chunkPosition.X * Configuration.CHUNK_DIMENSION.X + x, chunkPosition.Z * Configuration.CHUNK_DIMENSION.Z + z));

                    for (int y = 0; y < Configuration.CHUNK_DIMENSION.Y; y++)
                    {
                        var globalPosition = new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z) * Configuration.CHUNK_DIMENSION + new Vector3I(x, y, z);
                        var blockType = terrainGenerator.GetBlockType(globalPosition, height);

                        SetBlock(new Vector3I(x, y, z), blockType);
                    }
                }
            }

            var random = new Random();

            foreach (var ore in worldPreset.Ores)
            {
                // Respect max height and add size
                // Block, Size, Frequency, MinHeight, MaxHeight
                for (int i = 0; i < ore.Frequency; i++)
                {
                    var localStartPosition = new Vector3I(
                        random.Next(0, Configuration.CHUNK_DIMENSION.X),
                        random.Next(0, Configuration.CHUNK_DIMENSION.Y),
                        random.Next(0, Configuration.CHUNK_DIMENSION.Z)
                    );

                    var globalStartPosition = GetGlobalPosition(localStartPosition);

                    if (globalStartPosition.Y < ore.MinHeight || globalStartPosition.Y > ore.MaxHeight) continue;

                    var veinPositions = new List<Vector3I>();

                    for (int j = 0; j < ore.Size; j++)
                    {
                        // Calculate new offsets relative to the previous position
                        var xOffset = random.Next(-1, 2); // -1, 0, or 1
                        var yOffset = random.Next(-1, 2);
                        var zOffset = random.Next(-1, 2);

                        var previousPosition = (j > 0) ? veinPositions[j - 1] : localStartPosition;

                        var localPosition = new Vector3I(
                            previousPosition.X + xOffset,
                            previousPosition.Y + yOffset,
                            previousPosition.Z + zOffset
                        );

                        veinPositions.Add(localPosition);

                        if (!IsInChunk(localPosition)) continue;

                        SetBlock(localPosition, BlockTypes.Instance.Get(ore.Block), false, BlockTypes.Instance.Get("stone"));
                    }
                }
            }

            // Add grass
            for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
            {
                for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                {
                    var height = terrainGenerator.GetHeight(new Vector2I(chunkPosition.X * Configuration.CHUNK_DIMENSION.X + x, chunkPosition.Z * Configuration.CHUNK_DIMENSION.Z + z));

                    var localHeight = height - chunkPosition.Y * Configuration.CHUNK_DIMENSION.Y;
                    if (localHeight < -1 || localHeight > Configuration.CHUNK_DIMENSION.Y - 2) continue;

                    var blockBelowPosition = new Vector3I(x, localHeight, z);
                    var blockBelow = IsInChunk(blockBelowPosition) ? GetBlock(blockBelowPosition) : chunkManager.GetBlock(GetGlobalPosition(blockBelowPosition));

                    if (blockBelow == null)
                    {
                        // Todo, add grass and other things in second pass after neighbors have been generated
                        continue;
                    }

                    if (blockBelow.Name != "grass" && blockBelow.Name != "snow") continue;

                    if (random.NextDouble() > 0.2f) continue;
                    SetBlock(new Vector3I(x, localHeight + 1, z), BlockTypes.Instance.Get("short_grass"));
                }
            }

            //Add trees
            for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
            {
                for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                {
                    var randomNumber = random.NextDouble();
                    if (randomNumber > 0.02f) continue;

                    var height = terrainGenerator.GetHeight(new Vector2I(chunkPosition.X * Configuration.CHUNK_DIMENSION.X + x, chunkPosition.Z * Configuration.CHUNK_DIMENSION.Z + z));

                    var localHeight = height - chunkPosition.Y * Configuration.CHUNK_DIMENSION.Y;
                    if (localHeight < 0 || localHeight >= Configuration.CHUNK_DIMENSION.Y) continue;

                    var blockBelow = _chunkData.GetBlock(new Vector3I(x, localHeight, z));
                    if (blockBelow.Name != "grass" && blockBelow.Name != "snow") continue;

                    var trunkHeight = random.Next(5, 10);

                    // Kill the grass
                    SetBlock(new Vector3I(x, localHeight, z), BlockTypes.Instance.Get("dirt"));

                    // Trunk
                    for (int y = 1; y <= trunkHeight; y++)
                    {
                        var localPosition = new Vector3I(x, localHeight + y, z);
                        SetBlock(localPosition, BlockTypes.Instance.Get("spruce_log"));
                    }

                    // Brim
                    var brimHeight = random.Next(1, 3);
                    int brimWidth = 2;
                    for (int y = 1; y <= brimHeight; y++)
                    {
                        for (int xOffset = -brimWidth; xOffset <= brimWidth; xOffset++)
                        {
                            for (int zOffset = -brimWidth; zOffset <= brimWidth; zOffset++)
                            {
                                var localPosition = new Vector3I(x + xOffset, localHeight + trunkHeight + y, z + zOffset);
                                SetBlock(localPosition, BlockTypes.Instance.Get("spruce_leaves"));
                            }
                        }
                    }

                    // Top
                    var topHeight = random.Next(1, 2);
                    int topWidth = 1;
                    for (int y = 1; y <= topHeight; y++)
                    {
                        for (int xOffset = -topWidth; xOffset <= topWidth; xOffset++)
                        {
                            for (int zOffset = -topWidth; zOffset <= topWidth; zOffset++)
                            {
                                var localPosition = new Vector3I(x + xOffset, localHeight + trunkHeight + brimHeight + y, z + zOffset);
                                SetBlock(localPosition, BlockTypes.Instance.Get("spruce_leaves"));
                            }
                        }
                    }
                }
            }

            _chunkData.Optimize(this);

            generated = true;
        }

        private Vector3I GetGlobalPosition(Vector3I localPosition)
        {
            return new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z) * Configuration.CHUNK_DIMENSION + localPosition;
        }

        public void SetChunkData(ChunkData chunkData)
        {
            _chunkData = chunkData;
        }

        private void SetBlock(Vector3I localPosition, BlockType blockType, bool optimize = false, BlockType replaces = null)
        {
            if (!IsInChunk(localPosition))
            {
                //throw new ArgumentOutOfRangeException("localPosition");
                return;
            }

            if (replaces != null && _chunkData.GetBlock(localPosition) != replaces)
            {
                return;
            }

            _chunkData.SetBlock(this, localPosition, blockType);

            if (optimize)
            {
                _chunkData.Optimize(this);
            }
        }

        private bool IsInChunk(Vector3I localPosition)
        {
            return
                localPosition.X >= 0 &&
                localPosition.X < Configuration.CHUNK_DIMENSION.X &&
                localPosition.Y >= 0 &&
                localPosition.Y < Configuration.CHUNK_DIMENSION.Y &&
                localPosition.Z >= 0 &&
                localPosition.Z < Configuration.CHUNK_DIMENSION.Z;
        }

        public BlockType GetBlock(Vector3I localPosition)
        {
            return _chunkData.GetBlock(localPosition);
        }

        public void Render()
        {
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
            surfaceTool.SetSmoothGroup(UInt32.MaxValue);

            transparentSurfaceTool.Begin(Mesh.PrimitiveType.Triangles);
            transparentSurfaceTool.SetSmoothGroup(UInt32.MaxValue);

            collisionSurfaceTool.Begin(Mesh.PrimitiveType.Triangles);
            collisionSurfaceTool.SetSmoothGroup(UInt32.MaxValue);

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

            surfaceTool.GenerateNormals(false);
            mesh = surfaceTool.Commit();

            transparentSurfaceTool.GenerateNormals(false);
            transparentMesh = transparentSurfaceTool.Commit();

            collisionSurfaceTool.GenerateNormals(false);
            collisionShape = collisionSurfaceTool.Commit().CreateTrimeshShape();

            CallThreadSafe("AfterRender");
        }

        public void AfterRender()
        {
            meshInstance.MaterialOverride = BlockTextures.Instance.material;
            meshInstance.Mesh = mesh;

            transparentMeshInstance.MaterialOverride = BlockTextures.Instance.transparentMaterial;
            transparentMeshInstance.Mesh = transparentMesh;

            collisionShapeNode.Shape = collisionShape;

            rendered = true;
        }

        private Vector3I GetWorldPosition(Vector3I localPosition)
        {
            return localPosition + chunkPosition * Configuration.CHUNK_DIMENSION;
        }

        private bool ShouldRender(Vector3I localPosition, Direction faceToCheck, BlockType sourceBlockType)
        {
            var blockType = IsOutsideChunk(localPosition)
                ? chunkManager.GetBlock(GetWorldPosition(localPosition))
                : GetBlock(localPosition);

            if (!blockType.Occludes.Contains(faceToCheck))
            {
                return true;
            }

            return blockType.Transparent && (blockType != sourceBlockType || !blockType.CullsSelf);
        }

        private bool IsOutsideChunk(Vector3I localPosition)
        {
            return localPosition.X < 0 || localPosition.X >= Configuration.CHUNK_DIMENSION.X || localPosition.Y < 0 || localPosition.Y >= Configuration.CHUNK_DIMENSION.Y || localPosition.Z < 0 || localPosition.Z >= Configuration.CHUNK_DIMENSION.Z;
        }

        private void RenderBlock(Vector3I localPosition)
        {
            var blockType = _chunkData.GetBlock(localPosition);

            if (blockType.Name == "air")
            {
                return;
            }

            for (int i = 0; i < blockType.Faces.Count; i++)
            {
                var face = blockType.Faces[i];

                if (face.OccludedBy.HasValue)
                {

                    var directionOfBlockToCheck = faceDirections[face.OccludedBy.Value];
                    var faceToCheck = inverseDirections[face.OccludedBy.Value];

                    if (!ShouldRender(localPosition + directionOfBlockToCheck, faceToCheck, blockType))
                    {
                        continue;
                    }
                }

                RenderFace(face, localPosition, blockType);
            }
        }

        private void RenderFace(Face face, Vector3I localPosition, bool transparent = false, bool hasCollision = true)
        {
            Vector2 uvOffset = face.TextureAtlasOffset / BlockTextures.Instance.size;
            float height = 1.0f / BlockTextures.Instance.size.Y;
            float width = 1.0f / BlockTextures.Instance.size.X;

            Vector2 uva = uvOffset + new Vector2(face.UV[0] * width, face.UV[1] * height);
            Vector2 uvb = uvOffset + new Vector2(face.UV[0] * width, face.UV[3] * height);
            Vector2 uvc = uvOffset + new Vector2(face.UV[2] * width, face.UV[3] * height);

            var st = transparent ? transparentSurfaceTool : surfaceTool;

            Vector3 a = face.Vertices[0] + localPosition;
            Vector3 b = face.Vertices[1] + localPosition;
            Vector3 c = face.Vertices[2] + localPosition;

            st.AddTriangleFan(new Vector3[] { a, b, c }, new Vector2[] { uva, uvb, uvc });

            if (hasCollision)
            {
                collisionSurfaceTool.AddTriangleFan(new Vector3[] { a, b, c }, new Vector2[] { uva, uvb, uvc });
            }

            if (face.Vertices.Count == 4)
            {
                Vector2 uvd = uvOffset + new Vector2(face.UV[2] * width, face.UV[1] * height);
                Vector3 d = face.Vertices[3] + localPosition;
                st.AddTriangleFan(new Vector3[] { a, c, d }, new Vector2[] { uva, uvc, uvd });

                if (hasCollision)
                {
                    collisionSurfaceTool.AddTriangleFan(new Vector3[] { a, c, d }, new Vector2[] { uva, uvc, uvd });
                }
            }
        }

        public void Initialize(ChunkManager chunkManager, Vector3I chunkPosition)
        {
            this.chunkManager = chunkManager;
            this.chunkPosition = chunkPosition;

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
