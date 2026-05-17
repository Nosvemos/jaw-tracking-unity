namespace JawTracking.FileAccess
{
    public interface IStlMeshLoader
    {
        StlImportResult LoadMesh(byte[] stlBytes, string meshName);
    }
}
