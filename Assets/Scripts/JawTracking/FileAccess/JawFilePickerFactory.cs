namespace JawTracking.FileAccess
{
    public static class JawFilePickerFactory
    {
        public static IJawFilePicker CreateDefault()
        {
            return new EditorJawFilePicker();
        }
    }
}
