using UnityEngine;

namespace JawTracking.FileAccess
{
    public sealed class StlImportResult
    {
        public bool Success { get; }
        public Mesh Mesh { get; }
        public string ErrorMessage { get; }
        public int TriangleCount { get; }

        private StlImportResult(bool success, Mesh mesh, string errorMessage, int triangleCount)
        {
            Success = success;
            Mesh = mesh;
            ErrorMessage = errorMessage;
            TriangleCount = triangleCount;
        }

        public static StlImportResult Ok(Mesh mesh, int triangleCount)
        {
            return new StlImportResult(true, mesh, string.Empty, triangleCount);
        }

        public static StlImportResult Failure(string errorMessage)
        {
            return new StlImportResult(false, null, errorMessage, 0);
        }
    }
}
