using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Aeon
{
    public partial class ChunkManager : Node3D
    {
        private PackedScene chunkScene = Godot.ResourceLoader.Load("res://world/chunk.tscn") as PackedScene;
        private ConcurrentDictionary<Vector3I, Chunk> chunks = new();
        private ConcurrentQueue<Vector3I> chunksToGenerate = new();
        private ConcurrentQueue<Vector3I> chunksToRender = new();
        private ConcurrentQueue<Vector3I> chunksToRemove = new();
        private Task[] tasks = new Task[OS.GetProcessorCount() - 2];
        private Task renderTask;
        private Vector3I? lastPlayerChunkPosition;

        public void Update(Vector3 playerPosition)
        {
            RemoveChunks();
            GenerateChunks();
            RenderNextChunk();

            var playerChunkPosition = WorldToChunkPosition(playerPosition);

            if (lastPlayerChunkPosition != null && playerChunkPosition == lastPlayerChunkPosition)
            {
                return;
            }

            lastPlayerChunkPosition = playerChunkPosition;

            var nearbyChunkPositions = GetNearbyChunkPositions(playerChunkPosition);

            foreach (var chunkPosition in chunks.Keys)
            {
                if (!nearbyChunkPositions.Contains(chunkPosition) && !chunksToRemove.Contains(chunkPosition))
                {
                    chunksToRemove.Enqueue(chunkPosition);
                }
            }

            foreach (var chunkPosition in nearbyChunkPositions)
            {
                if (!chunks.ContainsKey(chunkPosition) && !chunksToGenerate.Contains(chunkPosition))
                {
                    chunksToGenerate.Enqueue(chunkPosition);
                }
            }
        }

        private void RemoveChunks()
        {
            for (int i = 0; i < tasks.Length; i++)
            {
                Task task = tasks[i];
                if (chunksToRemove.Count > 0 && (task == null || task.IsCompleted))
                {
                    chunksToRemove.TryDequeue(out Vector3I chunkPosition);

                    tasks[i] = Task.Run(() => {
                        chunks.Remove(chunkPosition, out Chunk chunk);
                        chunk.QueueFree();
                    });
                }
            }
        }

        private void GenerateChunks()
        {
            var terrainGenerator = GetNode<TerrainGenerator>("/root/TerrainGenerator");

            for (int i = 0; i < tasks.Length; i++)
            {
                Task task = tasks[i];
                if (chunksToGenerate.Count > 0 && (task == null || task.IsCompleted))
                {
                    chunksToGenerate.TryDequeue(out Vector3I chunkPosition);

                    Chunk chunk = chunkScene.Instantiate<Chunk>();
                    chunk.SetChunkPosition(chunkPosition);
                    AddChild(chunk);

                    chunks[chunkPosition] = chunk;

                    tasks[i] = Task.Run(() =>
                    {
                        chunk.GenerateBlocks(terrainGenerator, WorldPresets.Instance.Get("default"));

                        chunksToRender.Enqueue(chunkPosition);
                    });
                }
            }
        }

        private void RenderNextChunk()
        {
            if (chunksToRender.Count > 0 && (renderTask == null || renderTask.IsCompleted))
            {
                chunksToRender.TryDequeue(out Vector3I chunkPosition);

                if (!chunks.ContainsKey(chunkPosition))
                {
                    return;
                }

                if (!CanRenderChunk(chunkPosition))
                {
                    chunksToRender.Enqueue(chunkPosition);
                    return;
                }

                renderTask = Task.Run(() => {
                    if (!chunks.ContainsKey(chunkPosition))
                    {
                        return;
                    }

                    var chunk = chunks[chunkPosition];

                    var northChunkPosition = chunkPosition + Vector3I.Forward;
                    var eastChunkPosition = chunkPosition + Vector3I.Right;
                    var southChunkPosition = chunkPosition + Vector3I.Back;
                    var westChunkPosition = chunkPosition + Vector3I.Left;
                    var upChunkPosition = chunkPosition + Vector3I.Up;
                    var downChunkPosition = chunkPosition + Vector3I.Down;

                    var northChunk = chunks[northChunkPosition];
                    var eastChunk = chunks[eastChunkPosition];
                    var southChunk = chunks[southChunkPosition];
                    var westChunk = chunks[westChunkPosition];
                    var upChunk = chunks[upChunkPosition];
                    var downChunk = chunks[downChunkPosition];

                    chunk.Render(northChunk, eastChunk, southChunk, westChunk, upChunk, downChunk);
                });
            }
        }

        private bool CanRenderChunk(Vector3I chunkPosition)
        {
            var northChunkPosition = chunkPosition + Vector3I.Forward;
            var eastChunkPosition = chunkPosition + Vector3I.Right;
            var southChunkPosition = chunkPosition + Vector3I.Back;
            var westChunkPosition = chunkPosition + Vector3I.Left;
            var upChunkPosition = chunkPosition + Vector3I.Up;
            var downChunkPosition = chunkPosition + Vector3I.Down;

            return
                chunks.ContainsKey(chunkPosition) &&
                chunks[chunkPosition].generated &&
                chunks.ContainsKey(northChunkPosition) &&
                chunks[northChunkPosition].generated &&
                chunks.ContainsKey(eastChunkPosition) &&
                chunks[eastChunkPosition].generated &&
                chunks.ContainsKey(southChunkPosition) &&
                chunks[southChunkPosition].generated &&
                chunks.ContainsKey(westChunkPosition) &&
                chunks[westChunkPosition].generated &&
                chunks.ContainsKey(upChunkPosition) &&
                chunks[upChunkPosition].generated &&
                chunks.ContainsKey(downChunkPosition) &&
                chunks[downChunkPosition].generated;
        }

        private IEnumerable<Vector3I> GetNearbyChunkPositions(Vector3I playerChunkPosition)
        {
            var radius = Configuration.CHUNK_LOAD_RADIUS;

            int x = 0, z = 0;
            int dx = 0, dz = -1;

            for (int i = 0; i < Mathf.Pow((2 * radius + 1), 2); i++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    var chunkPosition = new Vector3I(x + playerChunkPosition.X, y + playerChunkPosition.Y, z + playerChunkPosition.Z);
                    if (((Vector3)playerChunkPosition).DistanceTo((Vector3)chunkPosition) <= radius)
                    {
                        yield return chunkPosition;
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

        private Vector3I WorldToChunkPosition(Vector3 worldPosition)
        {
            return new Vector3I(
                Mathf.FloorToInt(worldPosition.X / Configuration.CHUNK_DIMENSION.X),
                Mathf.FloorToInt(worldPosition.Y / Configuration.CHUNK_DIMENSION.Y),
                Mathf.FloorToInt(worldPosition.Z / Configuration.CHUNK_DIMENSION.Z)
            );
        }
    }

}