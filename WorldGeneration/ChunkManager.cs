using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class ChunkManager : Node3D
{
    private PackedScene chunkScene = ResourceLoader.Load("res://WorldGeneration/Chunk.tscn") as PackedScene;
    static readonly Dictionary<Vector3I, Chunk> chunks = new();
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

    public IEnumerable<Vector3I> GetNearbyChunkPositions(Vector3 playerPosition)
    {
        var radius = Configuration.CHUNK_LOAD_RADIUS;
        var playerChunkPosition = (Vector3I)(playerPosition / Configuration.CHUNK_DIMENSION).Round();

        int x = 0, z = 0;
        int dx = 0, dz = -1;

        for (int i = 0; i < Math.Pow((2 * radius + 1), 2); i++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                // Check if the absolute values of x, y, and z are all less than or equal to the respective radii
                if (-radius <= x && x <= radius &&
                    -radius <= y && y <= radius &&
                    -radius <= z && z <= radius)
                {
                    yield return new Vector3I(x + playerChunkPosition.X, y + playerChunkPosition.Y, z + playerChunkPosition.Z);
                }
            }

            // If at a corner, change direction
            if (x == z || (x < 0 && x == -z) || (x > 0 && x == 1 - z))
            {
                int temp = dx;
                dx = -dz;
                dz = temp;
            }

            // Move to the next position in the spiral
            x += dx;
            z += dz;
        }
    }
}
