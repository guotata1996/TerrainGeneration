using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Priority_Queue;

public class InitTerrianFromHeightMap : MonoBehaviour
{
    // Start is called before the first frame update
    [Header("Input")]public Texture2D heightMap;
    public Material material;

    // Just same as resolutgion of heightMap; larger value makes no sense
    public int heightMapResolution = 513; 

    [Header("Output")]
    public GameObject outTerrian;

    void Start()
    {
        TerrainData dt = new TerrainData();
        dt.heightmapResolution = heightMapResolution;
        dt.size = new Vector3(1000,100,1000);
        dt.baseMapResolution = 256;
        dt.SetDetailResolution(4096, 16);
        
        #region set height
        float[,] heights = new float[heightMapResolution, heightMapResolution];
        for (int i = 0; i != heightMapResolution; ++i){
            for (int j = 0; j != heightMapResolution; ++j){
                heights[i,j] = heightMap.GetPixelBilinear(((float)i) / (heightMapResolution - 1), ((float)j) / (heightMapResolution - 1)).r;
                //heights[i,j] = Mathf.PerlinNoise(((float)i) / 256, ((float)j) / 256);
            }
        }
        dt.SetHeights(0,0, heights);
        
        #endregion

        #region set layers
        // float[,,] alphaMap = new float[dt.alphamapWidth, dt.alphamapHeight, 2];
        // for (int i = 0; i != dt.alphamapWidth; ++i){
        //     for (int j = 0; j != dt.alphamapHeight; ++j){
        //         alphaMap[i,j,0] = Mathf.Abs(i-j) < 20 ? 0 : 1;
        //         alphaMap[i,j,1] = 1 - alphaMap[i,j,0];
        //     }
        // }
        // dt.SetAlphamaps(0, 0, alphaMap);

        #endregion
        outTerrian = Terrain.CreateTerrainGameObject(dt);
        outTerrian.GetComponent<Terrain>().materialTemplate = material;

        Texture2D waterlevel = GetComponent<RiverCreator>().ComputeWaterLevel(true);
        outTerrian.GetComponent<Terrain>().materialTemplate.SetTexture("_WaterLevel", waterlevel);
        Texture2D lakes = GetComponent<RiverCreator>().GatherBasinWater(waterlevel);
        outTerrian.GetComponent<Terrain>().materialTemplate.SetTexture("_Lakes", lakes);
    }
}
