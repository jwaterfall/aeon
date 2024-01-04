using Godot;
using System;
using System.Collections.Generic;

namespace Aeon
{
    public partial class Chunk : StaticBody3D
    {
        private static class Faces
        {
            public static readonly int[] TOP = new int[] { 2, 3, 7, 6 };
            public static readonly int[] BOTTOM = new int[] { 0, 4, 5, 1 };
            public static readonly int[] LEFT = new int[] { 6, 4, 0, 2 };
            public static readonly int[] RIGHT = new int[] { 3, 1, 5, 7 };
            public static readonly int[] FRONT = new int[] { 7, 5, 4, 6 };
            public static readonly int[] BACK = new int[] { 2, 0, 1, 3 };
        }

        private static readonly Vector3I[] vertices = new Vector3I[]
        {
            new(0, 0, 0), // 0
	        new(1, 0, 0), // 1
	        new(0, 1, 0), // 2
	        new(1, 1, 0), // 3
	        new(0, 0, 1), // 4
	        new(1, 0, 1), // 5
	        new(0, 1, 1), // 6
	        new(1, 1, 1)  // 7
        };

        private SurfaceTool surfaceTool = new SurfaceTool();
        private Mesh mesh;
        private MeshInstance3D meshInstance;
        private ConcavePolygonShape3D collisionShape;
        private CollisionShape3D collisionShapeNode;
        public Vector3I chunkPosition;
        public bool generated = false;
        public bool rendered = false;

        private BlockType[] chunkBlockTypes = new BlockType[Configuration.CHUNK_DIMENSION.X * Configuration.CHUNK_DIMENSION.Y * Configuration.CHUNK_DIMENSION.Z];

        public override void _Ready()
        {
            collisionShapeNode = new();
            AddChild(collisionShapeNode);

            meshInstance = new();
            AddChild(meshInstance);

            if (Configuration.CHUNK_BORDERS)
            {
                CallDeferred("RenderDebug");
            }
        }

