using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

//[BurstCompile]
struct WorleyNoiseJob : IJobParallelFor
{
    #region Noise
    
    #endregion
    
    public NativeArray<CellData> cellMap;

    [ReadOnly] public float3 offset;
    [ReadOnly] public int squareWidth;
    [ReadOnly] public int seed;
    [ReadOnly] public float frequency;
	[ReadOnly] public float perterbAmp;
    [ReadOnly] public JobUtil util;
    [ReadOnly] public WorleyNoiseGenerator noise;

    //  Fill flattened 2D array with noise matrix
    public void Execute(int i)
    {
        float3 position = util.Unflatten2D(i, squareWidth) + offset;

        cellMap[i] = noise.GetEdgeData(position.x, position.z, seed, frequency, perterbAmp);
    }
}

struct WorleyNoiseGenerator
{
    int X_PRIME;
	int Y_PRIME;
    float m_cellularJitter;

    NativeArray<float2> CELL_2D;

	public WorleyNoiseGenerator(byte param)
	{
		CELL_2D = new NativeArray<float2>(256, Allocator.TempJob);
		CELL_2D.CopyFrom(new float2[]
		{
			new float2(-0.2700222198f, -0.9628540911f), new float2(0.3863092627f, -0.9223693152f), new float2(0.04444859006f, -0.999011673f), new float2(-0.5992523158f, -0.8005602176f), new float2(-0.7819280288f, 0.6233687174f), new float2(0.9464672271f, 0.3227999196f), new float2(-0.6514146797f, -0.7587218957f), new float2(0.9378472289f, 0.347048376f),
			new float2(-0.8497875957f, -0.5271252623f), new float2(-0.879042592f, 0.4767432447f), new float2(-0.892300288f, -0.4514423508f), new float2(-0.379844434f, -0.9250503802f), new float2(-0.9951650832f, 0.0982163789f), new float2(0.7724397808f, -0.6350880136f), new float2(0.7573283322f, -0.6530343002f), new float2(-0.9928004525f, -0.119780055f),
			new float2(-0.0532665713f, 0.9985803285f), new float2(0.9754253726f, -0.2203300762f), new float2(-0.7665018163f, 0.6422421394f), new float2(0.991636706f, 0.1290606184f), new float2(-0.994696838f, 0.1028503788f), new float2(-0.5379205513f, -0.84299554f), new float2(0.5022815471f, -0.8647041387f), new float2(0.4559821461f, -0.8899889226f),
			new float2(-0.8659131224f, -0.5001944266f), new float2(0.0879458407f, -0.9961252577f), new float2(-0.5051684983f, 0.8630207346f), new float2(0.7753185226f, -0.6315704146f), new float2(-0.6921944612f, 0.7217110418f), new float2(-0.5191659449f, -0.8546734591f), new float2(0.8978622882f, -0.4402764035f), new float2(-0.1706774107f, 0.9853269617f),
			new float2(-0.9353430106f, -0.3537420705f), new float2(-0.9992404798f, 0.03896746794f), new float2(-0.2882064021f, -0.9575683108f), new float2(-0.9663811329f, 0.2571137995f), new float2(-0.8759714238f, -0.4823630009f), new float2(-0.8303123018f, -0.5572983775f), new float2(0.05110133755f, -0.9986934731f), new float2(-0.8558373281f, -0.5172450752f),
			new float2(0.09887025282f, 0.9951003332f), new float2(0.9189016087f, 0.3944867976f), new float2(-0.2439375892f, -0.9697909324f), new float2(-0.8121409387f, -0.5834613061f), new float2(-0.9910431363f, 0.1335421355f), new float2(0.8492423985f, -0.5280031709f), new float2(-0.9717838994f, -0.2358729591f), new float2(0.9949457207f, 0.1004142068f),
			new float2(0.6241065508f, -0.7813392434f), new float2(0.662910307f, 0.7486988212f), new float2(-0.7197418176f, 0.6942418282f), new float2(-0.8143370775f, -0.5803922158f), new float2(0.104521054f, -0.9945226741f), new float2(-0.1065926113f, -0.9943027784f), new float2(0.445799684f, -0.8951327509f), new float2(0.105547406f, 0.9944142724f),
			new float2(-0.992790267f, 0.1198644477f), new float2(-0.8334366408f, 0.552615025f), new float2(0.9115561563f, -0.4111755999f), new float2(0.8285544909f, -0.5599084351f), new float2(0.7217097654f, -0.6921957921f), new float2(0.4940492677f, -0.8694339084f), new float2(-0.3652321272f, -0.9309164803f), new float2(-0.9696606758f, 0.2444548501f),
			new float2(0.08925509731f, -0.996008799f), new float2(0.5354071276f, -0.8445941083f), new float2(-0.1053576186f, 0.9944343981f), new float2(-0.9890284586f, 0.1477251101f), new float2(0.004856104961f, 0.9999882091f), new float2(0.9885598478f, 0.1508291331f), new float2(0.9286129562f, -0.3710498316f), new float2(-0.5832393863f, -0.8123003252f),
			new float2(0.3015207509f, 0.9534596146f), new float2(-0.9575110528f, 0.2883965738f), new float2(0.9715802154f, -0.2367105511f), new float2(0.229981792f, 0.9731949318f), new float2(0.955763816f, -0.2941352207f), new float2(0.740956116f, 0.6715534485f), new float2(-0.9971513787f, -0.07542630764f), new float2(0.6905710663f, -0.7232645452f),
			new float2(-0.290713703f, -0.9568100872f), new float2(0.5912777791f, -0.8064679708f), new float2(-0.9454592212f, -0.325740481f), new float2(0.6664455681f, 0.74555369f), new float2(0.6236134912f, 0.7817328275f), new float2(0.9126993851f, -0.4086316587f), new float2(-0.8191762011f, 0.5735419353f), new float2(-0.8812745759f, -0.4726046147f),
			new float2(0.9953313627f, 0.09651672651f), new float2(0.9855650846f, -0.1692969699f), new float2(-0.8495980887f, 0.5274306472f), new float2(0.6174853946f, -0.7865823463f), new float2(0.8508156371f, 0.52546432f), new float2(0.9985032451f, -0.05469249926f), new float2(0.1971371563f, -0.9803759185f), new float2(0.6607855748f, -0.7505747292f),
			new float2(-0.03097494063f, 0.9995201614f), new float2(-0.6731660801f, 0.739491331f), new float2(-0.7195018362f, -0.6944905383f), new float2(0.9727511689f, 0.2318515979f), new float2(0.9997059088f, -0.0242506907f), new float2(0.4421787429f, -0.8969269532f), new float2(0.9981350961f, -0.061043673f), new float2(-0.9173660799f, -0.3980445648f),
			new float2(-0.8150056635f, -0.5794529907f), new float2(-0.8789331304f, 0.4769450202f), new float2(0.0158605829f, 0.999874213f), new float2(-0.8095464474f, 0.5870558317f), new float2(-0.9165898907f, -0.3998286786f), new float2(-0.8023542565f, 0.5968480938f), new float2(-0.5176737917f, 0.8555780767f), new float2(-0.8154407307f, -0.5788405779f),
			new float2(0.4022010347f, -0.9155513791f), new float2(-0.9052556868f, -0.4248672045f), new float2(0.7317445619f, 0.6815789728f), new float2(-0.5647632201f, -0.8252529947f), new float2(-0.8403276335f, -0.5420788397f), new float2(-0.9314281527f, 0.363925262f), new float2(0.5238198472f, 0.8518290719f), new float2(0.7432803869f, -0.6689800195f),
			new float2(-0.985371561f, -0.1704197369f), new float2(0.4601468731f, 0.88784281f), new float2(0.825855404f, 0.5638819483f), new float2(0.6182366099f, 0.7859920446f), new float2(0.8331502863f, -0.553046653f), new float2(0.1500307506f, 0.9886813308f), new float2(-0.662330369f, -0.7492119075f), new float2(-0.668598664f, 0.743623444f),
			new float2(0.7025606278f, 0.7116238924f), new float2(-0.5419389763f, -0.8404178401f), new float2(-0.3388616456f, 0.9408362159f), new float2(0.8331530315f, 0.5530425174f), new float2(-0.2989720662f, -0.9542618632f), new float2(0.2638522993f, 0.9645630949f), new float2(0.124108739f, -0.9922686234f), new float2(-0.7282649308f, -0.6852956957f),
			new float2(0.6962500149f, 0.7177993569f), new float2(-0.9183535368f, 0.3957610156f), new float2(-0.6326102274f, -0.7744703352f), new float2(-0.9331891859f, -0.359385508f), new float2(-0.1153779357f, -0.9933216659f), new float2(0.9514974788f, -0.3076565421f), new float2(-0.08987977445f, -0.9959526224f), new float2(0.6678496916f, 0.7442961705f),
			new float2(0.7952400393f, -0.6062947138f), new float2(-0.6462007402f, -0.7631674805f), new float2(-0.2733598753f, 0.9619118351f), new float2(0.9669590226f, -0.254931851f), new float2(-0.9792894595f, 0.2024651934f), new float2(-0.5369502995f, -0.8436138784f), new float2(-0.270036471f, -0.9628500944f), new float2(-0.6400277131f, 0.7683518247f),
			new float2(-0.7854537493f, -0.6189203566f), new float2(0.06005905383f, -0.9981948257f), new float2(-0.02455770378f, 0.9996984141f), new float2(-0.65983623f, 0.751409442f), new float2(-0.6253894466f, -0.7803127835f), new float2(-0.6210408851f, -0.7837781695f), new float2(0.8348888491f, 0.5504185768f), new float2(-0.1592275245f, 0.9872419133f),
			new float2(0.8367622488f, 0.5475663786f), new float2(-0.8675753916f, -0.4973056806f), new float2(-0.2022662628f, -0.9793305667f), new float2(0.9399189937f, 0.3413975472f), new float2(0.9877404807f, -0.1561049093f), new float2(-0.9034455656f, 0.4287028224f), new float2(0.1269804218f, -0.9919052235f), new float2(-0.3819600854f, 0.924178821f),
			new float2(0.9754625894f, 0.2201652486f), new float2(-0.3204015856f, -0.9472818081f), new float2(-0.9874760884f, 0.1577687387f), new float2(0.02535348474f, -0.9996785487f), new float2(0.4835130794f, -0.8753371362f), new float2(-0.2850799925f, -0.9585037287f), new float2(-0.06805516006f, -0.99768156f), new float2(-0.7885244045f, -0.6150034663f),
			new float2(0.3185392127f, -0.9479096845f), new float2(0.8880043089f, 0.4598351306f), new float2(0.6476921488f, -0.7619021462f), new float2(0.9820241299f, 0.1887554194f), new float2(0.9357275128f, -0.3527237187f), new float2(-0.8894895414f, 0.4569555293f), new float2(0.7922791302f, 0.6101588153f), new float2(0.7483818261f, 0.6632681526f),
			new float2(-0.7288929755f, -0.6846276581f), new float2(0.8729032783f, -0.4878932944f), new float2(0.8288345784f, 0.5594937369f), new float2(0.08074567077f, 0.9967347374f), new float2(0.9799148216f, -0.1994165048f), new float2(-0.580730673f, -0.8140957471f), new float2(-0.4700049791f, -0.8826637636f), new float2(0.2409492979f, 0.9705377045f),
			new float2(0.9437816757f, -0.3305694308f), new float2(-0.8927998638f, -0.4504535528f), new float2(-0.8069622304f, 0.5906030467f), new float2(0.06258973166f, 0.9980393407f), new float2(-0.9312597469f, 0.3643559849f), new float2(0.5777449785f, 0.8162173362f), new float2(-0.3360095855f, -0.941858566f), new float2(0.697932075f, -0.7161639607f),
			new float2(-0.002008157227f, -0.9999979837f), new float2(-0.1827294312f, -0.9831632392f), new float2(-0.6523911722f, 0.7578824173f), new float2(-0.4302626911f, -0.9027037258f), new float2(-0.9985126289f, -0.05452091251f), new float2(-0.01028102172f, -0.9999471489f), new float2(-0.4946071129f, 0.8691166802f), new float2(-0.2999350194f, 0.9539596344f),
			new float2(0.8165471961f, 0.5772786819f), new float2(0.2697460475f, 0.962931498f), new float2(-0.7306287391f, -0.6827749597f), new float2(-0.7590952064f, -0.6509796216f), new float2(-0.907053853f, 0.4210146171f), new float2(-0.5104861064f, -0.8598860013f), new float2(0.8613350597f, 0.5080373165f), new float2(0.5007881595f, -0.8655698812f),
			new float2(-0.654158152f, 0.7563577938f), new float2(-0.8382755311f, -0.545246856f), new float2(0.6940070834f, 0.7199681717f), new float2(0.06950936031f, 0.9975812994f), new float2(0.1702942185f, -0.9853932612f), new float2(0.2695973274f, 0.9629731466f), new float2(0.5519612192f, -0.8338697815f), new float2(0.225657487f, -0.9742067022f),
			new float2(0.4215262855f, -0.9068161835f), new float2(0.4881873305f, -0.8727388672f), new float2(-0.3683854996f, -0.9296731273f), new float2(-0.9825390578f, 0.1860564427f), new float2(0.81256471f, 0.5828709909f), new float2(0.3196460933f, -0.9475370046f), new float2(0.9570913859f, 0.2897862643f), new float2(-0.6876655497f, -0.7260276109f),
			new float2(-0.9988770922f, -0.047376731f), new float2(-0.1250179027f, 0.992154486f), new float2(-0.8280133617f, 0.560708367f), new float2(0.9324863769f, -0.3612051451f), new float2(0.6394653183f, 0.7688199442f), new float2(-0.01623847064f, -0.9998681473f), new float2(-0.9955014666f, -0.09474613458f), new float2(-0.81453315f, 0.580117012f),
			new float2(0.4037327978f, -0.9148769469f), new float2(0.9944263371f, 0.1054336766f), new float2(-0.1624711654f, 0.9867132919f), new float2(-0.9949487814f, -0.100383875f), new float2(-0.6995302564f, 0.7146029809f), new float2(0.5263414922f, -0.85027327f), new float2(-0.5395221479f, 0.841971408f), new float2(0.6579370318f, 0.7530729462f),
			new float2(0.01426758847f, -0.9998982128f), new float2(-0.6734383991f, 0.7392433447f), new float2(0.639412098f, -0.7688642071f), new float2(0.9211571421f, 0.3891908523f), new float2(-0.146637214f, -0.9891903394f), new float2(-0.782318098f, 0.6228791163f), new float2(-0.5039610839f, -0.8637263605f), new float2(-0.7743120191f, -0.6328039957f),
		});

		X_PRIME = 1619;
		Y_PRIME = 31337;

		m_cellularJitter = 0.45f;
	}

