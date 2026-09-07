using System.Collections.Generic;
using UnityEngine;

namespace WhackAMole
{
    public class HoleLayoutGenerator : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private GameObject holePrefab;
        [SerializeField] private int rows = 3;
        [SerializeField] private int columns = 3;

        [Header("Arcade Table")]
        [SerializeField] private Transform arcadeTable;
        [SerializeField] private float tablePadding = 0.12f;
        [SerializeField] private float tableMargin = 0.18f;
        [SerializeField] private float meshBaseRadius = 0.5f;

        [Header("Object Sizes")]
        [SerializeField] private float fixedHoleScale = 0.06f;
        [SerializeField] private float fixedMoleScale = 1.0f;
        [SerializeField] private float outerRowScaleMultiplier = 1.3f;

        [Header("Row Layout")]
        [SerializeField] private float rowSpacing = 0.12f;

        [Header("Shrubs")]
        [SerializeField] private GameObject shrubPrefabA;
        [SerializeField] private GameObject shrubPrefabB;
        [SerializeField] private float shrubSpacing = 0.25f;
        [SerializeField] private float shrubYOffset = 0.0f;

        [Header("ROM Arc Expansion")]
        [SerializeField] private float reachMultiplier = 1.15f;
        [SerializeField] private float angleMultiplier = 1.1f;
        [SerializeField] [Range(0f, 0.15f)] private float edgeColumnRadialSpacing = 0f;
        [SerializeField] private float holeDepthOffset = -0.02f;

        private readonly List<Transform> spawnPoints = new List<Transform>();
        private readonly List<GameObject> generatedHoles = new List<GameObject>();
        private readonly List<GameObject> spawnedShrubs = new List<GameObject>();
        private readonly List<Transform> shrubSpawnPoints = new List<Transform>();

        private int activeRows = 3;
        private int activeColumns = 3;
        private float initialTableTopY = -1f;

        public List<Transform> SpawnPoints => spawnPoints;
        public List<Transform> ShrubSpawnPoints => shrubSpawnPoints;
        public int Columns => activeColumns;
        public int ActiveRows => activeRows;

        public float GetMinRadius(float maxReach) => Mathf.Max(tablePadding, maxReach * 0.35f);

        public void SetGridDimensions(int newRows, int newCols)
        {
            rows = newRows;
            columns = newCols;
        }

        public void GenerateLayoutIfNeeded()
        {
            if (generatedHoles.Count == 0) GenerateLayout();
        }

        public void GenerateLayout()
        {
            if (initialTableTopY < 0f) initialTableTopY = transform.position.y;
            ClearLayout();

            if (holePrefab == null || GameManager.Instance?.RehabProfile == null) return;

            float maxReach = 0.6f, boostedMaxReach = 0.6f;
            float leftAngleLimit = 168f, rightAngleLimit = 11.4f;

            RehabProfileSO profile = GameManager.Instance.RehabProfile;
            maxReach = profile.armLength * Mathf.Clamp01(profile.maxFlexion / 90f);
            boostedMaxReach = maxReach * reachMultiplier;

            float boostedSweepLeft = profile.shoulderHorizontalAdductionMax * angleMultiplier;
            float boostedSweepRight = profile.shoulderHorizontalAdductionMax * angleMultiplier;
            
            leftAngleLimit = 90f + boostedSweepLeft - 5f;
            rightAngleLimit = 90f - boostedSweepRight + 5f;

            float minReach = Mathf.Max(tablePadding, boostedMaxReach * 0.35f);
            int possibleRows = Mathf.FloorToInt((boostedMaxReach - minReach) / rowSpacing) + 1;
            
            activeRows = Mathf.Clamp(possibleRows, 1, 4);
            activeColumns = 3 + (activeRows - 1);

            Vector3 origin = transform.position;
            ResizeAndPositionTable(origin, maxReach, 0f);
            CreateHoleGrid(origin, boostedMaxReach, leftAngleLimit, rightAngleLimit, 0f);
            SpawnShrubBorder(origin, boostedMaxReach, leftAngleLimit, rightAngleLimit);
        }

