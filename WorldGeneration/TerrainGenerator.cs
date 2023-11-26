using Godot;
using System;

public partial class TerrainGenerator : Node
{
    private static FastNoiseLite noise = new();

    public static float GetHeight(Vector3I globalPosition, int waterLevel)
    {
        // Use different noise functions for various aspects of terrain generation
        float continentalnessScale = 0.05f; // Adjust this scale factor as needed
        float continentalness = noise.GetNoise2Dv(new Vector2(globalPosition.X * continentalnessScale, globalPosition.Z * continentalnessScale));

        float peaksAndValleysScale = 0.5f; // Adjust this scale factor as needed
        float peaksAndValleys = noise.GetNoise2Dv(new Vector2(globalPosition.X * peaksAndValleysScale, globalPosition.Z * peaksAndValleysScale));

        float erosionScale = 0.01f; // Adjust this scale factor as needed
        float erosion = noise.GetNoise2Dv(new Vector2(globalPosition.X * erosionScale, globalPosition.Z * erosionScale));

        // Map continentalness to the desired height range
        int height = Mathf.RoundToInt(((waterLevel * MapContinentalnessToMultiplier(continentalness)) + (96 * MapPeaksAndValleysToMultiplier(peaksAndValleys))) * MapErosionToMultiplier(erosion));

        return height;
    }

    private static float MapContinentalnessToMultiplier(float continentalness)
    {
        float t;

        if (continentalness <= -0.6f)
        {
            return 0.5f;
        }
        else if (continentalness <= -0.4f)
        {
            t = Mathf.SmoothStep(-0.6f, -0.4f, continentalness);
            return Mathf.Lerp(0.5f, 1.125f, t);
        }
        else if (continentalness <= -0.25f)
        {
            t = Mathf.SmoothStep(-0.4f, -0.25f, continentalness);
            return Mathf.Lerp(1.125f, 1.25f, t);
        }
        else if (continentalness <= -0.2f)
        {
            t = Mathf.SmoothStep(-0.25f, -0.2f, continentalness);
            return Mathf.Lerp(1.25f, 2, t);
        }
        else if (continentalness <= 0)
        {
            t = Mathf.SmoothStep(-0.2f, 0, continentalness);
            return Mathf.Lerp(2, 2.1f, t);
        }
        else
        {
            t = Mathf.SmoothStep(0, 1, continentalness);
            return Mathf.Lerp(2.1f, 2.5f, t);
        }
    }

    private static float MapPeaksAndValleysToMultiplier(float peaksAndValleys)
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

    private static float MapErosionToMultiplier(float errosion)
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
            return Mathf.Lerp(0.4f, 0.2f, t);
        }
        else
        {
            t = Mathf.SmoothStep(0.7f, 1f, errosion);
            return Mathf.Lerp(0.2f, 0.1f, t);
        }
    }
}
