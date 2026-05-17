using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace JawTracking.FileAccess
{
    public sealed class JawModelImportService : MonoBehaviour
    {
        [Header("Mesh Targets")]
        [SerializeField] private MeshFilter upperJawMeshFilter;
        [SerializeField] private MeshFilter lowerJawMeshFilter;

        [Header("Materials")]
        [SerializeField] private Material upperJawMaterial;
        [SerializeField] private Material lowerJawMaterial;

        private IJawFilePicker filePicker;
        private IStlMeshLoader meshLoader;
        private CancellationTokenSource destroyCancellation;

        public event Action<JawModelRole, StlImportResult, string> ModelImportCompleted;
        public event Action<string> StatusChanged;

        public string UpperJawPath { get; private set; } = string.Empty;
        public string LowerJawPath { get; private set; } = string.Empty;

        public void Configure(MeshFilter upperTarget, MeshFilter lowerTarget, Material upperMaterial, Material lowerMaterial)
        {
            upperJawMeshFilter = upperTarget;
            lowerJawMeshFilter = lowerTarget;
            upperJawMaterial = upperMaterial;
            lowerJawMaterial = lowerMaterial;
        }

        private void Awake()
        {
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
            ModelImportCompleted?.Invoke(role, importResult, pickResult.Path);
        }

        private void ApplyMesh(JawModelRole role, Mesh mesh)
        {
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
