using Godot;
using System;

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

    public float GetHeight(Vector3I globalPosition, int waterLevel)
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
}
