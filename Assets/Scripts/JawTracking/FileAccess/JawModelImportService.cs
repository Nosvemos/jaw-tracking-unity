using System;
using System.Threading;
using System.Threading.Tasks;
using JawTracking.Visualization;
using UnityEngine;

namespace JawTracking.FileAccess
{
    public sealed class JawModelImportService : MonoBehaviour
    {
        [Header("Mesh Targets")]
        [SerializeField] private MeshFilter upperJawMeshFilter;
        [SerializeField] private MeshFilter lowerJawMeshFilter;

        [Header("Model Roots")]
        [SerializeField] private Transform upperJawRoot;
        [SerializeField] private Transform lowerJawRoot;

        [Header("Materials")]
        [SerializeField] private Material upperJawMaterial;
        [SerializeField] private Material lowerJawMaterial;

        [Header("Initial View Fit")]
        [SerializeField] private bool fitModelOnImport = true;
        [SerializeField] private float targetMaxDimension = 0.18f;
        [SerializeField] private float verticalSeparation = 0.035f;
        [SerializeField] private JawOrbitCameraController orbitCameraController;

        private IJawFilePicker filePicker;
        private IStlMeshLoader meshLoader;
        private CancellationTokenSource destroyCancellation;

        public event Action<JawModelRole, StlImportResult, string> ModelImportCompleted;
        public event Action<string> StatusChanged;

        public string UpperJawPath { get; private set; } = string.Empty;
        public string LowerJawPath { get; private set; } = string.Empty;

        public Bounds? CombinedBounds { get; private set; }

        public void Configure(MeshFilter upperTarget, MeshFilter lowerTarget, Material upperMaterial, Material lowerMaterial)
        {
            upperJawMeshFilter = upperTarget;
            lowerJawMeshFilter = lowerTarget;
            upperJawMaterial = upperMaterial;
            lowerJawMaterial = lowerMaterial;
            upperJawRoot = upperTarget != null ? upperTarget.transform.parent : null;
            lowerJawRoot = lowerTarget != null ? lowerTarget.transform.parent : null;
        }

        private void Awake()
        {
            AutoWireSceneReferences();
            destroyCancellation = new CancellationTokenSource();
            filePicker = JawFilePickerFactory.CreateDefault();
            meshLoader = new RuntimeStlMeshLoader();
        }

        private void OnDestroy()
        {
            destroyCancellation?.Cancel();
            destroyCancellation?.Dispose();
        }

        public void LoadUpperJawFromPicker()
        {
            _ = LoadFromPickerAsync(JawModelRole.UpperJaw);
        }

        public void LoadLowerJawFromPicker()
        {
            _ = LoadFromPickerAsync(JawModelRole.LowerJaw);
        }

        public async Task LoadFromPickerAsync(JawModelRole role)
        {
            StatusChanged?.Invoke(role == JawModelRole.UpperJaw
                ? "Üst çene STL dosyası seçiliyor..."
                : "Alt çene STL dosyası seçiliyor...");

            JawFilePickResult pickResult = await filePicker.PickStlFileAsync(role, destroyCancellation.Token);
            if (pickResult.Cancelled)
            {
                StatusChanged?.Invoke("Dosya seçimi iptal edildi.");
                return;
            }

            if (!pickResult.Success)
            {
                StatusChanged?.Invoke(pickResult.ErrorMessage);
                return;
            }

            string meshName = role == JawModelRole.UpperJaw ? "Üst Çene STL" : "Alt Çene STL";
            StlImportResult importResult = meshLoader.LoadMesh(pickResult.Bytes, meshName);

            if (!importResult.Success)
            {
                StatusChanged?.Invoke(importResult.ErrorMessage);
                ModelImportCompleted?.Invoke(role, importResult, pickResult.Path);
                return;
            }

            ApplyMesh(role, importResult.Mesh);
            if (role == JawModelRole.UpperJaw)
            {
                UpperJawPath = pickResult.Path;
            }
            else
            {
                LowerJawPath = pickResult.Path;
            }

            StatusChanged?.Invoke($"{meshName} yüklendi. Üçgen: {importResult.TriangleCount:N0}");
            FrameCameraIfPossible();
            ModelImportCompleted?.Invoke(role, importResult, pickResult.Path);
        }

        private void ApplyMesh(JawModelRole role, Mesh mesh)
        {
            AutoWireSceneReferences();
            MeshFilter target = role == JawModelRole.UpperJaw ? upperJawMeshFilter : lowerJawMeshFilter;
            if (target == null)
            {
                StatusChanged?.Invoke("Model hedefi sahnede bulunamadı.");
                return;
            }

            target.sharedMesh = mesh;

            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = role == JawModelRole.UpperJaw ? upperJawMaterial : lowerJawMaterial;
                renderer.sharedMaterial = material != null ? material : CreateFallbackMaterial(role);
            }

