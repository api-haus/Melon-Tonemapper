#pragma once
#pragma multi_compile_local_fragment _ HDR_COLORSPACE_CONVERSION HDR_INPUT HDR_ENCODING

#ifdef HDR_ENCODING
#define HDR_INPUT 1 // this should be defined when HDR_ENCODING is defined
#endif

float4 _HDROutputParams;
float4 _HDROutputParams2;

#define _MinNits            _HDROutputParams.x
#define _MaxNits            _HDROutputParams.y
#define _PaperWhite         _HDROutputParams.z
#define _OneOverPaperWhite  _HDROutputParams.w
#define _RangeReductionMode (int)_HDROutputParams2.x

// #ifndef _BlitScaleBias
// #include
// "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl" #endif
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/HDROutput.hlsl"
#ifdef _BlitScaleBias
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScaling.hlsl"
#endif

void ApplyDynamicScaling_float(float2 uv, out float2 scaled) {
#ifndef _BlitScaleBias
  scaled = uv;
#else
  scaled = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv);
#endif
}

float Cmax_float3(float3 value) {
  return max(max(value.r, value.g), value.b);
}

float3 HueShift_float3(float3 value) {
  float a = max(value.x, value.y);
  return float3(a, max(a, value.z), value.z);
}

void ApplyMelonToneMap_float(in float3 inColor, out float3 outColor) {
  // Map contrast to a power curve near filmic 1.56
  float exponent = 1.0 + saturate(_Contrast) * (1.56 - 1.0);

#if defined(HDR_COLORSPACE_CONVERSION)
  // HDR path: work relative to paper white, then expand highlights toward display peak
  float3 colorRel = max(inColor, (0.0).xxx) * _OneOverPaperWhite; // 1.0 = paper white

  // Mid-tone contrast shaping
  float3 mids = pow(colorRel, exponent);

  // Gentle shoulder in the [0..1] region (keeps mids pleasing)
  float3 low = mids / (mids + 0.84);

  // Hue shift and approach to white for bright regions (computed in relative domain)
  float factor = Cmax_float3(low) * saturate(_WhiteIntensity);
  factor = factor / (factor + 1.0);
  factor *= factor;
  float3 looked = lerp(low, HueShift_float3(low), factor);
  looked = lerp(looked, (1.0).xxx, factor);

  // Extend highlights smoothly beyond paper white up to peak
  float peak = max(_MaxNits * _OneOverPaperWhite, 1.0001);
  float kneeCoeff = lerp(0.5, 2.0, saturate(_WhiteIntensity));
  float3 delta = max(colorRel - 1.0, (0.0).xxx) * kneeCoeff;
  float3 highlight = 1.0.xxx + (delta * (peak - 1.0)) / (delta + (peak - 1.0));

  // Piecewise combine: below PW use looked; above PW add highlight extension
  float3 toneRel = (colorRel <= 1.0).xxx ? looked : (1.0.xxx + (highlight - 1.0));

  // Convert back to nits and clamp to device peak for safety
  outColor = min(toneRel * _PaperWhite, _MaxNits);
#else
  // LDR path: classic Melon curve in [0..1]
  float3 color = pow(max(inColor, (0.0).xxx), exponent);
  color = color / (color + 0.84);
  float factor = Cmax_float3(color) * saturate(_WhiteIntensity);
  factor = factor / (factor + 1.0);
  factor *= factor;
  color = lerp(color, HueShift_float3(color), factor);
  color = lerp(color, (1.0).xxx, factor);
  outColor = saturate(color);
#endif
}