        private void Awake()
        {
            if (GetComponent<WorkspaceAutoPositioner>() == null) gameObject.AddComponent<WorkspaceAutoPositioner>();
            if (arcadeTable == null) arcadeTable = GameObject.Find("Cube")?.transform;
        }

        private void ResizeAndPositionTable(Vector3 origin, float maxReach, float xShift)
        {
            if (arcadeTable == null) return;

            if (!arcadeTable.gameObject.scene.IsValid())
            {
                arcadeTable = Instantiate(arcadeTable, transform);
            }
            arcadeTable.gameObject.name = "ArcadeTable";

            MeshFilter filter = arcadeTable.GetComponentInChildren<MeshFilter>();
            float totalExtraMargin = tablePadding + tableMargin;
            float thickness = initialTableTopY > 0f ? initialTableTopY : Mathf.Max(origin.y, 0.05f);

            if (meshBaseRadius > 0.6f)
            {
                float targetRadius = maxReach + totalExtraMargin + tablePadding;
                float uniformScale = targetRadius / meshBaseRadius;
                arcadeTable.localScale = new Vector3(uniformScale, thickness, uniformScale);
            }
            else
            {
                float depth = maxReach + (totalExtraMargin * 2f);
                float width = (maxReach * 2f) + (totalExtraMargin * 2f);
                float meshDiameter = meshBaseRadius * 2f;
                arcadeTable.localScale = new Vector3(width / meshDiameter, thickness, depth / meshDiameter);
            }

            CollisionIsolator.IsolateObject(arcadeTable.gameObject);

            float meshTopY = filter?.sharedMesh != null ? filter.sharedMesh.bounds.max.y : 0f;
            Vector3 center = origin + transform.right * xShift;
            center.y = origin.y - (meshTopY * thickness);

            if (meshBaseRadius <= 0.6f)
            {
                float depth = maxReach + (totalExtraMargin * 2f);
                center += transform.forward * ((depth / 2f) - tablePadding);
            }
            else
            {
                center -= transform.forward * tablePadding;
            }

            arcadeTable.position = center;
            arcadeTable.rotation = transform.rotation;
            arcadeTable.gameObject.SetActive(true);
        }

        private void CreateHoleGrid(Vector3 origin, float maxReachableRadius, float leftAngleLimit, float rightAngleLimit, float xShift)
        {
            float minRadius = Mathf.Max(0.1f, maxReachableRadius * 0.35f);
            float leftAngleRad = leftAngleLimit * Mathf.Deg2Rad;
            float rightAngleRad = rightAngleLimit * Mathf.Deg2Rad;

            for (int r = 0; r < activeRows; r++)
            {
                int holesInThisRow = 3 + r;
                for (int c = 0; c < holesInThisRow; c++)
                {
                    bool isOutermostRow = (activeRows > 1 && r == activeRows - 1);
                    bool isEdgeColumn = (c == 0 || c == holesInThisRow - 1);
                    if (isOutermostRow && isEdgeColumn) continue;

                    float rRatio = activeRows > 1 ? (float)r / (activeRows - 1) : 0.5f;
                    float cRatio = holesInThisRow > 1 ? (float)c / (holesInThisRow - 1) : 0.5f;
                    float radius = Mathf.Lerp(minRadius, maxReachableRadius, rRatio);

                    if (isEdgeColumn && activeRows > 1 && r <= activeRows - 2)
                    {
                        radius = Mathf.Max(0.05f, radius - ((activeRows - 2 - r) * edgeColumnRadialSpacing));
                    }

                    float angle = Mathf.Lerp(leftAngleRad, rightAngleRad, cRatio);
                    Vector3 pos = origin + transform.right * ((Mathf.Cos(angle) * radius) + xShift) + transform.forward * (Mathf.Sin(angle) * radius);
                    pos.y += holeDepthOffset;

                    GameObject hole = Instantiate(holePrefab, pos, transform.rotation, transform);
                    Vector3 targetHoleScale = holePrefab.transform.localScale;
                    if (isOutermostRow) targetHoleScale *= outerRowScaleMultiplier;
                    hole.transform.localScale = targetHoleScale;
                    generatedHoles.Add(hole);

                    GameObject flatProxy = new GameObject("ProxyFlatSpawn");
                    flatProxy.transform.SetParent(transform, false);
                    flatProxy.transform.rotation = transform.rotation;
                    flatProxy.transform.localScale = Vector3.one;
                    flatProxy.transform.position = pos;

                    MoleScaleHint hint = flatProxy.AddComponent<MoleScaleHint>();
                    hint.rowIndex = r;
                    hint.columnIndex = c;
                    hint.holesInThisRow = holesInThisRow;
                    hint.desiredWorldScale = isOutermostRow ? fixedMoleScale * outerRowScaleMultiplier : fixedMoleScale;

                    generatedHoles.Add(flatProxy);
                    spawnPoints.Add(flatProxy.transform);
                }
            }
        }

