using UnityEngine;
using Unity.Mathematics;
using MyComponents;

namespace Noise
{
    public struct TopologyUtility
    {
        int typeCount;
        int marginDepth;


        /*public Topology Type(CellData cell)
        {
            TerrainTypes type = 0;

            float cellValue = cell.currentCellValue;
            float adjacentValue = cell.adjacentCellValue;

            float increment = 1.0f / typeCount;

            float cellHeight = math.lerp(0, typeCount, cellValue) * levelHeight;
            float adjacentHeight = math.lerp(0, typeCount, adjacentValue) * levelHeight;
            
            //  Close to the edge between two cells of different heights = cliff
            if(cell.distance2Edge < marginDepth*2 && cellHeight != adjacentHeight)
            {
                type = TerrainTypes.CLIFF;            
            
                //  Closer to the edge between cells, interpolate
                //  between cell heigts for smooth transition
                if(cell.distance2Edge < marginDepth) 
                {
                    float halfway = (cellHeight + adjacentHeight) / 2;
                    float interpolator = Mathf.InverseLerp(0, marginDepth, cell.distance2Edge);

                    //  Interpolate towards midpoint using distance from midpoint
                    height += (int)math.lerp(halfway, cellHeight, interpolator);
                }
                else
                    height += (int)cellHeight;
            }
            //  If not cliff then grass
            else
            {
                type = TerrainTypes.GRASS;
                height += (int)cellHeight;
            }

            return new Topology{
                height = height,
                type = type
            };
        } */

    }

}