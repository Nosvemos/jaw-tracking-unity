namespace JawTracking.FileAccess
{
    public static class JawFilePickerFactory
    {
        public static IJawFilePicker CreateDefault()
        {
#if UNITY_EDITOR
            return new EditorJawFilePicker();
#elif UNITY_STANDALONE_WIN
            return new WindowsJawFilePicker();
#elif UNITY_ANDROID
            return new AndroidJawFilePicker();
#else
            return new UnsupportedJawFilePicker();
#endif
        }
    }
}
