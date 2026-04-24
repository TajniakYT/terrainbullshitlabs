using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Terrain))]
public class TerrainGenerator : MonoBehaviour
{
    private Terrain terrain;
    private TerrainData terrainData;

    private int xRes;
    private int yRes;

    public int numberOfPasses = 5;

    [Header("Player")]
    public Transform player;
    public float safeRadius = 30f;

    [Header("Transition")]
    public float transitionDuration = 3f;

    [Header("Triggers")]
    public GameObject triggerPrefab;
    public int triggerCount = 10;

    public float grockBorder = 0.43f;
    public float rnowBorder = 0.5f;

    private float[,] currentHeights;
    private float[,] startHeights;
    private float[,] targetHeights;
    private SpringJoint playerSpringJoint;

    private bool isTransitioning = false;

    void Start()
    {
        terrain = GetComponent<Terrain>();
        terrainData = terrain.terrainData;

        xRes = terrainData.heightmapResolution;
        yRes = terrainData.heightmapResolution;

        if (player != null)
            playerSpringJoint = player.GetComponent<SpringJoint>();

        GenerateInitialTerrain();
        UpdateTerrainTextures();
        //SpawnTriggers();
    }

    // =========================
    // GENEROWANIE TERENU
    // =========================
    void GenerateInitialTerrain()
    {
        currentHeights = GenerateNoise();
        terrainData.SetHeights(0, 0, currentHeights);
    }

    float[,] GenerateNoise()
    {
        float[,] heights = new float[xRes, yRes];

        float[] scale = new float[numberOfPasses];
        for (int k = 0; k < numberOfPasses; k++)
            scale[k] = numberOfPasses * 1.75f - k;

        float offsetX = Random.Range(0f, 1000f);
        float offsetY = Random.Range(0f, 1000f);

        for (int i = 0; i < xRes; i++)
        {
            for (int j = 0; j < yRes; j++)
            {
                float x = (float)i / xRes;
                float y = (float)j / yRes;

                float h = 0;

                for (int k = 0; k < numberOfPasses; k++)
                {
                    h += Mathf.PerlinNoise(
                        x * scale[k] + offsetX,
                        y * scale[k] + offsetY
                    );
                }

                heights[i, j] = h / numberOfPasses;
            }
        }

        return heights;
    }

    // =========================
    // TRANSITION
    // =========================
    public void StartTerrainTransition(Vector3 impactPoint)
    {
        if (isTransitioning) return;

        startHeights = terrainData.GetHeights(0, 0, xRes, yRes);
        targetHeights = GenerateNoise();

        if (playerSpringJoint != null) {
            playerSpringJoint.minDistance = 0f;
        }
        StartCoroutine(BlendTerrain(impactPoint));
    }

    IEnumerator BlendTerrain(Vector3 impactPoint)
    {
        isTransitioning = true;

        float time = 0f;

        while (time < transitionDuration)
        {
            float t = time / transitionDuration;

            float[,] newHeights = new float[xRes, yRes];

            Vector3 playerPos = player.position;

            for (int i = 0; i < xRes; i++)
            {
                for (int j = 0; j < yRes; j++)
                {
                    float worldX = (float)i / xRes * terrainData.size.x;
                    float worldZ = (float)j / yRes * terrainData.size.z;

                    float dist = Vector2.Distance(
                     new Vector2(worldX, worldZ),
                     new Vector2(impactPoint.x, impactPoint.z)
                    );

                    float mask = Mathf.Clamp01(dist / safeRadius);

                    float h = Mathf.Lerp(startHeights[i, j], targetHeights[i, j], t);

                    newHeights[i, j] = Mathf.Lerp(startHeights[i, j], h, mask);
                }
            }

            terrainData.SetHeights(0, 0, newHeights);
            terrain.Flush();
            time += Time.deltaTime;
            yield return null;
        }

        currentHeights = targetHeights;
        UpdateTerrainTextures();
        isTransitioning = false;
        if (playerSpringJoint != null)
        {
            playerSpringJoint.minDistance = 10000f;
        }
    }

    // =========================
    // TRIGGERS
    // =========================
    void SpawnTriggers()
    {
        for (int i = 0; i < triggerCount; i++)
        {
            float x = Random.Range(0, terrainData.size.x);
            float z = Random.Range(0, terrainData.size.z);

            float y = terrain.SampleHeight(new Vector3(x, 0, z));

            Vector3 pos = new Vector3(x, y + 1f, z);

            GameObject t = Instantiate(triggerPrefab, pos, Quaternion.identity);

            TerrainTrigger trig = t.GetComponent<TerrainTrigger>();
            trig.terrain = this;
        }
    }

    private void UpdateTerrainTextures()
    {
        int alphaRes = terrainData.alphamapResolution;
        float[,,] splatmapData = new float[alphaRes, alphaRes, terrainData.alphamapLayers];

        for (int y = 0; y < alphaRes; y++)
        {
            for (int x = 0; x < alphaRes; x++)
            {
                float normX = (float)x / (alphaRes - 1);
                float normY = (float)y / (alphaRes - 1);

                float height = terrainData.GetInterpolatedHeight(normX, normY) / terrainData.size.y;

                float[] weights = new float[terrainData.alphamapLayers];

                // Przyk�ad: 3 tekstury (0 = trawa, 1 = ska�a, 2 = �nieg)
                float blendRange = 0.05f; // szeroko�� przej�cia (dostosuj!)

                float grassWeight = 0f;
                float rockWeight = 0f;
                float snowWeight = 0f;

                // Trawa -> Ska�a
                float t1 = Mathf.InverseLerp(grockBorder - blendRange, grockBorder + blendRange, height);
                t1 = Mathf.Clamp01(t1);

                // Ska�a -> �nieg
                float t2 = Mathf.InverseLerp(rnowBorder - blendRange, rnowBorder + blendRange, height);
                t2 = Mathf.Clamp01(t2);

                // Wyliczanie wag
                grassWeight = 1f - t1;
                rockWeight = t1 * (1f - t2);
                snowWeight = t2;

                // Przypisanie
                weights[0] = grassWeight;
                weights[1] = rockWeight;
                weights[2] = snowWeight;

                // Normalizacja (wa�ne!)
                float sum = 0;
                for (int i = 0; i < weights.Length; i++) sum += weights[i];
                for (int i = 0; i < weights.Length; i++) weights[i] /= sum;

                for (int i = 0; i < weights.Length; i++)
                {
                    splatmapData[y, x, i] = weights[i];
                }
            }
        }

        terrainData.SetAlphamaps(0, 0, splatmapData);
    }
}