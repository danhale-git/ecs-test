using UnityEngine;

public struct BiomeIndex
{
    public int GetIndex(float cellNoise)
    {
        if(cellNoise > 0.5f)
            return 0;
        else    
            return 1;
    }
}

public struct BiomeUtility
{
    BiomeIndex index;

    Cliff cliff;
    Hills hills;
    Flats flat;

    public void InitialiseBiomes()
    {
        cliff = new Cliff(new SimplexNoiseGenerator(0));
        hills = new Hills(new SimplexNoiseGenerator(0));
        flat = new Flats(new SimplexNoiseGenerator(0));

        index = new BiomeIndex();
    }

    public float AddNoise(float cellNoise, int x, int z)
    {
        return AddNoise(index.GetIndex(cellNoise), x, z);
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
    public SimplexNoiseGenerator noise;
    public Hills(SimplexNoiseGenerator noise)
    {
        this.noise = noise;
    }

    public float AddHeight(int x, int z)
    {
        return noise.GetSimplex(x, z, 1337, 0.005f) * 20;
    }    
}

public struct Flats : Biome
{
    public SimplexNoiseGenerator noise;
    public Flats(SimplexNoiseGenerator noise)
    {
        this.noise = noise;
    }

    public float AddHeight(int x, int z)
    {
        return noise.GetSimplex(x, z, 1337, 0.001f) * 5;
    }     
}

public struct Cliff : Biome
{
    public SimplexNoiseGenerator noise;
    public Cliff(SimplexNoiseGenerator noise)
    {
       this.noise = noise;
    }

    public float AddHeight(int x, int z)
    {
        return (noise.GetSimplex(x, z, 1337, 0.05f) * 8) - 4;
    }    
}

