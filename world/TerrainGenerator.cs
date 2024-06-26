using Godot;
using System;

namespace Aeon
{
    public partial class TerrainGenerator : Node3D
    {
        [Export]
        private FastNoiseLite continentalnessNoise;
        [Export]
        private Curve continentalnessCurve;
        [Export]
        private FastNoiseLite peaksAndValleysNoise;
        [Export]
        private Curve peaksAndValleysCurve;
        [Export]
        private FastNoiseLite erosionNoise;
        [Export]
        private Curve erosionCurve;
        [Export]
        private FastNoiseLite cheeseCaveNoise;
        [Export]
        private float cheeseCaveNoiseThreshold = 0.6f;
        [Export]
        private FastNoiseLite noodleCaveNoise;
        [Export]
        private FastNoiseLite noodleCaveSecondaryNoise;
        [Export]
        private float noodleCaveNoiseThreshold = 0.05f;

        public bool initialized = false;

        public override void _Ready()
        {
            continentalnessCurve.Bake();
            peaksAndValleysCurve.Bake();
            erosionCurve.Bake();

            initialized = true;
        }

        public int GetHeight(Vector2I globalPosition)
        {
            float continentalness = (continentalnessNoise.GetNoise2D(globalPosition.X, globalPosition.Y) + 1) / 2;
            float peaksAndValleys = (peaksAndValleysNoise.GetNoise2D(globalPosition.X, globalPosition.Y) + 1) / 2;
            float erosion = (erosionNoise.GetNoise2D(globalPosition.X, globalPosition.Y) + 1) / 2;

            int height = Mathf.RoundToInt(
                continentalnessCurve.Sample(continentalness) +
                (peaksAndValleysCurve.Sample(peaksAndValleys) * erosionCurve.Sample(erosion))
            );

            return height;
        }

        public Block GetBlockType(Vector3I globalPosition, int height, int waterLevel = 64)
        {
            Block blockType = BlockTypes.Instance.Get("air");

            if (globalPosition.Y > height && globalPosition.Y <= waterLevel)
            {
                blockType = BlockTypes.Instance.Get("water");
            }
            else if (IsCave(globalPosition))
            {
                blockType = BlockTypes.Instance.Get("air");
            }
            else if (globalPosition.Y < height - 2)
            {
                blockType = BlockTypes.Instance.Get("stone");
            }
            else if (height <= 68 && globalPosition.Y <= height)
            {
                blockType = BlockTypes.Instance.Get("sand");
            }
            else if (globalPosition.Y < height)
            {
                blockType = BlockTypes.Instance.Get("dirt");
            }
            else if (globalPosition.Y == height)
            {
                if (height > 100)
                {
                    blockType = BlockTypes.Instance.Get("snow");
                }
                else
                {
                    blockType = BlockTypes.Instance.Get("grass");
                }
            }

            return blockType;
        }

        public bool IsCave(Vector3I globalPosition)
        {
            return false;

            var isNoodleCave = Mathf.Abs(noodleCaveNoise.GetNoise3D(globalPosition.X, globalPosition.Y, globalPosition.Z)) < noodleCaveNoiseThreshold &&
                Mathf.Abs(noodleCaveSecondaryNoise.GetNoise3D(globalPosition.X, globalPosition.Y, globalPosition.Z)) < noodleCaveNoiseThreshold;

            var isCheeseCave = cheeseCaveNoise.GetNoise3D(globalPosition.X, globalPosition.Y, globalPosition.Z) > cheeseCaveNoiseThreshold;

            return isNoodleCave || isCheeseCave;
        }
    }
}
