using Godot;
using System;

public partial class Chunk : StaticBody3D
{
	private readonly Vector3[] vertices = new Vector3[]
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

	private readonly int[] TOP_FACE = new int[] { 2, 3, 7, 6 };
	private readonly int[] BOTTOM_FACE = new int[] { 0, 4, 5, 1 };
	private readonly int[] LEFT_FACE = new int[] { 6, 4, 0, 2 };
	private readonly int[] RIGHT_FACE = new int[] { 3, 1, 5, 7 };
	private readonly int[] FRONT_FACE = new int[] { 7, 5, 4, 6 };
	private readonly int[] BACK_FACE = new int[] { 2, 0, 1, 3 };

	// private List<Block> blocks = new List<Block>();

	private SurfaceTool surfaceTool = new SurfaceTool();
	private ArrayMesh mesh = null;
	private MeshInstance3D meshInstance = null;

	public override void _Ready()
	{
		Update();
	}

	public void Update() {
		var global = GetNode<Global>("/root/Global");

		// Unload
		if (meshInstance != null) {
			meshInstance.CallDeferred("queue_free");
			meshInstance = null;
		}

		mesh = new ArrayMesh();
		meshInstance = new MeshInstance3D();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		for(int x = 0; x < global.CHUNK_DIMENSION.X; x++)
		{
			for(int y = 0; y < global.CHUNK_DIMENSION.Y; y++)
			{
				for(int z = 0; z < global.CHUNK_DIMENSION.Z; z++)
				{
					CreateBlock(new Vector3(x, y, z));
				}
			}
		}

		surfaceTool.GenerateNormals();
		surfaceTool.Commit(mesh);
		meshInstance.Mesh = mesh;

		AddChild(meshInstance);
		meshInstance.CreateTrimeshCollision();
	}

	private void CreateBlock(Vector3 position) {
		CreateFace(TOP_FACE, position);
		CreateFace(BOTTOM_FACE, position);
		CreateFace(LEFT_FACE, position);
		CreateFace(RIGHT_FACE, position);
		CreateFace(FRONT_FACE, position);
		CreateFace(BACK_FACE, position);
	}

	private void CreateFace(int[] face, Vector3 position) {
		Vector3 a = vertices[face[0]] + position;
		Vector3 b = vertices[face[1]] + position;
		Vector3 c = vertices[face[2]] + position;
		Vector3 d = vertices[face[3]] + position;

		surfaceTool.AddTriangleFan(new Vector3[] { a, b, c });
		surfaceTool.AddTriangleFan(new Vector3[] { a, c, d });
	}
}