            if (fitModelOnImport)
            {
                FitMeshForInitialView(role, target, mesh);
            }

            UpdateCombinedBounds();
        }

        private void AutoWireSceneReferences()
        {
            if (upperJawRoot == null)
            {
                upperJawRoot = FindSceneTransformByName("UpperJawRoot");
            }

            if (lowerJawRoot == null)
            {
                lowerJawRoot = FindSceneTransformByName("LowerJawRoot");
            }

            if (upperJawMeshFilter == null)
            {
                upperJawMeshFilter = EnsureMeshTarget("UpperJawMesh", upperJawRoot);
            }

            if (lowerJawMeshFilter == null)
            {
                lowerJawMeshFilter = EnsureMeshTarget("LowerJawMesh", lowerJawRoot);
            }

            EnsureRenderer(upperJawMeshFilter);
            EnsureRenderer(lowerJawMeshFilter);
        }

        private static MeshFilter EnsureMeshTarget(string objectName, Transform fallbackParent)
        {
            Transform targetTransform = FindSceneTransformByName(objectName);
            if (targetTransform == null && fallbackParent != null)
            {
                var targetObject = new GameObject(objectName);
                targetTransform = targetObject.transform;
                targetTransform.SetParent(fallbackParent, false);
            }

            if (targetTransform == null)
            {
                return null;
            }

            MeshFilter meshFilter = targetTransform.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = targetTransform.gameObject.AddComponent<MeshFilter>();
            }

            return meshFilter;
        }

        private static void EnsureRenderer(MeshFilter meshFilter)
        {
            if (meshFilter == null)
            {
                return;
            }

            if (meshFilter.GetComponent<MeshRenderer>() == null)
            {
                meshFilter.gameObject.AddComponent<MeshRenderer>();
            }
        }

        private static Transform FindSceneTransformByName(string objectName)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform sceneTransform in transforms)
            {
                if (sceneTransform.name == objectName && sceneTransform.gameObject.scene.IsValid())
                {
                    return sceneTransform;
                }
            }

            return null;
        }

        private void FitMeshForInitialView(JawModelRole role, MeshFilter target, Mesh mesh)
        {
            if (mesh == null || targetMaxDimension <= 0f)
            {
                return;
            }

            Bounds bounds = mesh.bounds;
            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDimension <= Mathf.Epsilon)
            {
                return;
            }

            float scale = targetMaxDimension / maxDimension;
            target.transform.localScale = Vector3.one * scale;
            target.transform.localPosition = -bounds.center * scale;
            target.transform.localRotation = Quaternion.identity;

            Transform root = GetRoot(role, target);
            if (root != null)
            {
                float yOffset = role == JawModelRole.UpperJaw ? verticalSeparation * 0.5f : -verticalSeparation * 0.5f;
                root.localPosition = new Vector3(0f, yOffset, 0f);
                root.localRotation = Quaternion.identity;
            }
        }

        private Transform GetRoot(JawModelRole role, MeshFilter target)
        {
            Transform configuredRoot = role == JawModelRole.UpperJaw ? upperJawRoot : lowerJawRoot;
            if (configuredRoot != null)
            {
                return configuredRoot;
            }

            return target != null ? target.transform.parent : null;
        }

        private void UpdateCombinedBounds()
        {
            bool hasBounds = false;
            Bounds combined = default;

            AddRendererBounds(upperJawMeshFilter, ref combined, ref hasBounds);
            AddRendererBounds(lowerJawMeshFilter, ref combined, ref hasBounds);

            CombinedBounds = hasBounds ? (Bounds?)combined : null;
        }

        private void FrameCameraIfPossible()
        {
            if (orbitCameraController == null)
            {
                orbitCameraController = FindFirstObjectByType<JawOrbitCameraController>();
            }

            if (orbitCameraController != null && CombinedBounds.HasValue)
            {
                orbitCameraController.FrameBounds(CombinedBounds.Value);
            }
        }

        private static void AddRendererBounds(MeshFilter meshFilter, ref Bounds combined, ref bool hasBounds)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            MeshRenderer renderer = meshFilter.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        private static Material CreateFallbackMaterial(JawModelRole role)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                name = role == JawModelRole.UpperJaw ? "Üst Çene Varsayılan" : "Alt Çene Varsayılan",
                color = role == JawModelRole.UpperJaw
                    ? new Color(0.78f, 0.86f, 0.9f, 1f)
                    : new Color(0.35f, 0.82f, 0.88f, 1f)
            };

            return material;
        }
    }
}
