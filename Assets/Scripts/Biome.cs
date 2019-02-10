using UnityEngine;

public class BiomeUtility
{
    Hills hills;
    Flat flat;
    public BiomeUtility()
    {
        hills = new Hills(new FastNoise());
        flat = new Flat(new FastNoise());
    }

    public static int Index(float cellNoise)
    {
        if(cellNoise > 0.5f)
            return 0;
        else    
            return 1;
    }

    public float AddNoise(float cellNoise, int x, int z)
    {
        return AddNoise(Index(cellNoise), x, z);
    }

    public float AddNoise(int biomeIndex, int x, int z)
    {
        switch(biomeIndex)
        {
            case 0: return hills.AddHeight(x, z);
            case 1: return flat.AddHeight(x, z);

            default: throw new System.Exception("Unknown biome index.");
        }
    }
}

public interface Biome
{
    float AddHeight(int x, int z);
}

public struct Hills : Biome
{
    FastNoise noise;
    public Hills(FastNoise noise)
    {
        this.noise = noise;

        this.noise.SetFrequency(0.01f);
        this.noise.SetFractalOctaves(5);
        this.noise.SetNoiseType(FastNoise.NoiseType.SimplexFractal);
    }

    public float AddHeight(int x, int z)
    {
        return noise.GetNoise01(x, z);
    }    
}

public struct Flat : Biome
{
    FastNoise noise;
    public Flat(FastNoise noise)
    {
        this.noise = noise;
    }

    public float AddHeight(int x, int z)
    {
        return 0;
    }    
}

