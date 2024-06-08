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
        private readonly PackedScene _chunkScene = ResourceLoader.Load("res://world/Chunk.tscn") as PackedScene;
        private readonly ConcurrentDictionary<Vector3I, Chunk> _chunks = new();
        private ConcurrentQueue<Vector3I> _chunksToGenerate = new();
        private Queue<Vector3I> _chunksToRemove = new();
        private Task[] _tasks = new Task[OS.GetProcessorCount() - 2];
        private Task _renderTask;
        private Vector3I? _lastPlayerChunkPosition;

        private readonly Stopwatch _renderingStopwatch = new();
        private double _totalGenerationTime = 0;
        private double _totalRenderingTime = 0;
        private int _generationCount = 0;
        private int _renderingCount = 0;
        private double _timeAccumulator = 0;

        public override void _Ready()
        {
            var customSignals = GetNode<CustomSignals>("/root/CustomSignals");
            customSignals.BreakBlock += BreakBlock;
            customSignals.PlaceBlock += PlaceBlock;
        }

        public override void _Process(double delta)
        {
            _timeAccumulator += delta;

            if (_timeAccumulator >= 5.0f)
            {
                LogAverages();
                _timeAccumulator = 0;
            }
        }

        public void Update(Vector3 playerPosition, TerrainGenerator terrainGenerator)
        {
            var playerChunkPosition = WorldToChunkPosition(playerPosition);

            var chunksToRender = _chunks.Values
                .OrderBy(chunk => ((Vector3)chunk.ChunkPosition).DistanceTo(playerChunkPosition))
                .Where(chunk => chunk.IsGenerated && !chunk.IsRendered && CanRenderChunk(chunk.ChunkPosition))
                .Select(chunk => chunk.ChunkPosition)
                .ToList();

            GenerateChunks(playerChunkPosition, terrainGenerator);
            RenderChunks(chunksToRender);

            for (int i = 0; i < _chunksToRemove.Count; i++)
            {
                var chunkPosition = _chunksToRemove.Dequeue();

                if (_chunks.TryRemove(chunkPosition, out var chunk))
                {
                    chunk.QueueFree();
                }
                else
                {
                    _chunksToRemove.Enqueue(chunkPosition);
                }
            }

            if (_lastPlayerChunkPosition != playerChunkPosition)
            {
                _lastPlayerChunkPosition = playerChunkPosition;

                var nearbyChunkPositions = new HashSet<Vector3I>(GetNearbyChunkPositions(playerChunkPosition));

                // Remove chunks that are no longer nearby
                foreach (var chunkPosition in _chunks.Keys.Except(nearbyChunkPositions).ToList())
                {
                    if (_chunks.TryRemove(chunkPosition, out var chunk))
                    {
                        chunk.QueueFree();
                    }
                    else
                    {
                        _chunksToRemove.Enqueue(chunkPosition);
                    }
                }

                // Add chunks that are now nearby
                foreach (var chunkPosition in nearbyChunkPositions.Except(_chunks.Keys))
                {
                    if (!_chunks.ContainsKey(chunkPosition) && !_chunksToGenerate.Contains(chunkPosition))
                    {
                        _chunksToGenerate.Enqueue(chunkPosition);
                    }
                }
            }
        }

        private void GenerateChunks(Vector3I playerChunkPosition, TerrainGenerator terrainGenerator)
        {
            var sortedChunksToGenerate = _chunksToGenerate
                .OrderBy(chunkPosition => ((Vector3)chunkPosition).DistanceTo(playerChunkPosition))
                .ToList();

            for (int i = 0; i < _tasks.Length; i++)
            {
                var task = _tasks[i];
                if (sortedChunksToGenerate.Count > 0 && (task == null || task.IsCompleted))
                {
                    var chunkPosition = sortedChunksToGenerate[0];
                    sortedChunksToGenerate.RemoveAt(0);
                    _chunksToGenerate = new ConcurrentQueue<Vector3I>(sortedChunksToGenerate); // Update the queue

                    var chunk = _chunkScene.Instantiate<Chunk>();
                    chunk.Initialize(this, chunkPosition);
                    AddChild(chunk);

                    _chunks[chunkPosition] = chunk;

                    _tasks[i] = Task.Run(() =>
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
                            _totalGenerationTime += stopwatch.Elapsed.TotalMilliseconds;
                            _generationCount++;
                        }
                    });
                }
            }
        }

        private void RenderChunks(List<Vector3I> chunksToRender)
        {
            if (chunksToRender.Count > 0 && (_renderTask == null || _renderTask.IsCompleted))
            {
                var chunkPosition = chunksToRender[0];
                chunksToRender.RemoveAt(0);

                _renderTask = Task.Run(() =>
                {
                    _renderingStopwatch.Restart();
                    RenderChunk(chunkPosition);
                    _renderingStopwatch.Stop();

                    lock (this)
                    {
                        _totalRenderingTime += _renderingStopwatch.Elapsed.TotalMilliseconds;
                        _renderingCount++;
                    }
                });
            }
        }

        private void RenderChunk(Vector3I chunkPosition)
        {
            var chunk = _chunks[chunkPosition];
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
                _chunks.ContainsKey(chunkPosition) &&
                _chunks[chunkPosition].IsGenerated &&
                _chunks.ContainsKey(northChunkPosition) &&
                _chunks[northChunkPosition].IsGenerated &&
                _chunks.ContainsKey(eastChunkPosition) &&
                _chunks[eastChunkPosition].IsGenerated &&
                _chunks.ContainsKey(southChunkPosition) &&
                _chunks[southChunkPosition].IsGenerated &&
                _chunks.ContainsKey(westChunkPosition) &&
                _chunks[westChunkPosition].IsGenerated &&
                _chunks.ContainsKey(upChunkPosition) &&
                _chunks[upChunkPosition].IsGenerated &&
                _chunks.ContainsKey(downChunkPosition) &&
                _chunks[downChunkPosition].IsGenerated;
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

            if (_chunks.ContainsKey(chunkPosition))
            {
                return _chunks[chunkPosition].GetBlock(localPosition);
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

            _chunks[chunkPosition].PlaceBlock(localPosition, BlockTypes.Instance.Get(block));
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
            if (_generationCount > 0)
            {
                var averageGenerationTime = _totalGenerationTime / _generationCount;
                GD.Print($"Average Generation Time: {averageGenerationTime} ms");
            }

            if (_renderingCount > 0)
            {
                var averageRenderingTime = _totalRenderingTime / _renderingCount;
                GD.Print($"Average Rendering Time: {averageRenderingTime} ms");
            }
        }
    }
}
