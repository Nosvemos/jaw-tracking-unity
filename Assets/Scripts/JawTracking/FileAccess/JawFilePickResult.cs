using System;

namespace JawTracking.FileAccess
{
    public sealed class JawFilePickResult
    {
        public bool Success { get; }
        public bool Cancelled { get; }
        public string Path { get; }
        public byte[] Bytes { get; }
        public string ErrorMessage { get; }

        private JawFilePickResult(bool success, bool cancelled, string path, byte[] bytes, string errorMessage)
        {
            Success = success;
            Cancelled = cancelled;
            Path = path;
            Bytes = bytes;
            ErrorMessage = errorMessage;
        }

        public static JawFilePickResult FromBytes(string path, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return Failure("Model dosyası boş veya okunamadı.");
            }

            return new JawFilePickResult(true, false, path ?? string.Empty, bytes, string.Empty);
        }

        public static JawFilePickResult Cancel()
        {
            return new JawFilePickResult(false, true, string.Empty, Array.Empty<byte>(), string.Empty);
        }

        public static JawFilePickResult Failure(string errorMessage)
        {
            return new JawFilePickResult(false, false, string.Empty, Array.Empty<byte>(), errorMessage);
        }
    }
}
