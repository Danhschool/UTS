using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ProjectRTS.MapTools.Editor
{
    public sealed class QuickPrefabScatterWindow : EditorWindow
    {
        private const string WindowName = "Quick Prefab Scatter";

        [SerializeField] private List<GameObject> prefabPool = new List<GameObject>();
        [SerializeField] private Transform parentRoot;
        [SerializeField] private bool paintMode;
        [SerializeField] private bool alignToSurface = true;
        [SerializeField] private bool randomYaw = true;
        [SerializeField] private Vector2 randomScaleRange = new Vector2(1f, 1f);
        [SerializeField] private float brushRadius = 6f;
        [SerializeField] private int spawnPerStroke = 4;
        [SerializeField] private float minSpacing = 1.2f;
        [SerializeField] private float overlapPadding = 0.4f;
        [SerializeField] private float rayDistance = 5000f;
        [SerializeField] private LayerMask paintMask = ~0;
        [SerializeField] private string spawnedNamePrefix = "QPS_";
        [SerializeField] private int spawnedCount;

        private readonly List<Vector3> strokeSpawnPoints = new List<Vector3>();

        [MenuItem("RTS/Map Tools/Quick Prefab Scatter")]
        private static void Open()
        {
            GetWindow<QuickPrefabScatterWindow>(WindowName);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Click or drag in Scene view to spawn random prefabs quickly. " +
                "Use brush radius and spacing to avoid overlap.",
                MessageType.Info);

            DrawPrefabPool();

            parentRoot = (Transform)EditorGUILayout.ObjectField("Parent Root", parentRoot, typeof(Transform), true);
            paintMask = DrawLayerMaskField("Paint Layer Mask", paintMask);
            alignToSurface = EditorGUILayout.Toggle("Align To Surface", alignToSurface);
            randomYaw = EditorGUILayout.Toggle("Random Y Rotation", randomYaw);
            randomScaleRange = EditorGUILayout.Vector2Field("Random Scale Range", randomScaleRange);
            brushRadius = EditorGUILayout.Slider("Brush Radius", brushRadius, 0.2f, 40f);
            spawnPerStroke = EditorGUILayout.IntSlider("Spawn Per Stroke", spawnPerStroke, 1, 40);
            minSpacing = EditorGUILayout.Slider("Min Spacing", minSpacing, 0f, 8f);
            overlapPadding = EditorGUILayout.Slider("Overlap Padding", overlapPadding, 0f, 3f);
            rayDistance = EditorGUILayout.FloatField("Ray Distance", rayDistance);
            spawnedNamePrefix = EditorGUILayout.TextField("Spawned Name Prefix", spawnedNamePrefix);

            int existingCount = CountSpawnedInstancesInScope();
            EditorGUILayout.LabelField("Spawned (Session)", spawnedCount.ToString());
            EditorGUILayout.LabelField("Existing In Scope", existingCount.ToString());

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = paintMode ? new Color(0.25f, 0.85f, 0.25f) : Color.white;
                if (GUILayout.Button(paintMode ? "Painting: ON" : "Painting: OFF", GUILayout.Height(30f)))
                {
                    TogglePaintMode();
                }

                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("Clear Stroke Cache", GUILayout.Height(30f)))
                {
                    strokeSpawnPoints.Clear();
                }
            }

            EditorGUILayout.LabelField("Tip: Hold Alt to orbit camera while paint mode is ON.", EditorStyles.miniLabel);
        }

        private void DrawPrefabPool()
        {
            EditorGUILayout.LabelField("Prefab List", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Selected Prefabs", GUILayout.Height(24f)))
                {
                    AddSelectedPrefabs();
                }

                if (GUILayout.Button("Clear List", GUILayout.Height(24f)))
                {
                    prefabPool.Clear();
                }
            }

            EditorGUILayout.LabelField($"Total Prefabs: {prefabPool.Count}", EditorStyles.miniLabel);
            DrawPrefabDropArea();

            if (prefabPool.Count == 0)
            {
                EditorGUILayout.HelpBox("Select prefabs in Project window, click Add Selected Prefabs, or drag/drop prefabs below.", MessageType.Warning);
                return;
            }

            for (int i = 0; i < prefabPool.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    prefabPool[i] = (GameObject)EditorGUILayout.ObjectField($"Prefab {i + 1}", prefabPool[i], typeof(GameObject), false);
                    if (GUILayout.Button("X", GUILayout.Width(26f)))
                    {
                        prefabPool.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        private void AddSelectedPrefabs()
        {
            Object[] selectedObjects = Selection.objects;
            int addedCount = 0;
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                if (TryAddPrefabToPool(selectedObjects[i]))
                {
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                Repaint();
            }
        }

        private void DrawPrefabDropArea()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop Prefabs Here");

            Event currentEvent = Event.current;
            if (!dropArea.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                currentEvent.Use();
            }

            if (currentEvent.type != EventType.DragPerform)
            {
                return;
            }

            DragAndDrop.AcceptDrag();
            Object[] draggedObjects = DragAndDrop.objectReferences;
            for (int i = 0; i < draggedObjects.Length; i++)
            {
                TryAddPrefabToPool(draggedObjects[i]);
            }

            currentEvent.Use();
            Repaint();
        }

        private bool TryAddPrefabToPool(Object obj)
        {
            GameObject candidate = obj as GameObject;
            if (candidate == null)
            {
                return false;
            }

            if (!PrefabUtility.IsPartOfPrefabAsset(candidate))
            {
                return false;
            }

            if (prefabPool.Contains(candidate))
            {
                return false;
            }

            prefabPool.Add(candidate);
            return true;
        }

        private static LayerMask DrawLayerMaskField(string label, LayerMask selectedMask)
        {
            string[] layers = InternalEditorUtility.layers;
            int displayedMask = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                int layerNumber = LayerMask.NameToLayer(layers[i]);
                if ((selectedMask.value & (1 << layerNumber)) != 0)
                {
                    displayedMask |= 1 << i;
                }
            }

            displayedMask = EditorGUILayout.MaskField(label, displayedMask, layers);

            int finalMask = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                if ((displayedMask & (1 << i)) != 0)
                {
                    finalMask |= 1 << LayerMask.NameToLayer(layers[i]);
                }
            }

            selectedMask.value = finalMask;
            return selectedMask;
        }

        private void TogglePaintMode()
        {
            paintMode = !paintMode;
            SceneView.RepaintAll();
            Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!paintMode)
            {
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            DrawStatusOverlay();
            DrawBrushPreview(currentEvent);

            if (currentEvent.alt)
            {
                return;
            }

            if (currentEvent.type != EventType.MouseDown && currentEvent.type != EventType.MouseDrag)
            {
                if (currentEvent.type == EventType.MouseUp)
                {
                    strokeSpawnPoints.Clear();
                }

                return;
            }

            if (currentEvent.button != 0)
            {
                return;
            }

            if (!TryGetTerrainHit(currentEvent.mousePosition, out RaycastHit centerHit))
            {
                return;
            }

            if (currentEvent.shift)
            {
                ErasePrefabsInBrush(centerHit.point);
                currentEvent.Use();
                return;
            }

            PlacePrefabsInBrush(centerHit);
            currentEvent.Use();
        }

        private void DrawStatusOverlay()
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10f, 10f, 300f, 30f), "Quick Prefab Scatter: ON", EditorStyles.helpBox);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void DrawBrushPreview(Event currentEvent)
        {
            if (!TryGetTerrainHit(currentEvent.mousePosition, out RaycastHit hit))
            {
                return;
            }

            Handles.color = new Color(0.3f, 0.9f, 0.95f, 0.9f);
            Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);
        }

        private bool TryGetTerrainHit(Vector2 mousePosition, out RaycastHit hit)
        {
            Ray sceneRay = HandleUtility.GUIPointToWorldRay(mousePosition);
            return Physics.Raycast(sceneRay, out hit, rayDistance, paintMask.value, QueryTriggerInteraction.Ignore);
        }

        private void PlacePrefabsInBrush(RaycastHit centerHit)
        {
            List<GameObject> validPrefabs = GetValidPrefabs();
            if (validPrefabs.Count == 0)
            {
                return;
            }

            List<PlacedSample> placedSamples = BuildPlacedSamplesFromScene();

            for (int i = 0; i < spawnPerStroke; i++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * brushRadius;
                Vector3 origin = centerHit.point + new Vector3(randomCircle.x, 30f, randomCircle.y);

                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, paintMask.value, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (!HasEnoughSpacing(hit.point))
                {
                    continue;
                }

                GameObject randomPrefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
                float randomScale = Random.Range(GetMinScale(), GetMaxScale());
                float candidateRadius = EstimatePrefabRadius(randomPrefab) * randomScale;
                if (!CanPlaceWithoutOverlap(hit.point, candidateRadius, placedSamples))
                {
                    continue;
                }

                SpawnPrefabAtHit(randomPrefab, hit, randomScale);
                placedSamples.Add(new PlacedSample(hit.point, candidateRadius));
                strokeSpawnPoints.Add(hit.point);
            }
        }

        private bool HasEnoughSpacing(Vector3 candidatePoint)
        {
            if (minSpacing <= 0f)
            {
                return true;
            }

            for (int i = 0; i < strokeSpawnPoints.Count; i++)
            {
                if (Vector3.Distance(strokeSpawnPoints[i], candidatePoint) < minSpacing)
                {
                    return false;
                }
            }

            return true;
        }

        private void SpawnPrefabAtHit(GameObject prefab, RaycastHit hit, float scaleMultiplier)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
            {
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Quick Scatter Prefab");

            if (parentRoot != null)
            {
                instance.transform.SetParent(parentRoot, true);
            }

            instance.transform.position = hit.point;
            instance.transform.rotation = BuildRotation(hit.normal);
            instance.transform.localScale *= scaleMultiplier;
            instance.name = $"{GetEffectivePrefix()}{prefab.name}";
            spawnedCount++;
        }

        private Quaternion BuildRotation(Vector3 surfaceNormal)
        {
            Quaternion rotation = alignToSurface ? Quaternion.FromToRotation(Vector3.up, surfaceNormal) : Quaternion.identity;
            if (randomYaw)
            {
                rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * rotation;
            }

            return rotation;
        }

        private float GetMinScale()
        {
            return Mathf.Max(0.01f, Mathf.Min(randomScaleRange.x, randomScaleRange.y));
        }

        private float GetMaxScale()
        {
            float min = GetMinScale();
            return Mathf.Max(min, Mathf.Max(randomScaleRange.x, randomScaleRange.y));
        }

        private List<GameObject> GetValidPrefabs()
        {
            List<GameObject> result = new List<GameObject>(prefabPool.Count);
            for (int i = 0; i < prefabPool.Count; i++)
            {
                if (prefabPool[i] != null)
                {
                    result.Add(prefabPool[i]);
                }
            }

            return result;
        }

        private void ErasePrefabsInBrush(Vector3 centerPoint)
        {
            List<GameObject> candidates = GetManagedObjects();
            for (int i = 0; i < candidates.Count; i++)
            {
                GameObject candidate = candidates[i];
                if (candidate == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(candidate.transform.position, centerPoint);
                if (distance <= brushRadius)
                {
                    Undo.DestroyObjectImmediate(candidate);
                }
            }
        }

        private int CountSpawnedInstancesInScope()
        {
            return GetManagedObjects().Count;
        }

        private List<GameObject> GetManagedObjects()
        {
            string prefix = GetEffectivePrefix();
            if (parentRoot != null)
            {
                return parentRoot
                    .GetComponentsInChildren<Transform>(true)
                    .Select(t => t.gameObject)
                    .Where(go => go != parentRoot.gameObject && go.name.StartsWith(prefix))
                    .ToList();
            }

            return FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Select(t => t.gameObject)
                .Where(go => go.scene.IsValid() && go.name.StartsWith(prefix))
                .ToList();
        }

        private string GetEffectivePrefix()
        {
            return string.IsNullOrWhiteSpace(spawnedNamePrefix) ? "QPS_" : spawnedNamePrefix;
        }

        private List<PlacedSample> BuildPlacedSamplesFromScene()
        {
            List<GameObject> managedObjects = GetManagedObjects();
            List<PlacedSample> samples = new List<PlacedSample>(managedObjects.Count);
            for (int i = 0; i < managedObjects.Count; i++)
            {
                GameObject managedObject = managedObjects[i];
                if (managedObject == null)
                {
                    continue;
                }

                float radius = EstimateInstanceRadius(managedObject);
                samples.Add(new PlacedSample(managedObject.transform.position, radius));
            }

            return samples;
        }

        private bool CanPlaceWithoutOverlap(Vector3 candidatePosition, float candidateRadius, List<PlacedSample> placedSamples)
        {
            float finalCandidateRadius = Mathf.Max(0.05f, candidateRadius);
            for (int i = 0; i < placedSamples.Count; i++)
            {
                PlacedSample sample = placedSamples[i];
                float requiredDistance = finalCandidateRadius + sample.Radius + overlapPadding;
                if (Vector3.Distance(sample.Position, candidatePosition) < requiredDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private static float EstimatePrefabRadius(GameObject prefab)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            return EstimateRadiusFromRenderers(renderers);
        }

        private static float EstimateInstanceRadius(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            return EstimateRadiusFromRenderers(renderers);
        }

        private static float EstimateRadiusFromRenderers(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
            {
                return 0.5f;
            }

            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 size = combinedBounds.size;
            float horizontalExtent = Mathf.Max(size.x, size.z) * 0.5f;
            return Mathf.Max(0.1f, horizontalExtent);
        }

        private readonly struct PlacedSample
        {
            public PlacedSample(Vector3 position, float radius)
            {
                Position = position;
                Radius = Mathf.Max(0.05f, radius);
            }

            public Vector3 Position { get; }
            public float Radius { get; }
        }
    }
}
