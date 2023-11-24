using Godot;
using System;

public partial class World : Node3D
{
    private PackedScene chunkScene = ResourceLoader.Load("res://Chunk.tscn") as PackedScene;

    public override void _Ready()
	{
        Node3D chunks = GetNode<Node3D>("Chunks");

        for (int x = 0; x < Configuration.CHUNK_LOAD_RADIUS; x++)
        {
            for (int z = 0; z < Configuration.CHUNK_LOAD_RADIUS; z++)
            {
                Chunk chunk = chunkScene.Instantiate<Chunk>();
                chunk.SetChunkPosition(new Vector2(x, z));
                chunks.AddChild(chunk);
            }
        }
    }

    public override void _Process(double delta)
    {
        Node3D chunks = GetNode<Node3D>("Chunks");
        Node3D player = GetNode<Player>("Player");

        foreach (Chunk chunk in chunks.GetChildren())
        {
            int cx = (int)chunk.ChunkPosition.X;
            int cz = (int)chunk.ChunkPosition.Y;

            float px = Mathf.Floor(player.Position.X / Configuration.CHUNK_DIMENSION.X);
            float pz = Mathf.Floor(player.Position.Z / Configuration.CHUNK_DIMENSION.Z);

            float newX = Mathf.PosMod(cx - px + Configuration.CHUNK_LOAD_RADIUS / 2, Configuration.CHUNK_LOAD_RADIUS) + px - Configuration.CHUNK_LOAD_RADIUS / 2;
            float newZ = Mathf.PosMod(cz - pz + Configuration.CHUNK_LOAD_RADIUS / 2, Configuration.CHUNK_LOAD_RADIUS) + pz - Configuration.CHUNK_LOAD_RADIUS / 2;

            if (newX != cx || newZ != cz)
            {
                chunk.SetChunkPosition(new Vector2((int)newX, (int)newZ));
                chunk.Generate();
                chunk.Update();
            }
        }
    }
}