        public void GenerateBlocks(TerrainGenerator terrainGenerator, WorldPreset worldPreset)
        {
            for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
            {
                for (int y = 0; y < Configuration.CHUNK_DIMENSION.Y; y++)
                {
                    for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                    {
                        var globalPosition = new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z) * Configuration.CHUNK_DIMENSION + new Vector3I(x, y, z);

                        int waterLevel = 64;

                        var blockType = terrainGenerator.GetBlockType(globalPosition, waterLevel);
                            
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
            if (replaces != null)
            {
                var currentBlockType = chunkBlockTypes[GetFlatIndex(localPosition)];
                if (currentBlockType != replaces) return;
            }

            int index = GetFlatIndex(localPosition);
            chunkBlockTypes[index] = blockType;
        }

        private int GetFlatIndex(Vector3I localPosition)
        {
            return localPosition.X + localPosition.Y * Configuration.CHUNK_DIMENSION.X + localPosition.Z * Configuration.CHUNK_DIMENSION.X * Configuration.CHUNK_DIMENSION.Y;
        }

        public void Render(Chunk northChunk, Chunk southChunk, Chunk eastChunk, Chunk westChunk, Chunk upChunk, Chunk downChunk)
        {
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
            surfaceTool.SetSmoothGroup(UInt32.MaxValue);

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

            CallThreadSafe("AfterRender");
        }

        public void AfterRender()
        {
            meshInstance.MaterialOverride = BlockTextures.Instance.material;
            meshInstance.Mesh = mesh;
            collisionShapeNode.Shape = collisionShape;
            rendered = true;
        }

        private bool CheckTransparent(Vector3I localPosition, BlockType sourceBlockType, Chunk northChunk, Chunk eastChunk, Chunk southChunk, Chunk westChunk, Chunk upChunk, Chunk downChunk)
        {
            if (localPosition.X < 0)
            {
                // Check if the block in the west neighboring chunk is transparent
                return westChunk.CheckTransparent(new Vector3I(Configuration.CHUNK_DIMENSION.X - 1, localPosition.Y, localPosition.Z), sourceBlockType, null, null, null, null, null, null);
            }

            if (localPosition.X >= Configuration.CHUNK_DIMENSION.X)
            {
                // Check if the block in the east neighboring chunk is transparent
                return eastChunk.CheckTransparent(new Vector3I(0, localPosition.Y, localPosition.Z), sourceBlockType, null, null, null, null, null, null);
            }

            if (localPosition.Y < 0)
            {
                // Check if the block in the down neighboring chunk is transparent
                return downChunk.CheckTransparent(new Vector3I(localPosition.X, Configuration.CHUNK_DIMENSION.Y - 1, localPosition.Z), sourceBlockType, null, null, null, null, null, null);
            }

            if (localPosition.Y >= Configuration.CHUNK_DIMENSION.Y)
            {
                // Check if the block in the up neighboring chunk is transparent
                return upChunk.CheckTransparent(new Vector3I(localPosition.X, 0, localPosition.Z), sourceBlockType, null, null, null, null, null, null);
            }

            if (localPosition.Z < 0)
            {
                // Check if the block in the north neighboring chunk is transparent
                return northChunk.CheckTransparent(new Vector3I(localPosition.X, localPosition.Y, Configuration.CHUNK_DIMENSION.Z - 1), sourceBlockType, null, null, null, null, null, null);
            }

            if (localPosition.Z >= Configuration.CHUNK_DIMENSION.Z)
            {
                // Check if the block in the south neighboring chunk is transparent
                return southChunk.CheckTransparent(new Vector3I(localPosition.X, localPosition.Y, 0), sourceBlockType, null, null, null, null, null, null);
            }

            // Check in the current chunk
            BlockType blockType = chunkBlockTypes[GetFlatIndex(localPosition)];
            return !blockType.Solid && blockType != sourceBlockType;
        }

        private void RenderBlock(Vector3I localPosition, Chunk northChunk, Chunk southChunk, Chunk eastChunk, Chunk westChunk, Chunk upChunk, Chunk downChunk)
        {
            BlockType blockType = chunkBlockTypes[GetFlatIndex(localPosition)];

            if (blockType.Name == "air")
            {
                return;
            }

            if (CheckTransparent(localPosition + Vector3I.Up, blockType, northChunk, southChunk, eastChunk, westChunk, upChunk, downChunk))
            {
                RenderFace(Faces.TOP, localPosition, blockType.TextureAtlasOffsetTop);
            }
            if (CheckTransparent(localPosition + Vector3I.Down, blockType, northChunk, southChunk, eastChunk, westChunk, upChunk, downChunk))
            {
                RenderFace(Faces.BOTTOM, localPosition, blockType.TextureAtlasOffsetBottom);
            }
            if (CheckTransparent(localPosition + Vector3I.Left, blockType, northChunk, southChunk, eastChunk, westChunk, upChunk, downChunk))
            {
                RenderFace(Faces.LEFT, localPosition, blockType.TextureAtlasOffsetLeft);
            }
            if (CheckTransparent(localPosition + Vector3I.Right, blockType, northChunk, southChunk, eastChunk, westChunk, upChunk, downChunk))
            {
                RenderFace(Faces.RIGHT, localPosition, blockType.TextureAtlasOffsetRight);
            }
            if (CheckTransparent(localPosition + Vector3I.Forward, blockType, northChunk, southChunk, eastChunk, westChunk, upChunk, downChunk))
            {
                RenderFace(Faces.BACK, localPosition, blockType.TextureAtlasOffsetBack);
            }
            if (CheckTransparent(localPosition + Vector3I.Back, blockType, northChunk, southChunk, eastChunk, westChunk, upChunk, downChunk))
            {
                RenderFace(Faces.FRONT, localPosition, blockType.TextureAtlasOffsetFront);
            }
        }

        private void RenderFace(int[] face, Vector3I localPosition, Vector2 textureAtlasOffset)
        {
            Vector3 a = vertices[face[0]] + localPosition;
            Vector3 b = vertices[face[1]] + localPosition;
            Vector3 c = vertices[face[2]] + localPosition;
            Vector3 d = vertices[face[3]] + localPosition;

            Vector2 uvOffset = textureAtlasOffset / BlockTextures.Instance.size;
            float height = 1.0f / BlockTextures.Instance.size.Y;
            float width = 1.0f / BlockTextures.Instance.size.X;

            Vector2 uva = uvOffset + new Vector2(0, 0);
            Vector2 uvb = uvOffset + new Vector2(0, height);
            Vector2 uvc = uvOffset + new Vector2(width, height);
            Vector2 uvd = uvOffset + new Vector2(width, 0);

            surfaceTool.AddTriangleFan(new Vector3[] { a, b, c }, new Vector2[] { uva, uvb, uvc });
            surfaceTool.AddTriangleFan(new Vector3[] { a, c, d }, new Vector2[] { uva, uvc, uvd });
        }

        public void SetChunkPosition(Vector3I newChunkPosition)
        {
            chunkPosition = newChunkPosition;
            Position = new Vector3I(newChunkPosition.X, newChunkPosition.Y, newChunkPosition.Z) * Configuration.CHUNK_DIMENSION;
        }
        public void RenderDebug()
        {
            MeshInstance3D debugMeshInstance = new();
            AddChild(debugMeshInstance);

            ImmediateMesh debugMesh = new();
            debugMeshInstance.Mesh = debugMesh;

            debugMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

            RenderDebugFace(Faces.TOP, debugMesh);
            RenderDebugFace(Faces.BOTTOM, debugMesh);
            RenderDebugFace(Faces.LEFT, debugMesh);
            RenderDebugFace(Faces.RIGHT, debugMesh);
            RenderDebugFace(Faces.FRONT, debugMesh);
            RenderDebugFace(Faces.BACK, debugMesh);

            debugMesh.SurfaceEnd();
        }

        public void RenderDebugFace(int[] face, ImmediateMesh debugMesh)
        {
            debugMesh.SurfaceAddVertex(vertices[face[0]] * Configuration.CHUNK_DIMENSION);
            debugMesh.SurfaceAddVertex(vertices[face[1]] * Configuration.CHUNK_DIMENSION);
            debugMesh.SurfaceAddVertex(vertices[face[1]] * Configuration.CHUNK_DIMENSION);
            debugMesh.SurfaceAddVertex(vertices[face[2]] * Configuration.CHUNK_DIMENSION);
            debugMesh.SurfaceAddVertex(vertices[face[2]] * Configuration.CHUNK_DIMENSION);
            debugMesh.SurfaceAddVertex(vertices[face[3]] * Configuration.CHUNK_DIMENSION);
            debugMesh.SurfaceAddVertex(vertices[face[3]] * Configuration.CHUNK_DIMENSION);
            debugMesh.SurfaceAddVertex(vertices[face[0]] * Configuration.CHUNK_DIMENSION);
        }
    }
}
