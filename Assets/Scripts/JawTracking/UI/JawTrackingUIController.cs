using System.IO;
using JawTracking.Data;
using JawTracking.FileAccess;
using JawTracking.Motion;
using JawTracking.Network;
using JawTracking.Simulation;
using JawTracking.Visualization;
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
        private const float LiveMetricUiIntervalSeconds = 0.05f;
        private static readonly Vector3 DefaultPivotBoundsAnchor = new Vector3(0f, 0.72f, -0.58f);
        private const float DefaultPivotLiftMm = 0f;
        private const float DefaultOpeningAngleScale = 0.82f;

        [SerializeField] private JawModelImportService modelImportService;
        [SerializeField] private JawModelController modelController;
        [SerializeField] private JawDataSimulator simulator;
        [SerializeField] private UdpJawMotionSource udpMotionSource;
        [SerializeField] private Camera viewportCamera;
        [SerializeField] private JawOrbitCameraController orbitCameraController;

        [Header("Network Settings")]
        [SerializeField] private string udpListenAddress = "0.0.0.0";
        [SerializeField, Range(1, 65535)] private int udpListenPort = 5055;

        private VisualElement appRoot;
        private VisualElement viewportPanel;
        private Image viewportImage;
        private ScrollView rightRailScroll;
        private Label statusLabel;
        private Label viewportEmptyHintLabel;
        private Label upperFileLabel;
        private Label lowerFileLabel;
        private Label openingValueLabel;
        private Label lateralValueLabel;
        private Label protrusionValueLabel;
        private Label confidenceValueLabel;
        private Label trackingLabel;
        private Label packetRateLabel;
        private Label motionPauseLabel;
        private Label pivotYValueLabel;
        private Label pivotZValueLabel;
        private Label pivotXValueLabel;
        private Label pivotLiftValueLabel;
        private Label openingScaleValueLabel;
        private VisualElement modelSectionBody;
        private VisualElement connectionSectionBody;
        private VisualElement calibrationSectionBody;
        private VisualElement motionSectionBody;
        private VisualElement modelSectionToggle;
        private VisualElement connectionSectionToggle;
        private VisualElement calibrationSectionToggle;
        private VisualElement motionSectionToggle;
        private Label modelSectionIcon;
        private Label connectionSectionIcon;
        private Label calibrationSectionIcon;
        private Label motionSectionIcon;
        private Button resetPivotButton;
        private Button invertOpeningButton;
        private Slider pivotYSlider;
        private Slider pivotZSlider;
        private Slider pivotXSlider;
        private Slider pivotLiftSlider;
        private Slider openingScaleSlider;
        private RenderTexture viewportRenderTexture;
        private int currentViewportTextureWidth;
        private int currentViewportTextureHeight;
        private float nextLiveMetricUiUpdateTime;
        private bool isMotionPaused;
        private Camera displayFallbackCamera;

        private Button loadUpperButton;
        private Button loadLowerButton;
        private Button clearModelsButton;
        private Button startUdpButton;
        private Button simulationButton;
        private Button pauseMotionButton;
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
            BindModelController();
            BindButtons();
            BindAccordionButtons();
            BindMotionControls();
            BindImportService();
            BindSimulator();
            BindUdpMotionSource();
            SetupViewportRenderTarget();

            if (appRoot != null)
            {
                appRoot.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
                ApplyResponsiveClass(appRoot.resolvedStyle.width);
            }

            UpdateButtonStates();
        }

        private void OnDisable()
        {
            if (appRoot != null)
            {
                appRoot.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            }

            UnbindButtons();
            UnbindAccordionButtons();
            UnbindMotionControls();
            ReleaseViewportRenderTarget();

            if (modelImportService != null)
            {
                modelImportService.StatusChanged -= HandleStatusChanged;
                modelImportService.ModelImportCompleted -= HandleModelImportCompleted;
                modelImportService.PreMeshApplied -= HandlePreMeshApplied;
            }

            if (simulator != null)
            {
                simulator.MotionUpdated -= HandleSimulationMotionUpdated;
            }

            if (udpMotionSource != null)
            {
                udpMotionSource.MotionUpdated -= HandleUdpMotionUpdated;
                udpMotionSource.StatusChanged -= HandleStatusChanged;
                udpMotionSource.StatsUpdated -= HandleUdpStatsUpdated;
            }
        }

        private void BindElements()
        {
            VisualElement documentRoot = GetComponent<UIDocument>().rootVisualElement;
            appRoot = documentRoot.Q<VisualElement>("app-root") ?? documentRoot;
            viewportPanel = documentRoot.Q<VisualElement>("viewport-panel");
            viewportImage = documentRoot.Q<Image>("viewport-render");
            rightRailScroll = documentRoot.Q<ScrollView>("right-rail-scroll");
            if (rightRailScroll != null)
            {
                rightRailScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
                rightRailScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            }

            statusLabel = documentRoot.Q<Label>("connection-status");
            viewportEmptyHintLabel = documentRoot.Q<Label>("viewport-empty-hint");
            upperFileLabel = documentRoot.Q<Label>("upper-file-label");
            lowerFileLabel = documentRoot.Q<Label>("lower-file-label");
            openingValueLabel = documentRoot.Q<Label>("opening-value");
            lateralValueLabel = documentRoot.Q<Label>("lateral-value");
            protrusionValueLabel = documentRoot.Q<Label>("protrusion-value");
            confidenceValueLabel = documentRoot.Q<Label>("confidence-value");
            trackingLabel = documentRoot.Q<Label>("tracking-label");
            packetRateLabel = documentRoot.Q<Label>("packet-rate-label");
            motionPauseLabel = documentRoot.Q<Label>("motion-pause-label");
            pivotYValueLabel = documentRoot.Q<Label>("pivot-y-value");
            pivotZValueLabel = documentRoot.Q<Label>("pivot-z-value");
            pivotXValueLabel = documentRoot.Q<Label>("pivot-x-value");
            pivotLiftValueLabel = documentRoot.Q<Label>("pivot-lift-value");
            openingScaleValueLabel = documentRoot.Q<Label>("opening-scale-value");
            modelSectionBody = documentRoot.Q<VisualElement>("model-section-body");
            connectionSectionBody = documentRoot.Q<VisualElement>("connection-section-body");
            calibrationSectionBody = documentRoot.Q<VisualElement>("calibration-section-body");
            motionSectionBody = documentRoot.Q<VisualElement>("motion-section-body");

            loadUpperButton = documentRoot.Q<Button>("load-upper-button");
            loadLowerButton = documentRoot.Q<Button>("load-lower-button");
            clearModelsButton = documentRoot.Q<Button>("clear-models-button");
            startUdpButton = documentRoot.Q<Button>("start-udp-button");
            simulationButton = documentRoot.Q<Button>("simulation-button");
            pauseMotionButton = documentRoot.Q<Button>("pause-motion-button");
            calibrateButton = documentRoot.Q<Button>("calibrate-button");
            resetCalibrationButton = documentRoot.Q<Button>("reset-calibration-button");
            modelSectionToggle = documentRoot.Q<VisualElement>("model-section-toggle");
            connectionSectionToggle = documentRoot.Q<VisualElement>("connection-section-toggle");
            calibrationSectionToggle = documentRoot.Q<VisualElement>("calibration-section-toggle");
            motionSectionToggle = documentRoot.Q<VisualElement>("motion-section-toggle");
            modelSectionIcon = documentRoot.Q<Label>("model-section-icon");
            connectionSectionIcon = documentRoot.Q<Label>("connection-section-icon");
            calibrationSectionIcon = documentRoot.Q<Label>("calibration-section-icon");
            motionSectionIcon = documentRoot.Q<Label>("motion-section-icon");
            resetPivotButton = documentRoot.Q<Button>("reset-pivot-button");
            invertOpeningButton = documentRoot.Q<Button>("invert-opening-button");
            pivotYSlider = documentRoot.Q<Slider>("pivot-y-slider");
            pivotZSlider = documentRoot.Q<Slider>("pivot-z-slider");
            pivotXSlider = documentRoot.Q<Slider>("pivot-x-slider");
            pivotLiftSlider = documentRoot.Q<Slider>("pivot-lift-slider");
            openingScaleSlider = documentRoot.Q<Slider>("opening-scale-slider");
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

            if (clearModelsButton != null)
            {
                clearModelsButton.clicked += ClearModels;
            }

            if (startUdpButton != null)
            {
                startUdpButton.clicked += ToggleUdp;
            }

            if (simulationButton != null)
            {
                simulationButton.clicked += ToggleSimulation;
            }

            if (pauseMotionButton != null)
            {
                pauseMotionButton.clicked += ToggleMotionPause;
            }

            if (calibrateButton != null)
            {
                calibrateButton.clicked += TriggerCalibration;
            }

            if (resetCalibrationButton != null)
            {
                resetCalibrationButton.clicked += TriggerCalibrationReset;
            }
        }

        private void BindAccordionButtons()
        {
            UnbindAccordionButtons();

            if (modelSectionToggle != null)
            {
                modelSectionToggle.RegisterCallback<ClickEvent>(HandleModelSectionClicked);
            }

            if (connectionSectionToggle != null)
            {
                connectionSectionToggle.RegisterCallback<ClickEvent>(HandleConnectionSectionClicked);
            }

            if (calibrationSectionToggle != null)
            {
                calibrationSectionToggle.RegisterCallback<ClickEvent>(HandleCalibrationSectionClicked);
            }

            if (motionSectionToggle != null)
            {
                motionSectionToggle.RegisterCallback<ClickEvent>(HandleMotionSectionClicked);
            }

            RefreshAccordionIcons();
        }

        private void UnbindAccordionButtons()
        {
            if (modelSectionToggle != null)
            {
                modelSectionToggle.UnregisterCallback<ClickEvent>(HandleModelSectionClicked);
            }

            if (connectionSectionToggle != null)
            {
                connectionSectionToggle.UnregisterCallback<ClickEvent>(HandleConnectionSectionClicked);
            }

            if (calibrationSectionToggle != null)
            {
                calibrationSectionToggle.UnregisterCallback<ClickEvent>(HandleCalibrationSectionClicked);
            }

            if (motionSectionToggle != null)
            {
                motionSectionToggle.UnregisterCallback<ClickEvent>(HandleMotionSectionClicked);
            }
        }

        private void BindMotionControls()
        {
            UnbindMotionControls();

            if (pivotYSlider != null)
            {
                pivotYSlider.RegisterValueChangedCallback(HandlePivotYChanged);
            }

            if (pivotZSlider != null)
            {
                pivotZSlider.RegisterValueChangedCallback(HandlePivotZChanged);
            }

            if (pivotXSlider != null)
            {
                pivotXSlider.RegisterValueChangedCallback(HandlePivotXChanged);
            }

            if (pivotLiftSlider != null)
            {
                pivotLiftSlider.RegisterValueChangedCallback(HandlePivotLiftChanged);
            }

            if (openingScaleSlider != null)
            {
                openingScaleSlider.RegisterValueChangedCallback(HandleOpeningScaleChanged);
            }

            if (resetPivotButton != null)
            {
                resetPivotButton.clicked += RecalculatePivot;
            }

            if (invertOpeningButton != null)
            {
                invertOpeningButton.clicked += InvertOpeningDirection;
            }

            SyncMotionControlValues();
        }

        private void UnbindMotionControls()
        {
            if (pivotYSlider != null)
            {
                pivotYSlider.UnregisterValueChangedCallback(HandlePivotYChanged);
            }

            if (pivotZSlider != null)
            {
                pivotZSlider.UnregisterValueChangedCallback(HandlePivotZChanged);
            }

            if (pivotXSlider != null)
            {
                pivotXSlider.UnregisterValueChangedCallback(HandlePivotXChanged);
            }

            if (pivotLiftSlider != null)
            {
                pivotLiftSlider.UnregisterValueChangedCallback(HandlePivotLiftChanged);
            }

            if (openingScaleSlider != null)
            {
                openingScaleSlider.UnregisterValueChangedCallback(HandleOpeningScaleChanged);
            }

            if (resetPivotButton != null)
            {
                resetPivotButton.clicked -= RecalculatePivot;
            }

            if (invertOpeningButton != null)
            {
                invertOpeningButton.clicked -= InvertOpeningDirection;
            }
        }

        private void BindModelController()
        {
            if (modelController == null)
            {
                modelController = FindFirstObjectByType<JawModelController>();
            }

            if (modelController == null)
            {
                modelController = gameObject.AddComponent<JawModelController>();
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

            if (clearModelsButton != null)
            {
                clearModelsButton.clicked -= ClearModels;
            }

            if (startUdpButton != null)
            {
                startUdpButton.clicked -= ToggleUdp;
            }

            if (simulationButton != null)
            {
                simulationButton.clicked -= ToggleSimulation;
            }

            if (pauseMotionButton != null)
            {
                pauseMotionButton.clicked -= ToggleMotionPause;
            }

            if (calibrateButton != null)
            {
                calibrateButton.clicked -= TriggerCalibration;
            }

            if (resetCalibrationButton != null)
            {
                resetCalibrationButton.clicked -= TriggerCalibrationReset;
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
            modelImportService.PreMeshApplied -= HandlePreMeshApplied;
            
            modelImportService.StatusChanged += HandleStatusChanged;
            modelImportService.ModelImportCompleted += HandleModelImportCompleted;
            modelImportService.PreMeshApplied += HandlePreMeshApplied;
        }

        private void HandlePreMeshApplied(JawModelRole role)
        {
            if (role == JawModelRole.LowerJaw)
            {
                BindModelController();
                modelController?.ResetPivotToOrigin();
            }
        }

        private void BindSimulator()
        {
            if (simulator == null)
            {
                simulator = FindFirstObjectByType<JawDataSimulator>();
            }

            if (simulator == null)
            {
                var simulatorObject = new GameObject("JawSimulationRuntime");
                simulator = simulatorObject.AddComponent<JawDataSimulator>();
            }

            if (modelController != null)
            {
                simulator.SetModelController(modelController);
            }

            simulator.MotionUpdated -= HandleSimulationMotionUpdated;
            simulator.MotionUpdated += HandleSimulationMotionUpdated;
            simulator.SetMotionPaused(isMotionPaused);
            SyncMotionControlValues();
        }

        private void BindUdpMotionSource()
        {
            if (udpMotionSource == null)
            {
                udpMotionSource = FindFirstObjectByType<UdpJawMotionSource>();
            }

            if (udpMotionSource == null)
            {
                var udpObject = new GameObject("JawUdpRuntime");
                udpMotionSource = udpObject.AddComponent<UdpJawMotionSource>();
            }

            if (modelController != null)
            {
                udpMotionSource.SetModelController(modelController);
            }

            udpMotionSource.ConfigureNetwork(udpListenAddress, udpListenPort);
            udpMotionSource.MotionUpdated -= HandleUdpMotionUpdated;
            udpMotionSource.StatusChanged -= HandleStatusChanged;
            udpMotionSource.StatsUpdated -= HandleUdpStatsUpdated;
            udpMotionSource.MotionUpdated += HandleUdpMotionUpdated;
            udpMotionSource.StatusChanged += HandleStatusChanged;
            udpMotionSource.StatsUpdated += HandleUdpStatsUpdated;
            udpMotionSource.SetMotionPaused(isMotionPaused);
            UpdateUdpButtonText();
            UpdateMotionPauseText();
        }

        private void LoadUpperJaw()
        {
            modelImportService?.LoadUpperJawFromPicker();
        }

        private void LoadLowerJaw()
        {
            modelImportService?.LoadLowerJawFromPicker();
        }

        private void ClearModels()
        {
            modelImportService?.ClearModels();
            if (upperFileLabel != null) upperFileLabel.text = "Üst çene: yüklenmedi";
            if (lowerFileLabel != null) lowerFileLabel.text = "Alt çene: yüklenmedi";
            
            BindModelController();
            modelController?.ResetPivotToOrigin();
            ReturnSimulationToRest();
            simulator?.RefreshRestPose();

            if (viewportEmptyHintLabel != null)
            {
                viewportEmptyHintLabel.style.display = DisplayStyle.Flex;
            }

            UpdateButtonStates();
        }

        private void ToggleUdp()
        {
            BindUdpMotionSource();

            if (!udpMotionSource.IsReceiving)
            {
                ReturnSimulationToRest();
            }

            udpMotionSource.ToggleReceiving();
            udpMotionSource.SetMotionPaused(isMotionPaused);
            UpdateUdpButtonText();
            UpdateMotionPauseText();
            UpdateButtonStates();

            if (udpMotionSource.IsReceiving)
            {
                SetTrackingText("Takip: UDP bekleniyor");
                SetPacketRate(0f);
            }
            else
            {
                SetTrackingText("Takip: bekleniyor");
                SetPacketRate(0f);
            }
        }

        private void ToggleSimulation()
        {
            BindSimulator();
            if (udpMotionSource != null && udpMotionSource.IsReceiving)
            {
                udpMotionSource.StopReceiving();
                UpdateUdpButtonText();
            }

            simulator.Toggle();
            simulator.SetMotionPaused(isMotionPaused && simulator.IsRunning);
            UpdateSimulationButtonText();
            UpdateMotionPauseText();
            UpdateButtonStates();

            if (simulator.IsRunning)
            {
                SetStatus("Simülasyon modu çalışıyor.");
                SetTrackingText("Takip: simülasyon");
            }
            else
            {
                SetStatus("Simülasyon modu durduruldu.");
                SetTrackingText("Takip: bekleniyor");
            }
        }

        private void ToggleMotionPause()
        {
            isMotionPaused = !isMotionPaused;
            ApplyMotionPauseState();

            if (isMotionPaused)
            {
                SetStatus("Hareket durduruldu.");
                SetTrackingText("Takip: hareket duraklatıldı");
            }
            else
            {
                SetStatus("Hareket devam ediyor.");
                SetTrackingText(CurrentTrackingTextAfterResume());
            }
        }

        private void ApplyMotionPauseState()
        {
            BindSimulator();
            BindUdpMotionSource();

            simulator?.SetMotionPaused(isMotionPaused && simulator.IsRunning);
            udpMotionSource?.SetMotionPaused(isMotionPaused && udpMotionSource.IsReceiving);
            UpdateMotionPauseText();
        }

        private void TriggerCalibration()
        {
            ReturnSimulationToRest();
            simulator?.RefreshRestPose();
            ResetMetricValues();

            if (udpMotionSource != null && udpMotionSource.IsReceiving)
            {
                udpMotionSource.CalibrateRestPosition();
            }
            else
            {
                SetStatus("Simülasyon dinlenme pozisyonu kalibre edildi.");
            }
        }

        private void TriggerCalibrationReset()
        {
            ReturnSimulationToRest();
            simulator?.RefreshRestPose();
            ResetMetricValues();

            if (udpMotionSource != null)
            {
                udpMotionSource.ResetCalibration();
            }
            else
            {
                SetStatus("Kalibrasyon sıfırlandı.");
            }
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
            ReturnSimulationToRest();
            simulator?.RefreshRestPose();
            UpdateButtonStates();
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                string nextText = string.IsNullOrWhiteSpace(message) ? "Hazır" : message;
                if (statusLabel.text != nextText)
                {
                    statusLabel.text = nextText;
                }
            }
        }

        private void SetTrackingText(string message)
        {
            if (trackingLabel != null)
            {
                if (trackingLabel.text != message)
                {
                    trackingLabel.text = message;
                }
            }
        }

        private void SetPacketRate(float packetsPerSecond)
        {
            if (packetRateLabel != null)
            {
                string nextText = $"Paket hızı: {packetsPerSecond:0.0} FPS";
                if (packetRateLabel.text != nextText)
                {
                    packetRateLabel.text = nextText;
                }
            }
        }

        private void UpdateButtonStates()
        {
            bool hasUpper = modelImportService != null && !string.IsNullOrEmpty(modelImportService.UpperJawPath);
            bool hasLower = modelImportService != null && !string.IsNullOrEmpty(modelImportService.LowerJawPath);
            bool hasAnyModel = hasUpper || hasLower;

            bool isSimRunning = simulator != null && simulator.IsRunning;
            bool isUdpRunning = udpMotionSource != null && udpMotionSource.IsReceiving;
            bool isMotionActive = isSimRunning || isUdpRunning;

            if (clearModelsButton != null)
                clearModelsButton.SetEnabled(hasAnyModel);

            if (pauseMotionButton != null)
                pauseMotionButton.SetEnabled(isMotionActive);

            if (calibrateButton != null)
                calibrateButton.SetEnabled(isUdpRunning);

            if (resetCalibrationButton != null)
                resetCalibrationButton.SetEnabled(isUdpRunning);

            if (resetPivotButton != null)
                resetPivotButton.SetEnabled(hasLower);

            if (invertOpeningButton != null)
                invertOpeningButton.SetEnabled(hasLower);
        }

        private void UpdateMotionPauseText()
        {
            if (pauseMotionButton != null)
            {
                pauseMotionButton.text = isMotionPaused ? "Harekete Devam Et" : "Hareketi Durdur";
            }

            if (motionPauseLabel != null)
            {
                SetLabelTextIfChanged(
                    motionPauseLabel,
                    isMotionPaused ? "Hareket: duraklatıldı" : "Hareket: devam ediyor");
            }
        }

        private string CurrentTrackingTextAfterResume()
        {
            if (simulator != null && simulator.IsRunning)
            {
                return "Takip: simülasyon";
            }

            if (udpMotionSource != null && udpMotionSource.IsReceiving)
            {
                return "Takip: UDP bekleniyor";
            }

            return "Takip: bekleniyor";
        }

        private void HandleModelSectionClicked(ClickEvent evt)
        {
            ToggleSection(modelSectionBody);
        }

        private void HandleConnectionSectionClicked(ClickEvent evt)
        {
            ToggleSection(connectionSectionBody);
        }

        private void HandleCalibrationSectionClicked(ClickEvent evt)
        {
            ToggleSection(calibrationSectionBody);
        }

        private void HandleMotionSectionClicked(ClickEvent evt)
        {
            ToggleSection(motionSectionBody);
        }

        private void ToggleSection(VisualElement sectionBody)
        {
            if (sectionBody == null)
            {
                return;
            }

            if (sectionBody.ClassListContains("collapsed"))
            {
                sectionBody.RemoveFromClassList("collapsed");
            }
            else
            {
                sectionBody.AddToClassList("collapsed");
            }

            RefreshAccordionIcons();
        }

        private void RefreshAccordionIcons()
        {
            SetAccordionIcon(modelSectionBody, modelSectionIcon);
            SetAccordionIcon(connectionSectionBody, connectionSectionIcon);
            SetAccordionIcon(calibrationSectionBody, calibrationSectionIcon);
            SetAccordionIcon(motionSectionBody, motionSectionIcon);
        }

        private static void SetAccordionIcon(VisualElement sectionBody, Label icon)
        {
            if (sectionBody == null || icon == null)
            {
                return;
            }

            icon.text = sectionBody.ClassListContains("collapsed") ? "+" : "-";
        }

        private void HandlePivotYChanged(ChangeEvent<float> evt)
        {
            ReturnSimulationToRest();
            Vector3 anchor = CurrentPivotAnchor();
            anchor.y = evt.newValue;
            ApplyPivotAnchor(anchor);
        }

        private void HandlePivotZChanged(ChangeEvent<float> evt)
        {
            ReturnSimulationToRest();
            Vector3 anchor = CurrentPivotAnchor();
            anchor.z = evt.newValue;
            ApplyPivotAnchor(anchor);
        }

        private void HandlePivotXChanged(ChangeEvent<float> evt)
        {
            ReturnSimulationToRest();
            Vector3 anchor = CurrentPivotAnchor();
            anchor.x = evt.newValue;
            ApplyPivotAnchor(anchor);
        }

        private void HandlePivotLiftChanged(ChangeEvent<float> evt)
        {
            ReturnSimulationToRest();
            BindModelController();
            Vector3 offset = modelController != null ? modelController.ManualPivotWorldOffset : Vector3.zero;
            offset.y = evt.newValue * 0.001f;
            modelController?.SetManualPivotWorldOffset(offset);
            UpdateMotionValueLabels();
        }

        private void HandleOpeningScaleChanged(ChangeEvent<float> evt)
        {
            ReturnSimulationToRest();
            simulator?.SetOpeningAngleScale(evt.newValue);
            UpdateMotionValueLabels();
        }

        private void RecalculatePivot()
        {
            ReturnSimulationToRest();
            BindModelController();
            ApplyDefaultPivotControls();
            SetStatus("Pivot varsayılan değerlerle yeniden hesaplandı.");
            UpdateMotionValueLabels();
        }

        private void InvertOpeningDirection()
        {
            ReturnSimulationToRest();
            BindModelController();
            modelController?.ToggleOpeningDirection();
            modelController?.ForceRestPoseImmediate();
            SetStatus("Açılma yönü ters çevrildi.");
        }

        private Vector3 CurrentPivotAnchor()
        {
            BindModelController();
            return modelController != null ? modelController.PivotBoundsAnchor : DefaultPivotBoundsAnchor;
        }

        private void ApplyPivotAnchor(Vector3 anchor)
        {
            BindModelController();
            modelController?.SetPivotBoundsAnchor(anchor);
            modelController?.ForceRestPoseImmediate();
            UpdateMotionValueLabels();
        }

        private void ReturnSimulationToRest()
        {
            BindSimulator();
            simulator?.StopAndReturnToRest();
            UpdateSimulationButtonText();
            SetTrackingText("Takip: bekleniyor");
            ResetMetricValues();
        }

        private void SyncMotionControlValues()
        {
            BindModelController();

            Vector3 anchor = CurrentPivotAnchor();
            if (pivotYSlider != null)
            {
                pivotYSlider.SetValueWithoutNotify(anchor.y);
            }

            if (pivotZSlider != null)
            {
                pivotZSlider.SetValueWithoutNotify(anchor.z);
            }

            if (pivotXSlider != null)
            {
                pivotXSlider.SetValueWithoutNotify(anchor.x);
            }

            if (pivotLiftSlider != null && modelController != null)
            {
                pivotLiftSlider.SetValueWithoutNotify(modelController.ManualPivotWorldOffset.y * 1000f);
            }

            if (openingScaleSlider != null && simulator != null)
            {
                openingScaleSlider.SetValueWithoutNotify(simulator.OpeningAngleScale);
            }

            UpdateMotionValueLabels();
        }

        private void ApplyDefaultPivotControls()
        {
            if (pivotXSlider != null)
            {
                pivotXSlider.SetValueWithoutNotify(DefaultPivotBoundsAnchor.x);
            }

            if (pivotYSlider != null)
            {
                pivotYSlider.SetValueWithoutNotify(DefaultPivotBoundsAnchor.y);
            }

            if (pivotZSlider != null)
            {
                pivotZSlider.SetValueWithoutNotify(DefaultPivotBoundsAnchor.z);
            }

            if (pivotLiftSlider != null)
            {
                pivotLiftSlider.SetValueWithoutNotify(DefaultPivotLiftMm);
            }

            if (openingScaleSlider != null)
            {
                openingScaleSlider.SetValueWithoutNotify(DefaultOpeningAngleScale);
            }

            modelController?.SetManualPivotWorldOffset(Vector3.zero);
            modelController?.SetPivotBoundsAnchor(DefaultPivotBoundsAnchor);
            modelController?.ForceRestPoseImmediate();
            simulator?.SetOpeningAngleScale(DefaultOpeningAngleScale);
        }

        private void UpdateMotionValueLabels()
        {
            if (pivotYValueLabel != null && pivotYSlider != null)
            {
                pivotYValueLabel.text = $"Pivot yükseklik: {pivotYSlider.value:0.00}";
            }

            if (pivotZValueLabel != null && pivotZSlider != null)
            {
                pivotZValueLabel.text = $"Pivot arka/ön: {pivotZSlider.value:0.00}";
            }

            if (pivotXValueLabel != null && pivotXSlider != null)
            {
                pivotXValueLabel.text = $"Pivot sağ/sol: {pivotXSlider.value:0.00}";
            }

            if (pivotLiftValueLabel != null && pivotLiftSlider != null)
            {
                pivotLiftValueLabel.text = $"Pivot yukarı offset: {pivotLiftSlider.value:0} mm";
            }

            if (openingScaleValueLabel != null && openingScaleSlider != null)
            {
                openingScaleValueLabel.text = $"Açılma etkisi: {openingScaleSlider.value:0.00}";
            }
        }

        private void HandleSimulationMotionUpdated(JawMotionState state)
        {
            UpdateMetricValues(state);
        }

        private void HandleUdpMotionUpdated(JawMotionState state)
        {
            if (Time.unscaledTime >= nextLiveMetricUiUpdateTime || !state.TrackingValid)
            {
                nextLiveMetricUiUpdateTime = Time.unscaledTime + LiveMetricUiIntervalSeconds;
                UpdateMetricValues(state);
            }

            SetTrackingText(state.TrackingValid ? "Takip: geçerli" : "Takip: geçersiz");
        }

        private void HandleUdpStatsUpdated(float packetRate, bool trackingFresh)
        {
            SetPacketRate(packetRate);
            if (isMotionPaused)
            {
                SetTrackingText("Takip: hareket duraklatıldı");
                return;
            }

            if (udpMotionSource != null && udpMotionSource.IsReceiving && !trackingFresh)
            {
                SetTrackingText("Takip: veri bekleniyor");
            }
        }

        private void UpdateMetricValues(JawMotionState state)
        {
            if (openingValueLabel != null)
            {
                SetLabelTextIfChanged(openingValueLabel, $"{state.OpeningMm:0.0} mm");
            }

            if (lateralValueLabel != null)
            {
                SetLabelTextIfChanged(lateralValueLabel, $"{state.LateralMm:0.0} mm");
            }

            if (protrusionValueLabel != null)
            {
                SetLabelTextIfChanged(protrusionValueLabel, $"{state.ProtrusionMm:0.0} mm");
            }

            if (confidenceValueLabel != null)
            {
                SetLabelTextIfChanged(confidenceValueLabel, $"{state.Confidence * 100f:0} %");
            }
        }

        private void ResetMetricValues()
        {
            if (openingValueLabel != null)
            {
                SetLabelTextIfChanged(openingValueLabel, "0.0 mm");
            }

            if (lateralValueLabel != null)
            {
                SetLabelTextIfChanged(lateralValueLabel, "0.0 mm");
            }

            if (protrusionValueLabel != null)
            {
                SetLabelTextIfChanged(protrusionValueLabel, "0.0 mm");
            }

            if (confidenceValueLabel != null)
            {
                SetLabelTextIfChanged(confidenceValueLabel, "-- %");
            }
        }

        private static void SetLabelTextIfChanged(Label label, string text)
        {
            if (label != null && label.text != text)
            {
                label.text = text;
            }
        }

        private void UpdateSimulationButtonText()
        {
            if (simulationButton != null)
            {
                simulationButton.text = simulator != null && simulator.IsRunning
                    ? "Simülasyonu Durdur"
                    : "Simülasyon Modu";
            }
        }

        private void UpdateUdpButtonText()
        {
            if (startUdpButton != null)
            {
                startUdpButton.text = udpMotionSource != null && udpMotionSource.IsReceiving
                    ? "UDP Durdur"
                    : "UDP Başlat";
            }
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyResponsiveClass(evt.newRect.width);
            UpdateViewportInputBounds();
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

            ApplyViewportSizing(width);
            ResizeViewportRenderTarget();
        }

        private void ApplyViewportSizing(float width)
        {
            if (viewportPanel == null || width <= 0f)
            {
                return;
            }

            if (width < NarrowWidth)
            {
                float mobileViewportHeight = Mathf.Clamp(Screen.height * 0.44f, 320f, 560f);
                viewportPanel.style.minHeight = mobileViewportHeight;
            }
            else
            {
                viewportPanel.style.minHeight = StyleKeyword.Null;
            }
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
                DisableAudioListener(viewportCamera);
                if (orbitCameraController == null)
                {
                    orbitCameraController = viewportCamera.GetComponent<JawOrbitCameraController>();
                }

                viewportCamera.clearFlags = CameraClearFlags.SolidColor;
                viewportCamera.backgroundColor = new Color(0.047f, 0.065f, 0.082f, 1f);
                viewportCamera.nearClipPlane = 0.003f;
                viewportCamera.farClipPlane = 10f;
            }

            ResizeViewportRenderTarget();
            UpdateViewportInputBounds();
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
            viewportCamera.aspect = width / (float)height;
            viewportImage.image = viewportRenderTexture;
            EnsureDisplayFallbackCamera();
            currentViewportTextureWidth = width;
            currentViewportTextureHeight = height;
            UpdateViewportInputBounds();
            ReframeImportedModelIfPossible();
        }

        private void UpdateViewportInputBounds()
        {
            if (viewportPanel == null)
            {
                return;
            }

            if (orbitCameraController == null && viewportCamera != null)
            {
                orbitCameraController = viewportCamera.GetComponent<JawOrbitCameraController>();
            }

            if (orbitCameraController == null)
            {
                orbitCameraController = FindFirstObjectByType<JawOrbitCameraController>();
            }

            if (orbitCameraController == null)
            {
                return;
            }

            orbitCameraController.PointerInViewportChecker = screenPos =>
            {
                if (viewportPanel == null || viewportPanel.panel == null)
                {
                    return false;
                }

                Vector2 panelPos = UnityEngine.UIElements.RuntimePanelUtils.ScreenToPanel(viewportPanel.panel, screenPos);
                return viewportPanel.worldBound.Contains(panelPos);
            };
        }

        private void ReframeImportedModelIfPossible()
        {
            if (orbitCameraController == null || modelImportService == null || !modelImportService.CombinedBounds.HasValue)
            {
                return;
            }

            orbitCameraController.FrameBounds(modelImportService.CombinedBounds.Value);
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
                DisableAudioListener(displayFallbackCamera);
            }

            displayFallbackCamera.enabled = true;
        }

        private static void DisableAudioListener(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = false;
            }
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
            connectionPanel.Add(CreateButton("pause-motion-button", "Hareketi Durdur"));
            connectionPanel.Add(CreateMetricLine("motion-pause-label", "Hareket: devam ediyor"));
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
