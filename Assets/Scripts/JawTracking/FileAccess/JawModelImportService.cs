using System;
using System.Threading;
using System.Threading.Tasks;
using JawTracking.Visualization;
using UnityEngine;

namespace JawTracking.FileAccess
{
    public sealed class JawModelImportService : MonoBehaviour
    {
        [Header("Model Roots")]
        [SerializeField] private Transform upperJawRoot;
        [SerializeField] private Transform lowerJawRoot;
        [SerializeField] private Transform biteScan1Root;
        [SerializeField] private Transform biteScan2Root;

        [Header("Mesh Targets")]
        [SerializeField] private MeshFilter upperJawMeshFilter;
        [SerializeField] private MeshFilter lowerJawMeshFilter;
        [SerializeField] private MeshFilter biteScan1MeshFilter;
        [SerializeField] private MeshFilter biteScan2MeshFilter;

        [Header("Materials")]
        [SerializeField] private Material upperJawMaterial;
        [SerializeField] private Material lowerJawMaterial;
        [SerializeField] private Material biteScanMaterial;

        [Header("Initial View Fit")]
        [Tooltip("If true, retains the original 3D spatial coordinates from the STL (useful for pre-aligned dental scans in occlusion). If false, centers and separates them artificially.")]
        [SerializeField] private bool useClinicalAlignment = true;
        [Tooltip("If useClinicalAlignment is true, we assume the STL is in millimeters and multiply by 0.001 to convert to Unity meters.")]
        [SerializeField] private float stlToUnityScale = 0.001f;
        [SerializeField] private bool fitModelOnImport = false;
        [SerializeField] private float targetMaxDimension = 0.18f;
        [SerializeField] private float verticalSeparation = 0.035f;
        [SerializeField] private JawOrbitCameraController orbitCameraController;

        private IJawFilePicker filePicker;
        private IMeshLoader meshLoader;
        private CancellationTokenSource destroyCancellation;

        public event Action<JawModelRole, ModelImportResult, string> ModelImportCompleted;
        public event Action<JawModelRole> PreMeshApplied;
        public event Action<string> StatusChanged;

        public string UpperJawPath { get; private set; } = string.Empty;
        public string LowerJawPath { get; private set; } = string.Empty;
        public string BiteScan1Path { get; private set; } = string.Empty;
        public string BiteScan2Path { get; private set; } = string.Empty;

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
            meshLoader = new RuntimeMeshLoader();
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

        public void LoadBiteScan1FromPicker()
        {
            _ = LoadFromPickerAsync(JawModelRole.BiteScan1);
        }

        public void LoadBiteScan2FromPicker()
        {
            _ = LoadFromPickerAsync(JawModelRole.BiteScan2);
        }

        public void ClearModels()
        {
            AutoWireSceneReferences();
            if (upperJawMeshFilter != null) upperJawMeshFilter.sharedMesh = null;
            if (lowerJawMeshFilter != null) lowerJawMeshFilter.sharedMesh = null;
            if (biteScan1MeshFilter != null) biteScan1MeshFilter.sharedMesh = null;
            if (biteScan2MeshFilter != null) biteScan2MeshFilter.sharedMesh = null;

            UpperJawPath = string.Empty;
            LowerJawPath = string.Empty;
            BiteScan1Path = string.Empty;
            BiteScan2Path = string.Empty;
            CombinedBounds = null;
            
            StatusChanged?.Invoke("Modeller temizlendi.");
        }