        private void ClearLayout()
        {
            foreach (var h in generatedHoles) if (h != null) Destroy(h);
            generatedHoles.Clear();
            spawnPoints.Clear();

            foreach (var s in spawnedShrubs) if (s != null) Destroy(s);
            spawnedShrubs.Clear();
            shrubSpawnPoints.Clear();

            if (holePrefab != null)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = transform.GetChild(i);
                    if (child != arcadeTable && child.GetComponent<WorkspaceAutoPositioner>() == null)
                    {
                        if (child.name.Contains(holePrefab.name) || child.name.Contains("Hole") || child.name.Contains("Proxy"))
                            Destroy(child.gameObject);
                    }
                }
            }
        }

        private void SpawnShrubBorder(Vector3 origin, float maxReach, float holeLeftLimit, float holeRightLimit)
        {
            float bushRadius = maxReach + tablePadding + tableMargin - 0.1f;
            
            // Override the hole angle limits with fixed table edges to cover exactly the semi-circle table (no extra wrap)
            float tableLeftAngleRad = 180f * Mathf.Deg2Rad;
            float tableRightAngleRad = 0f * Mathf.Deg2Rad;
            
            float arcLength = bushRadius * Mathf.Abs(tableLeftAngleRad - tableRightAngleRad);

            int numShrubs = Mathf.Max(3, Mathf.RoundToInt(arcLength / Mathf.Max(shrubSpacing, 0.05f)));

            for (int i = 0; i < numShrubs; i++)
            {
                float angle = Mathf.Lerp(tableLeftAngleRad, tableRightAngleRad, (numShrubs > 1) ? (float)i / (numShrubs - 1) : 0.5f);
                Vector3 pos = origin + transform.right * (Mathf.Cos(angle) * bushRadius) + transform.forward * (Mathf.Sin(angle) * bushRadius);
                pos.y += shrubYOffset;

                Vector3 lookDir = origin - pos;
                lookDir.y = 0;
                Quaternion rot = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir) : transform.rotation;
                rot *= Quaternion.Euler(0f, Random.Range(-15f, 15f), 0f);

                GameObject prefab = (i % 2 == 0) ? shrubPrefabA : shrubPrefabB;
                if (prefab == null) continue;

                GameObject shrub = Instantiate(prefab, transform);
                shrub.transform.SetPositionAndRotation(pos, rot);
                shrub.transform.localScale = prefab.transform.localScale;

                foreach (Collider col in shrub.GetComponentsInChildren<Collider>()) col.enabled = false;

                spawnedShrubs.Add(shrub);
                shrubSpawnPoints.Add(shrub.transform);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && GameManager.Instance?.RehabProfile != null && generatedHoles.Count > 0)
                GenerateLayout();
        }
#endif
    }
}
