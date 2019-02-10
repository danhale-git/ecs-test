using UnityEngine;

public class BiomeUtility
{
    Cliff cliff;

    Hills hills;
    Flats flat;
    public BiomeUtility()
    {
        cliff = new Cliff(new FastNoise());

        hills = new Hills(new FastNoise());
        flat = new Flats(new FastNoise());
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

    public float CliffDetail(int x, int z)
    {
        return cliff.AddHeight(x, z);
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

        this.noise.SetFrequency(0.005f);
        this.noise.SetFractalOctaves(5);
        this.noise.SetNoiseType(FastNoise.NoiseType.SimplexFractal);
    }

    public float AddHeight(int x, int z)
    {
        return noise.GetNoise01(x, z) * 20;
    }    
}

public struct Flats : Biome
{
    FastNoise noise;
    public Flats(FastNoise noise)
    {
        this.noise = noise;

        this.noise.SetFrequency(0.001f);
        this.noise.SetFractalOctaves(5);
        this.noise.SetNoiseType(FastNoise.NoiseType.SimplexFractal);
    }

    public float AddHeight(int x, int z)
    {
        return noise.GetNoise01(x, z) * 5;
    }     
}

public struct Cliff : Biome
{
    FastNoise noise;
    public Cliff(FastNoise noise)
    {
       this.noise = noise;

        this.noise.SetFrequency(0.05f);
        this.noise.SetNoiseType(FastNoise.NoiseType.Simplex);
    }

    public float AddHeight(int x, int z)
    {
        return (noise.GetNoise01(x, z) * 8) - 4;
    }    
}

