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

    private static readonly Vector3[] vertices = new Vector3[]
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
	private ArrayMesh mesh;
	private MeshInstance3D meshInstance;
	private StandardMaterial3D material = ResourceLoader.Load("res://assets/atlas_material.tres") as StandardMaterial3D;
	public Vector2I ChunkPosition;

    List<List<List<BlockType>>> blockTypes = new();

    public override void _Ready()
	{
		Generate();
		Update();
	}

	public void Generate()
	{
        for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
        {
            blockTypes.Add(new List<List<BlockType>>());
            for (int y = 0; y < Configuration.CHUNK_DIMENSION.Y; y++)
            {
                blockTypes[x].Add(new List<BlockType>());
                for (int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
                {
					BlockType blockType = BlockTypes.Air;

					if (y < 14)
					{
						blockType = BlockTypes.Stone;
					}
					else if (y < 16)
					{
						blockType = BlockTypes.Dirt;
                    }
                    else if(y == 16)
                    {
                        blockType = BlockTypes.Grass;
                    }

					blockTypes[x][y].Add(blockType);
                }
            }
        }
    }

	public void Update()
	{
		// Unload
		if (meshInstance != null) {
			meshInstance.CallDeferred("queue_free");
			meshInstance = null;
		}

		mesh = new ArrayMesh();
		meshInstance = new MeshInstance3D();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		for(int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
		{
			for(int y = 0; y < Configuration.CHUNK_DIMENSION.Y; y++)
			{
				for(int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
				{
					CreateBlock(new Vector3(x, y, z));
				}
			}
		}

		surfaceTool.GenerateNormals(false);
		surfaceTool.SetMaterial(material);
		surfaceTool.Commit(mesh);
		meshInstance.Mesh = mesh;

		AddChild(meshInstance);
		meshInstance.CreateTrimeshCollision();

		Visible = true;
	}

    /// <summary>
    /// <c>Returns</c> true if the given block is transparent or outside of the chunk
    /// </summary>
    private bool CheckTransparent(Vector3 localPosition)
	{
		if (
			localPosition.X >= 0 && localPosition.X < Configuration.CHUNK_DIMENSION.X &&
            localPosition.Y >= 0 && localPosition.Y < Configuration.CHUNK_DIMENSION.Y &&
            localPosition.Z >= 0 && localPosition.Z < Configuration.CHUNK_DIMENSION.Z
		)
        {
            BlockType blockType = blockTypes[(int)localPosition.X][(int)localPosition.Y][(int)localPosition.Z];
            return !blockType.Solid;
        }

		return true;
    }

	private void CreateBlock(Vector3 localPosition)
	{
		BlockType blockType = blockTypes[(int)localPosition.X][(int)localPosition.Y][(int)localPosition.Z];

		if (blockType == BlockTypes.Air) {
			return;
		}

		if (CheckTransparent(localPosition + Vector3.Up))
        {
			CreateFace(Faces.TOP, localPosition, blockType.TextureAtlasOffsetTop);
        }
        if (CheckTransparent(localPosition + Vector3.Down))
        {
			CreateFace(Faces.BOTTOM, localPosition, blockType.TextureAtlasOffsetBottom);
        }
        if (CheckTransparent(localPosition + Vector3.Left))
        {
            CreateFace(Faces.LEFT, localPosition, blockType.TextureAtlasOffsetLeft);
        }
        if (CheckTransparent(localPosition + Vector3.Right))
        {
            CreateFace(Faces.RIGHT, localPosition, blockType.TextureAtlasOffsetRight);
        }
        if (CheckTransparent(localPosition + Vector3.Forward))
        {
            CreateFace(Faces.BACK, localPosition, blockType.TextureAtlasOffsetBack);
        }
        if (CheckTransparent(localPosition + Vector3.Back))
        {
            CreateFace(Faces.FRONT, localPosition, blockType.TextureAtlasOffsetFront);
        }
	}

	private void CreateFace(int[] face, Vector3 localPosition, Vector2 textureAtlasOffset)
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
		ChunkPosition = newChunkPosition;
		Position = (new Vector3(newChunkPosition.X, 0, newChunkPosition.Y)) * Configuration.CHUNK_DIMENSION;

        Visible = false;
	}
}
