using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ChunkManager : Node3D
{
    private PackedScene chunkScene = ResourceLoader.Load("res://WorldGeneration/Chunk.tscn") as PackedScene;
    private readonly Dictionary<Vector2I, Chunk> chunks = new();
    private readonly List<Vector2I> chunksToGenerate = new();
    private readonly List<Vector2I> activeChunks = new();
    private readonly List<Vector2I> staleChunks = new();
    private Task[] generationTasks = new Task[6];
    private Vector3? previousPlayerPosition;

    public void Update(Vector3 playerPosition)
    {
        GenerateNextChunk();

        // If the player hasn't moved skip new chunk generation
        if (previousPlayerPosition == playerPosition)
        {
            return;
        }

        previousPlayerPosition = playerPosition;

        var newChunkPositions = GetNearbyChunkPositions(playerPosition);

        // If the a chunk waiting to be generated is no longer in range stop it from trying to generate
        List<Vector2I> chunksToStopGenerating = new();
        foreach (var chunkPosition in chunksToGenerate)
        {
            if (!newChunkPositions.Contains(chunkPosition))
            {
                chunksToStopGenerating.Add(chunkPosition);
            }
        }

        foreach (var chunkPosition in chunksToStopGenerating)
        {
            chunksToGenerate.Remove(chunkPosition);
        }

        // If an active chunk is no longer in range set it to stale
        List<Vector2I> chunksToMakeStale = new();
        foreach (var chunkPosition in activeChunks)
        {
            if (!newChunkPositions.Contains(chunkPosition))
            {
                chunksToMakeStale.Add(chunkPosition);
            }
        }

        foreach (var chunkPosition in chunksToMakeStale)
        {
            activeChunks.Remove(chunkPosition);
            staleChunks.Add(chunkPosition);
            var chunk = chunks[chunkPosition];
            chunk.Visible = false;
        }

        // Generate or restore chunks in range
        foreach (var chunkPosition in newChunkPositions)
        {
            if (staleChunks.Contains(chunkPosition))
            {
               staleChunks.Remove(chunkPosition);
               activeChunks.Add(chunkPosition);
                var chunk = chunks[chunkPosition];
                chunk.Visible = true;
            } 
            else if (!activeChunks.Contains(chunkPosition) && !chunksToGenerate.Contains(chunkPosition))
            {
                chunksToGenerate.Add(chunkPosition);
            }
        }
    }

    public void GenerateNextChunk()
    {
        for (int i = 0; i < generationTasks.Length; i++)
        {
            Task task = generationTasks[i];
            if (chunksToGenerate.Count > 0 && (task == null || task.IsCompleted))
            {
                var chunkPosition = chunksToGenerate[0];
                chunksToGenerate.RemoveAt(0);

                Chunk chunk = chunkScene.Instantiate<Chunk>();
                chunk.SetChunkPosition(chunkPosition);
                AddChild(chunk);
                chunks.Add(chunkPosition, chunk);
                activeChunks.Add(chunkPosition);

                generationTasks[i] = Task.Run(() => {
                    chunk.Generate();
                    chunk.Update();
                });
            }
        }
    }

    public IEnumerable<Vector2I> GetNearbyChunkPositions(Vector3 playerPosition)
    {
        var radius = Configuration.CHUNK_LOAD_RADIUS;
        var playerChunkPosition = (Vector2I)(new Vector2(playerPosition.X, playerPosition.Z) / new Vector2I(Configuration.CHUNK_DIMENSION.X, Configuration.CHUNK_DIMENSION.Z)).Floor();

        int x = 0, z = 0;
        int dx = 0, dz = -1;

        for (int i = 0; i < Math.Pow((2 * radius + 1), 2); i++)
        {
            var chunkPosition = new Vector2I(x + playerChunkPosition.X, z + playerChunkPosition.Y);
            if (((Vector2)playerChunkPosition).DistanceTo((Vector2)chunkPosition) <= radius)
            {
                yield return chunkPosition;
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
