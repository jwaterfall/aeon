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

    public float GetHeight(Vector3I globalPosition, int waterLevel)
    {
        float continentalness = (continentalnessNoise.GetNoise2D(globalPosition.X, globalPosition.Z) + 1) / 2;
        float peaksAndValleys = (peaksAndValleysNoise.GetNoise2D(globalPosition.X, globalPosition.Z) + 1) / 2;

        //float erosionScale = 0.025f; // Adjust this scale factor as needed
        //float erosion = noise.GetNoise2Dv(new Vector2(globalPosition.X * erosionScale, globalPosition.Z * erosionScale));

        // Map continentalness to the desired height range
        int height = Mathf.RoundToInt(continentalnessCurve.Sample(continentalness) + peaksAndValleysCurve.Sample(peaksAndValleys));

        return height;
    }

    private float MapPeaksAndValleysToMultiplier(float peaksAndValleys)
    {
        float t;

        if (peaksAndValleys <= -0.8f)
        {
            t = Mathf.SmoothStep(-1, -0.8f, peaksAndValleys);
            return Mathf.Lerp(-0.25f, 0.15f, t);
        }
        else if (peaksAndValleys <= -0.4f)
        {
            t = Mathf.SmoothStep(-0.8f, -0.4f, peaksAndValleys);
            return Mathf.Lerp(0.15f, 0, t);
        }
        else if (peaksAndValleys <= 0)
        {
            t = Mathf.SmoothStep(-0.4f, -0f, peaksAndValleys);
            return Mathf.Lerp(0, 0.15f, t);
        }
        else if (peaksAndValleys <= 0.4)
        {
            t = Mathf.SmoothStep(0, 0.4f, peaksAndValleys);
            return Mathf.Lerp(0.15f, 0.5f, t);
        }
        else
        {
            t = Mathf.SmoothStep(0.4F, 1, peaksAndValleys);
            return Mathf.Lerp(0.5f, 0.35f, t);
        }
    }

    private float MapErosionToMultiplier(float errosion)
    {
        float t;

        if (errosion <= -0.8f)
        {
            t = Mathf.SmoothStep(-1, -0.8f, errosion);
            return Mathf.Lerp(1, 0.75f, t);
        }
        else if (errosion <= -0.5f)
        {
            t = Mathf.SmoothStep(-0.8f, -0.5f, errosion);
            return Mathf.Lerp(0.75f, 0.5f, t);
        }
        else if (errosion <= -0.45f)
        {
            t = Mathf.SmoothStep(-0.5f, -0.45f, errosion);
            return Mathf.Lerp(0.5f, 0.6f, t);
        }
        else if (errosion <= -0.2f)
        {
            t = Mathf.SmoothStep(-0.45f, -0.2f, errosion);
            return Mathf.Lerp(0.6f, 0.2f, t);
        }
        else if (errosion <= 0.3f)
        {
            t = Mathf.SmoothStep(-0.2f, 0.3f, errosion);
            return Mathf.Lerp(0.2f, 0.4f, t);
        }
        else if (errosion <= 0.6f)
        {
            return 0.4f;
        }
        else if (errosion <= 0.7f)
        {
            t = Mathf.SmoothStep(0.6f, 0.7f, errosion);
            return Mathf.Lerp(0.4f, 0.3f, t);
        }
        else
        {
            t = Mathf.SmoothStep(0.7f, 1f, errosion);
            return Mathf.Lerp(0.3f, 0.2f, t);
        }
    }
}
