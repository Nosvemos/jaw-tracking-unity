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
        public Task<JawFilePickResult> PickModelFileAsync(JawModelRole role, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(JawFilePickResult.Cancel());
            }

#if UNITY_EDITOR
            string title = "Model Seç";
            if (role == JawModelRole.UpperJaw) title = "Üst Çene Modeli Seç";
            else if (role == JawModelRole.LowerJaw) title = "Alt Çene Modeli Seç";
            else if (role == JawModelRole.BiteScan1) title = "1. Isırma Modeli Seç";
            else if (role == JawModelRole.BiteScan2) title = "2. Isırma Modeli Seç";
            string path = EditorUtility.OpenFilePanel(title, string.Empty, "stl,ply");

            if (string.IsNullOrWhiteSpace(path))
            {
                return Task.FromResult(JawFilePickResult.Cancel());
            }

            if (!path.EndsWith(".stl", StringComparison.OrdinalIgnoreCase) && 
                !path.EndsWith(".ply", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JawFilePickResult.Failure("Lütfen .stl veya .ply uzantılı bir dosya seçin."));
            }

            try
            {
                return Task.FromResult(JawFilePickResult.FromBytes(path, File.ReadAllBytes(path)));
            }
            catch (Exception ex)
            {
                return Task.FromResult(JawFilePickResult.Failure($"Model dosyası okunamadı: {ex.Message}"));
            }
#else
            return Task.FromResult(JawFilePickResult.Failure("Bu platform için dosya seçici adapter henüz bağlanmadı."));
#endif
        }
    }
}
