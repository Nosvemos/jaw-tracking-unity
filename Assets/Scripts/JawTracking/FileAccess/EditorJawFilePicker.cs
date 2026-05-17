using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JawTracking.FileAccess
{
    public sealed class EditorJawFilePicker : IJawFilePicker
    {
        public Task<JawFilePickResult> PickStlFileAsync(JawModelRole role, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(JawFilePickResult.Cancel());
            }

#if UNITY_EDITOR
            string title = role == JawModelRole.UpperJaw ? "Üst Çene STL Seç" : "Alt Çene STL Seç";
            string path = EditorUtility.OpenFilePanel(title, string.Empty, "stl");

            if (string.IsNullOrWhiteSpace(path))
            {
                return Task.FromResult(JawFilePickResult.Cancel());
            }

            if (!path.EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JawFilePickResult.Failure("Lütfen .stl uzantılı bir dosya seçin."));
            }

            try
            {
                return Task.FromResult(JawFilePickResult.FromBytes(path, File.ReadAllBytes(path)));
            }
            catch (Exception ex)
            {
                return Task.FromResult(JawFilePickResult.Failure($"STL dosyası okunamadı: {ex.Message}"));
            }
#else
            return Task.FromResult(JawFilePickResult.Failure("Bu platform için dosya seçici adapter henüz bağlanmadı."));
#endif
        }
    }
}
