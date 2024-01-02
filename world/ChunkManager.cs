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
        private ConcurrentDictionary<Vector2I, Chunk> chunks = new();
        private ConcurrentQueue<Vector2I> chunksToGenerate = new();
        private ConcurrentQueue<Vector2I> chunksToRender = new();
        private ConcurrentQueue<Vector2I> chunksToRemove = new();
        private Task[] tasks = new Task[OS.GetProcessorCount() - 2];
        private Task renderTask;
        private Vector2I? lastPlayerChunkPosition;

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
                    chunksToRemove.TryDequeue(out Vector2I chunkPosition);

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
                    chunksToGenerate.TryDequeue(out Vector2I chunkPosition);

                    Chunk chunk = chunkScene.Instantiate<Chunk>();
                    chunk.SetChunkPosition(chunkPosition);
                    AddChild(chunk);

                    chunks[chunkPosition] = chunk;

                    tasks[i] = Task.Run(() =>
                    {
                        chunk.GenerateBlocks(terrainGenerator);

                        chunksToRender.Enqueue(chunkPosition);
                    });
                }
            }
        }

        private void RenderNextChunk()
        {
            if (chunksToRender.Count > 0 && (renderTask == null || renderTask.IsCompleted))
            {
                chunksToRender.TryDequeue(out Vector2I chunkPosition);

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

                    var northChunkPosition = chunkPosition + Vector2I.Up;
                    var eastChunkPosition = chunkPosition + Vector2I.Right;
                    var southChunkPosition = chunkPosition + Vector2I.Down;
                    var westChunkPosition = chunkPosition + Vector2I.Left;

                    var northChunk = chunks[northChunkPosition];
                    var eastChunk = chunks[eastChunkPosition];
                    var southChunk = chunks[southChunkPosition];
                    var westChunk = chunks[westChunkPosition];

                    chunk.Render(northChunk, eastChunk, southChunk, westChunk);
                });
            }
        }

        private bool CanRenderChunk(Vector2I chunkPosition)
        {
            var northChunkPosition = chunkPosition + Vector2I.Up;
            var eastChunkPosition = chunkPosition + Vector2I.Right;
            var southChunkPosition = chunkPosition + Vector2I.Down;
            var westChunkPosition = chunkPosition + Vector2I.Left;

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
                chunks[westChunkPosition].generated;
        }

        private IEnumerable<Vector2I> GetNearbyChunkPositions(Vector2I playerChunkPosition)
        {
            var radius = Configuration.CHUNK_LOAD_RADIUS;

            int x = 0, z = 0;
            int dx = 0, dz = -1;

            for (int i = 0; i < Mathf.Pow((2 * radius + 1), 2); i++)
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

}