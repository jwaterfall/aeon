using Godot;
using System;
using System.Collections.Generic;

public partial class ChunkManager : Node3D
{
    private PackedScene chunkScene = ResourceLoader.Load("res://WorldGeneration/Chunk.tscn") as PackedScene;
    static readonly Dictionary<Vector2I, Chunk> chunks = new();

    public void Update(Vector3 playerPosition)
    {
        foreach (var chunkPosition in GetNearbyChunkPositions(playerPosition))
        {
            if (!chunks.ContainsKey(chunkPosition))
            {
                Chunk chunk = chunkScene.Instantiate<Chunk>();
                chunk.SetChunkPosition(chunkPosition);
                AddChild(chunk);
                chunks.Add(chunkPosition, chunk);
            }
        }
    }

    public IEnumerable<Vector2I> GetNearbyChunkPositions(Vector3 playerPosition)
    {
        var loadRadius = Configuration.CHUNK_LOAD_RADIUS;
        var playerChunkPosition = (Vector3I)(playerPosition / Configuration.CHUNK_DIMENSION).Round();

        int x = 0; int y = 0;
        int dx = 0; int dy = -1;
        for (int i = 0; i < loadRadius * loadRadius; i++)
        {
            if (-loadRadius / 2 < x && x < loadRadius / 2 && -loadRadius / 2 < y && y < loadRadius / 2)
            {
                yield return new Vector2I(x + playerChunkPosition.X, y + playerChunkPosition.Z);
            }
            if (x == y || (x < 0 && x == -y) || (x > 0 && x == 1 - y))
            {
                int dc = dx;
                dx = -dy;
                dy = dc;
            }
            x += dx;
            y += dy;
        }
        //Source: https://stackoverflow.com/a/398302
    }
}
