using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MeshGenerator : MonoBehaviour
{
    Mesh mesh;

    Material material;

    Vector3[] vertices;
    int[] triangles;
    Vector2[] uvs;

    int xSize = 100;

    float scale = 10f;

    float scale2 = 2f;

    /* flow[directional edge] 
     * = outflow of triangle on RHS of edge
     * - outflow of triangle on LHS of edge
     */

    Dictionary<Vector2Int, float> flow, flowTmp;

    Dictionary<Vector2Int, GameObject> flowIndicators;

    Dictionary<Vector2Int, bool> boundary;
    
    [SerializeField]
    GameObject flowIndicatorPrefab;

    [SerializeField]
    float sedimentIntensity, cutIntensity;

    int flowUpdateCount = 0;

    void Start()
    {
        InitMesh();

        InitMaterial();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterial = material;

        InitFlow();
        InitFlowIndicator();

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            Updateflow();

            UpdateflowIndicater();

            if (flowUpdateCount >= 10)
            {
                UpdateTerrian();
            }

            flowUpdateCount++;
        }
    }

    void InitMesh(){
        mesh = new Mesh();

        vertices = new Vector3[(xSize + 1) * (xSize + 1)];
        uvs = new Vector2[(xSize + 1) * (xSize + 1)];

        for (int i = 0, z = 0; z <= xSize; ++z){
            for (int x = 0; x <= xSize; ++x){
                vertices[i] = new Vector3(x, 5f * Mathf.PerlinNoise(x * scale / xSize, z * scale / xSize) + 25f * Mathf.PerlinNoise(x * scale2 / xSize, z * scale2 / xSize), z);
                //vertices[i] = new Vector3(x, (x + z) * 0.2f, z);
                uvs[i] = new Vector2(x * 1.0f / xSize, z * 1.0f / xSize);
                i++;
            }
        }

        triangles = new int[xSize * xSize * 6];
        int vert = 0, tris = 0;
        for (int z = 0; z < xSize; ++z){
            for (int x = 0; x < xSize; ++x){
                triangles[tris + 0] = vert;
                triangles[tris + 1] = vert + xSize + 1;
                triangles[tris + 2] = vert + 1;
                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + xSize + 1;
                triangles[tris + 5] = vert + xSize + 2;

                vert++;
                tris += 6;
            }
            vert++;
        }


        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.MarkDynamic();

    }

    private void InitMaterial()
    {
        material = new Material(Shader.Find("Standard"));
        Texture2D texture = new Texture2D(xSize, xSize);

        for (int i = 0; i != xSize; ++i){
            for (int j = 0; j != xSize; ++j)
            {
                if ((i + j) % 2 == 0)
                {
                    texture.SetPixel(i, j, Color.blue);
                }
                else
                {
                    texture.SetPixel(i, j, Color.blue);
                }
            }
        }
        texture.Apply();

        material.mainTexture = texture;
    }

    private void InitFlow(){
        flow = new Dictionary<Vector2Int, float>();
        flowTmp = new Dictionary<Vector2Int, float>();
        boundary = new Dictionary<Vector2Int, bool>();

        for (int i = 0; i != triangles.Length / 3; ++i){
            addEdgeIfNotExist(flow, new Vector2Int(triangles[3 * i], triangles[3 * i + 1]), 0f);
            addEdgeIfNotExist(flow, new Vector2Int(triangles[3 * i], triangles[3 * i + 2]), 0f);
            addEdgeIfNotExist(flow, new Vector2Int(triangles[3 * i + 1], triangles[3 * i + 2]), 0f);

            addEdgeIfNotExist(flowTmp, new Vector2Int(triangles[3 * i], triangles[3 * i + 1]), 0f);
            addEdgeIfNotExist(flowTmp, new Vector2Int(triangles[3 * i], triangles[3 * i + 2]), 0f);
            addEdgeIfNotExist(flowTmp, new Vector2Int(triangles[3 * i + 1], triangles[3 * i + 2]), 0f);

            addBoundary(new Vector2Int(triangles[3 * i], triangles[3 * i + 1]));
            addBoundary(new Vector2Int(triangles[3 * i], triangles[3 * i + 2]));
            addBoundary(new Vector2Int(triangles[3 * i + 1], triangles[3 * i + 2]));

        }

    }

    private void InitFlowIndicator(){
        flowIndicators = new Dictionary<Vector2Int, GameObject>();

        foreach (var flowEdge in flow.Keys){
            Vector3 p1 = vertices[flowEdge.x];
            Vector3 p2 = vertices[flowEdge.y];

            flowIndicators.Add(flowEdge, Instantiate(flowIndicatorPrefab, (p1 + p2) * 0.5f, Quaternion.identity));

        }
    }

    private void UpdateTerrian(){

        for (int i = 0; i != triangles.Length / 3; ++i){

            //sediment

            var outFlows = getOutflowDistribution(i);

            float incomingFlow = 0f, outgoingFlow = 1f;
            foreach (var edge in outFlows.Keys)
            {
                if (boundary[edge])
                {
                    continue;
                }

                int thirdVertice = triangles[3 * i] + triangles[3 * i + 1] + triangles[3 * i + 2] - edge.x - edge.y;
                if (testRight(edge, thirdVertice))
                {
                    if (flow[edge] < 0)
                    {
                        incomingFlow -= flow[edge];
                    }
                    else{
                        outgoingFlow += flow[edge];
                    }
                }
                else
                {
                    if (flow[edge] > 0)
                    {
                        incomingFlow += flow[edge];
                    }
                    else{
                        outgoingFlow -= flow[edge];
                    }
                }
            }

            vertices[getLowestVertice(i)].y += incomingFlow * sedimentIntensity;


            //cut

            float cutAmout = outgoingFlow * getSlope(i);
            vertices[getSecondHighestVertice(i)].y -= cutAmout * cutIntensity;
        }

        
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
    }

    private Dictionary<Vector2Int, float> getOutflowDistribution(int triangleIndex){
        Vector3 v0 = vertices[triangles[3 * triangleIndex]];
        Vector3 v1 = vertices[triangles[3 * triangleIndex + 1]];
        Vector3 v2 = vertices[triangles[3 * triangleIndex + 2]];

        Vector3 up3 = Vector3.Cross(v1 - v0, v2 - v0);
        Vector2 gradDown = new Vector2(up3.x, up3.z);
        Vector2 e01 = (triangleIndex % 2 == 0) ? Vector2.left : - new Vector2(1, 1).normalized;
        Vector2 e02 = (triangleIndex % 2 == 0) ? Vector2.down : Vector2.right;
        Vector2 e12 = (triangleIndex % 2 == 0) ? new Vector2(1, 1).normalized : Vector2.up;

        float dote01 = Mathf.Max(0f, Vector2.Dot(e01, gradDown));
        float dote02 = Mathf.Max(0f, Vector2.Dot(e02, gradDown));
        float dote03 = Mathf.Max(0f, Vector2.Dot(e12, gradDown));
        float dotsum = dote01 + dote02 + dote03;

        if (dotsum == 0){
            //flat triangle
            dote01 = dote02 = dote03 = 1f;
            dotsum = 3f;
        }

        var rtn = new Dictionary<Vector2Int, float>();
        addEdgeIfNotExist(rtn, new Vector2Int(triangles[3 * triangleIndex], triangles[3 * triangleIndex + 1]), dote01 / dotsum);
        addEdgeIfNotExist(rtn, new Vector2Int(triangles[3 * triangleIndex], triangles[3 * triangleIndex + 2]), dote02 / dotsum);
        addEdgeIfNotExist(rtn, new Vector2Int(triangles[3 * triangleIndex + 1], triangles[3 * triangleIndex + 2]), dote03 / dotsum);
        return rtn;
    }

    private float getSlope(int triangleIndex){
        Vector3 v0 = vertices[triangles[3 * triangleIndex]];
        Vector3 v1 = vertices[triangles[3 * triangleIndex + 1]];
        Vector3 v2 = vertices[triangles[3 * triangleIndex + 2]];

        Vector3 up3 = Vector3.Cross(v1 - v0, v2 - v0);
        Vector2 horizontalProjection = new Vector2(up3.x, up3.z);
        return horizontalProjection.magnitude / up3.y;
    }

    void Updateflow(){
        foreach(var k in flow.Keys.ToList()){
            flowTmp[k] = flow[k];
            flow[k] = 0f;
        }

        for (int i = 0; i != triangles.Length / 3; ++i){

            var outFlows = getOutflowDistribution(i);

            float totalInflow = 1f;  //perception

            foreach(var edge in outFlows.Keys){
                if (boundary[edge]){
                    continue;
                }

                int thirdVertice = triangles[3 * i] + triangles[3 * i + 1] + triangles[3 * i + 2] - edge.x - edge.y;
                if (testRight(edge, thirdVertice)){
                    if (flowTmp[edge] < 0)
                    {
                        totalInflow -= flowTmp[edge];
                    }
                }
                else{
                    if (flowTmp[edge] > 0)
                    {
                        totalInflow += flowTmp[edge];
                    }
                }
            }


            foreach(Vector2Int edge in outFlows.Keys){
                int thirdVertice = triangles[3 * i] + triangles[3 * i + 1] + triangles[3 * i + 2] - edge.x - edge.y;
                if (testRight(edge, thirdVertice)){
                    flow[edge] += outFlows[edge] * totalInflow;
                }
                else{
                    flow[edge] -= outFlows[edge] * totalInflow;
                }
            }
        }
    }

    void UpdateflowIndicater(){
        foreach(Vector2Int edge in flow.Keys){
            Vector3 edgeDir = vertices[edge.y] - vertices[edge.x];
            Vector3 edgeRightNormal = new Vector3(edgeDir.z, 0f, -edgeDir.x);
            if (flow[edge] > 0){
                //RHS's outflow > LHS's outflow
                flowIndicators[edge].transform.rotation = Quaternion.LookRotation(-edgeRightNormal);
            }
            if (flow[edge] < 0){
                //RHS's outflow < LHS's outflow
                flowIndicators[edge].transform.rotation = Quaternion.LookRotation(edgeRightNormal);
            }

            flowIndicators[edge].transform.localScale = Vector3.one * Mathf.Abs(flow[edge]) * 0.1f;
            flowIndicators[edge].transform.position = (vertices[edge.y] + vertices[edge.x]) / 2f;
        }
    }

    void addEdgeIfNotExist(Dictionary<Vector2Int, float> dict, Vector2Int key, float value){
        if (key.x > key.y){
            int tmp = key.x;
            key.x = key.y;
            key.y = tmp;
        }
        if (!dict.ContainsKey(key)){
            dict.Add(key, value);
        }
    }

    void addBoundary(Vector2Int key){
        if (key.x > key.y){
            int tmp = key.x;
            key.x = key.y;
            key.y = tmp;
        }
        if (!boundary.ContainsKey(key)){
            boundary.Add(key, true);
        }
        else{
            boundary[key] = false;
        }
    }

    bool testRight(Vector2Int directionalEdge, int thirdVerticeIndex){
        Debug.Assert(directionalEdge.x < directionalEdge.y);

        Vector2 p1 = new Vector2(vertices[directionalEdge.x].x, vertices[directionalEdge.x].z);
        Vector2 p2 = new Vector2(vertices[directionalEdge.y].x, vertices[directionalEdge.y].z);
        Vector2 c = new Vector2(vertices[thirdVerticeIndex].x, vertices[thirdVerticeIndex].z);
        Vector2 cP1 = c - p1;
        Vector2 cP2 = c - p2;
        return cP2.x * cP1.y - cP2.y * cP1.x > 0;
    }

    int getLowestVertice(int triangleIndex){
        int i1 = triangles[triangleIndex * 3];
        int i2 = triangles[triangleIndex * 3 + 1];
        int i3 = triangles[triangleIndex * 3 + 2];
        if (vertices[i1].y > vertices[i2].y){
            if (vertices[i2].y > vertices[i3].y){
                return i3;
            }
            else{
                return i2;
            }
        }
        else{
            if (vertices[i1].y > vertices[i3].y){
                return i3;
            }
            else{
                return i1;
            }
        }
    }

    int getHighestVertice(int triangleIndex){
        int i1 = triangles[triangleIndex * 3];
        int i2 = triangles[triangleIndex * 3 + 1];
        int i3 = triangles[triangleIndex * 3 + 2];
        if (vertices[i1].y > vertices[i2].y)
        {
            if (vertices[i3].y > vertices[i1].y)
            {
                return i3;
            }
            else
            {
                return i1;
            }
        }
        else
        {
            if (vertices[i3].y > vertices[i2].y)
            {
                return i3;
            }
            else
            {
                return i2;
            }
        }
    }

    int getSecondHighestVertice(int triangleIndex){
        int i1 = triangles[triangleIndex * 3];
        int i2 = triangles[triangleIndex * 3 + 1];
        int i3 = triangles[triangleIndex * 3 + 2];
        return i1 + i2 + i3 - getLowestVertice(triangleIndex) - getHighestVertice(triangleIndex);
    }
}
