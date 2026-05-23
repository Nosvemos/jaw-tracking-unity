namespace JawTracking.FileAccess
{
    public interface IMeshLoader
    {
        ModelImportResult LoadMesh(byte[] stlBytes, string meshName);
    }
}
