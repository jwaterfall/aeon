using Godot;
using System;

public partial class ChunkManager : Node3D
{
    private PackedScene chunkScene = ResourceLoader.Load("res://WorldGeneration/Chunk.tscn") as PackedScene;

    public override void _Ready()
    {
        for (int x = 0; x < Configuration.CHUNK_LOAD_RADIUS; x++)
        {
            for (int z = 0; z < Configuration.CHUNK_LOAD_RADIUS; z++)
            {
                Chunk chunk = chunkScene.Instantiate<Chunk>();
                chunk.SetChunkPosition(new Vector2(x, z));
                AddChild(chunk);
            }
        }
    }

    public void Update(Vector3 playerWorldPosition)
    {
        foreach (Chunk chunk in GetChildren())
        {
            int cx = (int)chunk.ChunkPosition.X;
            int cz = (int)chunk.ChunkPosition.Y;

            float px = Mathf.Floor(playerWorldPosition.X / Configuration.CHUNK_DIMENSION.X);
            float pz = Mathf.Floor(playerWorldPosition.Z / Configuration.CHUNK_DIMENSION.Z);

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
