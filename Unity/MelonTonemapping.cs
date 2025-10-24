#region Header

// **    Copyright (C) 2023 Nicolas Reinhard, @LTMX. All rights reserved.
// **    Github Profile: https://github.com/LTMX
// **    Repository : https://github.com/LTMX/Unity.Athena

#endregion

#if UNITY_HDRP || UNITY_URP

namespace Melon_Tonemapper.Unity
{
	using System;
	using UnityEngine;
	using UnityEngine.Experimental.Rendering;
	using UnityEngine.Rendering;
	using UnityEngine.Serialization;
#if UNITY_HDRP
	using UnityEngine.Rendering.HighDefinition;
#endif
#if UNITY_URP
	using UnityEngine.Rendering.Universal;
#endif

	[Serializable]
#if UNITY_HDRP
	[SupportedOnRenderPipeline(typeof(HDRenderPipelineAsset))]
	public sealed class MelonTonemapping : CustomPostProcessVolumeComponent, IPostProcessComponent
#elif UNITY_URP
	[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
	[VolumeComponentMenu("Post-processing/Melon Tonemapping")]
	public sealed class MelonTonemapping : VolumeComponent, IPostProcessComponent
#endif
	{
		static readonly int k_MainTex = Shader.PropertyToID("_MainTex");
		static readonly int k_HDRIndex = Shader.PropertyToID("_HDRIndex");
		public BoolParameter isActive = new(true, true);

		// Exposure and clamping are driven by render pipeline; Melon reads globals in HLSL

		[Tooltip("Override HDR output parameters below when enabled.")]
		public BoolParameter OverrideHDRSettings = new(false, false);

		[Tooltip("Display paper white (nits). Used when Override HDR Settings is enabled.")]
		public ClampedFloatParameter PaperWhite = new(300.0f, 1.0f, 10000.0f, false);

		[Tooltip("Minimum display luminance (nits). Used when Override HDR Settings is enabled.")]
		public ClampedFloatParameter MinNits = new(0.005f, 0.0f, 50.0f, false);

		[Tooltip("Maximum display luminance (nits). Used when Override HDR Settings is enabled.")]
		public ClampedFloatParameter MaxNits = new(1000.0f, 10.0f, 10000.0f, false);

		[Tooltip("Default to 0.15")]
		public ClampedFloatParameter WhiteIntensity = new(0.15f, 0, 1, true);

		[Tooltip("Default to 0.64")]
		public ClampedFloatParameter Contrast = new(0.64f, 0, 1, true);

#if UNITY_HDRP
		[FormerlySerializedAs("m_Material")]
		[SerializeField]
		[HideInInspector]
		Material material;

		public override CustomPostProcessInjectionPoint injectionPoint =>
			CustomPostProcessInjectionPoint.AfterPostProcess;

		public override bool visibleInSceneView => true;
#endif

		public bool IsActive()
		{
#if UNITY_HDRP
			return material != null && isActive.value;
#elif UNITY_URP
			return isActive.value;
#else
			return false;
#endif
		}

#if UNITY_HDRP
		public override void Setup()
		{
			if (!material)
				material = CoreUtils.CreateEngineMaterial("FullScreen/MelonTonemapping");
		}

		public override void Render(
			CommandBuffer cmd,
			HDCamera camera,
			RTHandle source,
			RTHandle destination
		)
		{
			Debug.Assert(material != null);

			material.SetFloat(ShaderIDs.Contrast, Contrast.value);
			material.SetFloat(ShaderIDs.WhiteIntensity, WhiteIntensity.value);

			// Drive Post-Exposure from the active volume so bloom stays consistent with HDRP's pipeline
			// (HDRP multiplies PostExposure just before grading when built-in tonemapping is used).
			var stack = VolumeManager.instance.stack;
			var colorAdj = stack.GetComponent<ColorAdjustments>();
			// No direct exposure push; HDRP handles pre-exposure and HDR output

			// If the destination is HDR (e.g. R11G11B10 or FP16), keep output unclamped so HDR output conversion can occur later.
			bool isHdrTarget = false;
			if (destination != null && destination.rt != null)
			{
				var fmt = destination.rt.descriptor.graphicsFormat;
				isHdrTarget =
					fmt == GraphicsFormat.B10G11R11_UFloatPack32
					|| fmt == GraphicsFormat.R16G16B16A16_SFloat
					|| fmt == GraphicsFormat.R32G32B32A32_SFloat;
			}
			// No explicit clamp flag; handled in HLSL based on HDR output path

			//_MainTex is a hacky way to explicitly include support for dynamic resolution, as unity's documentation suggests
			//can't care enough to look at their source codes to for a non-hacky way - it works

			material.SetTexture(k_MainTex, source);

			// Configure HDR output keywords and parameters so our shader can use HDROutput.hlsl
			if (HDROutputSettings.main != null && HDROutputSettings.main.active)
			{
				var gamut = HDROutputSettings.main.displayColorGamut;
				HDROutputUtils.ConfigureHDROutput(
					material,
					gamut,
					HDROutputUtils.Operation.ColorConversion
				);
				// Provide _HDROutputParams vectors like HDRP FinalPass
				var tonemapping = stack.GetComponent<Tonemapping>() ?? new Tonemapping();
				var info = new HDROutputUtils.HDRDisplayInformation(
					HDROutputSettings.main.maxFullFrameToneMapLuminance,
					HDROutputSettings.main.maxToneMapLuminance,
					HDROutputSettings.main.minToneMapLuminance,
					HDROutputSettings.main.paperWhiteNits
				);
				Vector4 p1,
					p2;
				GetHDROutParams(
					info,
					gamut,
					tonemapping,
					OverrideHDRSettings.value,
					MinNits.value,
					MaxNits.value,
					PaperWhite.value,
					out p1,
					out p2
				);
				material.SetVector(ShaderIDs.HDROutputParams, p1);
				material.SetVector(ShaderIDs.HDROutputParams2, p2);
			}
			else
			{
				HDROutputUtils.ConfigureHDROutput(material, HDROutputUtils.Operation.None);
			}

			HDUtils.DrawFullScreen(cmd, material, destination);
		}

		public override void Cleanup()
		{
			CoreUtils.Destroy(material);
		}
#endif

		internal class ShaderIDs
		{
			// public static readonly int k_InputTexture = Shader.PropertyToID("_InputTexture");
			public static readonly int Contrast = Shader.PropertyToID("_Contrast");
			public static readonly int WhiteIntensity = Shader.PropertyToID("_WhiteIntensity");
			public static readonly int HDROutputParams = Shader.PropertyToID("_HDROutputParams");
			public static readonly int HDROutputParams2 = Shader.PropertyToID("_HDROutputParams2");
		}

		internal static void GetHDROutParams(
			float minNitsIn,
			float maxNitsIn,
			float paperWhiteIn,
			ColorGamut gamut,
			bool useOverrides,
			float overrideMinNits,
			float overrideMaxNits,
			float overridePaperWhite,
			out Vector4 p1,
			out Vector4 p2
		)
		{
			var minNits = minNitsIn;
			var maxNits = maxNitsIn;
			var paperWhite = paperWhiteIn;
			var eetfMode = 0;
			var hueShift = 0.0f;

			if (useOverrides)
			{
				minNits = overrideMinNits;
				maxNits = overrideMaxNits;
				paperWhite = overridePaperWhite;
			}
			else
			{
				var failedLimits = minNits < 0 || maxNits <= 0;
				if (failedLimits)
				{
					minNits = 0;
					maxNits = 1000;
				}
				var failedPW = paperWhite <= 0;
				if (failedPW)
				{
					paperWhite = 300;
				}
			}

			p1 = new Vector4(minNits, maxNits, paperWhite, 1f / Mathf.Max(0.001f, paperWhite));
			p2 = new Vector4(
				eetfMode,
				hueShift,
				paperWhite,
				(int)ColorGamutUtility.GetColorPrimaries(gamut)
			);
		}

#if UNITY_HDRP
		static void GetHDROutParams(
			HDROutputUtils.HDRDisplayInformation hdrInfo,
			ColorGamut gamut,
			Tonemapping tonemapping,
			bool useOverrides,
			float overrideMinNits,
			float overrideMaxNits,
			float overridePaperWhite,
			out Vector4 p1,
			out Vector4 p2
		)
		{
			float minNits = hdrInfo.minToneMapLuminance;
			float maxNits = hdrInfo.maxToneMapLuminance;
			float paperWhite = hdrInfo.paperWhiteNits;

			if (!useOverrides)
			{
				bool failedLimits = minNits < 0 || maxNits <= 0;
				if (failedLimits && tonemapping.detectBrightnessLimits.value)
				{
					minNits = 0;
					maxNits = 1000;
				}
				bool failedPW = paperWhite <= 0;
				if (failedPW && tonemapping.detectPaperWhite.value)
				{
					paperWhite = 300;
				}

				if (!tonemapping.detectPaperWhite.value)
					paperWhite = tonemapping.paperWhite.value;
				if (!tonemapping.detectBrightnessLimits.value)
				{
					minNits = tonemapping.minNits.value;
					maxNits = tonemapping.maxNits.value;
				}
			}

			GetHDROutParams(
				minNits,
				maxNits,
				paperWhite,
				gamut,
				useOverrides,
				overrideMinNits,
				overrideMaxNits,
				overridePaperWhite,
				out p1,
				out p2
			);
		}
#endif
	}
}

#endif
