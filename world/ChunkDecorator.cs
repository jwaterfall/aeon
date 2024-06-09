using Godot;
using System;
using System.Collections.Generic;

namespace Aeon
{
    internal class ChunkDecorator
    {
        private Chunk _chunk;
        private ChunkManager _chunkManager;

        public ChunkDecorator(Chunk chunk, ChunkManager chunkManager)
        {
            _chunk = chunk;
            _chunkManager = chunkManager;
        }

        public void Decorate(TerrainGenerator terrainGenerator, WorldPreset worldPreset)
        {
            //GenerateOres(worldPreset);
            GenerateGrass(terrainGenerator);
            GenerateTrees(terrainGenerator);
        }

        private void SetBlockInOrOut(Vector3I localPosition, BlockType blockType)
        {
            if (_chunk.IsInChunk(localPosition))
            {
                _chunk.SetBlock(localPosition, blockType);
            }
            else
            {
                _chunkManager.SetBlock(_chunk.GetWorldPosition(localPosition), blockType);
            }
        }

        private void GenerateOres(WorldPreset worldPreset)
        {
            var random = new Random();
            foreach (var ore in worldPreset.Ores)
            {
                for (int i = 0; i < ore.Frequency; i++)
                {
                    Vector3I localStartPosition = new Vector3I(
                        random.Next(0, _chunk.Dimensions.X),
                        random.Next(0, _chunk.Dimensions.Y),
                        random.Next(0, _chunk.Dimensions.Z)
                    );

                    Vector3I globalStartPosition = _chunk.GetWorldPosition(localStartPosition);
                    if (globalStartPosition.Y < ore.MinHeight || globalStartPosition.Y > ore.MaxHeight) continue;

                    var veinPositions = new List<Vector3I>();
                    for (int j = 0; j < ore.Size; j++)
                    {
                        Vector3I localPosition = veinPositions.Count == 0 ? localStartPosition : veinPositions[j - 1] + new Vector3I(random.Next(-1, 2), random.Next(-1, 2), random.Next(-1, 2));
                        veinPositions.Add(localPosition);

                        if (_chunk.IsInChunk(localPosition))
                        {
                            SetBlockInOrOut(localPosition, BlockTypes.Instance.Get(ore.Block));
                        }
                    }
                }
            }
        }

        private void GenerateGrass(TerrainGenerator terrainGenerator)
        {
            var random = new Random();
            for (int x = 0; x < _chunk.Dimensions.X; x++)
            {
                for (int z = 0; z < _chunk.Dimensions.Z; z++)
                {
                    int height = terrainGenerator.GetHeight(_chunk.ChunkPosition * new Vector2I(_chunk.Dimensions.X, _chunk.Dimensions.Z) + new Vector2I(x, z));
                    if (height < 0 || height > _chunk.Dimensions.Y - 2) continue;

                    Vector3I blockBelowPosition = new Vector3I(x, height, z);
                    var blockBelow = _chunk.IsInChunk(blockBelowPosition) ? _chunk.GetBlock(blockBelowPosition) : _chunkManager.GetBlock(_chunk.GetWorldPosition(blockBelowPosition));

                    if (blockBelow != null && (blockBelow.Name == "grass" || blockBelow.Name == "snow") && random.NextDouble() <= 0.025f)
                    {
                        SetBlockInOrOut(new Vector3I(x, height + 1, z), BlockTypes.Instance.Get("short_grass"));
                    }
                }
            }
        }

        private void GenerateTrees(TerrainGenerator terrainGenerator)
        {
            var random = new Random();
            for (int x = 0; x < _chunk.Dimensions.X; x++)
            {
                for (int z = 0; z < _chunk.Dimensions.Z; z++)
                {
                    if (random.NextDouble() > 0.005f) continue;

                    int height = terrainGenerator.GetHeight(_chunk.ChunkPosition * new Vector2I(_chunk.Dimensions.X, _chunk.Dimensions.Z) + new Vector2I(x, z));
                    if (height < 0 || height > _chunk.Dimensions.Y - 2) continue;

                    var blockBelow = _chunk.GetBlock(new Vector3I(x, height, z));
                    if (blockBelow.Name != "grass" && blockBelow.Name != "snow") continue;

                    int trunkHeight = random.Next(5, 7);
                    SetBlockInOrOut(new Vector3I(x, height, z), BlockTypes.Instance.Get("dirt"));
                    GenerateTreeTrunk(height, trunkHeight, x, z);
                    GenerateTreeLeaves(height, trunkHeight, x, z);
                }
            }
        }

        private void GenerateTreeTrunk(int localHeight, int trunkHeight, int x, int z)
        {
            for (int y = 1; y <= trunkHeight; y++)
            {
                SetBlockInOrOut(new Vector3I(x, localHeight + y, z), BlockTypes.Instance.Get("spruce_log"));
            }
        }

        private void GenerateTreeLeaves(int localHeight, int trunkHeight, int x, int z)
        {
            var random = new Random();
            int brimHeight = random.Next(1, 3);
            int brimWidth = 2;

            for (int y = 1; y <= brimHeight; y++)
            {
                for (int xOffset = -brimWidth; xOffset <= brimWidth; xOffset++)
                {
                    for (int zOffset = -brimWidth; zOffset <= brimWidth; zOffset++)
                    {
                        SetBlockInOrOut(new Vector3I(x + xOffset, localHeight + trunkHeight + y, z + zOffset), BlockTypes.Instance.Get("spruce_leaves"));
                    }
                }
            }

            int topHeight = random.Next(1, 2);
            int topWidth = 1;

            for (int y = 1; y <= topHeight; y++)
            {
                for (int xOffset = -topWidth; xOffset <= topWidth; xOffset++)
                {
                    for (int zOffset = -topWidth; zOffset <= topWidth; zOffset++)
                    {
                        SetBlockInOrOut(new Vector3I(x + xOffset, localHeight + trunkHeight + brimHeight + y, z + zOffset), BlockTypes.Instance.Get("spruce_leaves"));
                    }
                }
            }
        }
    }
}
