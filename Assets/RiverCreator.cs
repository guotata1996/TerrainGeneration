using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Priority_Queue;

[RequireComponent(typeof(InitTerrianFromHeightMap))]
public class RiverCreator : MonoBehaviour
{
    [SerializeField]public int randomCount = 100000;
    [SerializeField]public float penetrationRate = 0.01f;
    [SerializeField]public float dirRandomNess = 0.05f;
    [SerializeField]public float stepLength = 1f;
    [SerializeField]int maxTraceLength = 300;
    [SerializeField] protected ComputeShader computeShader;
    [SerializeField] public float lowSnowline, highSnowline;

    //For ice, (pene, rand) = (0.05, 0.1)

    List<Vector3> candidate_dirs = new List<Vector3>{Vector3.forward, Vector3.left, Vector3.back, Vector3.right, 
    (Vector3.forward + Vector3.left).normalized,(Vector3.left + Vector3.back).normalized,
    (Vector3.back + Vector3.right).normalized, (Vector3.right + Vector3.forward).normalized};
    public Texture2D ComputeWaterLevel(bool useGPU)
    {
        Terrain terrian = GetComponent<InitTerrianFromHeightMap>().outTerrian.GetComponent<Terrain>();
        int resolution = GetComponent<InitTerrianFromHeightMap>().heightMapResolution;
        Texture2D outWaterTexture;
        if (!useGPU){
            float[,] waterlevel = new float[resolution, resolution];
            for (int run = 0; run != randomCount; ++run){
                float x = Random.Range(0f, (float)resolution - 0.001f);
                float y = Random.Range(0f, (float)resolution - 0.001f);
                float waterleft = 1;
                for (int step = 0; step != maxTraceLength; ++step){
                    waterlevel[Mathf.FloorToInt(x),Mathf.FloorToInt(y)] += waterleft * penetrationRate;
                    waterleft *= (1f - penetrationRate);
                    Vector3 norm = terrian.terrainData.GetInterpolatedNormal(x / resolution, y / resolution);
                    Vector2 stepDir = new Vector2(norm.x, norm.z);
                    if (stepDir.magnitude < 1e-3){
                        break;
                    }
                    else{
                        norm.y *= 0.4f;
                        norm.Normalize();
                        List<float> probs = candidate_dirs.ConvertAll(v => Mathf.Max(0, Vector3.Dot(v, norm)));
                        float sumProbs = 0;
                        probs.ForEach(p => sumProbs += p);
                        probs.ForEach(p => p /= sumProbs);
                        int dirindex = SampleWithProb(probs);
                        Vector3 choosen = candidate_dirs[dirindex];
                        x += choosen.x;
                        y += choosen.z;
                        if (x < 0 || x >= resolution || y < 0 || y >= resolution){
                            break;
                        }
                    }
                }
            }

            outWaterTexture = new Texture2D(resolution, resolution);
            for (int i = 0; i != resolution; ++i){
                for (int j = 0; j != resolution; ++j){
                    outWaterTexture.SetPixel(i, j, new Color(waterlevel[i, j], 0, 0));
                }
            }
            outWaterTexture.Apply();
        }
        else{
            int kernel = computeShader.FindKernel("CSMain");
            Texture2D interpolatedNormals = new Texture2D(resolution + 1, resolution + 1);
            for (int i = 1; i < resolution; ++i){
                for (int j = 1; j < resolution; ++j){
                    Vector3 interpolatedNormal = terrian.terrainData.GetInterpolatedNormal(
                    (i - 0.5f) / (resolution - 1),
                    (j - 0.5f) / (resolution - 1));
                    // Height normalized to [0,1];
                    float interpolatedH = terrian.terrainData.GetInterpolatedHeight(
                    (i - 0.5f) / (resolution - 1),
                    (j - 0.5f) / (resolution - 1)
                    )/100f;
                    interpolatedNormals.SetPixel(i, j, new Color(interpolatedNormal.x * 0.5f + 0.5f, interpolatedNormal.y * 0.5f + 0.5f, interpolatedNormal.z * 0.5f + 0.5f, interpolatedH));
                }
            }
            for (int i = 0; i != resolution + 1; ++i){
                interpolatedNormals.SetPixel(0, i, new Color(0, 1, 0, interpolatedNormals.GetPixel(1, i).a));
                interpolatedNormals.SetPixel(i, 0, new Color(0, 1, 0, interpolatedNormals.GetPixel(i, 1).a));
                interpolatedNormals.SetPixel(resolution, i, new Color(0, 1, 0, interpolatedNormals.GetPixel(resolution - 1, i).a));
                interpolatedNormals.SetPixel(i, resolution, new Color(0, 1, 0, interpolatedNormals.GetPixel(i, resolution - 1).a));
            }
            interpolatedNormals.Apply();
            computeShader.SetTexture(kernel, "NormalandH", interpolatedNormals);

            RenderTexture waterlevel = new RenderTexture(resolution - 1, resolution - 1, 24);
            waterlevel.enableRandomWrite = true;
            waterlevel.Create();
            computeShader.SetTexture(kernel, "waterlevel", waterlevel);
            computeShader.SetInt("waterlevelResolution", resolution - 1);
            computeShader.SetFloat("lowSnowLine", lowSnowline);
            computeShader.SetFloat("highSnowLine", highSnowline);
            int groups = Mathf.RoundToInt(Mathf.Sqrt((float)randomCount)) / 8;
            computeShader.Dispatch(kernel, groups, groups, 1);

            RenderTexture.active = waterlevel;

            outWaterTexture = new Texture2D(resolution - 1, resolution - 1);
            outWaterTexture.ReadPixels(new Rect(0, 0, waterlevel.width, waterlevel.height), 0, 0);
            outWaterTexture.Apply();
            
        }
        return outWaterTexture;
    }