	public void Dispose()
	{
		CELL_2D.Dispose();
	}

    public CellData GetEdgeData(float x, float y, int m_seed, float m_frequency, float perterbAmp)
	{
		if(perterbAmp > 0)SingleGradientPerturb(m_seed, perterbAmp, m_frequency, ref x, ref y);

		x *= m_frequency;
		y *= m_frequency;

		int xr = FastRound(x);
		int yr = FastRound(y);

		float[] distance = { 999999, 999999 };

		//	Store distance[1] index
		int xc1 = 0, yc1 = 0;

		//	Store distance[0] index in case it is assigned to distance[1] later
		int xc0 = 0, yc0 = 0;

		//	All adjacent cell indices and distances
		int[] otherX = new int[9];
		int[] otherY = new int[9];
		float[] otherDist = { 999999, 999999, 999999, 999999, 999999, 999999, 999999, 999999, 999999 };
		int indexCount = 0;

		for (int xi = xr - 1; xi <= xr + 1; xi++)
				{
					for (int yi = yr - 1; yi <= yr + 1; yi++)
					{
						float2 vec = CELL_2D[Hash2D(m_seed, xi, yi) & 255];

						float vecX = xi - x + vec.x * m_cellularJitter;
						float vecY = yi - y + vec.y * m_cellularJitter;

						//	Natural distance function
						//float newDistance = (math.abs(vecX) + math.abs(vecY)) + (vecX * vecX + vecY * vecY);
						
						//	Euclidean distance function
						float newDistance = newDistance = vecX * vecX + vecY * vecY;

						if(newDistance <= distance[1])	//	Math.Min(distance[i], newDistance)
						{
							if(newDistance >= distance[0])	//	Math.Max((newDistance)), distance[i - 1])
							{
								distance[1] = newDistance;
								xc1 = xi;
								yc1 = yi;
							}
							else
							{
								distance[1] = distance[0];
								xc1 = xc0;
								yc1 = yc0;
							}
						}

						if(newDistance <= distance[0])	//	Math.Min(distance[0], newDistance)
						{
							distance[0] = newDistance;
							xc0 = xi;
							yc0 = yi;
						}

						//	Store all adjacent cells
						otherX[indexCount] = xi;
						otherY[indexCount] = yi;
						otherDist[indexCount] = newDistance;
						indexCount++;			
					}
				}

		//	Current cell
		float currentCellValue = To01(ValCoord2D(m_seed, xc0, yc0));
		int currentBiome = TerrainSettings.BiomeIndex(currentCellValue);

		//	Final closest adjacent cell values
		float adjacentEdgeDistance = 999999;
		float adjacentCellValue = 0;

		//	Iterate over all adjacent cells
		for(int i = 0; i < otherDist.Length; i++)
		{	
			//	Find closest cell within smoothing radius
			float dist2Edge = otherDist[i] - distance[0];
			if(dist2Edge < adjacentEdgeDistance)
			{
				float otherCellValue = To01(ValCoord2D(m_seed, otherX[i], otherY[i]));
				int otherBiome = TerrainSettings.BiomeIndex(otherCellValue);

				///	Assign as final value if not current biome
				if(otherBiome != currentBiome)
				{
					adjacentEdgeDistance = dist2Edge;
					adjacentCellValue = otherCellValue;
				}
			}
		}
		if(adjacentEdgeDistance == 999999) adjacentEdgeDistance = 0;
		
		//	Data for use in terrain generation
		return new CellData(	currentCellValue,
								adjacentEdgeDistance,
								adjacentCellValue);
	}

