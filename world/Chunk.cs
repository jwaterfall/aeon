using Godot;
using System;

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
        }

        public void GenerateBlocks(TerrainGenerator terrainGenerator)
        {
            lock (BlockTypes.Instance)
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
                            
                            int index = GetFlatIndex(new Vector3I(x, y, z));
                            chunkBlockTypes[index] = blockType;
                        }
                    }
                }
            }

            generated = true;
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
    }
}
