﻿using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aeon
{
    internal class ChunkLightManager
    {
        private ChunkRGBLightData _lightData = new(Configuration.CHUNK_DIMENSION);
        private Queue<(Vector3I, Vector3I, Vector3I)> _lightPropagationQueue = new();
        private Queue<(Vector3I, Vector3I)> _darknessPropagationQueue = new();
        private Queue<Vector3I> _lightRepairQueue = new();

        private Chunk _chunk;
        private World _world;

        private Vector3I[] channels = {
            Vector3I.Right,
            Vector3I.Up,
            Vector3I.Back,
        };

        public ChunkLightManager(Chunk chunk, World world)
        {
            _chunk = chunk;
            _world = world;
        }

        private Vector3I[] GetNeighbors(Vector3I localPosition)
        {
            return new Vector3I[]
            {
                localPosition + new Vector3I(1, 0, 0),
                localPosition + new Vector3I(-1, 0, 0),
                localPosition + new Vector3I(0, 1, 0),
                localPosition + new Vector3I(0, -1, 0),
                localPosition + new Vector3I(0, 0, 1),
                localPosition + new Vector3I(0, 0, -1),
            };
        }

        private Vector3I SetVectorWithChannel(Vector3I localPosition, Vector3I value, Vector3I channel, bool keepMax = false)
        {
            var existingValue = _world.GetBlockLightLevel(_chunk.GetWorldPosition(localPosition));
            var existingChannelValue = existingValue * channel;
            var newChannelValue = keepMax && existingChannelValue > value * channel ? existingChannelValue : value * channel;
            var newValue = existingValue - existingChannelValue + newChannelValue;
            return newValue;
        }

        public Vector3I GetLightLevel(Vector3I localPosition)
        {
            return _lightData.Get(localPosition);
        }

        public void SetLightLevel(Vector3I localPosition, Vector3I lightLevel)
        {
            _lightData.Set(localPosition, lightLevel);
        }

        public void PropagateNeighborLight()
        {
            for (var x = -1; x <= Configuration.CHUNK_DIMENSION.X; x++)
            {
                for (var y = -1; y <= Configuration.CHUNK_DIMENSION.Y; y++)
                {
                    for (var z = -1; z < Configuration.CHUNK_DIMENSION.Z; z++)
                    {
                        var localPosition = new Vector3I(x, y, z);

                        if (_chunk.IsInChunk(localPosition)) continue;

                        var worldPosition = _chunk.GetWorldPosition(localPosition);
                        var lightLevel = _world.GetBlockLightLevel(worldPosition);

                        foreach (var channel in channels)
                        {
                            if (lightLevel * channel > Vector3I.Zero)
                            {
                                _lightPropagationQueue.Enqueue((localPosition, lightLevel * channel, channel));
                            }
                        }
                    }
                }
            }

            Propagate();
        }

        public void AddLightSource(Vector3I localPosition, Vector3I lightLevel)
        {
            foreach (var channel in channels)
            {
                if (lightLevel * channel > Vector3I.Zero)
                {
                    _lightPropagationQueue.Enqueue((localPosition, lightLevel * channel, channel));
                }
            }

            Propagate();
        }

        public void RemoveLightSource(Vector3I localPosition, Vector3I lightLevel)
        {
            foreach (var channel in channels)
            {
                if (lightLevel * channel > Vector3I.Zero)
                {
                    _darknessPropagationQueue.Enqueue((localPosition, channel));
                    _lightRepairQueue.Enqueue(localPosition);
                }
            }

            Propagate();
        }

        private void Propagate()
        {
            while (_darknessPropagationQueue.Count > 0)
            {
                var (localPosition, channel) = _darknessPropagationQueue.Dequeue();

                var existingChannelLightLevel = _world.GetBlockLightLevel(_chunk.GetWorldPosition(localPosition)) * channel;
                var newLightLevel = SetVectorWithChannel(localPosition, Vector3I.Zero, channel);

                _world.SetBlockLightLevel(_chunk.GetWorldPosition(localPosition), newLightLevel);

                foreach (var neighbor in GetNeighbors(localPosition))
                {
                    var neighborWorldPosition = _chunk.GetWorldPosition(neighbor);
                    var neighborLightLevel = _world.GetBlockLightLevel(neighborWorldPosition) * channel;
                    var neighborChannelLightLevel = neighborLightLevel * channel;

                    if (neighborChannelLightLevel > Vector3I.Zero && neighborChannelLightLevel < existingChannelLightLevel)
                    {
                        if (!_darknessPropagationQueue.Contains((neighbor, channel)))
                        {
                            _darknessPropagationQueue.Enqueue((neighbor, channel));
                        }
                    }
                    else if (neighborChannelLightLevel >= existingChannelLightLevel && !_lightPropagationQueue.Contains((neighbor, neighborChannelLightLevel, channel)))
                    {
                        _lightPropagationQueue.Enqueue((neighbor, neighborChannelLightLevel, channel));
                    }
                }
            }

            while (_lightPropagationQueue.Count > 0)
            {
                var (localPosition, lightLevel, channel) = _lightPropagationQueue.Dequeue();

                var newLightLevel = SetVectorWithChannel(localPosition, lightLevel, channel, true);

                _world.SetBlockLightLevel(_chunk.GetWorldPosition(localPosition), newLightLevel);

                var newChannelLightLevel = newLightLevel * channel;

                if (newChannelLightLevel <= Vector3I.Zero) continue;

                foreach (var neighbor in GetNeighbors(localPosition))
                {
                    var neighborWorldPosition = _chunk.GetWorldPosition(neighbor);
                    var neighborLightLevel = _world.GetBlockLightLevel(neighborWorldPosition) * channel;
                    var neighborBlockType = _world.GetBlock(neighborWorldPosition);

                    if (neighborBlockType == null) continue;

                    var newNeighborLightLevel = newChannelLightLevel - channel;

                    if (neighborBlockType.Transparent && neighborLightLevel < newNeighborLightLevel && !_lightPropagationQueue.Contains((neighbor, newNeighborLightLevel, channel)))
                    {
                        _lightPropagationQueue.Enqueue((neighbor, newNeighborLightLevel, channel));
                    }
                }
            }

            while (_lightRepairQueue.Count > 0)
            {
                var localPosition = _lightRepairQueue.Dequeue();
                var neighbors = GetNeighbors(localPosition);

                foreach (var neighbor in neighbors)
                {
                    foreach (var channel in channels)
                    {
                        var neighborWorldPosition = _chunk.GetWorldPosition(neighbor);
                        var neighborChannelLightLevel = _world.GetBlockLightLevel(neighborWorldPosition) * channel;

                        if (neighborChannelLightLevel > Vector3I.Zero)
                        {
                            _lightPropagationQueue.Enqueue((neighbor, neighborChannelLightLevel, channel));
                        }
                    }
                }
            }
        }
    }
}