    float ValCoord2D(int seed, int x, int y)
	{
		int n = seed;
		n ^= X_PRIME * x;
		n ^= Y_PRIME * y;

		return (n * n * n * 60493) / (float)2147483648.0;
	}

	int Hash2D(int seed, int x, int y)
	{
		int hash = seed;
		hash ^= X_PRIME * x;
		hash ^= Y_PRIME * y;

		hash = hash * hash * hash * 60493;
		hash = (hash >> 13) ^ hash;

		return hash;
	}

    int FastRound(float f) { return (f >= 0) ? (int)(f + (float)0.5) : (int)(f - (float)0.5); }
	int FastFloor(float f) { return (f >= 0 ? (int)f : (int)f - 1); }

    float To01(float value)
	{
		return (value * 0.5f) + 0.5f;
	}

	void SingleGradientPerturb(int seed, float perturbAmp, float frequency, ref float x, ref float y)
	{
		float xf = x * frequency;
		float yf = y * frequency;

		int x0 = FastFloor(xf);
		int y0 = FastFloor(yf);
		int x1 = x0 + 1;
		int y1 = y0 + 1;

		float xs, ys;
		
		//Interp.Linear:
		xs = xf - x0;
		ys = yf - y0;
		//Interp.Hermite:
		//xs = InterpHermiteFunc(xf - x0);
		//ys = InterpHermiteFunc(yf - y0);
		//Interp.Quintic:
		//xs = InterpQuinticFunc(xf - x0);
		//ys = InterpQuinticFunc(yf - y0);

		float2 vec0 = CELL_2D[Hash2D(seed, x0, y0) & 255];
		float2 vec1 = CELL_2D[Hash2D(seed, x1, y0) & 255];

		float lx0x = math.lerp(vec0.x, vec1.x, xs);
		float ly0x = math.lerp(vec0.y, vec1.y, xs);

		vec0 = CELL_2D[Hash2D(seed, x0, y1) & 255];
		vec1 = CELL_2D[Hash2D(seed, x1, y1) & 255];

		float lx1x = math.lerp(vec0.x, vec1.x, xs);
		float ly1x = math.lerp(vec0.y, vec1.y, xs);

		x += math.lerp(lx0x, lx1x, ys) * perturbAmp;
		y += math.lerp(ly0x, ly1x, ys) * perturbAmp;
	}
	float InterpHermiteFunc(float t) { return t * t * (3 - 2 * t); }
}

public struct CellData
{
    public readonly float currentCellValue, distance2Edge, adjacentCellValue;
    public CellData(float currentCellValue, float distance2Edge, float adjacentCellValue)
    {
        this.currentCellValue = currentCellValue;
        this.distance2Edge = distance2Edge;
        this.adjacentCellValue = adjacentCellValue;
    }
}