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
    private Task[] generationTasks = new Task[4];
    private Task[] renderTasks = new Task[1];
    private Vector3? previousPlayerPosition;

    public void Update(Vector3 playerPosition)
    {
        GenerateNextChunk();
        RenderChunks();

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

        if (staleChunks.Count > Configuration.MAX_STALE_CHUNKS)
        {
            var chunkPosition = staleChunks[0];
            staleChunks.RemoveAt(0);

            var chunk = chunks[chunkPosition];
            chunks.Remove(chunkPosition);

            chunk.QueueFree();
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
                    chunk.GenerateBlocks();
                });
            }
        }
    }

    private bool CanRenderChunk(Chunk chunk)
    {
        var northChunkPosition = chunk.ChunkPosition + Vector2I.Up;
        var eastChunkPosition = chunk.ChunkPosition + Vector2I.Right;
        var southChunkPosition = chunk.ChunkPosition + Vector2I.Down;
        var westChunkPosition = chunk.ChunkPosition + Vector2I.Left;

        return chunk.generated && !chunk.rendered &&
            chunks.ContainsKey(northChunkPosition) &&
            chunks.ContainsKey(eastChunkPosition) &&
            chunks.ContainsKey(southChunkPosition) &&
            chunks.ContainsKey(westChunkPosition) &&
            chunks[northChunkPosition].generated &&
            chunks[eastChunkPosition].generated &&
            chunks[southChunkPosition].generated &&
            chunks[westChunkPosition].generated;
    }

    public void RenderChunks()
    {
        var chunksToRender = (from chunk in chunks.Values where (CanRenderChunk(chunk)) select chunk).ToList();

        for (int i = 0; i < renderTasks.Length; i++)
        {
            Task task = renderTasks[i];
            if (chunksToRender.Count > 0 && (task == null || task.IsCompleted))
            {
                var chunk = chunksToRender[0];

                var northChunkPosition = chunk.ChunkPosition + Vector2I.Up;
                var eastChunkPosition = chunk.ChunkPosition + Vector2I.Right;
                var southChunkPosition = chunk.ChunkPosition + Vector2I.Down;
                var westChunkPosition = chunk.ChunkPosition + Vector2I.Left;

                chunksToRender.RemoveAt(0);

                renderTasks[i] = Task.Run(() => {
                    chunk.Render(chunks[northChunkPosition], chunks[eastChunkPosition], chunks[southChunkPosition], chunks[westChunkPosition]);
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
