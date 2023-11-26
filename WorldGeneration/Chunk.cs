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
	private Mesh mesh;
	private MeshInstance3D meshInstance;
    private ConcavePolygonShape3D collisionShape;
    private CollisionShape3D collisionShapeNode;
    private StandardMaterial3D material = ResourceLoader.Load("res://assets/atlas_material.tres") as StandardMaterial3D;
	public Vector2I ChunkPosition;
    private FastNoiseLite noise = new();

    List<List<List<BlockType>>> blockTypes = new();

    public override void _Ready()
    {
        collisionShapeNode = new();
        AddChild(collisionShapeNode);

        meshInstance = new();
        meshInstance.MaterialOverride = material;
        AddChild(meshInstance);
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
                    var globalPosition = new Vector3(ChunkPosition.X, 0, ChunkPosition.Y) * Configuration.CHUNK_DIMENSION + new Vector3I(x, y, z);

                    // Use different noise functions for various aspects of terrain generation
                    float continentalnessScale = 0.1f; // Adjust this scale factor as needed
                    float continentalness = noise.GetNoise2Dv(new Vector2(globalPosition.X * continentalnessScale, globalPosition.Z * continentalnessScale));

                    // Use different noise functions for various aspects of terrain generation
                    float peaksAndValleysScale = 0.5f; // Adjust this scale factor as needed
                    float peaksAndValleys = noise.GetNoise2Dv(new Vector2(globalPosition.X * peaksAndValleysScale, globalPosition.Z * peaksAndValleysScale));

                    // Map continentalness to the desired height range
                    int height = MapContinentalnessToHeight(continentalness) + MapPeaksAndValleysToHeight(peaksAndValleys);

                    // Adjust parameters based on noise values
                    int waterLevel = 64;

                    BlockType blockType = BlockTypes.Air;

                    if (globalPosition.Y > height && globalPosition.Y <= waterLevel)
                    {
                        blockType = BlockTypes.Water;
                    }
                    else if(globalPosition.Y < height - 2)
					{
						blockType = BlockTypes.Stone;
					}
					else if (globalPosition.Y < height)
					{
						blockType = BlockTypes.Dirt;
                    }
                    else if(globalPosition.Y == height)
                    {
                        blockType = BlockTypes.Grass;
                    }

					blockTypes[x][y].Add(blockType);
                }
            }
        }
    }

    // Custom non-linear mapping function based on the provided values
    private int MapContinentalnessToHeight(float continentalness)
    {
        // Use the provided non-linear mapping values with smoothstep
        float t;

        if (continentalness <= -0.6f)
        {
            return 32;
        }
        else if (continentalness <= -0.4f)
        {
            t = Mathf.SmoothStep(-0.6f, -0.4f, continentalness);
            return (int)Mathf.Round(Mathf.Lerp(32, 72, t));
        }
        else if (continentalness <= -0.25f)
        {
            t = Mathf.SmoothStep(-0.4f, -0.25f, continentalness);
            return (int)Mathf.Round(Mathf.Lerp(72, 80, t));
        }
        else if (continentalness <= -0.2f)
        {
            t = Mathf.SmoothStep(-0.25f, -0.2f, continentalness);
            return (int)Mathf.Round(Mathf.Lerp(80, 128, t));
        }
        else if (continentalness <= 0)
        {
            t = Mathf.SmoothStep(-0.2f, 0, continentalness);
            return (int)Mathf.Round(Mathf.Lerp(128, 134, t));
        }
        else
        {
            t = Mathf.SmoothStep(0, 1, continentalness);
            return (int)Mathf.Round(Mathf.Lerp(134, 160, t));
        }
    }

    private int MapPeaksAndValleysToHeight(float peaksAndValleys)
    {
        // Use the provided non-linear mapping values with smoothstep
        float t;

        if (peaksAndValleys <= -0.8f)
        {
            t = Mathf.SmoothStep(0, -0.8f, peaksAndValleys);
            return (int)Mathf.Round(Mathf.Lerp(-16, -10, t));
        }
        else if (peaksAndValleys <= -0.4f)
        {
            t = Mathf.SmoothStep(-0.8f, -0.4f, peaksAndValleys);
            return (int)Mathf.Round(Mathf.Lerp(-10, 0, t));
        }
        else if (peaksAndValleys <= 0)
        {
            t = Mathf.SmoothStep(-0.4f, -0f, peaksAndValleys);
            return (int)Mathf.Round(Mathf.Lerp(0, 8, t));
        }
        else if (peaksAndValleys <= 0.4)
        {
            t = Mathf.SmoothStep(0, 0.4f, peaksAndValleys);
            return (int)Mathf.Round(Mathf.Lerp(8, 32, t));
        }
        else
        {
            t = Mathf.SmoothStep(0.4F, 1, peaksAndValleys);
            return (int)Mathf.Round(Mathf.Lerp(32, 24, t));
        }
    }

    public void Update()
	{
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        surfaceTool.SetSmoothGroup(UInt32.MaxValue);

        for (int x = 0; x < Configuration.CHUNK_DIMENSION.X; x++)
		{
			for(int y = 0; y < Configuration.CHUNK_DIMENSION.Y; y++)
			{
				for(int z = 0; z < Configuration.CHUNK_DIMENSION.Z; z++)
				{
					CreateBlock(new Vector3I(x, y, z));
				}
			}
		}

		surfaceTool.GenerateNormals(false);
        mesh = surfaceTool.Commit();
        collisionShape = mesh.CreateTrimeshShape();

        CallThreadSafe("AfterUpdate");
    }
    public void AfterUpdate()
    {
        meshInstance.Mesh = mesh;
        collisionShapeNode.Shape = collisionShape;
    }

    /// <summary>
    /// <c>Returns</c> true if the given block is transparent or outside of the chunk
    /// </summary>
    private bool CheckTransparent(Vector3I localPosition, BlockType sourceBlockType)
	{
		if (
			!(localPosition.X >= 0 && localPosition.X < Configuration.CHUNK_DIMENSION.X &&
            localPosition.Y >= 0 && localPosition.Y < Configuration.CHUNK_DIMENSION.Y &&
            localPosition.Z >= 0 && localPosition.Z < Configuration.CHUNK_DIMENSION.Z)
		)
        {
            return true;
        }

        BlockType blockType = blockTypes[localPosition.X][localPosition.Y][localPosition.Z];
        return (!blockType.Solid) && (blockType != sourceBlockType);
    }

	private void CreateBlock(Vector3I localPosition)
	{
		BlockType blockType = blockTypes[localPosition.X][localPosition.Y][localPosition.Z];

		if (blockType == BlockTypes.Air) {
			return;
		}

		if (CheckTransparent(localPosition + Vector3I.Up, blockType))
        {
			CreateFace(Faces.TOP, localPosition, blockType.TextureAtlasOffsetTop);
        }
        if (CheckTransparent(localPosition + Vector3I.Down, blockType))
        {
			CreateFace(Faces.BOTTOM, localPosition, blockType.TextureAtlasOffsetBottom);
        }
        if (CheckTransparent(localPosition + Vector3I.Left, blockType))
        {
            CreateFace(Faces.LEFT, localPosition, blockType.TextureAtlasOffsetLeft);
        }
        if (CheckTransparent(localPosition + Vector3I.Right, blockType))
        {
            CreateFace(Faces.RIGHT, localPosition, blockType.TextureAtlasOffsetRight);
        }
        if (CheckTransparent(localPosition + Vector3I.Forward, blockType))
        {
            CreateFace(Faces.BACK, localPosition, blockType.TextureAtlasOffsetBack);
        }
        if (CheckTransparent(localPosition + Vector3I.Back, blockType))
        {
            CreateFace(Faces.FRONT, localPosition, blockType.TextureAtlasOffsetFront);
        }
	}

	private void CreateFace(int[] face, Vector3I localPosition, Vector2 textureAtlasOffset)
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
		Position = new Vector3I(newChunkPosition.X, 0, newChunkPosition.Y) * Configuration.CHUNK_DIMENSION;
	}
}
