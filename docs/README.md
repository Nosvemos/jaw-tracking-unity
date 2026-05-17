# Jaw Tracking Unity Client

Jaw Tracking Unity Client is a Unity-based real-time visualization client for a marker-based mandibular movement tracking system.

The application receives processed jaw movement data from a Python/OpenCV backend over UDP, maps that data onto a fixed upper jaw and a moving lower jaw model, and displays movement, measurements, trajectory, graphs, calibration state, recording controls, and replay tools.

## Purpose

This project is a research and visualization prototype for low-cost, optical, marker-based mandibular movement tracking.

Unity is responsible for:

- Visualizing upper and lower jaw models.
- Keeping the upper jaw fixed as the reference model.
- Moving the lower jaw in real time from incoming tracking data.
- Showing numerical movement metrics.
- Displaying movement graphs and trajectory.
- Supporting rest-position calibration.
- Running in simulation mode when the backend is unavailable.
- Recording and replaying jaw movement sessions.

The OpenCV marker detection pipeline is handled outside Unity. Unity receives already-processed movement data.

## Recommended Environment

- Unity 2022.3 LTS or newer.
- Primary target: PC and mobile from the beginning.
- Supported target intent: Windows, macOS, Linux, Android, and iOS.
- UI, file import, networking, and rendering decisions must be made with cross-platform compatibility in mind.
- UI Toolkit is the default UI stack for screens, panels, controls, and styling.
- Runtime UI text must be Turkish and must support Turkish characters such as `ç`, `ğ`, `ı`, `İ`, `ö`, `ş`, and `ü`.

## Model Setup

STL is the required source model format for this project.

The jaw models are not static project assets. The user must be able to load/select the upper and lower jaw `.stl` files at runtime through the app's file import flow. Bundled demo files may exist for testing, but they are optional samples, not the primary workflow.

Do not replace the source workflow with FBX, OBJ, or GLB. If a cached runtime mesh or generated Unity asset is needed for performance, it should be treated as a derived cache while the original user-provided STL remains the source of truth.

Keep the original model geometry intact. Alignment, scaling, and pivot correction should be handled through parent GameObjects in Unity.

## File Import

The app should include a file explorer/import flow so STL jaw models can be selected by the user.

The file import layer must be platform-aware:

- Desktop: Windows, macOS, and Linux.
- Mobile: Android and iOS document picker support.
- Editor: a simple development path for testing local STL files.

The project may use a ready-made Unity package for file picking and STL parsing, or a custom implementation if package constraints make that safer.

## UI Approach

The app UI should be created with Unity UI Toolkit using UXML, USS, and C# controllers. Iteration can be done through vibe-coded UI changes directly in the project while keeping the layout responsive for both PC and mobile.

Project documentation can stay in English for AI agent clarity, but all user-facing Unity UI labels, buttons, statuses, warnings, and empty states should be Turkish.

Use a font/font asset that supports Turkish glyphs on every target platform. Do not rely blindly on platform fallback fonts.

Special rendering surfaces such as 3D jaw visualization and high-frequency graphs may use dedicated Unity rendering components when UI Toolkit is not the right tool.

## UDP Integration

Default UDP settings:

- Listen address: `0.0.0.0`
- Listen port: `5055`
- Encoding: UTF-8
- Preferred format: JSON

Minimum JSON packet:

```json
{
  "type": "jaw_frame",
  "timestamp_ms": 1710000000000,
  "tracking_valid": true,
  "relative": {
    "dx_px": 0.0,
    "dy_px": 50.0,
    "dtheta_deg": 0.0
  }
}
```

If millimeter pose data is available, Unity should prefer it:

```json
{
  "pose": {
    "x_mm": 0.0,
    "y_mm": 20.0,
    "z_mm": 0.0
  }
}
```

## Calibration

Use rest-position calibration before measuring movement. During calibration, the jaw should be relaxed/closed while Unity collects and averages valid frames. After calibration, opening, lateral deviation, and protrusion should read close to zero at rest.

## Simulation Mode

Simulation mode allows the Unity client to be developed and demonstrated without the OpenCV backend. It should support open-close movement, lateral movement, protrusion/retrusion, combined movement, jitter testing, and tracking-loss testing.

## Recording and Replay

The app should support CSV recording for processed movement sessions and replay those sessions through the same visualization pipeline used for live data.

Suggested export folder:

```text
Application.persistentDataPath/JawTrackingRecords/
```

## Prototype Disclaimer

This application is a research and visualization prototype for mandibular movement tracking. It is not a certified medical device and must not be used as the sole basis for diagnosis or treatment decisions.
