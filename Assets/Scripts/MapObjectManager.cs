using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Places random objects on three Tilemaps (Frost/Forest/Dessert).
// Configure prefab lists per tilemap in the Inspector and control overall spawn density and per-prefab weights.
public class MapObjectManager : MonoBehaviour
{
    [Header("Tilemap GameObjects")]
    public GameObject tilemapFrost;
    public GameObject tilemapForest;
    public GameObject tilemapDessert;

    // cached Tilemap components
    private Tilemap frostTilemap;
    private Tilemap forestTilemap;
    private Tilemap dessertTilemap;

    [Header("Prefabs Per Area")]
    public List<GameObject> frostPrefabs = new List<GameObject>();
    public List<GameObject> forestPrefabs = new List<GameObject>();
    public List<GameObject> dessertPrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    // default probabilities (lowered slightly to reduce density)
    [Range(0f, 1f)] public float frostSpawnProbability = 0.02f;
    [Range(0f, 1f)] public float forestSpawnProbability = 0.02f;
    [Range(0f, 1f)] public float dessertSpawnProbability = 0.02f;

    [Header("Minimum spacing (world units)")]
    // minimum allowed distance between spawned objects (helps avoid overlap)
    public float frostMinSpacing = 1f;
    public float forestMinSpacing = 1f;
    public float dessertMinSpacing = 1f;
    [Header("Density Mode (alternative to per-tile probability)")]
    // When true, spawn by selecting a fraction of valid tiles rather than per-tile random sampling.
    public bool useDensityMode = true;
    [Range(0f, 1f)] public float frostSpawnDensity = 0.02f;
    [Range(0f, 1f)] public float forestSpawnDensity = 0.02f;
    [Range(0f, 1f)] public float dessertSpawnDensity = 0.02f;
    [Header("Edge Padding")]
    // extra safety margin (world units) to avoid placing prefabs too close to the tilemap edge
    public float edgePadding = 0.15f;
    // Layer mask used when doing physics overlap checks to avoid placing objects over colliders (tilemap colliders, other static geometry)
    public LayerMask placementMask = ~0;
    [Header("Placement Collision")]
    // Layers to test for physics overlap when placing objects (tilemap colliders, other world colliders)


    // Optional per-prefab weights (if empty, prefabs chosen uniformly)
    public List<float> frostPrefabWeights = new List<float>();
    public List<float> forestPrefabWeights = new List<float>();
    public List<float> dessertPrefabWeights = new List<float>();

    [Header("Parent Containers for Spawned Objects")]
    public Transform frostParent;
    public Transform forestParent;
    public Transform dessertParent;

    // track spawned objects so we can clear them
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    private void Start()
    {
        frostTilemap = tilemapFrost != null ? tilemapFrost.GetComponent<Tilemap>() : null;
        forestTilemap = tilemapForest != null ? tilemapForest.GetComponent<Tilemap>() : null;
        dessertTilemap = tilemapDessert != null ? tilemapDessert.GetComponent<Tilemap>() : null;
    }

    // Public API
    public void GenerateAll()
    {
        if (frostTilemap != null) GenerateForTilemap(frostTilemap, frostPrefabs, frostPrefabWeights, frostSpawnProbability, frostParent, frostMinSpacing, useDensityMode, frostSpawnDensity);
        if (forestTilemap != null) GenerateForTilemap(forestTilemap, forestPrefabs, forestPrefabWeights, forestSpawnProbability, forestParent, forestMinSpacing, useDensityMode, forestSpawnDensity);
        if (dessertTilemap != null) GenerateForTilemap(dessertTilemap, dessertPrefabs, dessertPrefabWeights, dessertSpawnProbability, dessertParent, dessertMinSpacing, useDensityMode, dessertSpawnDensity);
    }

