using Godot;
using System;
using System.Collections.Generic;

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
    private StandardMaterial3D material = ResourceLoader.Load("res://assets/atlas_material.tres") as StandardMaterial3D;
	public Vector2I chunkPosition;
    public bool generated = false;
    public bool rendered = false;

    private BlockType[] blockTypes = new BlockType[Configuration.CHUNK_DIMENSION.X * Configuration.CHUNK_DIMENSION.Y * Configuration.CHUNK_DIMENSION.Z];

    public override void _Ready()
    {
        collisionShapeNode = new();
        AddChild(collisionShapeNode);

        meshInstance = new();
        meshInstance.MaterialOverride = material;
        AddChild(meshInstance);
    }

    public void GenerateBlocks(TerrainGenerator terrainGenerator)
    {
        for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
        {
            for (int y = 0; y < Configuration.CHUNK_DIMENSION.Y; y++)
            {
                for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                {
                    var globalPosition = new Vector3I(chunkPosition.X, 0, chunkPosition.Y) * Configuration.CHUNK_DIMENSION + new Vector3I(x, y, z);

                    int waterLevel = 64;

                    var height = terrainGenerator.GetHeight(globalPosition, waterLevel);

                    BlockType blockType = BlockTypes.Get("air");

                    if (globalPosition.Y > height && globalPosition.Y <= waterLevel)
                    {
                        blockType = BlockTypes.Get("water");
                    }
                    else if(globalPosition.Y < height - 2)
					{
                        blockType = BlockTypes.Get("stone");
					}
                    else if(height <= 68 && globalPosition.Y <= height)
                    {
                        blockType = BlockTypes.Get("sand");
                    }
                    else if (globalPosition.Y < height)
					{
                        blockType = BlockTypes.Get("dirt");
                    }
                    else if(globalPosition.Y == height)
                    {
                        blockType = BlockTypes.Get("grass");
                    }

                    int index = GetFlatIndex(new Vector3I(x, y, z));
                    blockTypes[index] = blockType;
                }
            }
        }

        generated = true;
    }

    private int GetFlatIndex(Vector3I localPosition)
    {
        return localPosition.X + localPosition.Y * Configuration.CHUNK_DIMENSION.X + localPosition.Z * Configuration.CHUNK_DIMENSION.X * Configuration.CHUNK_DIMENSION.Y;
    }

    public void Render(Chunk northChunk, Chunk southChunk, Chunk eastChunk, Chunk westChunk)
    {
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        surfaceTool.SetSmoothGroup(UInt32.MaxValue);

        for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
		{
			for(int y = 0; y < Configuration.CHUNK_DIMENSION.Y; y++)
			{
				for(int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
				{
                    RenderBlock(new Vector3I(x, y, z), northChunk, southChunk, eastChunk, westChunk);
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
        meshInstance.Mesh = mesh;
        collisionShapeNode.Shape = collisionShape;
        rendered = true;
    }

    private bool CheckTransparent(Vector3I localPosition, BlockType sourceBlockType, Chunk northChunk, Chunk eastChunk, Chunk southChunk, Chunk westChunk)
    {
        // If the block is outside of the chunk in the Y axis render the face
        if (localPosition.Y < 0 || localPosition.Y >= Configuration.CHUNK_DIMENSION.Y)
        {
            return true;
        }

        if (localPosition.X < 0)
        {
            // Check if the block in the west neighboring chunk is transparent
            return westChunk.CheckTransparent(new Vector3I(Configuration.CHUNK_DIMENSION.X - 1, localPosition.Y, localPosition.Z), sourceBlockType, null, null, null, null);
        }

        if (localPosition.X >= Configuration.CHUNK_DIMENSION.X)
        {
            // Check if the block in the east neighboring chunk is transparent
            return eastChunk.CheckTransparent(new Vector3I(0, localPosition.Y, localPosition.Z), sourceBlockType, null, null, null, null);
        }

        if (localPosition.Z < 0)
        {
            // Check if the block in the north neighboring chunk is transparent
            return northChunk.CheckTransparent(new Vector3I(localPosition.X, localPosition.Y, Configuration.CHUNK_DIMENSION.Z - 1), sourceBlockType, null, null, null, null);
        }

        if (localPosition.Z >= Configuration.CHUNK_DIMENSION.Z)
        {
            // Check if the block in the south neighboring chunk is transparent
            return southChunk.CheckTransparent(new Vector3I(localPosition.X, localPosition.Y, 0), sourceBlockType, null, null, null, null);
        }

        // Check in the current chunk
        BlockType blockType = blockTypes[GetFlatIndex(localPosition)];
        return !blockType.Solid && blockType != sourceBlockType;
    }

    private void RenderBlock(Vector3I localPosition, Chunk northChunk, Chunk southChunk, Chunk eastChunk, Chunk westChunk)
    {
        BlockType blockType = blockTypes[GetFlatIndex(localPosition)];

        if (blockType.Name == "air")
        {
            return;
        }

        if (CheckTransparent(localPosition + Vector3I.Up, blockType, northChunk, southChunk, eastChunk, westChunk))
        {
            RenderFace(Faces.TOP, localPosition, blockType.TextureAtlasOffsetTop);
        }
        if (CheckTransparent(localPosition + Vector3I.Down, blockType, northChunk, southChunk, eastChunk, westChunk))
        {
            RenderFace(Faces.BOTTOM, localPosition, blockType.TextureAtlasOffsetBottom);
        }
        if (CheckTransparent(localPosition + Vector3I.Left, blockType, northChunk, southChunk, eastChunk, westChunk))
        {
            RenderFace(Faces.LEFT, localPosition, blockType.TextureAtlasOffsetLeft);
        }
        if (CheckTransparent(localPosition + Vector3I.Right, blockType, northChunk, southChunk, eastChunk, westChunk))
        {
            RenderFace(Faces.RIGHT, localPosition, blockType.TextureAtlasOffsetRight);
        }
        if (CheckTransparent(localPosition + Vector3I.Forward, blockType, northChunk, southChunk, eastChunk, westChunk))
        {
            RenderFace(Faces.BACK, localPosition, blockType.TextureAtlasOffsetBack);
        }
        if (CheckTransparent(localPosition + Vector3I.Back, blockType, northChunk, southChunk, eastChunk, westChunk))
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

		Vector2 uvOffset = textureAtlasOffset / Configuration.TEXTURE_ATLAS_SIZE;
		float height = 1.0f / Configuration.TEXTURE_ATLAS_SIZE.Y;
		float width = 1.0f / Configuration.TEXTURE_ATLAS_SIZE.X;

        Vector2 uva = uvOffset + new Vector2(0, 0);
        Vector2 uvb = uvOffset + new Vector2(0, height);
        Vector2 uvc = uvOffset + new Vector2(width, height);
        Vector2 uvd = uvOffset + new Vector2(width, 0);

        surfaceTool.AddTriangleFan(new Vector3[] { a, b, c }, new Vector2[] { uva, uvb, uvc });
		surfaceTool.AddTriangleFan(new Vector3[] { a, c, d }, new Vector2[] { uva, uvc, uvd });
	}

	public void SetChunkPosition(Vector2I newChunkPosition)
	{
		chunkPosition = newChunkPosition;
		Position = new Vector3I(newChunkPosition.X, 0, newChunkPosition.Y) * Configuration.CHUNK_DIMENSION;
	}
}
