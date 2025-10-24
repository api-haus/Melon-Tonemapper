# Melon Tonemapping for Universal Render Pipeline (URP)

This package provides a custom tonemapping solution for Unity's Universal Render Pipeline with Render Graph support (Unity 6+).

## Features

- Custom Reinhart-based tonemapping
- Full HDR output support with proper color space conversion
- Preserves HDR output when HDR display is enabled
- Consistent LDR output when HDR is disabled
- Volume component for easy parameter adjustment
- Render Graph API implementation for optimal performance

## Setup Instructions

### 1. Add the Render Feature

1. Open your **Universal Renderer Data** asset (typically in `Assets/Settings/`)
2. Click **Add Renderer Feature**
3. Select **Melon Tonemapping Render Feature**

### 2. Disable Built-in Tonemapping

**Important:** To avoid double tonemapping, you must disable Unity's built-in tonemapping:

1. Open your **Post-Processing Volume Profile**
2. Find the **Tonemapping** component
3. Set **Mode** to **None**

### 3. Add Melon Tonemapping Volume

1. Add a **Volume Component** to your scene (or use existing Volume)
2. In the Volume's profile, click **Add Override**
3. Select **Post-processing > Melon Tonemapping**
4. Enable the volume component and adjust parameters as needed

## Volume Parameters

- **Is Active**: Enable/disable the tonemapping effect
- **Contrast** (default: 0.64): Controls the mid-tone contrast curve
- **White Intensity** (default: 0.15): Controls the hue shift and approach to white for bright regions
- **Override HDR Settings**: Enable to manually control HDR output parameters
- **Paper White** (default: 300 nits): Display paper white luminance when overriding
- **Min Nits** (default: 0.005 nits): Minimum display luminance when overriding
- **Max Nits** (default: 1000 nits): Maximum display luminance when overriding

## HDR Output Behavior

### When HDR Display is Active:
- The tonemapper works in relative luminance space (paper white = 1.0)
- Highlights smoothly extend beyond paper white up to display peak
- Color conversion to display gamut is handled automatically
- Output preserves HDR range (B10G11R11 or R16G16B16A16 format)

### When HDR Display is Inactive (LDR):
- Classic Melon curve in [0..1] range
- Output is clamped to LDR range
- No HDR color space conversion

## Technical Details

### Render Pass Timing
The render feature runs at `RenderPassEvent.BeforeRenderingPostProcessing`, which ensures it executes before Unity's built-in tonemapping would run.

### Shader
The implementation uses the **FullScreen/MelonTonemappingURP** shader graph, which:
- Uses the `MelonTone.hlsl` include for tonemapping logic
- Has two passes: Pass 0 (procedural) and Pass 1 (blitter)
- Handles HDR_COLORSPACE_CONVERSION keyword for HDR output

### Supported Graphics Formats
HDR formats detected automatically:
- B10G11R11_UFloatPack32
- R16G16B16A16_SFloat
- R32G32B32A32_SFloat
- R16G16B16A16_UNorm

## Troubleshooting

### Shader Not Found Error
If you see "Could not find shader 'FullScreen/MelonTonemappingURP'":
1. Wait for Unity to compile the shader graph
2. Reimport the `MelonTonemappingURP.shadergraph` file
3. Check that the shader graph has no compilation errors

### No Effect Visible
1. Ensure the Volume is active and the camera is within its bounds
2. Check that **Is Active** is enabled in the Melon Tonemapping volume component
3. Verify that Unity's built-in tonemapping is set to **None**
4. Check that the Render Feature is added to your URP Renderer

### Compilation Errors
The package requires:
- Unity 6.0 or later
- Universal Render Pipeline package (version 17.0.0+)
- Shader Graph package

## Comparison with HDRP Version

The URP implementation maintains feature parity with the HDRP version:
- Same volume parameters and defaults
- Same tonemapping algorithm
- Same HDR output handling
- Uses shared helper methods for HDR parameter calculation

## Performance

The render feature uses Unity's Render Graph API for optimal performance:
- Efficient GPU resource management
- Automatic synchronization
- Minimal CPU overhead
- Full-screen pass with optimized blitting

## Credits

Copyright (C) 2023 Nicolas Reinhard, @LTMX
- Github: https://github.com/LTMX
- Repository: https://github.com/LTMX/Unity.Athena
