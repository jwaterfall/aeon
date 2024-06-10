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
        private readonly ConcurrentDictionary<Vector2I, Chunk> _chunks = new();
        private ConcurrentQueue<Vector2I> _chunksToGenerate = new();
        private Queue<Vector2I> _chunksToRemove = new();
        private Vector2I? _lastPlayerChunkPosition;
        private Task[] _tasks = new Task[OS.GetProcessorCount() - 2];
        private Task _renderTask;

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

            var orderedChunks = _chunks.Values
                .OrderBy(chunk => ((Vector2)chunk.ChunkPosition).DistanceTo(playerChunkPosition));

            var chunksToDecorate = orderedChunks
                .Where(chunk => chunk.IsGenerated && !chunk.IsDecorated)
                .ToList();

            DecorateChunks(chunksToDecorate, terrainGenerator);

            var chunksToRender = orderedChunks
                .Where(chunk => chunk.IsGenerated && chunk.IsDecorated && !chunk.IsRendered && CanRenderChunk(chunk.ChunkPosition))
                .Select(chunk => chunk.ChunkPosition)
                .ToList();

            RenderChunks(chunksToRender);

            GenerateChunks(playerChunkPosition, terrainGenerator);

            foreach (var chunk in _chunks.Values)
            {
                chunk.Update();
            }

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

                var nearbyChunkPositions = new HashSet<Vector2I>(GetNearbyChunkPositions(playerChunkPosition));

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

        private void GenerateChunks(Vector2I playerChunkPosition, TerrainGenerator terrainGenerator)
        {
            var sortedChunksToGenerate = _chunksToGenerate
                .OrderBy(chunkPosition => ((Vector2)chunkPosition).DistanceTo(playerChunkPosition))
                .ToList();

            for (int i = 0; i < _tasks.Length; i++)
            {
                var task = _tasks[i];
                if (sortedChunksToGenerate.Count > 0 && (task == null || task.IsCompleted))
                {
                    var chunkPosition = sortedChunksToGenerate[0];
                    sortedChunksToGenerate.RemoveAt(0);
                    _chunksToGenerate = new (sortedChunksToGenerate); // Update the queue

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
                            chunk.Generate(terrainGenerator, WorldPresets.Instance.Get("default"));
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

        private void DecorateChunks(List<Chunk> chunksToDecorate, TerrainGenerator terrainGenerator)
        {
            for (int i = 0; i < _tasks.Length; i++)
            {
                var task = _tasks[i];
                if (chunksToDecorate.Count > 0 && (task == null || task.IsCompleted))
                {
                    var chunk = chunksToDecorate[0];
                    chunksToDecorate.RemoveAt(0);

                    _tasks[i] = Task.Run(() =>
                    {
                        chunk.Decorate(terrainGenerator, WorldPresets.Instance.Get("default"));
                    });
                }
            }
        }

        private void RenderChunks(List<Vector2I> chunksToRender)
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

        private void RenderChunk(Vector2I chunkPosition)
        {
            var chunk = _chunks[chunkPosition];
            chunk.Render();
        }

        private bool CanRenderChunk(Vector2I chunkPosition)
        {
            var westChunkPosition = chunkPosition + Vector2I.Up;
            var southChunkPosition = chunkPosition + Vector2I.Right;
            var eastChunkPosition = chunkPosition + Vector2I.Down;
            var northChunkPosition = chunkPosition + Vector2I.Left;

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
                _chunks[westChunkPosition].IsGenerated;
        }

        private IEnumerable<Vector2I> GetNearbyChunkPositions(Vector2I playerChunkPosition)
        {
            var radius = Configuration.CHUNK_LOAD_RADIUS;

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    var chunkPosition = new Vector2I(x + playerChunkPosition.X, z + playerChunkPosition.Y);
                    if (((Vector2)playerChunkPosition).DistanceTo(chunkPosition) <= radius)
                    {
                        yield return chunkPosition;
                    }
                }
            }
        }

        public void SetBlock(Vector3I worldPosition, BlockType block)
        {
            var chunkPosition = WorldToChunkPosition(worldPosition);
            var localPosition = WorldToLocalPosition(worldPosition);

            if (_chunks.ContainsKey(chunkPosition))
            {
                _chunks[chunkPosition].SetBlock(localPosition, block);
            }
        }

        public BlockType GetBlock(Vector3I worldPosition)
        {
            var chunkPosition = WorldToChunkPosition(worldPosition);

            if (_chunks.ContainsKey(chunkPosition))
            {
                return _chunks[chunkPosition].GetBlock(WorldToLocalPosition(worldPosition));
            }

            return null;
        }

        public Vector3I GetLightLevel(Vector3I worldPosition)
        {
            var chunkPosition = WorldToChunkPosition(worldPosition);

            if (_chunks.ContainsKey(chunkPosition))
            {
                return _chunks[chunkPosition].GetLightLevel(WorldToLocalPosition(worldPosition));
            }

            return Vector3I.Zero;
        }

        public void SetLightLevel(Vector3I worldPosition, Vector3I lightLevel)
        {
            var chunkPosition = WorldToChunkPosition(worldPosition);
            var localPosition = WorldToLocalPosition(worldPosition);

            if (_chunks.ContainsKey(chunkPosition))
            {
                _chunks[chunkPosition].SetLightLevel(localPosition, lightLevel);
            }
        }

        private Vector2I WorldToChunkPosition(Vector3 worldPosition)
        {
            return new Vector2I(
                Mathf.FloorToInt(worldPosition.X / Configuration.CHUNK_DIMENSION.X),
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
                RenderChunk(chunkPosition + Vector2I.Left);
            }
            else if (localPosition.X > Configuration.CHUNK_DIMENSION.X - 2)
            {
                RenderChunk(chunkPosition + Vector2I.Right);
            }
            if (localPosition.Z < 1)
            {
                RenderChunk(chunkPosition + Vector2I.Up);
            }
            else if (localPosition.Z > Configuration.CHUNK_DIMENSION.Z - 2)
            {
                RenderChunk(chunkPosition + Vector2I.Down);
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