    public void ClearAll()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            var go = spawnedObjects[i];
            if (go != null) DestroyImmediate(go);
            spawnedObjects.RemoveAt(i);
        }
    }

    public void RegenerateAll()
    {
        ClearAll();
        GenerateAll();
    }

    // Context menu helpers so designers can run these from the component's context menu
    [ContextMenu("Generate All Objects")]
    private void ContextGenerateAll() { GenerateAll(); }

    [ContextMenu("Clear All Objects")]
    private void ContextClearAll() { ClearAll(); }

    [ContextMenu("Regenerate All Objects")]
    private void ContextRegenerateAll() { RegenerateAll(); }

    // Generate objects on a specific Tilemap
    private void GenerateForTilemap(Tilemap tm, List<GameObject> prefabs, List<float> weights, float spawnProb, Transform parent, float minSpacing, bool densityMode, float spawnDensity)
    {
        if (tm == null) return;
        if (prefabs == null || prefabs.Count == 0) return;

        // prepare weights
        var w = NormalizeWeights(prefabs, weights);
        var bounds = tm.cellBounds;

        // collect candidate cells that have tiles
        var candidateCells = new List<Vector3Int>();
        for (int x = bounds.xMin; x <= bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y <= bounds.yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                var tile = tm.GetTile(cell);
                if (tile == null) continue;
                candidateCells.Add(cell);
            }
        }

        if (candidateCells.Count == 0) return;

        // compute cell-based spacing radius using tile size
        var cellSize = tm.cellSize;
        float referenceCellSize = Mathf.Max(cellSize.x, cellSize.y);
        int spacingCells = 0;
        if (minSpacing > 0f && referenceCellSize > 0f) spacingCells = Mathf.CeilToInt(minSpacing / referenceCellSize);

        // compute tilemap world bounds (outer bounds) to prevent edge overflow
        // Use cell centers to compute min/max then expand by half a cell to get full tile extents in world space
        var minCenter = tm.GetCellCenterWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0));
        var maxCenter = tm.GetCellCenterWorld(new Vector3Int(bounds.xMax, bounds.yMax, 0));
        var halfCell = new Vector3(cellSize.x * 0.5f, cellSize.y * 0.5f, 0f);
        var worldMin = minCenter - halfCell;
        var worldMax = maxCenter + halfCell;

        // blocked cells set (in cell coordinates) to enforce spacing when placing
        var blocked = new HashSet<Vector3Int>();

        if (densityMode)
        {
            int targetCount = Mathf.Clamp(Mathf.RoundToInt(candidateCells.Count * spawnDensity), 0, candidateCells.Count);
            // shuffle candidate indices
            for (int i = 0; i < candidateCells.Count; i++)
            {
                int j = Random.Range(i, candidateCells.Count);
                var tmp = candidateCells[i]; candidateCells[i] = candidateCells[j]; candidateCells[j] = tmp;
            }

            int placed = 0;
            for (int i = 0; i < candidateCells.Count && placed < targetCount; i++)
            {
                var cell = candidateCells[i];
                if (blocked.Contains(cell)) continue;

                // choose prefab
                int idx = ChooseIndexByWeights(w);
                var prefab = prefabs[Mathf.Clamp(idx, 0, prefabs.Count - 1)];
                if (prefab == null) continue;

                // determine prefab size in world units and compute padding/spacing in cells
                float prefabDiameter = GetPrefabMaxDiameter(prefab);
                int padCells = 0;
                if (referenceCellSize > 0f) padCells = Mathf.CeilToInt((prefabDiameter * 0.5f) / referenceCellSize);
                // skip cells too close to tilemap edge so prefab won't overflow
                if (cell.x < bounds.xMin + padCells || cell.x > bounds.xMax - padCells || cell.y < bounds.yMin + padCells || cell.y > bounds.yMax - padCells) continue;

                var worldPos = tm.GetCellCenterWorld(cell);
                var jitter = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f), 0f);
                worldPos += jitter;

                float prefabRadius = prefabDiameter * 0.5f;
                // ensure prefab fits inside tilemap world bounds with extra edge padding
                float pad = prefabRadius + edgePadding;
                if (worldPos.x - pad < worldMin.x || worldPos.x + pad > worldMax.x || worldPos.y - pad < worldMin.y || worldPos.y + pad > worldMax.y) continue;

                // physics overlap check
                float placementRadius = prefabRadius + edgePadding;
                if (Physics2D.OverlapCircle(worldPos, placementRadius, placementMask) != null) continue;

                // instance-distance check against already spawned objects (use renderer bounds)
                bool collided = false;
                for (int si = 0; si < spawnedObjects.Count; si++)
                {
                    var ex = spawnedObjects[si];
                    if (ex == null) continue;
                    float exDia = GetInstanceMaxDiameter(ex);
                    float exRad = exDia * 0.5f;
                    float minDist = (placementRadius + exRad);
                    if ((ex.transform.position - worldPos).sqrMagnitude < (minDist * minDist)) { collided = true; break; }
                }
                if (collided) continue;

                var go = Instantiate(prefab, worldPos, Quaternion.identity, parent != null ? parent : tm.transform);
                spawnedObjects.Add(go);
                placed++;

                // mark blocked neighboring cells according to prefab size + minSpacing
                int localSpacing = 1;
                if (referenceCellSize > 0f) localSpacing = Mathf.CeilToInt((minSpacing + prefabDiameter) / referenceCellSize);
                if (localSpacing > 0)
                {
                    for (int dx = -localSpacing; dx <= localSpacing; dx++)
                    {
                        for (int dy = -localSpacing; dy <= localSpacing; dy++)
                        {
                            blocked.Add(new Vector3Int(cell.x + dx, cell.y + dy, 0));
                        }
                    }
                }
            }
        }
        else
        {
            // per-tile probability sampling with spacing enforcement
            foreach (var cell in candidateCells)
            {
                if (Random.value > spawnProb) continue;
                if (blocked.Contains(cell)) continue;

                int idx = ChooseIndexByWeights(w);
                var prefab = prefabs[Mathf.Clamp(idx, 0, prefabs.Count - 1)];
                if (prefab == null) continue;

                // compute prefab diameter and padding in cells
                float prefabDiameter = GetPrefabMaxDiameter(prefab);
                int padCells = 0;
                if (referenceCellSize > 0f) padCells = Mathf.CeilToInt((prefabDiameter * 0.5f) / referenceCellSize);
                if (cell.x < bounds.xMin + padCells || cell.x > bounds.xMax - padCells || cell.y < bounds.yMin + padCells || cell.y > bounds.yMax - padCells) continue;

                var worldPos = tm.GetCellCenterWorld(cell);
                var jitter = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f), 0f);
                worldPos += jitter;

                float prefabRadius = prefabDiameter * 0.5f;
                // ensure prefab fits inside tilemap world bounds with extra edge padding
                float pad2 = prefabRadius + edgePadding;
                if (worldPos.x - pad2 < worldMin.x || worldPos.x + pad2 > worldMax.x || worldPos.y - pad2 < worldMin.y || worldPos.y + pad2 > worldMax.y) continue;

                // physics overlap check
                float placementRadius2 = prefabRadius + edgePadding;
                if (Physics2D.OverlapCircle(worldPos, placementRadius2, placementMask) != null) continue;

                // instance-distance check versus spawnedObjects
                bool collided2 = false;
                for (int si = 0; si < spawnedObjects.Count; si++)
                {
                    var ex = spawnedObjects[si];
                    if (ex == null) continue;
                    float exDia = GetInstanceMaxDiameter(ex);
                    float exRad = exDia * 0.5f;
                    float minDist = (placementRadius2 + exRad);
                    if ((ex.transform.position - worldPos).sqrMagnitude < (minDist * minDist)) { collided2 = true; break; }
                }
                if (collided2) continue;

                var go = Instantiate(prefab, worldPos, Quaternion.identity, parent != null ? parent : tm.transform);
                spawnedObjects.Add(go);

                int localSpacing = 1;
                if (referenceCellSize > 0f) localSpacing = Mathf.CeilToInt((minSpacing + prefabDiameter) / referenceCellSize);
                if (localSpacing > 0)
                {
                    for (int dx = -localSpacing; dx <= localSpacing; dx++)
                    {
                        for (int dy = -localSpacing; dy <= localSpacing; dy++)
                        {
                            blocked.Add(new Vector3Int(cell.x + dx, cell.y + dy, 0));
                        }
                    }
                }
            }
        }
    }

    // Normalize weights: if weights list mismatches prefab count or sums to zero, return uniform weights
    private float[] NormalizeWeights(List<GameObject> prefabs, List<float> weights)
    {
        int n = prefabs.Count;
        var outW = new float[n];
        if (weights == null || weights.Count != n)
        {
            for (int i = 0; i < n; i++) outW[i] = 1f / n;
            return outW;
        }

        float sum = 0f;
        for (int i = 0; i < n; i++)
        {
            float v = Mathf.Max(0f, weights[i]);
            outW[i] = v;
            sum += v;
        }
        if (sum <= Mathf.Epsilon)
        {
            for (int i = 0; i < n; i++) outW[i] = 1f / n;
            return outW;
        }
        for (int i = 0; i < n; i++) outW[i] /= sum;
        return outW;
    }

    private int ChooseIndexByWeights(float[] weights)
    {
        float r = Random.value;
        float acc = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i];
            if (r <= acc) return i;
        }
        return weights.Length - 1;
    }

    // Compute approximate prefab max diameter (world units) by sampling its Renderers' bounds.
    private float GetPrefabMaxDiameter(GameObject prefab)
    {
        if (prefab == null) return 0.5f;
        var rends = prefab.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0) return 0.5f;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        // Use the maximum of x/y size as diameter approximation
        float dx = b.size.x;
        float dy = b.size.y;
        float max = Mathf.Max(dx, dy);
        if (max <= 0f) max = 0.5f;
        return max;
    }

    // Compute approximate instance max diameter by sampling its Renderers' bounds.
    private float GetInstanceMaxDiameter(GameObject go)
    {
        if (go == null) return 0.5f;
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0) return 0.5f;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        float dx = b.size.x;
        float dy = b.size.y;
        float max = Mathf.Max(dx, dy);
        if (max <= 0f) max = 0.5f;
        return max;
    }
}