        public async Task LoadFromPickerAsync(JawModelRole role)
        {
            string roleName = "Model";
            if (role == JawModelRole.UpperJaw) roleName = "Üst çene modeli";
            else if (role == JawModelRole.LowerJaw) roleName = "Alt çene modeli";
            else if (role == JawModelRole.BiteScan1) roleName = "1. Isırma modeli";
            else if (role == JawModelRole.BiteScan2) roleName = "2. Isırma modeli";

            StatusChanged?.Invoke($"{roleName} dosyası seçiliyor...");

            JawFilePickResult pickResult = await filePicker.PickModelFileAsync(role, destroyCancellation.Token);
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

            ModelImportResult importResult = meshLoader.LoadMesh(pickResult.Bytes, roleName);

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
            else if (role == JawModelRole.LowerJaw)
            {
                LowerJawPath = pickResult.Path;
            }
            else if (role == JawModelRole.BiteScan1)
            {
                BiteScan1Path = pickResult.Path;
            }
            else if (role == JawModelRole.BiteScan2)
            {
                BiteScan2Path = pickResult.Path;
            }

            StatusChanged?.Invoke($"{roleName} yüklendi. Üçgen: {importResult.TriangleCount:N0}");
            FrameCameraIfPossible();
            ModelImportCompleted?.Invoke(role, importResult, pickResult.Path);
        }

        private void ApplyMesh(JawModelRole role, Mesh mesh)
        {
            PreMeshApplied?.Invoke(role);
            AutoWireSceneReferences();
            
            MeshFilter target = null;
            Material material = null;
            if (role == JawModelRole.UpperJaw) { target = upperJawMeshFilter; material = upperJawMaterial; }
            else if (role == JawModelRole.LowerJaw) { target = lowerJawMeshFilter; material = lowerJawMaterial; }
            else if (role == JawModelRole.BiteScan1) { target = biteScan1MeshFilter; material = biteScanMaterial; }
            else if (role == JawModelRole.BiteScan2) { target = biteScan2MeshFilter; material = biteScanMaterial; }

            if (target == null)
            {
                StatusChanged?.Invoke("Model hedefi sahnede bulunamadı.");
                return;
            }

            target.sharedMesh = mesh;

            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material customMat = null;
                if (mesh.colors.Length > 0)
                {
                    customMat = CreateVertexColorMaterial();
                    customMat.SetFloat("_UseVertexColor", 1.0f);
                }
                else
                {
                    Material baseMat = material != null ? material : CreateFallbackMaterial(role);
                    Color baseColor = GetMaterialColorSafe(baseMat);

                    customMat = new Material(baseMat);
                    Shader customShader = Shader.Find("Custom/URPVertexColorLit");
                    if (customShader != null)
                    {
                        customMat.shader = customShader;
                    }
                    customMat.SetFloat("_UseVertexColor", 0.0f);

                    if (customMat.HasProperty("_BaseColor"))
                    {
                        customMat.SetColor("_BaseColor", baseColor);
                    }
                }

                renderer.sharedMaterial = customMat;

                // Hide BiteScans visually in the viewport but keep their meshes loaded for alignment/bounds!
                if (role == JawModelRole.BiteScan1 || role == JawModelRole.BiteScan2)
                {
                    renderer.enabled = false;
                }
            }

            if (useClinicalAlignment)
            {
                FitMeshClinical(target);
            }
            else if (fitModelOnImport)
            {
                FitMeshForInitialView(role, target, mesh);
            }

            UpdateCombinedBounds();
        }

        private static Color GetMaterialColorSafe(Material mat)
        {
            if (mat == null) return Color.white;
            if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
            if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
            return Color.white;
        }

        private void FitMeshClinical(MeshFilter target)
        {
            target.transform.localScale = Vector3.one * stlToUnityScale;
            target.transform.localPosition = Vector3.zero;
            target.transform.localRotation = Quaternion.identity;

            Transform root = target.transform.parent;
            if (root != null)
            {
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
            }
        }

