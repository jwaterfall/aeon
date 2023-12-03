using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ChunkManager : Node3D
{
    private PackedScene chunkScene = ResourceLoader.Load("res://WorldGeneration/chunk.tscn") as PackedScene;
    private Dictionary<Vector2I, Chunk> chunks = new();
    private List<Vector2I> chunksToGenerate = new();
    private List<Vector2I> generatedChunks = new();
    private List<Vector2I> chunksToRemove = new();
    private Task[] generateTasks = new Task[OS.GetProcessorCount() - 3];
    private Task renderTask;
    private Task removalTask;

    public void Update(Vector3 playerPosition)
    {
        //RemoveChunks();
        GenerateChunks();
        RenderChunks();

        var nearbyChunkPositions = GetNearbyChunkPositions(playerPosition);

        foreach (var chunkPosition in chunks.Keys)
        {
            if (!nearbyChunkPositions.Contains(chunkPosition))
            {
                chunksToRemove.Add(chunkPosition);
            }
        }

        foreach (var chunkPosition in nearbyChunkPositions)
        {
            if (!chunks.ContainsKey(chunkPosition) && !chunksToGenerate.Contains(chunkPosition))
            {
                lock (chunksToGenerate)
                {
                    chunksToGenerate.Add(chunkPosition);
                }
            }
        }
    }

    private void RemoveChunks()
    {
        if (chunksToRemove.Count > 0 && (removalTask == null || removalTask.IsCompleted))
        {
            var chunkPosition = chunksToRemove[0];

            removalTask = Task.Run(() => {
                lock (chunks)
                {
                    var chunk = chunks[chunkPosition];
                    chunks.Remove(chunkPosition);
                    chunk.QueueFree();
                }
            });
        }
    }

    private void GenerateChunks()
    {
        var terrainGenerator = GetNode<TerrainGenerator>("/root/TerrainGenerator");

        lock (chunksToGenerate) {
            for (int i = 0; i < generateTasks.Length; i++)
            {
                Task task = generateTasks[i];
                if (chunksToGenerate.Count > 0 && (task == null || task.IsCompleted))
                {
                    var chunkPosition = chunksToGenerate[0];
                    chunksToGenerate.RemoveAt(0);

                    Chunk chunk = chunkScene.Instantiate<Chunk>();
                    chunk.SetChunkPosition(chunkPosition);
                    AddChild(chunk);

                    lock (chunks)
                    {
                        chunks[chunkPosition] = chunk;
                    }

                    generateTasks[i] = Task.Run(() =>
                    {
                        chunk.GenerateBlocks(terrainGenerator);

                        lock (generatedChunks)
                        {
                            generatedChunks.Add(chunkPosition);
                        }
                    });
                }
            }
        }
    }

    private void RenderChunks ()
    {
        var chunksToRender = (from chunkPosition in generatedChunks
                              where CanRenderChunk(chunkPosition)
                              select chunkPosition).ToList();

        if (chunksToRender.Count > 0 && (renderTask == null || renderTask.IsCompleted))
        {
            var chunkPosition = chunksToRender[0];

            renderTask = Task.Run(() => {
                var chunk = chunks[chunkPosition];

                var northChunkPosition = chunkPosition + Vector2I.Up;
                var eastChunkPosition = chunkPosition + Vector2I.Right;
                var southChunkPosition = chunkPosition + Vector2I.Down;
                var westChunkPosition = chunkPosition + Vector2I.Left;

                var northChunk = chunks[northChunkPosition];
                var eastChunk = chunks[eastChunkPosition];
                var southChunk = chunks[southChunkPosition];
                var westChunk = chunks[westChunkPosition];

                lock (chunk) lock (northChunk) lock (eastChunk) lock (southChunk) lock (westChunk)
                {
                    chunk.Render(northChunk, eastChunk, southChunk, westChunk);
                }

                lock (generatedChunks)
                {
                    generatedChunks.Remove(chunkPosition);
                }
            });
        }
    }

    private bool CanRenderChunk(Vector2I chunkPosition)
    {
        var northChunkPosition = chunkPosition + Vector2I.Up;
        var eastChunkPosition = chunkPosition + Vector2I.Right;
        var southChunkPosition = chunkPosition + Vector2I.Down;
        var westChunkPosition = chunkPosition + Vector2I.Left;

        var chunk = chunks[chunkPosition];

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

    private IEnumerable<Vector2I> GetNearbyChunkPositions(Vector3 playerPosition)
    {
        var radius = Configuration.CHUNK_LOAD_RADIUS;
        var playerChunkPosition = WorldToChunkPosition(playerPosition);

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

    private Vector2I WorldToChunkPosition(Vector3 worldPosition)
    {
        return new Vector2I(
            Mathf.FloorToInt(worldPosition.X / Configuration.CHUNK_DIMENSION.X),
            Mathf.FloorToInt(worldPosition.Z / Configuration.CHUNK_DIMENSION.Z)
        );
    }
}
