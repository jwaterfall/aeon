using Godot;

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
        private FastNoiseLite caveNoise;
        [Export]
        private float caveNoiseThreshold = 0.5f;

        private float GetHeight(Vector3I globalPosition, int waterLevel)
        {
            float continentalness = (continentalnessNoise.GetNoise2D(globalPosition.X, globalPosition.Z) + 1) / 2;
            float peaksAndValleys = (peaksAndValleysNoise.GetNoise2D(globalPosition.X, globalPosition.Z) + 1) / 2;
            float erosion = (erosionNoise.GetNoise2D(globalPosition.X, globalPosition.Z) + 1) / 2;

            int height = Mathf.RoundToInt(
                continentalnessCurve.SampleBaked(continentalness) +
                (peaksAndValleysCurve.SampleBaked(peaksAndValleys) * erosionCurve.SampleBaked(erosion))
            );

            return height;
        }

        public BlockType GetBlockType(Vector3I globalPosition, int waterLevel)
        {
            var height = GetHeight(globalPosition, waterLevel);

            BlockType blockType = BlockTypes.Instance.Get("air");

            if (globalPosition.Y > height && globalPosition.Y <= waterLevel)
            {
                blockType = BlockTypes.Instance.Get("water");
            }
            else if (caveNoise.GetNoise3D(globalPosition.X, globalPosition.Y, globalPosition.Z) > caveNoiseThreshold)
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
                blockType = BlockTypes.Instance.Get("grass");
            }

            return blockType;
        }
    }
}