        private void AutoWireSceneReferences()
        {
            if (upperJawRoot == null) upperJawRoot = FindSceneTransformByName("UpperJawRoot");
            if (lowerJawRoot == null) lowerJawRoot = FindSceneTransformByName("LowerJawRoot");

            if (biteScan1Root == null)
            {
                biteScan1Root = FindSceneTransformByName("BiteScan1Root");
                if (biteScan1Root == null)
                {
                    var go = new GameObject("BiteScan1Root");
                    biteScan1Root = go.transform;
                    if (upperJawRoot != null)
                    {
                        go.layer = upperJawRoot.gameObject.layer;
                    }
                }
            }

            if (biteScan2Root == null)
            {
                biteScan2Root = FindSceneTransformByName("BiteScan2Root");
                if (biteScan2Root == null)
                {
                    var go = new GameObject("BiteScan2Root");
                    biteScan2Root = go.transform;
                    if (upperJawRoot != null)
                    {
                        go.layer = upperJawRoot.gameObject.layer;
                    }
                }
            }

            if (upperJawMeshFilter == null) upperJawMeshFilter = EnsureMeshTarget("UpperJawMesh", upperJawRoot);
            if (lowerJawMeshFilter == null) lowerJawMeshFilter = EnsureMeshTarget("LowerJawMesh", lowerJawRoot);
            if (biteScan1MeshFilter == null) biteScan1MeshFilter = EnsureMeshTarget("BiteScan1Mesh", biteScan1Root);
            if (biteScan2MeshFilter == null) biteScan2MeshFilter = EnsureMeshTarget("BiteScan2Mesh", biteScan2Root);

            EnsureRenderer(upperJawMeshFilter);
            EnsureRenderer(lowerJawMeshFilter);
            EnsureRenderer(biteScan1MeshFilter);
            EnsureRenderer(biteScan2MeshFilter);
        }

        private static MeshFilter EnsureMeshTarget(string objectName, Transform fallbackParent)
        {
            Transform targetTransform = FindSceneTransformByName(objectName);
            if (targetTransform == null && fallbackParent != null)
            {
                var targetObject = new GameObject(objectName);
                targetTransform = targetObject.transform;
                targetTransform.SetParent(fallbackParent, false);
                targetObject.layer = fallbackParent.gameObject.layer; // Inherit layer!
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
            Transform configuredRoot = null;
            if (role == JawModelRole.UpperJaw) configuredRoot = upperJawRoot;
            else if (role == JawModelRole.LowerJaw) configuredRoot = lowerJawRoot;
            else if (role == JawModelRole.BiteScan1) configuredRoot = biteScan1Root;
            else if (role == JawModelRole.BiteScan2) configuredRoot = biteScan2Root;

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
            AddRendererBounds(biteScan1MeshFilter, ref combined, ref hasBounds);
            AddRendererBounds(biteScan2MeshFilter, ref combined, ref hasBounds);

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

        private static Material CreateVertexColorMaterial()
        {
            Shader shader = Shader.Find("Custom/URPVertexColorLit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            var material = new Material(shader)
            {
                name = "VertexColorMaterial"
            };
            return material;
        }

        private static Material CreateFallbackMaterial(JawModelRole role)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            string matName = "Model Varsayılan";
            Color matColor = new Color(0.8f, 0.8f, 0.8f, 1f);

            if (role == JawModelRole.UpperJaw)
            {
                matName = "Üst Çene Varsayılan";
                matColor = new Color(0.78f, 0.86f, 0.9f, 1f);
            }
            else if (role == JawModelRole.LowerJaw)
            {
                matName = "Alt Çene Varsayılan";
                matColor = new Color(0.35f, 0.82f, 0.88f, 1f);
            }
            else if (role == JawModelRole.BiteScan1)
            {
                matName = "Isırma 1 Varsayılan";
                matColor = new Color(0.95f, 0.85f, 0.65f, 0.65f); // Transparent peach/yellow
            }
            else if (role == JawModelRole.BiteScan2)
            {
                matName = "Isırma 2 Varsayılan";
                matColor = new Color(0.65f, 0.85f, 0.65f, 0.65f); // Transparent green
            }

            var material = new Material(shader)
            {
                name = matName,
                color = matColor
            };

            if (matColor.a < 1f)
            {
                material.SetFloat("_Blend", 1f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
            }

            return material;
        }
    }
}