    int SampleWithProb(List<float> prob){
        float p = Random.Range(0f,1f);
        float cumulativeP = 0;

        for (int i = 0; i != prob.Count; ++i){
            cumulativeP += prob[i];
            if (cumulativeP > p){
                return i;
            }
        }
        return prob.Count - 1;
    }

    public Texture2D GatherBasinWater(Texture2D waterlevel){
        int waterResolution = waterlevel.width;

        Texture2D lakeTexture = new Texture2D(waterResolution, waterResolution);
        var fillColorArray = lakeTexture.GetPixels();
        for (int i = 0; i != fillColorArray.Length; ++i){
            fillColorArray[i] = new Color(0,0,0);
        }
        lakeTexture.SetPixels(fillColorArray);
        lakeTexture.Apply();
        
        int exp_cnt = 0;

        for (int wi = 0; wi != waterResolution; ++wi){
            for (int wj = 0; wj != waterResolution; ++wj){
                if (lakeTexture.GetPixel(wi, wj).r < 0.01f && waterlevel.GetPixel(wi,wj).g > 0.99f){
                    exp_cnt++;
                    MergeLakeFrom(wi, wj, lakeTexture, waterlevel);
                }
            }
        }
        return lakeTexture;
    }

    private void MergeLakeFrom(int x, int y, Texture2D lakeTexture, Texture2D waterlevel){
        Terrain terrian = GetComponent<InitTerrianFromHeightMap>().outTerrian.GetComponent<Terrain>();
        LakeNeighbor.ter = terrian;
        LakeNeighbor.waterlevel = waterlevel;
        int waterResolution = waterlevel.width;

        HashSet<Vector2Int> newConfirmedLakeGrids = new HashSet<Vector2Int>();

        /*Not necessarily to be considered as lake, until lake surface rises above*/
        SimplePriorityQueue<LakeNeighbor> lakeBorder = new SimplePriorityQueue<LakeNeighbor>();
        /*Set up Hash just to avoid duplication */
        HashSet<Vector2Int> lakeBorderHash = new HashSet<Vector2Int>();
 
        LakeNeighbor start = new LakeNeighbor(x, y);
        lakeBorder.Enqueue(start, start.height);
        lakeBorderHash.Add(new Vector2Int(x, y));
        float lakeInflow = 0;
        int lakeArea = 0;
        float lakeSurfaceHeight = 0;
        float confirmedLakeSurfaceHeight = 0;

        /*To be considered as lake before formally added to lakeTexture, waiting for border confirmation */
        HashSet<Vector2Int> pendingGrids = new HashSet<Vector2Int>();
        int step = 0;

        while (lakeBorder.Count > 0){
            step++;

            LakeNeighbor n = lakeBorder.Dequeue();
            lakeBorderHash.Remove(new Vector2Int(n.x, n.y));

            // When reaches border of map, discard pending and abort
            if (n.x - 1 < 0 || n.x + 1 >= waterResolution || n.y - 1 < 0 || n.y + 1 >= waterResolution){
                break;
            }

            if (n.height > lakeSurfaceHeight){
                // last border is confirmed:
                foreach(Vector2Int grid in pendingGrids){
                    //lakeTexture.SetPixel(grid.x, grid.y, new Color(1,0,0,0));
                    newConfirmedLakeGrids.Add(new Vector2Int(grid.x, grid.y));
                }
                pendingGrids.Clear();
                confirmedLakeSurfaceHeight = lakeSurfaceHeight;
                // try rising lake surface
                lakeSurfaceHeight = n.height;
            }

            if (!lakeInflowHolds(lakeInflow + n.waterLevel, lakeArea + 1)){
                break;
            }
            else{
                lakeInflow += n.waterLevel;
                lakeArea++;
                pendingGrids.Add(new Vector2Int(n.x, n.y));

                for (int dx = -1; dx <= 1; ++dx){
                    for (int dy = -1; dy <= 1; ++dy){
                        Vector2Int newGrid = new Vector2Int(n.x + dx, n.y + dy);
                        if (
                            (dx != 0 || dy != 0) 
                        && !newConfirmedLakeGrids.Contains(newGrid)
                        && !lakeBorderHash.Contains(newGrid)
                        && !pendingGrids.Contains(newGrid)){
                            var newItem = new LakeNeighbor(n.x + dx, n.y+dy);
                            lakeBorder.Enqueue(newItem, newItem.height);
                            lakeBorderHash.Add(newGrid);
                        }
                    }
                }
            }
        }

        foreach(var grid in newConfirmedLakeGrids){
            lakeTexture.SetPixel(grid.x, grid.y, new Color(1, confirmedLakeSurfaceHeight, 0));
        }
        lakeTexture.Apply();
    }

    private bool lakeInflowHolds(float waterLevelTotal, int waterArea){
        return waterLevelTotal / (float)waterArea > 0.75f;
    }
}

struct LakeNeighbor{
    public int x, y;
    public float height;
    public float waterLevel;
    public static Terrain ter;
    public static Texture2D waterlevel;
    public LakeNeighbor(int x, int y){
        this.x = x;
        this.y = y;
        this.height = ter.terrainData.GetInterpolatedHeight((x + 0.5f)/waterlevel.width , (y + 0.5f) / waterlevel.height) * 0.01f;
        this.waterLevel = waterlevel.GetPixel(x, y).r;
    }
};
