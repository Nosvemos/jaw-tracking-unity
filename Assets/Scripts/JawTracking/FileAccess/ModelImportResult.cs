using UnityEngine;

namespace JawTracking.FileAccess
{
    public sealed class ModelImportResult
    {
        public bool Success { get; }
        public Mesh Mesh { get; }
        public string ErrorMessage { get; }
        public int TriangleCount { get; }

        private ModelImportResult(bool success, Mesh mesh, string errorMessage, int triangleCount)
        {
            Success = success;
            Mesh = mesh;
            ErrorMessage = errorMessage;
            TriangleCount = triangleCount;
        }

        public static ModelImportResult Ok(Mesh mesh, int triangleCount)
        {
            return new ModelImportResult(true, mesh, string.Empty, triangleCount);
        }

        public static ModelImportResult Failure(string errorMessage)
        {
            return new ModelImportResult(false, null, errorMessage, 0);
        }
    }
}
