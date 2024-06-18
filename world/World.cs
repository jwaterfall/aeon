using Godot;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Aeon
{
    /// <summary>
    /// Class <c>World</c> is responsible for managing the generation, decoration, rendering, and removal of chunks. It also provides methods for getting and setting blocks and light levels.
    /// </summary>
    public partial class World : Node3D
    {
        private Player _player;
        private TerrainGenerator _terrainGenerator;
        private Vector3I? _lastPlayerChunkPosition;
        private readonly PackedScene _chunkScene = ResourceLoader.Load("res://world/Chunk.tscn") as PackedScene;

        private readonly ConcurrentDictionary<Vector3I, Chunk> _chunks = new();
        private ConcurrentQueue<Vector3I> _chunksToGenerate = new();
        private readonly Queue<Vector3I> _chunksToRemove = new();
        private readonly ConcurrentDictionary<Vector3I, List<(Vector3I, Block)>> _blocksToPlace = new();

        private Task[] _tasks = new Task[OS.GetProcessorCount() - 2];
        private Task _renderTask;

        private int _generatedChunks = 0;
        private float _totalGenerationTime = 0;
        private int _renderedChunks = 0;
        private float _totalRenderTime = 0;

        public float AverageGenerationTime => _totalGenerationTime / _generatedChunks;
        public float AverageRenderTime => _totalRenderTime / _renderedChunks;

        /// <summary>
        /// Method <c>GetBlock</c> gets the block at the given world position.
        /// </summary>
        /// <param name="worldPosition">The world position of the block.</param>
        /// <returns>
        /// The block at the given world position.
        /// </returns>
        public Block GetBlock(Vector3I worldPosition)
        {
            var chunkPosition = WorldToChunkPosition(worldPosition);

            if (_chunks.ContainsKey(chunkPosition))
            {
                return _chunks[chunkPosition].GetBlock(WorldToLocalPosition(worldPosition));
            }

            return null;
        }

        /// <summary>
        /// Method <c>SetBlock</c> sets the block at the given world position. If the chunk containing the block does not exist, the block is added to a queue to be placed when the chunk is generated.
        /// </summary>
        /// <param name="worldPosition">The world position of the block.</param>
        /// <param name="block">The block to set.</param>
        public void SetBlock(Vector3I worldPosition, Block block)
        {
            var chunkPosition = WorldToChunkPosition(worldPosition);
            var localPosition = WorldToLocalPosition(worldPosition);

            if (_chunks.ContainsKey(chunkPosition))
            {
                _chunks[chunkPosition].SetBlock(localPosition, block);
            }
            else
            {
                if (!_blocksToPlace.ContainsKey(chunkPosition))
                {
                    _blocksToPlace[chunkPosition] = new() { (localPosition, block) };
                }
                else
                {
                    _blocksToPlace[chunkPosition].Add((localPosition, block));
                }
            }
        }

        /// <summary>
        /// Method <c>GetBlockLightLevel</c> gets the block light level at the given world position.
        /// </summary>
        /// <param name="worldPosition">The world position of the block.</param>
        /// <returns>A Vector3I representing the RGB light level of the block.</returns>
        public Vector3I GetBlockLightLevel(Vector3I worldPosition)
        {
            var chunkPosition = WorldToChunkPosition(worldPosition);

            if (_chunks.ContainsKey(chunkPosition))
            {
                return _chunks[chunkPosition].GetLightLevel(WorldToLocalPosition(worldPosition));
            }

            return Vector3I.Zero;
        }

        /// <summary>
        /// Method <c>SetBlockLightLevel</c> gets the block light level at the given world position.
        /// </summary>
        /// <param name="worldPosition">The world position of the block.</param>
        /// <param name="lightLevel">A Vector3I representing the RGB light level of the block.</param>
        public void SetBlockLightLevel(Vector3I worldPosition, Vector3I lightLevel)
        {
            var chunkPosition = WorldToChunkPosition(worldPosition);
            var localPosition = WorldToLocalPosition(worldPosition);

            if (_chunks.ContainsKey(chunkPosition))
            {
                _chunks[chunkPosition].SetLightLevel(localPosition, lightLevel);
            }
        }

        /// <summary>
        /// Method <c>WorldToChunkPosition</c> finds the chunk position containing the given world position.
        /// </summary>
        /// <param name="worldPosition">A Vector3 representing a position in the world.</param>
        /// <returns>A Vector3I representing the chunk position containing the given world position.</returns>
        private Vector3I WorldToChunkPosition(Vector3 worldPosition)
        {
            return (Vector3I)(worldPosition / Configuration.CHUNK_DIMENSION).Floor();
        }

        /// <summary>
        /// Method <c>WorldToLocalPosition</c> finds the local position of the block within the chunk containing the given world position.
        /// </summary>
        /// <param name="worldPosition">A Vector3 representing a position in the world.</param>
        /// <returns>A Vector3I representing the local position of the block within the chunk containing the given world position.</returns>
        private Vector3I WorldToLocalPosition(Vector3 worldPosition)
        {
            int x = (int)((worldPosition.X % Configuration.CHUNK_DIMENSION.X + Configuration.CHUNK_DIMENSION.X) % Configuration.CHUNK_DIMENSION.X);
            int y = (int)((worldPosition.Y % Configuration.CHUNK_DIMENSION.Y + Configuration.CHUNK_DIMENSION.Y) % Configuration.CHUNK_DIMENSION.Y);
            int z = (int)((worldPosition.Z % Configuration.CHUNK_DIMENSION.Z + Configuration.CHUNK_DIMENSION.Z) % Configuration.CHUNK_DIMENSION.Z);

            return new Vector3I(x, y, z);
        }

        private void GenerateChunks(Vector3I playerChunkPosition)
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
                    _chunksToGenerate = new(sortedChunksToGenerate); // Update the queue

                    var chunk = _chunkScene.Instantiate<Chunk>();
                    chunk.Initialize(this, chunkPosition);
                    AddChild(chunk);

                    _chunks[chunkPosition] = chunk;

                    _tasks[i] = Task.Run(() =>
                    {
                        if (!chunk.IsGenerated)
                        {
                            var stopWatch = new Stopwatch();
                            stopWatch.Start();

                            chunk.Generate(_terrainGenerator, WorldPresets.Instance.Get("default"));

                            if (_blocksToPlace.ContainsKey(chunkPosition))
                            {
                                foreach (var (localPosition, block) in _blocksToPlace[chunkPosition])
                                {
                                    chunk.PlaceBlock(localPosition, block);
                                }

                                _blocksToPlace.Remove(chunkPosition, out _);
                            }

                            stopWatch.Stop();

                            _totalGenerationTime += stopWatch.ElapsedMilliseconds;
                            _generatedChunks++;
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
                    var stopWatch = new Stopwatch();
                    stopWatch.Start();

                    RenderChunk(chunkPosition);

                    stopWatch.Stop();

                    _totalRenderTime += stopWatch.ElapsedMilliseconds;
                    _renderedChunks++;
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
            var chunksToCheck = new List<Vector3I>(12)
            {
                chunkPosition,
                chunkPosition + Vector3I.Left,
                chunkPosition + Vector3I.Right,
                chunkPosition + Vector3I.Forward,
                chunkPosition + Vector3I.Back,
            };

            //for (int i = 0; i < Configuration.VERTICAL_CHUNKS; i++)
            //{
            //    chunksToCheck.Add(new Vector3I(chunkPosition.X, i, chunkPosition.Z));
            //}

            return chunksToCheck.All(chunk => _chunks.ContainsKey(chunk) && _chunks[chunk].IsGenerated);
        }

        private IEnumerable<Vector3I> GetNearbyChunkPositions(Vector3I playerChunkPosition)
        {
            var radius = Configuration.CHUNK_LOAD_RADIUS;
            var playerXZPosition = new Vector2I(playerChunkPosition.X, playerChunkPosition.Z);

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    var xzChunkPosition = new Vector2I(x + playerChunkPosition.X, z + playerChunkPosition.Z);

                    if (((Vector2)playerXZPosition).DistanceTo(xzChunkPosition) <= radius)
                    {
                        for (int y = 0; y < Configuration.VERTICAL_CHUNKS; y++)
                        {
                            yield return new Vector3I(xzChunkPosition.X, y, xzChunkPosition.Y);
                        }
                    }
                }
            }
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

            _chunks[chunkPosition].Render();

            if (localPosition.X < 1)
            {
                _chunks[chunkPosition + Vector3I.Left].Render();
            }
            else if (localPosition.X > Configuration.CHUNK_DIMENSION.X - 2)
            {
                _chunks[chunkPosition + Vector3I.Right].Render();
            }

            if (localPosition.Y < 1 && chunkPosition.Y > 0)
            {
                _chunks[chunkPosition + Vector3I.Down].Render();
            }
            else if (localPosition.Y > Configuration.CHUNK_DIMENSION.Y - 2 && chunkPosition.Y < Configuration.VERTICAL_CHUNKS - 1)
            {
                _chunks[chunkPosition + Vector3I.Up].Render();
            }

            if (localPosition.Z < 1)
            {
                _chunks[chunkPosition + Vector3I.Forward].Render();
            }
            else if (localPosition.Z > Configuration.CHUNK_DIMENSION.Z - 2)
            {
                _chunks[chunkPosition + Vector3I.Back].Render();
            }
        }

        public override void _Ready()
        {
            BlockTypes.Instance.Load(BlockTextures.Instance.Load());
            WorldPresets.Instance.Load();

            _player = GetNode<Player>("Player");
            _terrainGenerator = GetNode<TerrainGenerator>("/root/TerrainGenerator");

            var customSignals = GetNode<CustomSignals>("/root/CustomSignals");
            customSignals.BreakBlock += BreakBlock;
            customSignals.PlaceBlock += PlaceBlock;
        }

        public override void _Process(double delta)
        {
            if (!BlockTextures.Instance.loaded || !BlockTypes.Instance.loaded || !_terrainGenerator.initialized)
            {
                return;
            }

            var playerChunkPosition = WorldToChunkPosition(_player.Position);

            var orderedChunks = _chunks.Values
                .OrderBy(chunk => ((Vector3)chunk.ChunkPosition).DistanceTo(playerChunkPosition));

            var chunksToRender = orderedChunks
                .Where(chunk => CanRenderChunk(chunk.ChunkPosition) && chunk.NeedsToBeRendered)
                .Select(chunk => chunk.ChunkPosition)
                .ToList();

            RenderChunks(chunksToRender);

            GenerateChunks(playerChunkPosition);

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
    }
}
