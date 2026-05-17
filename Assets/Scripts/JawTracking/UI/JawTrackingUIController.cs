using System.IO;
using JawTracking.Data;
using JawTracking.FileAccess;
using JawTracking.Motion;
using JawTracking.Simulation;
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
        private static readonly Vector3 DefaultPivotBoundsAnchor = new Vector3(0f, 0.72f, -0.58f);
        private const float DefaultPivotLiftMm = 0f;
        private const float DefaultOpeningAngleScale = 0.82f;

        [SerializeField] private JawModelImportService modelImportService;
        [SerializeField] private JawModelController modelController;
        [SerializeField] private JawDataSimulator simulator;
        [SerializeField] private Camera viewportCamera;

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
            BindModelController();
            BindButtons();
            BindAccordionButtons();
            BindMotionControls();
            BindImportService();
            BindSimulator();
            SetupViewportRenderTarget();
            ScheduleRuntimeScrollbarTheme();

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
            UnbindAccordionButtons();
            UnbindMotionControls();
            ReleaseViewportRenderTarget();

            if (modelImportService != null)
            {
                modelImportService.StatusChanged -= HandleStatusChanged;
                modelImportService.ModelImportCompleted -= HandleModelImportCompleted;
            }

            if (simulator != null)
            {
                simulator.MotionUpdated -= HandleSimulationMotionUpdated;
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
                rightRailScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
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
            startUdpButton = documentRoot.Q<Button>("start-udp-button");
            simulationButton = documentRoot.Q<Button>("simulation-button");
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

            if (startUdpButton != null)
            {
                startUdpButton.clicked += ShowUdpPlaceholder;
            }

            if (simulationButton != null)
            {
                simulationButton.clicked += ToggleSimulation;
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

            if (startUdpButton != null)
            {
                startUdpButton.clicked -= ShowUdpPlaceholder;
            }

            if (simulationButton != null)
            {
                simulationButton.clicked -= ToggleSimulation;
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
            SyncMotionControlValues();
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

        private void ToggleSimulation()
        {
            BindSimulator();
            simulator.Toggle();
            UpdateSimulationButtonText();

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

        private void ShowCalibrationPlaceholder()
        {
            ReturnSimulationToRest();
            simulator?.RefreshRestPose();
            ResetMetricValues();
            SetStatus("Dinlenme pozisyonu kalibre edildi.");
        }

        private void ShowResetCalibrationPlaceholder()
        {
            ReturnSimulationToRest();
            simulator?.RefreshRestPose();
            ResetMetricValues();
            SetStatus("Kalibrasyon sıfırlandı.");
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
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = string.IsNullOrWhiteSpace(message) ? "Hazır" : message;
            }
        }

        private void SetTrackingText(string message)
        {
            if (trackingLabel != null)
            {
                trackingLabel.text = message;
            }
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
            if (openingValueLabel != null)
            {
                openingValueLabel.text = $"{state.OpeningMm:0.0} mm";
            }

            if (lateralValueLabel != null)
            {
                lateralValueLabel.text = $"{state.LateralMm:0.0} mm";
            }

            if (protrusionValueLabel != null)
            {
                protrusionValueLabel.text = $"{state.ProtrusionMm:0.0} mm";
            }

            if (confidenceValueLabel != null)
            {
                confidenceValueLabel.text = $"{state.Confidence * 100f:0} %";
            }
        }

        private void ResetMetricValues()
        {
            if (openingValueLabel != null)
            {
                openingValueLabel.text = "0.0 mm";
            }

            if (lateralValueLabel != null)
            {
                lateralValueLabel.text = "0.0 mm";
            }

            if (protrusionValueLabel != null)
            {
                protrusionValueLabel.text = "0.0 mm";
            }

            if (confidenceValueLabel != null)
            {
                confidenceValueLabel.text = "-- %";
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

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyResponsiveClass(evt.newRect.width);
            HideRuntimeScrollbars();
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
                DisableAudioListener(viewportCamera);
                viewportCamera.clearFlags = CameraClearFlags.SolidColor;
                viewportCamera.backgroundColor = new Color(0.047f, 0.065f, 0.082f, 1f);
                viewportCamera.nearClipPlane = 0.003f;
                viewportCamera.farClipPlane = 10f;
            }

            ResizeViewportRenderTarget();
        }

        private void ScheduleRuntimeScrollbarTheme()
        {
            appRoot?.schedule.Execute(HideRuntimeScrollbars).ExecuteLater(100);
        }

        private void HideRuntimeScrollbars()
        {
            if (appRoot == null)
            {
                return;
            }

            VisualElement scrollRoot = rightRailScroll ?? appRoot;
            VisualElement verticalScroller = scrollRoot.Q<VisualElement>(className: "unity-scroll-view__vertical-scroller");
            if (verticalScroller == null)
            {
                verticalScroller = scrollRoot.Q<VisualElement>(className: "unity-scroller--vertical");
            }

            if (verticalScroller == null)
            {
                return;
            }

            HideScrollerElement(verticalScroller);

            verticalScroller.Query<VisualElement>(className: "unity-scroller__low-button")
                .ForEach(HideScrollerButton);
            verticalScroller.Query<VisualElement>(className: "unity-scroller__high-button")
                .ForEach(HideScrollerButton);
            verticalScroller.Query<VisualElement>(className: "unity-scroller__slider")
                .ForEach(HideScrollerElement);
            verticalScroller.Query<VisualElement>(className: "unity-base-slider__tracker")
                .ForEach(HideScrollerElement);
            verticalScroller.Query<VisualElement>(className: "unity-base-slider__dragger")
                .ForEach(HideScrollerElement);
        }

        private static void HideScrollerButton(VisualElement element)
        {
            element.style.display = DisplayStyle.None;
            element.style.width = 0;
            element.style.height = 0;
        }

        private static void HideScrollerElement(VisualElement element)
        {
            element.style.display = DisplayStyle.None;
            element.style.width = 0;
            element.style.minWidth = 0;
            element.style.maxWidth = 0;
            element.style.marginLeft = 0;
            element.style.marginRight = 0;
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
