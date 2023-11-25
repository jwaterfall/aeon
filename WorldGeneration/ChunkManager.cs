using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class ChunkManager : Node3D
{
    private PackedScene chunkScene = ResourceLoader.Load("res://WorldGeneration/Chunk.tscn") as PackedScene;
    static readonly Dictionary<Vector2I, Chunk> chunks = new();
    static readonly Queue<Chunk> generateQueue = new();

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
                generateQueue.Enqueue(chunk);
            }
            else
            {
                Chunk chunk = chunks[chunkPosition];
                chunk.Visit();
            }
        }

        GenerateNextChunk();
    }

    public static void removeChunk(Chunk chunk)
    {
        chunks.Remove(chunk.ChunkPosition);
    }

    private static Task[] generationTasks = new Task[6];
    public static void GenerateNextChunk()
    {
        for (int i = 0; i < generationTasks.Length; i++)
        {
            Task task = generationTasks[i];
            if (generateQueue.Count > 0 && (task == null || task.IsCompleted))
            {
                Chunk chunk = generateQueue.Dequeue();
                generationTasks[i] = Task.Run(() => {
                    chunk.Generate();
                    chunk.Update();
                });
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
