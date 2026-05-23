using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace JawTracking.FileAccess
{
    public sealed class WindowsJawFilePicker : IJawFilePicker
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        public async Task<JawFilePickResult> PickModelFileAsync(JawModelRole role, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return JawFilePickResult.Cancel();
            }

            FullScreenMode previousMode = Screen.fullScreenMode;
            bool previousFullscreen = Screen.fullScreen;
            if (previousFullscreen)
            {
                Screen.fullScreenMode = FullScreenMode.Windowed;
                Screen.fullScreen = false;
                await Task.Delay(250);
            }

            string title = "Model Seç";
            if (role == JawModelRole.UpperJaw) title = "Üst Çene Modeli Seç";
            else if (role == JawModelRole.LowerJaw) title = "Alt Çene Modeli Seç";
            else if (role == JawModelRole.BiteScan1) title = "1. Isırma Modeli Seç";
            else if (role == JawModelRole.BiteScan2) title = "2. Isırma Modeli Seç";
            try
            {
                WindowsPickResult pickResult = await PickPathWithExternalDialogAsync(title, cancellationToken);

                if (cancellationToken.IsCancellationRequested || pickResult.Cancelled)
                {
                    return JawFilePickResult.Cancel();
                }

                if (!string.IsNullOrEmpty(pickResult.ErrorMessage))
                {
                    return JawFilePickResult.Failure(pickResult.ErrorMessage);
                }

                if (string.IsNullOrWhiteSpace(pickResult.Path))
                {
                    return JawFilePickResult.Cancel();
                }

                if (!pickResult.Path.EndsWith(".stl", StringComparison.OrdinalIgnoreCase) &&
                    !pickResult.Path.EndsWith(".ply", StringComparison.OrdinalIgnoreCase))
                {
                    return JawFilePickResult.Failure("Lütfen .stl veya .ply uzantılı bir model dosyası seçin.");
                }

                try
                {
                    return JawFilePickResult.FromBytes(pickResult.Path, File.ReadAllBytes(pickResult.Path));
                }
                catch (Exception ex)
                {
                    return JawFilePickResult.Failure($"Model dosyası okunamadı: {ex.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                return JawFilePickResult.Cancel();
            }
            finally
            {
                if (previousFullscreen)
                {
                    Screen.fullScreenMode = previousMode;
                    Screen.fullScreen = true;
                }
            }
        }
#else
        public Task<JawFilePickResult> PickModelFileAsync(JawModelRole role, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(JawFilePickResult.Cancel());
            }

            return Task.FromResult(JawFilePickResult.Failure("Windows dosya seçici yalnızca Windows build içinde çalışır."));
        }
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static async Task<WindowsPickResult> PickPathWithExternalDialogAsync(string title, CancellationToken cancellationToken)
        {
            string resultPath = Path.Combine(
                Application.temporaryCachePath,
                $"jaw_stl_picker_{Guid.NewGuid():N}.txt");

            string script = BuildPowerShellPickerScript(title, resultPath);
            string encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoLogo -NoProfile -STA -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process process = null;
            try
            {
                process = Process.Start(startInfo);
                if (process == null)
                {
                    return WindowsPickResult.Failure("Windows dosya seçici başlatılamadı.");
                }

                using (cancellationToken.Register(() => TryKill(process)))
                {
                    await Task.Run(() => process.WaitForExit(), cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return WindowsPickResult.Cancel();
                }

                if (!File.Exists(resultPath))
                {
                    return WindowsPickResult.Failure("Windows dosya seçici sonuç döndürmedi.");
                }

                string selectedPath = File.ReadAllText(resultPath, Encoding.UTF8).Trim();
                return string.IsNullOrWhiteSpace(selectedPath)
                    ? WindowsPickResult.Cancel()
                    : WindowsPickResult.FromPath(selectedPath);
            }
            catch (OperationCanceledException)
            {
                return WindowsPickResult.Cancel();
            }
            catch (Exception ex)
            {
                return WindowsPickResult.Failure($"Windows dosya seçici açılamadı: {ex.Message}");
            }
            finally
            {
                process?.Dispose();
                TryDelete(resultPath);
            }
        }

        private static string BuildPowerShellPickerScript(string title, string resultPath)
        {
            return
                "$ErrorActionPreference = 'Stop'\r\n" +
                "Add-Type -AssemblyName System.Windows.Forms\r\n" +
                "$dialog = New-Object System.Windows.Forms.OpenFileDialog\r\n" +
                "$dialog.Title = " + ToPowerShellString(title) + "\r\n" +
                "$dialog.Filter = '3D Modeller (*.stl;*.ply)|*.stl;*.ply|Tüm Dosyalar (*.*)|*.*'\r\n" +
                "$dialog.CheckFileExists = $true\r\n" +
                "$dialog.CheckPathExists = $true\r\n" +
                "$dialog.Multiselect = $false\r\n" +
                "$result = $dialog.ShowDialog()\r\n" +
                "if ($result -eq [System.Windows.Forms.DialogResult]::OK) {\r\n" +
                "    [System.IO.File]::WriteAllText(" + ToPowerShellString(resultPath) + ", $dialog.FileName, [System.Text.Encoding]::UTF8)\r\n" +
                "} else {\r\n" +
                "    [System.IO.File]::WriteAllText(" + ToPowerShellString(resultPath) + ", '', [System.Text.Encoding]::UTF8)\r\n" +
                "}\r\n";
        }

        private static string ToPowerShellString(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        private sealed class WindowsPickResult
        {
            public bool Cancelled { get; }
            public string Path { get; }
            public string ErrorMessage { get; }

            private WindowsPickResult(bool cancelled, string path, string errorMessage)
            {
                Cancelled = cancelled;
                Path = path;
                ErrorMessage = errorMessage;
            }

            public static WindowsPickResult FromPath(string path)
            {
                return new WindowsPickResult(false, path ?? string.Empty, string.Empty);
            }

            public static WindowsPickResult Cancel()
            {
                return new WindowsPickResult(true, string.Empty, string.Empty);
            }

            public static WindowsPickResult Failure(string errorMessage)
            {
                return new WindowsPickResult(false, string.Empty, errorMessage);
            }
        }
#endif
    }
}
