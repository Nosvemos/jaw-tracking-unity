using JawTracking.FileAccess;
using JawTracking.UI;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JawTracking
{
    public static class JawTrackingSceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureScene()
        {
            ForceStandaloneFullscreen();
            ForceMobileLandscapeOrientation();
            RepairExistingUiDocuments();
        }

        private static void ForceStandaloneFullscreen()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
#endif
        }

        private static void ForceMobileLandscapeOrientation()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
#endif
        }

        private static void RepairExistingUiDocuments()
        {
            UIDocument[] documents = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (UIDocument document in documents)
            {
                if (document.panelSettings == null)
                {
                    document.panelSettings = CreatePanelSettings();
                }
                else if (document.panelSettings.themeStyleSheet == null)
                {
                    document.panelSettings.themeStyleSheet = LoadRuntimeTheme();
                }

#if UNITY_EDITOR
                if (document.visualTreeAsset == null)
                {
                    document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/UXML/JawTrackingMain.uxml");
                }
#endif
            }
        }

        private static PanelSettings CreatePanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "Runtime Jaw Tracking Panel Settings";
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1440, 900);
            settings.match = 0.5f;
            settings.themeStyleSheet = LoadRuntimeTheme();
            return settings;
        }

        private static ThemeStyleSheet LoadRuntimeTheme()
        {
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("JawTrackingRuntimeTheme");

#if UNITY_EDITOR
            if (theme == null)
            {
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/Resources/JawTrackingRuntimeTheme.tss");
            }
#endif

            return theme;
        }
    }
}
