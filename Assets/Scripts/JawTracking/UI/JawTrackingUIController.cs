using System.IO;
using JawTracking.FileAccess;
using UnityEngine;
using UnityEngine.UIElements;

namespace JawTracking.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class JawTrackingUIController : MonoBehaviour
    {
        private const float NarrowWidth = 760f;
        private const float MediumWidth = 1080f;
        private const int MinViewportTextureSize = 256;
        private const int MaxViewportTextureSize = 2048;

        [SerializeField] private JawModelImportService modelImportService;
        [SerializeField] private Camera viewportCamera;

        private VisualElement appRoot;
        private VisualElement viewportPanel;
        private Image viewportImage;
        private Label statusLabel;
        private Label viewportEmptyHintLabel;
        private Label upperFileLabel;
        private Label lowerFileLabel;
        private RenderTexture viewportRenderTexture;
        private int currentViewportTextureWidth;
        private int currentViewportTextureHeight;
        private Camera displayFallbackCamera;

        private Button loadUpperButton;
        private Button loadLowerButton;
        private Button startUdpButton;
        private Button simulationButton;
        private Button calibrateButton;
        private Button resetCalibrationButton;

        public void Configure(JawModelImportService importService)
        {
            modelImportService = importService;
            BindImportService();
        }

        private void OnEnable()
        {
            BuildFallbackUiIfNeeded();
            BindElements();
            BindButtons();
            BindImportService();
            SetupViewportRenderTarget();

            if (appRoot != null)
            {
                appRoot.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
                ApplyResponsiveClass(appRoot.resolvedStyle.width);
            }
        }

        private void OnDisable()
        {
            if (appRoot != null)
            {
                appRoot.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            }

            UnbindButtons();
            ReleaseViewportRenderTarget();

            if (modelImportService != null)
            {
                modelImportService.StatusChanged -= HandleStatusChanged;
                modelImportService.ModelImportCompleted -= HandleModelImportCompleted;
            }
        }

        private void BindElements()
        {
            VisualElement documentRoot = GetComponent<UIDocument>().rootVisualElement;
            appRoot = documentRoot.Q<VisualElement>("app-root") ?? documentRoot;
            viewportPanel = documentRoot.Q<VisualElement>("viewport-panel");
            viewportImage = documentRoot.Q<Image>("viewport-render");

            statusLabel = documentRoot.Q<Label>("connection-status");
            viewportEmptyHintLabel = documentRoot.Q<Label>("viewport-empty-hint");
            upperFileLabel = documentRoot.Q<Label>("upper-file-label");
            lowerFileLabel = documentRoot.Q<Label>("lower-file-label");

            loadUpperButton = documentRoot.Q<Button>("load-upper-button");
            loadLowerButton = documentRoot.Q<Button>("load-lower-button");
            startUdpButton = documentRoot.Q<Button>("start-udp-button");
            simulationButton = documentRoot.Q<Button>("simulation-button");
            calibrateButton = documentRoot.Q<Button>("calibrate-button");
            resetCalibrationButton = documentRoot.Q<Button>("reset-calibration-button");
        }

        private void BindButtons()
        {
            UnbindButtons();

            if (loadUpperButton != null)
            {
                loadUpperButton.clicked += LoadUpperJaw;
            }

            if (loadLowerButton != null)
            {
                loadLowerButton.clicked += LoadLowerJaw;
            }

            if (startUdpButton != null)
            {
                startUdpButton.clicked += ShowUdpPlaceholder;
            }

            if (simulationButton != null)
            {
                simulationButton.clicked += ShowSimulationPlaceholder;
            }

            if (calibrateButton != null)
            {
                calibrateButton.clicked += ShowCalibrationPlaceholder;
            }

            if (resetCalibrationButton != null)
            {
                resetCalibrationButton.clicked += ShowResetCalibrationPlaceholder;
            }
        }

        private void UnbindButtons()
        {
            if (loadUpperButton != null)
            {
                loadUpperButton.clicked -= LoadUpperJaw;
            }

            if (loadLowerButton != null)
            {
                loadLowerButton.clicked -= LoadLowerJaw;
            }

            if (startUdpButton != null)
            {
                startUdpButton.clicked -= ShowUdpPlaceholder;
            }

            if (simulationButton != null)
            {
                simulationButton.clicked -= ShowSimulationPlaceholder;
            }

            if (calibrateButton != null)
            {
                calibrateButton.clicked -= ShowCalibrationPlaceholder;
            }

            if (resetCalibrationButton != null)
            {
                resetCalibrationButton.clicked -= ShowResetCalibrationPlaceholder;
            }
        }

        private void BindImportService()
        {
            if (modelImportService == null)
            {
                return;
            }

            modelImportService.StatusChanged -= HandleStatusChanged;
            modelImportService.ModelImportCompleted -= HandleModelImportCompleted;
            modelImportService.StatusChanged += HandleStatusChanged;
            modelImportService.ModelImportCompleted += HandleModelImportCompleted;
        }

        private void LoadUpperJaw()
        {
            modelImportService?.LoadUpperJawFromPicker();
        }

        private void LoadLowerJaw()
        {
            modelImportService?.LoadLowerJawFromPicker();
        }

        private void ShowUdpPlaceholder()
        {
            SetStatus("UDP entegrasyonu sonraki aşamada bağlanacak.");
        }

        private void ShowSimulationPlaceholder()
        {
            SetStatus("Simülasyon modu sonraki aşamada bağlanacak.");
        }

        private void ShowCalibrationPlaceholder()
        {
            SetStatus("Kalibrasyon sistemi sonraki aşamada bağlanacak.");
        }

        private void ShowResetCalibrationPlaceholder()
        {
            SetStatus("Kalibrasyon sıfırlama sonraki aşamada bağlanacak.");
        }

        private void HandleStatusChanged(string message)
        {
            SetStatus(message);
        }

        private void HandleModelImportCompleted(JawModelRole role, StlImportResult result, string path)
        {
            if (!result.Success)
            {
                SetStatus(result.ErrorMessage);
                return;
            }

            string fileName = string.IsNullOrWhiteSpace(path) ? "seçilen dosya" : Path.GetFileName(path);
            if (role == JawModelRole.UpperJaw && upperFileLabel != null)
            {
                upperFileLabel.text = $"Üst çene: {fileName}";
            }
            else if (role == JawModelRole.LowerJaw && lowerFileLabel != null)
            {
                lowerFileLabel.text = $"Alt çene: {fileName}";
            }

            HideViewportEmptyHint();
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = string.IsNullOrWhiteSpace(message) ? "Hazır" : message;
            }
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyResponsiveClass(evt.newRect.width);
        }

        private void ApplyResponsiveClass(float width)
        {
            if (appRoot == null || width <= 0f)
            {
                return;
            }

            appRoot.RemoveFromClassList("layout-wide");
            appRoot.RemoveFromClassList("layout-medium");
            appRoot.RemoveFromClassList("layout-narrow");

            if (width < NarrowWidth)
            {
                appRoot.AddToClassList("layout-narrow");
            }
            else if (width < MediumWidth)
            {
                appRoot.AddToClassList("layout-medium");
            }
            else
            {
                appRoot.AddToClassList("layout-wide");
            }

            ResizeViewportRenderTarget();
        }

        private void SetupViewportRenderTarget()
        {
            if (viewportPanel == null)
            {
                return;
            }

            if (viewportImage == null)
            {
                viewportImage = new Image { name = "viewport-render", pickingMode = PickingMode.Ignore };
                viewportImage.AddToClassList("viewport-render");
                viewportPanel.Insert(0, viewportImage);
            }

            if (viewportCamera == null)
            {
                viewportCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            if (viewportCamera != null)
            {
                viewportCamera.clearFlags = CameraClearFlags.SolidColor;
                viewportCamera.backgroundColor = new Color(0.047f, 0.065f, 0.082f, 1f);
                viewportCamera.nearClipPlane = 0.003f;
                viewportCamera.farClipPlane = 10f;
            }

            ResizeViewportRenderTarget();
        }

        private void ResizeViewportRenderTarget()
        {
            if (viewportCamera == null || viewportImage == null || viewportPanel == null)
            {
                return;
            }

            int width = Mathf.Clamp(Mathf.CeilToInt(viewportPanel.resolvedStyle.width), MinViewportTextureSize, MaxViewportTextureSize);
            int height = Mathf.Clamp(Mathf.CeilToInt(viewportPanel.resolvedStyle.height), MinViewportTextureSize, MaxViewportTextureSize);

            if (viewportRenderTexture != null &&
                currentViewportTextureWidth == width &&
                currentViewportTextureHeight == height)
            {
                return;
            }

            ReleaseViewportRenderTarget();

            viewportRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Jaw Viewport Render Texture",
                antiAliasing = 2,
                useMipMap = false,
                autoGenerateMips = false
            };

            viewportRenderTexture.Create();
            viewportCamera.targetTexture = viewportRenderTexture;
            viewportImage.image = viewportRenderTexture;
            EnsureDisplayFallbackCamera();
            currentViewportTextureWidth = width;
            currentViewportTextureHeight = height;
        }

        private void EnsureDisplayFallbackCamera()
        {
            if (CameraRendersToDisplay())
            {
                return;
            }

            if (displayFallbackCamera == null)
            {
                var cameraObject = new GameObject("Display Fallback Camera");
                cameraObject.transform.SetParent(transform, false);
                displayFallbackCamera = cameraObject.AddComponent<Camera>();
                displayFallbackCamera.clearFlags = CameraClearFlags.SolidColor;
                displayFallbackCamera.backgroundColor = new Color(0.062f, 0.078f, 0.098f, 1f);
                displayFallbackCamera.cullingMask = 0;
                displayFallbackCamera.depth = -100f;
                displayFallbackCamera.nearClipPlane = 0.01f;
                displayFallbackCamera.farClipPlane = 1f;
            }

            displayFallbackCamera.enabled = true;
        }

        private static bool CameraRendersToDisplay()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Camera camera in cameras)
            {
                if (camera.enabled && camera.targetTexture == null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ReleaseViewportRenderTarget()
        {
            if (viewportCamera != null && viewportCamera.targetTexture == viewportRenderTexture)
            {
                viewportCamera.targetTexture = null;
            }

            if (viewportImage != null && viewportImage.image == viewportRenderTexture)
            {
                viewportImage.image = null;
            }

            if (viewportRenderTexture != null)
            {
                viewportRenderTexture.Release();
                Destroy(viewportRenderTexture);
                viewportRenderTexture = null;
            }

            currentViewportTextureWidth = 0;
            currentViewportTextureHeight = 0;
        }

        private void BuildFallbackUiIfNeeded()
        {
            UIDocument document = GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            if (root.childCount > 0)
            {
                return;
            }

            root.AddToClassList("app-root");
            root.AddToClassList("layout-wide");
            root.name = "app-root";

            var topBar = new VisualElement { name = "top-bar" };
            topBar.AddToClassList("top-bar");
            root.Add(topBar);

            var title = new Label("Çene Takip İstemcisi") { name = "app-title" };
            title.AddToClassList("app-title");
            topBar.Add(title);

            statusLabel = new Label("Hazır") { name = "connection-status" };
            statusLabel.AddToClassList("status-pill");
            topBar.Add(statusLabel);

            var workspace = new VisualElement { name = "workspace" };
            workspace.AddToClassList("workspace");
            root.Add(workspace);

            var leftColumn = new VisualElement { name = "left-column" };
            leftColumn.AddToClassList("left-column");
            workspace.Add(leftColumn);

            var viewport = new VisualElement { name = "viewport-panel" };
            viewport.AddToClassList("viewport-panel");
            viewportImage = new Image { name = "viewport-render", pickingMode = PickingMode.Ignore };
            viewportImage.AddToClassList("viewport-render");
            viewport.Add(viewportImage);
            var viewportHeader = new VisualElement();
            viewportHeader.AddToClassList("viewport-header");
            viewportHeader.Add(CreateTitle("3B Çene Görünümü"));
            viewportEmptyHintLabel = CreateMutedLabel("STL modeller yüklendiğinde çene görünümü burada hareket edecek.");
            viewportEmptyHintLabel.name = "viewport-empty-hint";
            viewportHeader.Add(viewportEmptyHintLabel);
            viewport.Add(viewportHeader);
            leftColumn.Add(viewport);

            var metricRow = new VisualElement { name = "primary-metrics" };
            metricRow.AddToClassList("metric-row");
            metricRow.Add(CreateMetricCard("Açıklık", "opening-value", "0.0 mm"));
            metricRow.Add(CreateMetricCard("Lateral Sapma", "lateral-value", "0.0 mm"));
            metricRow.Add(CreateMetricCard("Protrüzyon", "protrusion-value", "0.0 mm"));
            VisualElement confidenceCard = CreateMetricCard("Güven", "confidence-value", "-- %");
            confidenceCard.AddToClassList("last-metric-card");
            metricRow.Add(confidenceCard);
            leftColumn.Add(metricRow);

            var rightRail = new VisualElement { name = "right-rail" };
            rightRail.AddToClassList("right-rail");
            workspace.Add(rightRail);

            VisualElement importPanel = CreatePanel("Model Yükleme");
            importPanel.Add(CreateButton("load-upper-button", "Üst Çene STL Yükle"));
            importPanel.Add(CreateButton("load-lower-button", "Alt Çene STL Yükle"));
            upperFileLabel = CreateMutedLabel("Üst çene: yüklenmedi");
            upperFileLabel.name = "upper-file-label";
            lowerFileLabel = CreateMutedLabel("Alt çene: yüklenmedi");
            lowerFileLabel.name = "lower-file-label";
            importPanel.Add(upperFileLabel);
            importPanel.Add(lowerFileLabel);
            rightRail.Add(importPanel);

            VisualElement connectionPanel = CreatePanel("Bağlantı");
            connectionPanel.Add(CreateButton("start-udp-button", "UDP Başlat"));
            connectionPanel.Add(CreateButton("simulation-button", "Simülasyon Modu"));
            connectionPanel.Add(CreateMetricLine("packet-rate-label", "Paket hızı: -- FPS"));
            connectionPanel.Add(CreateMetricLine("tracking-label", "Takip: bekleniyor"));
            rightRail.Add(connectionPanel);

            VisualElement calibrationPanel = CreatePanel("Kalibrasyon");
            calibrationPanel.Add(CreateButton("calibrate-button", "Dinlenme Pozisyonunu Kalibre Et"));
            calibrationPanel.Add(CreateButton("reset-calibration-button", "Kalibrasyonu Sıfırla"));
            calibrationPanel.Add(CreateMetricLine("calibration-label", "Durum: kalibre edilmedi"));
            calibrationPanel.AddToClassList("last-panel");
            rightRail.Add(calibrationPanel);
        }

        private static VisualElement CreatePanel(string title)
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.Add(CreateTitle(title));
            return panel;
        }

        private static Label CreateTitle(string text)
        {
            var label = new Label(text);
            label.AddToClassList("panel-title");
            return label;
        }

        private static Label CreateMutedLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("muted-text");
            return label;
        }

        private static Label CreateMetricLine(string name, string text)
        {
            var label = new Label(text) { name = name };
            label.AddToClassList("metric-line");
            return label;
        }

        private static VisualElement CreateMetricCard(string title, string valueName, string value)
        {
            var card = new VisualElement();
            card.AddToClassList("metric-card");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("metric-title");
            card.Add(titleLabel);

            var valueLabel = new Label(value) { name = valueName };
            valueLabel.AddToClassList("metric-value");
            card.Add(valueLabel);

            return card;
        }

        private static Button CreateButton(string name, string text)
        {
            return new Button { name = name, text = text };
        }

        private void HideViewportEmptyHint()
        {
            if (viewportEmptyHintLabel != null)
            {
                viewportEmptyHintLabel.style.display = DisplayStyle.None;
            }
        }
    }
}
