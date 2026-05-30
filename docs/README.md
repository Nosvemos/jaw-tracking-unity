# Jaw Tracking Unity Client

The **Jaw Tracking Unity Client** is a cutting-edge, real-time 3D visualization platform designed for a marker-based mandibular movement tracking system.

> **Important Notice:** This project has been officially approved by the **Health Institutes of Turkey (TÜSEB)** and is designated for use in advanced health research and clinical studies. Its development adheres strictly to the requirements necessary for professional medical research applications.

## Overview

Developed using Unity, this application acts as the visualization layer for an optical jaw-tracking pipeline. It receives processed mandibular movement data via UDP from a Python/OpenCV computer vision backend and maps this data onto anatomically accurate, user-provided digital models (STL/PLY) of the upper and lower jaws.

The system is engineered to provide researchers and clinicians with real-time, actionable insights into mandibular kinematics, supporting detailed movement analysis, calibration, recording, and replay functionalities.

## Key Features

- **TÜSEB-Approved Research Tool:** Developed under the guidelines and approval of the Health Institutes of Turkey for clinical health research.
- **Real-Time 3D Visualization:** Accurately renders the upper jaw as a fixed anatomical reference while animating the lower jaw in real-time based on live tracking data.
- **Dynamic Model Loading:** Supports runtime importing of user-specific STL and PLY jaw models, accommodating patient-specific anatomical data without requiring Unity Editor modifications.
- **Advanced Kinematic Metrics:** Displays continuous numerical readouts for opening, lateral deviation, protrusion, and retrusion, alongside visual trajectory paths and motion graphs.
- **Precision Calibration:** Features a rest-position calibration system to establish an accurate anatomical baseline (centric relation/maximum intercuspation) prior to movement recording.
- **Session Management:** Built-in capability to record processed movement sessions to CSV format and seamlessly replay them for post-hoc analysis.
- **Simulation Mode:** Includes an independent simulation mode for system testing, demonstration, and development when the OpenCV backend is unavailable.
- **Cross-Platform Compatibility:** Designed for seamless deployment across PC (Windows, macOS, Linux) and Mobile (Android, iOS) platforms, utilizing a responsive UI built with Unity UI Toolkit.
- **Localized Interface:** The user-facing interface is fully localized in Turkish, completely supporting Turkish characters to serve regional clinical staff effectively.

## Recommended Environment & Tech Stack

- **Engine:** Unity 2022.3 LTS (or newer).
- **Target Platforms:** Windows, macOS, Linux, Android, iOS.
- **UI Framework:** Unity UI Toolkit (UXML/USS).
- **Architecture:** Modular C# backend with a strict separation between data ingestion, motion mapping, and UI visualization.
- **Network Protocol:** UDP (Listening on `0.0.0.0:5055`), optimized for high-frequency JSON or CSV packet ingestion on background threads.

## Medical Model Workflow

Unlike standard game development pipelines, this project prioritizes data integrity for health research:
- **Format Support:** Only `.stl` and `.ply` formats are supported as primary clinical inputs.
- **Non-Destructive Pipeline:** Original mesh geometry is preserved. All alignment, scaling, and pivot corrections are handled via parent `GameObject` transforms in Unity.
- **Runtime Import:** The application features a robust, platform-aware file explorer (supporting desktop and mobile native document pickers) allowing researchers to load distinct upper and lower jaw models directly into the running application.

## Data Protocol

The client expects a continuous stream of processed pose or relative pixel/angle data.

**Standard JSON Packet Structure:**
```json
{
  "type": "jaw_frame",
  "timestamp_ms": 1710000000000,
  "tracking_valid": true,
  "pose": {
    "x_mm": 0.0,
    "y_mm": 20.0,
    "z_mm": 0.0,
    "yaw_deg": 0.0,
    "pitch_deg": 2.5,
    "roll_deg": 0.0
  }
}
```
*(Refer to the technical documentation for complete schema specifications.)*

## Disclaimer

While this application is approved by **TÜSEB** for health research purposes, it is intended to function as an investigational and visualization tool. It must be used in conjunction with qualified clinical judgment and should not be used as the sole basis for clinical diagnosis or treatment planning without proper regulatory medical device certification.
