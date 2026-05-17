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

        [SerializeField] private JawModelImportService modelImportService;

        private VisualElement appRoot;
        private Label statusLabel;
        private Label upperFileLabel;
        private Label lowerFileLabel;

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

            statusLabel = documentRoot.Q<Label>("connection-status");
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
            viewport.Add(CreateTitle("3B Çene Görünümü"));
            viewport.Add(CreateMutedLabel("Üst ve alt çene STL dosyalarını yükleyerek başlayın."));
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
    }
}
