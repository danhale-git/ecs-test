using UnityEngine;

public class Biome
{
    protected FastNoise noise;

    public Biome()
    {
        noise = new FastNoise();
    }

    public virtual float AddHeight(int x, int z)
    {
        return 0;
    }

    public static int Index(float cellNoise)
    {
        if(cellNoise > 0.5f)
            return 0;
        else    
            return 1;
    }

    public static Biome Select(int index)
    {
        switch(index)
        {
            case 0:     return new Hills();
            case 1:     return new Flat();
            
            default: throw new System.Exception("unknown biome selector");
        }
    }
}

public class Hills : Biome
{
    public Hills() : base()
    {
        noise.SetFrequency(0.01f);
        noise.SetFractalOctaves(5);
        noise.SetNoiseType(FastNoise.NoiseType.SimplexFractal);
    }

    public override float AddHeight(int x, int z)
    {
        return noise.GetNoise01(x, z);
    }
}

public class Flat : Biome
{
    public Flat() : base() { }

    public override float AddHeight(int x, int z)
    {
        return 1;
    }
}
