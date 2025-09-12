#pragma once

// #include
// "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScaling.hlsl"

void ApplyDynamicScaling_float(float2 uv, out float2 scaled) {
  scaled = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv);
}
