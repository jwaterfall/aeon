using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Aeon
{
    public partial class ChunkManager : Node3D
    {
        private PackedScene chunkScene = ResourceLoader.Load("res://world/Chunk.tscn") as PackedScene;
        private ConcurrentDictionary<Vector3I, Chunk> chunks = new();
        private ConcurrentQueue<Vector3I> chunksToGenerate = new();
        private ConcurrentDictionary<Vector3I, bool> loadedChunks = new();
        private Task[] tasks = new Task[OS.GetProcessorCount() - 2];
        private Task renderTask;
        private Vector3I? lastPlayerChunkPosition;

        private Stopwatch renderingStopwatch = new Stopwatch();
        private double totalGenerationTime = 0;
        private double totalRenderingTime = 0;
        private int generationCount = 0;
        private int renderingCount = 0;
        private double timeAccumulator = 0;

        public override void _Ready()
        {
            var customSignals = GetNode<CustomSignals>("/root/CustomSignals");
            customSignals.BreakBlock += BreakBlock;
            customSignals.PlaceBlock += PlaceBlock;
        }

        public override void _Process(double delta)
        {
            timeAccumulator += delta;

            if (timeAccumulator >= 5.0f)
            {
                LogAverages();
                timeAccumulator = 0;
            }
        }

        public void Update(Vector3 playerPosition, TerrainGenerator terrainGenerator)
        {
            var playerChunkPosition = WorldToChunkPosition(playerPosition);

            List<Vector3I> chunksToRender = chunks.Values
                .OrderBy(chunk => ((Vector3)chunk.ChunkPosition).DistanceTo(playerChunkPosition))
                .Where(chunk => chunk.IsGenerated && !chunk.IsRendered && CanRenderChunk(chunk.ChunkPosition))
                .Select(chunk => chunk.ChunkPosition)
                .ToList();

            GenerateChunks(playerChunkPosition, terrainGenerator);
            RenderChunks(chunksToRender);

            if (lastPlayerChunkPosition != playerChunkPosition)
            {
                lastPlayerChunkPosition = playerChunkPosition;

                HashSet<Vector3I> nearbyChunkPositions = new(GetNearbyChunkPositions(playerChunkPosition));

                // Remove chunks that are no longer nearby
                foreach (var chunkPosition in loadedChunks.Keys.Except(nearbyChunkPositions).ToList())
                {
                    if (loadedChunks.TryRemove(chunkPosition, out _))
                    {
                        if (chunks.TryRemove(chunkPosition, out Chunk chunk))
                        {
                            chunk.QueueFree();
                        }
                        else
                        {
                            loadedChunks.TryAdd(chunkPosition, true);
                        }
                    }
                }

                // Add chunks that are now nearby
                foreach (var chunkPosition in nearbyChunkPositions.Except(loadedChunks.Keys))
                {
                    if (!chunks.ContainsKey(chunkPosition) && !chunksToGenerate.Contains(chunkPosition))
                    {
                        chunksToGenerate.Enqueue(chunkPosition);
                        loadedChunks[chunkPosition] = true;
                    }
                }
            }
        }

        private void GenerateChunks(Vector3I playerChunkPosition, TerrainGenerator terrainGenerator)
        {
            var sortedChunksToGenerate = chunksToGenerate
                .OrderBy(chunkPosition => ((Vector3)chunkPosition).DistanceTo(playerChunkPosition))
                .ToList();

            for (int i = 0; i < tasks.Length; i++)
            {
                Task task = tasks[i];
                if (sortedChunksToGenerate.Count > 0 && (task == null || task.IsCompleted))
                {
                    Vector3I chunkPosition = sortedChunksToGenerate[0];
                    sortedChunksToGenerate.RemoveAt(0);
                    chunksToGenerate = new ConcurrentQueue<Vector3I>(sortedChunksToGenerate); // Update the queue

                    Chunk chunk = chunkScene.Instantiate<Chunk>();
                    chunk.Initialize(this, chunkPosition);
                    AddChild(chunk);

                    chunks[chunkPosition] = chunk;

                    tasks[i] = Task.Run(() =>
                    {
                        var stopwatch = new Stopwatch();
                        stopwatch.Start();

                        if (!chunk.IsGenerated)
                        {
                            chunk.GenerateBlocks(terrainGenerator, WorldPresets.Instance.Get("default"));
                        }

                        stopwatch.Stop();

                        lock (this)
                        {
                            totalGenerationTime += stopwatch.Elapsed.TotalMilliseconds;
                            generationCount++;
                        }
                    });
                }
            }
        }

        private void RenderChunks(List<Vector3I> chunksToRender)
        {
            if (chunksToRender.Count > 0 && (renderTask == null || renderTask.IsCompleted))
            {
                var chunkPosition = chunksToRender[0];
                chunksToRender.RemoveAt(0);

                renderTask = Task.Run(() =>
                {
                    renderingStopwatch.Restart();
                    RenderChunk(chunkPosition);
                    renderingStopwatch.Stop();

                    lock (this)
                    {
                        totalRenderingTime += renderingStopwatch.Elapsed.TotalMilliseconds;
                        renderingCount++;
                    }
                });
            }
        }

        private void RenderChunk(Vector3I chunkPosition)
        {
            var chunk = chunks[chunkPosition];
            chunk.Render();
        }

        private bool CanRenderChunk(Vector3I chunkPosition)
        {
            var westChunkPosition = chunkPosition + Vector3I.Forward;
            var southChunkPosition = chunkPosition + Vector3I.Right;
            var eastChunkPosition = chunkPosition + Vector3I.Back;
            var northChunkPosition = chunkPosition + Vector3I.Left;
            var upChunkPosition = chunkPosition + Vector3I.Up;
            var downChunkPosition = chunkPosition + Vector3I.Down;

            return
                chunks.ContainsKey(chunkPosition) &&
                chunks[chunkPosition].IsGenerated &&
                chunks.ContainsKey(northChunkPosition) &&
                chunks[northChunkPosition].IsGenerated &&
                chunks.ContainsKey(eastChunkPosition) &&
                chunks[eastChunkPosition].IsGenerated &&
                chunks.ContainsKey(southChunkPosition) &&
                chunks[southChunkPosition].IsGenerated &&
                chunks.ContainsKey(westChunkPosition) &&
                chunks[westChunkPosition].IsGenerated &&
                chunks.ContainsKey(upChunkPosition) &&
                chunks[upChunkPosition].IsGenerated &&
                chunks.ContainsKey(downChunkPosition) &&
                chunks[downChunkPosition].IsGenerated;
        }

        private IEnumerable<Vector3I> GetNearbyChunkPositions(Vector3I playerChunkPosition)
        {
            var radius = Configuration.CHUNK_LOAD_RADIUS;

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        var chunkPosition = new Vector3I(x + playerChunkPosition.X, y + playerChunkPosition.Y, z + playerChunkPosition.Z);
                        if (((Vector3)playerChunkPosition).DistanceTo(chunkPosition) <= radius)
                        {
                            yield return chunkPosition;
                        }
                    }
                }
            }   
        }

        public BlockType GetBlock(Vector3I worldPosition)
        {
            var chunkPosition = WorldToChunkPosition(worldPosition);
            var localPosition = WorldToLocalPosition(worldPosition);

            if (chunks.ContainsKey(chunkPosition))
            {
                return chunks[chunkPosition].GetBlock(localPosition);
            }

            return null;
        }

        private Vector3I WorldToChunkPosition(Vector3 worldPosition)
        {
            return new Vector3I(
                Mathf.FloorToInt(worldPosition.X / Configuration.CHUNK_DIMENSION.X),
                Mathf.FloorToInt(worldPosition.Y / Configuration.CHUNK_DIMENSION.Y),
                Mathf.FloorToInt(worldPosition.Z / Configuration.CHUNK_DIMENSION.Z)
            );
        }

        private Vector3I WorldToLocalPosition(Vector3 worldPosition)
        {
            int x = (int)((worldPosition.X % Configuration.CHUNK_DIMENSION.X + Configuration.CHUNK_DIMENSION.X) % Configuration.CHUNK_DIMENSION.X);
            int y = (int)((worldPosition.Y % Configuration.CHUNK_DIMENSION.Y + Configuration.CHUNK_DIMENSION.Y) % Configuration.CHUNK_DIMENSION.Y);
            int z = (int)((worldPosition.Z % Configuration.CHUNK_DIMENSION.Z + Configuration.CHUNK_DIMENSION.Z) % Configuration.CHUNK_DIMENSION.Z);

            return new Vector3I(x, y, z);
        }

        private void BreakBlock(Vector3I worldPosition)
        {
            PlaceBlock(worldPosition, "air");
        }

        private void PlaceBlock(Vector3I worldPosition, string block)
        {
            var chunkPosition = WorldToChunkPosition(worldPosition);
            var localPosition = WorldToLocalPosition(worldPosition);

            chunks[chunkPosition].PlaceBlock(localPosition, BlockTypes.Instance.Get(block));
            RenderChunk(chunkPosition);

            if (localPosition.X < 1)
            {
                RenderChunk(chunkPosition + Vector3I.Left);
            }
            else if (localPosition.X > Configuration.CHUNK_DIMENSION.X - 2)
            {
                RenderChunk(chunkPosition + Vector3I.Right);
            }
            if (localPosition.Y < 1)
            {
                RenderChunk(chunkPosition + Vector3I.Down);
            }
            else if (localPosition.Y > Configuration.CHUNK_DIMENSION.Y - 2)
            {
                RenderChunk(chunkPosition + Vector3I.Up);
            }
            if (localPosition.Z < 1)
            {
                RenderChunk(chunkPosition + Vector3I.Forward);
            }
            else if (localPosition.Z > Configuration.CHUNK_DIMENSION.Z - 2)
            {
                RenderChunk(chunkPosition + Vector3I.Back);
            }
        }

        private void LogAverages()
        {
            if (generationCount > 0)
            {
                double averageGenerationTime = totalGenerationTime / generationCount;
                GD.Print($"Average Generation Time: {averageGenerationTime} ms");
            }

            if (renderingCount > 0)
            {
                double averageRenderingTime = totalRenderingTime / renderingCount;
                GD.Print($"Average Rendering Time: {averageRenderingTime} ms");
            }
        }
    }
}
