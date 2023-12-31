using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Aeon
{
    public partial class ChunkManager : Node3D
    {
        private PackedScene chunkScene = ResourceLoader.Load("res://world/Chunk.tscn") as PackedScene;
        private ConcurrentDictionary<Vector3I, Chunk> chunks = new();
        private ConcurrentQueue<Vector3I> chunksToGenerate = new();
        private ConcurrentQueue<Vector3I> chunksToRemove = new();
        private HashSet<Vector3I> loadedChunks = new();
        private Task[] tasks = new Task[OS.GetProcessorCount() - 2];
        private Task renderTask;
        private Vector3I? lastPlayerChunkPosition;

        public void Update(Vector3 playerPosition)
        {
            var playerChunkPosition = WorldToChunkPosition(playerPosition);

            List<Vector3I> chunksToRender = chunks.Values
                .OrderBy(chunk => ((Vector3)chunk.chunkPosition).DistanceTo(playerChunkPosition))
                .Where(chunk => chunk.generated && !chunk.rendered && CanRenderChunk(chunk.chunkPosition))
                .Select(chunk => chunk.chunkPosition)
                .ToList();

            RemoveChunks();
            GenerateChunks();
            RenderChunks(chunksToRender);

            if (lastPlayerChunkPosition != playerChunkPosition)
            {
                lastPlayerChunkPosition = playerChunkPosition;
                UpdateLoadedChunks(playerChunkPosition);
            }
        }

        private void UpdateLoadedChunks(Vector3I playerChunkPosition)
        {
            HashSet<Vector3I> nearbyChunkPositions = new (GetNearbyChunkPositions(playerChunkPosition));

            // Remove chunks that are no longer nearby
            foreach (var chunkPosition in loadedChunks.Except(nearbyChunkPositions).ToList())
            {
                if (!chunksToRemove.Contains(chunkPosition))
                {
                    chunksToRemove.Enqueue(chunkPosition);
                    loadedChunks.Remove(chunkPosition);
                }
            }

            // Add chunks that are now nearby
            foreach (var chunkPosition in nearbyChunkPositions.Except(loadedChunks))
            {
                if (!chunks.ContainsKey(chunkPosition) && !chunksToGenerate.Contains(chunkPosition))
                {
                    chunksToGenerate.Enqueue(chunkPosition);
                    loadedChunks.Add(chunkPosition);
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
                        if (!chunk.generated)
                        {
                            chunk.GenerateBlocks(terrainGenerator, WorldPresets.Instance.Get("default"));
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
                    if (!chunks.ContainsKey(chunkPosition)) return;

                    var chunk = chunks[chunkPosition];

                    if (!chunk.generated || chunk.rendered) return;

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
                    if (((Vector3)playerChunkPosition).DistanceTo(chunkPosition) <= radius)
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