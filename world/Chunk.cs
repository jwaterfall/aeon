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
        private ConcavePolygonShape3D collisionShape;
        private CollisionShape3D collisionShapeNode;

        private SurfaceTool transparentSurfaceTool = new SurfaceTool();
        private Mesh transparentMesh;
        private MeshInstance3D transparentMeshInstance;
        private ConcavePolygonShape3D transparentCollisionShape;
        private CollisionShape3D transparentCollisionShapeNode;

        public Vector3I chunkPosition;
        public bool generated = false;
        public bool rendered = false;

        private byte[] chunkBlockTypes = new byte[Configuration.CHUNK_DIMENSION.X * Configuration.CHUNK_DIMENSION.Y * Configuration.CHUNK_DIMENSION.Z];

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
            collisionShapeNode = new();
            AddChild(collisionShapeNode);

            meshInstance = new();
            AddChild(meshInstance);

            transparentCollisionShapeNode = new();
            AddChild(transparentCollisionShapeNode);

            transparentMeshInstance = new();
            AddChild(transparentMeshInstance);

            if (Configuration.CHUNK_BORDERS)
            {
                CallDeferred("RenderDebug");
            }
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

                        SetBlock(localPosition, BlockTypes.Instance.Get(ore.Block), BlockTypes.Instance.Get("stone"));
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
                    if (localHeight < 0 || localHeight >= Configuration.CHUNK_DIMENSION.Y) continue;

                    var blockBelowId = chunkBlockTypes[GetFlatIndex(new Vector3I(x, localHeight, z))];
                    var blockBelow = BlockTypes.Instance.Get(blockBelowId);
                    if (blockBelow.Name != "grass" && blockBelow.Name != "snow") continue;

                    var randomNumber = random.NextDouble();
                    if (randomNumber > 0.2f) continue;
                    SetBlock(new Vector3I(x, localHeight + 1, z), BlockTypes.Instance.Get("short_grass"));
                }
            }

            // Add trees
            for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
            {
                for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                {
                    var randomNumber = random.NextDouble();
                    if (randomNumber > 0.02f) continue;

                    var height = terrainGenerator.GetHeight(new Vector2I(chunkPosition.X * Configuration.CHUNK_DIMENSION.X + x, chunkPosition.Z * Configuration.CHUNK_DIMENSION.Z + z));

                    var localHeight = height - chunkPosition.Y * Configuration.CHUNK_DIMENSION.Y;
                    if (localHeight < 0 || localHeight >= Configuration.CHUNK_DIMENSION.Y) continue;

                    var blockBelowId = chunkBlockTypes[GetFlatIndex(new Vector3I(x, localHeight, z))];
                    var blockBelow = BlockTypes.Instance.Get(blockBelowId);
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

            generated = true;
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

        private Vector3I GetGlobalPosition(Vector3I localPosition)
        {
            return new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z) * Configuration.CHUNK_DIMENSION + localPosition;
        }

        private void SetBlock(Vector3I localPosition, BlockType blockType, BlockType replaces = null)
        {
            if (!IsInChunk(localPosition)) return;

            if (replaces != null)
            {
                var currentBlockTypeId = chunkBlockTypes[GetFlatIndex(localPosition)];
                var currentBlockType = BlockTypes.Instance.Get(currentBlockTypeId);
                if (currentBlockType != replaces) return;
            }

            int index = GetFlatIndex(localPosition);
            chunkBlockTypes[index] = blockType.Id;
        }

        private int GetFlatIndex(Vector3I localPosition)
        {
            return localPosition.X + localPosition.Y * Configuration.CHUNK_DIMENSION.X + localPosition.Z * Configuration.CHUNK_DIMENSION.X * Configuration.CHUNK_DIMENSION.Y;
        }

        public void Render(Chunk northChunk, Chunk southChunk, Chunk eastChunk, Chunk westChunk, Chunk upChunk, Chunk downChunk)
        {
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
            surfaceTool.SetSmoothGroup(UInt32.MaxValue);

            transparentSurfaceTool.Begin(Mesh.PrimitiveType.Triangles);
            transparentSurfaceTool.SetSmoothGroup(UInt32.MaxValue);

            for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
            {
                for (int y = 0; y < Configuration.CHUNK_DIMENSION.Y; y++)
                {
                    for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                    {
                        RenderBlock(new Vector3I(x, y, z), northChunk, southChunk, eastChunk, westChunk, upChunk, downChunk);
                    }
                }
            }

            surfaceTool.GenerateNormals(false);
            mesh = surfaceTool.Commit();
            collisionShape = mesh.CreateTrimeshShape();

            transparentSurfaceTool.GenerateNormals(false);
            transparentMesh = transparentSurfaceTool.Commit();
            transparentCollisionShape = transparentMesh.CreateTrimeshShape();

            CallThreadSafe("AfterRender");
        }

        public void AfterRender()
        {
            meshInstance.MaterialOverride = BlockTextures.Instance.material;
            meshInstance.Mesh = mesh;
            collisionShapeNode.Shape = collisionShape;

            transparentMeshInstance.MaterialOverride = BlockTextures.Instance.transparentMaterial;
            transparentMeshInstance.Mesh = transparentMesh;
            transparentCollisionShapeNode.Shape = transparentCollisionShape;

            rendered = true;
        }

        private bool CheckTransparent(Vector3I localPosition, Direction faceToCheck, BlockType sourceBlockType, Chunk northChunk, Chunk eastChunk, Chunk southChunk, Chunk westChunk, Chunk upChunk, Chunk downChunk)
        {
            if (localPosition.X < 0)
            {
                // Check if the block in the west neighboring chunk is transparent
                return northChunk.CheckTransparent(new Vector3I(Configuration.CHUNK_DIMENSION.X - 1, localPosition.Y, localPosition.Z), faceToCheck, sourceBlockType, null, null, null, null, null, null);
            }

            if (localPosition.X >= Configuration.CHUNK_DIMENSION.X)
            {
                // Check if the block in the east neighboring chunk is transparent
                return eastChunk.CheckTransparent(new Vector3I(0, localPosition.Y, localPosition.Z), faceToCheck, sourceBlockType, null, null, null, null, null, null);
            }

            if (localPosition.Y < 0)
            {
                // Check if the block in the down neighboring chunk is transparent
                return downChunk.CheckTransparent(new Vector3I(localPosition.X, Configuration.CHUNK_DIMENSION.Y - 1, localPosition.Z), faceToCheck, sourceBlockType, null, null, null, null, null, null);
            }

            if (localPosition.Y >= Configuration.CHUNK_DIMENSION.Y)
            {
                // Check if the block in the up neighboring chunk is transparent
                return upChunk.CheckTransparent(new Vector3I(localPosition.X, 0, localPosition.Z), faceToCheck, sourceBlockType, null, null, null, null, null, null);
            }

            if (localPosition.Z < 0)
            {
                // Check if the block in the north neighboring chunk is transparent
                return westChunk.CheckTransparent(new Vector3I(localPosition.X, localPosition.Y, Configuration.CHUNK_DIMENSION.Z - 1), faceToCheck, sourceBlockType, null, null, null, null, null, null);
            }

            if (localPosition.Z >= Configuration.CHUNK_DIMENSION.Z)
            {
                // Check if the block in the south neighboring chunk is transparent
                return southChunk.CheckTransparent(new Vector3I(localPosition.X, localPosition.Y, 0), faceToCheck, sourceBlockType, null, null, null, null, null, null);
            }

            // Check in the current chunk
            var blockTypeId = chunkBlockTypes[GetFlatIndex(localPosition)];
            var blockType = BlockTypes.Instance.Get(blockTypeId);

            if (!blockType.Occludes.Contains(faceToCheck))
            {
                return true;
            }

            return blockType.Transparent && blockType != sourceBlockType;
        }

        private void RenderBlock(Vector3I localPosition, Chunk northChunk, Chunk eastChunk, Chunk southChunk, Chunk westChunk, Chunk upChunk, Chunk downChunk)
        {
            var blockTypeId = chunkBlockTypes[GetFlatIndex(localPosition)];
            var blockType = BlockTypes.Instance.Get(blockTypeId);

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

                    if (!CheckTransparent(localPosition + directionOfBlockToCheck, faceToCheck, blockType, northChunk, southChunk, eastChunk, westChunk, upChunk, downChunk))
                    {
                        continue;
                    }
                }

                RenderFace(face, localPosition, blockType.Transparent);
            }
        }

        private void RenderFace(Face face, Vector3I localPosition, bool transparent = false)
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

            if (face.Vertices.Count == 4)
            {
                Vector2 uvd = uvOffset + new Vector2(face.UV[2] * width, face.UV[1] * height);
                Vector3 d = face.Vertices[3] + localPosition;
                st.AddTriangleFan(new Vector3[] { a, c, d }, new Vector2[] { uva, uvc, uvd });
            }
        }

        public void SetChunkPosition(Vector3I newChunkPosition)
        {
            chunkPosition = newChunkPosition;
            Position = new Vector3I(newChunkPosition.X, newChunkPosition.Y, newChunkPosition.Z) * Configuration.CHUNK_DIMENSION;
        }
        public void RenderDebug()
        {
            //MeshInstance3D debugMeshInstance = new();
            //AddChild(debugMeshInstance);

            //ImmediateMesh debugMesh = new();
            //debugMeshInstance.Mesh = debugMesh;

            //debugMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

            //RenderDebugFace(Faces.TOP, debugMesh);
            //RenderDebugFace(Faces.BOTTOM, debugMesh);
            //RenderDebugFace(Faces.LEFT, debugMesh);
            //RenderDebugFace(Faces.RIGHT, debugMesh);
            //RenderDebugFace(Faces.FRONT, debugMesh);
            //RenderDebugFace(Faces.BACK, debugMesh);

            //debugMesh.SurfaceEnd();
        }

        //public void RenderDebugFace(int[] face, ImmediateMesh debugMesh)
        //{
        //    debugMesh.SurfaceAddVertex(vertices[face[0]] * Configuration.CHUNK_DIMENSION);
        //    debugMesh.SurfaceAddVertex(vertices[face[1]] * Configuration.CHUNK_DIMENSION);
        //    debugMesh.SurfaceAddVertex(vertices[face[1]] * Configuration.CHUNK_DIMENSION);
        //    debugMesh.SurfaceAddVertex(vertices[face[2]] * Configuration.CHUNK_DIMENSION);
        //    debugMesh.SurfaceAddVertex(vertices[face[2]] * Configuration.CHUNK_DIMENSION);
        //    debugMesh.SurfaceAddVertex(vertices[face[3]] * Configuration.CHUNK_DIMENSION);
        //    debugMesh.SurfaceAddVertex(vertices[face[3]] * Configuration.CHUNK_DIMENSION);
        //    debugMesh.SurfaceAddVertex(vertices[face[0]] * Configuration.CHUNK_DIMENSION);
        //}

        public void BreakBlock(Vector3I localPosition)
        {
            SetBlock(localPosition, BlockTypes.Instance.Get("air"));
        }

        public void PlaceBlock(Vector3I localPosition, BlockType blockType)
        {
            SetBlock(localPosition, blockType);
        }
    }
}
