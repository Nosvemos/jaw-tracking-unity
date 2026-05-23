using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace JawTracking.FileAccess
{
    public sealed class AndroidJawFilePicker : IJawFilePicker
    {
        private TaskCompletionSource<JawFilePickResult> pendingPick;
        private CancellationTokenRegistration cancellationRegistration;

        public Task<JawFilePickResult> PickModelFileAsync(JawModelRole role, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(JawFilePickResult.Cancel());
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (pendingPick != null)
            {
                return Task.FromResult(JawFilePickResult.Failure("Zaten açık bir dosya seçimi var."));
            }

            pendingPick = new TaskCompletionSource<JawFilePickResult>();
            Task<JawFilePickResult> pickTask = pendingPick.Task;
            cancellationRegistration = cancellationToken.Register(() => CompletePendingPick(JawFilePickResult.Cancel()));
            AndroidJawFilePickerBridge.EnsureExists();
            AndroidJawFilePickerBridge.FilePicked += HandleAndroidFilePicked;

            try
            {
                string title = "Model Seç";
                if (role == JawModelRole.UpperJaw) title = "Üst Çene Modeli Seç";
                else if (role == JawModelRole.LowerJaw) title = "Alt Çene Modeli Seç";
                else if (role == JawModelRole.BiteScan1) title = "1. Isırma Modeli Seç";
                else if (role == JawModelRole.BiteScan2) title = "2. Isırma Modeli Seç";

                using var pickerClass = new AndroidJavaClass("com.jawtracking.fileaccess.JawFilePickerActivity");
                pickerClass.CallStatic("openPicker", title);
            }
            catch (Exception ex)
            {
                CompletePendingPick(JawFilePickResult.Failure($"Android dosya seçici açılamadı: {ex.Message}"));
            }

            return pickTask;
#else
            return Task.FromResult(JawFilePickResult.Failure("Android dosya seçici yalnızca Android build içinde çalışır."));
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void HandleAndroidFilePicked(AndroidJawFilePickerPayload payload)
        {
            if (payload == null)
            {
                CompletePendingPick(JawFilePickResult.Failure("Android dosya seçici boş yanıt döndürdü."));
                return;
            }

            if (payload.cancelled)
            {
                CompletePendingPick(JawFilePickResult.Cancel());
                return;
            }

            if (!payload.success)
            {
                CompletePendingPick(JawFilePickResult.Failure(payload.errorMessage));
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.path))
            {
                CompletePendingPick(JawFilePickResult.Failure("Seçilen model dosyasının geçici yolu alınamadı."));
                return;
            }

            try
            {
                CompletePendingPick(JawFilePickResult.FromBytes(payload.path, File.ReadAllBytes(payload.path)));
            }
            catch (Exception ex)
            {
                CompletePendingPick(JawFilePickResult.Failure($"Model dosyası okunamadı: {ex.Message}"));
            }
        }

        private void CompletePendingPick(JawFilePickResult result)
        {
            TaskCompletionSource<JawFilePickResult> taskCompletion = pendingPick;
            if (taskCompletion == null)
            {
                return;
            }

            AndroidJawFilePickerBridge.FilePicked -= HandleAndroidFilePicked;
            cancellationRegistration.Dispose();
            pendingPick = null;
            taskCompletion.TrySetResult(result);
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    [Serializable]
    internal sealed class AndroidJawFilePickerPayload
    {
        public bool success;
        public bool cancelled;
        public string path = string.Empty;
        public string displayName = string.Empty;
        public string errorMessage = string.Empty;
    }

    internal sealed class AndroidJawFilePickerBridge : MonoBehaviour
    {
        internal const string GameObjectName = "JawFilePickerBridge";

        private static AndroidJawFilePickerBridge instance;

        internal static event Action<AndroidJawFilePickerPayload> FilePicked;

        internal static void EnsureExists()
        {
            if (instance != null)
            {
                return;
            }

            var bridgeObject = new GameObject(GameObjectName);
            instance = bridgeObject.AddComponent<AndroidJawFilePickerBridge>();
            DontDestroyOnLoad(bridgeObject);
        }

        public void OnAndroidFilePicked(string json)
        {
            AndroidJawFilePickerPayload payload;
            try
            {
                payload = JsonUtility.FromJson<AndroidJawFilePickerPayload>(json);
            }
            catch (Exception ex)
            {
                payload = new AndroidJawFilePickerPayload
                {
                    success = false,
                    cancelled = false,
                    errorMessage = $"Android dosya seçici yanıtı okunamadı: {ex.Message}"
                };
            }

            FilePicked?.Invoke(payload);
        }
    }
#endif
}
