#region Header

// **    Copyright (C) 2023 Nicolas Reinhard, @LTMX. All rights reserved.
// **    Github Profile: https://github.com/LTMX
// **    Repository : https://github.com/LTMX/Unity.Athena

#endregion

#if UNITY_URP

namespace Melon_Tonemapper.Unity
{
	using UnityEngine;
	using UnityEngine.Experimental.Rendering;
	using UnityEngine.Rendering;
	using UnityEngine.Rendering.RenderGraphModule;
	using UnityEngine.Rendering.Universal;

	public class MelonTonemappingPass : ScriptableRenderPass
	{
		const string PASS_NAME = "Melon Tonemapping";
		static readonly int s_MainTexID = Shader.PropertyToID("_MainTex");

		Material m_Material;
		MelonTonemapping m_VolumeComponent;

		class PassData
		{
			public Material material;
			public TextureHandle sourceTexture;
			public TextureHandle destinationTexture;
			public float contrast;
			public float whiteIntensity;
			public Vector4 hdrOutputParams;
			public Vector4 hdrOutputParams2;
			public bool isHDROutput;
		}

		public MelonTonemappingPass()
		{
			profilingSampler = new ProfilingSampler(PASS_NAME);
		}

		public void Setup(Material material, MelonTonemapping volumeComponent)
		{
			m_Material = material;
			m_VolumeComponent = volumeComponent;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			if (m_Material == null || m_VolumeComponent == null)
			{
				return;
			}

			var resourceData = frameData.Get<UniversalResourceData>();
			var cameraData = frameData.Get<UniversalCameraData>();

			// Check if we have a valid camera color texture
			if (!resourceData.isActiveTargetBackBuffer)
			{
				// Import the camera color texture as both source and destination
				var source = resourceData.activeColorTexture;
				if (!source.IsValid())
				{
					return;
				}

				// Detect if output is HDR
				var colorDesc = renderGraph.GetTextureDesc(source);
				var isHDROutput = IsHDRFormat(colorDesc.format);

				// Create a temporary texture for rendering
				var tempTexture = renderGraph.CreateTexture(
					new TextureDesc(colorDesc.width, colorDesc.height)
					{
						colorFormat = colorDesc.format,
						depthBufferBits = DepthBits.None,
						msaaSamples = colorDesc.msaaSamples,
						enableRandomWrite = false,
						clearBuffer = false,
						name = "_MelonTonemapTemp",
					}
				);

				using (
					var builder = renderGraph.AddRasterRenderPass<PassData>(
						PASS_NAME,
						out var passData,
						profilingSampler
					)
				)
				{
					// Setup pass data
					passData.material = m_Material;
					passData.sourceTexture = source;
					passData.destinationTexture = tempTexture;
					passData.contrast = m_VolumeComponent.Contrast.value;
					passData.whiteIntensity = m_VolumeComponent.WhiteIntensity.value;
					passData.isHDROutput = isHDROutput;

					// Configure HDR output parameters
					ConfigureHDROutputParams(
						cameraData,
						isHDROutput,
						out passData.hdrOutputParams,
						out passData.hdrOutputParams2
					);

					// Declare texture usage - read from source, write to temp
					builder.UseTexture(passData.sourceTexture, AccessFlags.Read);
					builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);

					// Set render function
					builder.SetRenderFunc<PassData>(
						(PassData data, RasterGraphContext context) => ExecutePass(data, context)
					);
				}

				// Copy the temp texture back to the source
				using (
					var builder = renderGraph.AddRasterRenderPass<PassData>(
						"Copy Melon Tonemap Result",
						out var passData,
						profilingSampler
					)
				)
				{
					passData.material = null;
					passData.sourceTexture = tempTexture;
					passData.destinationTexture = source;

					builder.UseTexture(tempTexture, AccessFlags.Read);
					builder.SetRenderAttachment(source, 0, AccessFlags.Write);

					builder.SetRenderFunc<PassData>(
						(PassData data, RasterGraphContext context) =>
						{
							Blitter.BlitTexture(
								context.cmd,
								data.sourceTexture,
								new Vector4(1, 1, 0, 0),
								0,
								false
							);
						}
					);
				}
			}
		}

		static void ExecutePass(PassData data, RasterGraphContext context)
		{
			if (data.material == null)
			{
				return;
			}

			// Set material properties
			data.material.SetFloat(MelonTonemapping.ShaderIDs.Contrast, data.contrast);
			data.material.SetFloat(MelonTonemapping.ShaderIDs.WhiteIntensity, data.whiteIntensity);
			data.material.SetVector(MelonTonemapping.ShaderIDs.HDROutputParams, data.hdrOutputParams);
			data.material.SetVector(MelonTonemapping.ShaderIDs.HDROutputParams2, data.hdrOutputParams2);

			// Configure HDR color space conversion keyword
			if (data.isHDROutput)
			{
				data.material.EnableKeyword("HDR_COLORSPACE_CONVERSION");
			}
			else
			{
				data.material.DisableKeyword("HDR_COLORSPACE_CONVERSION");
			}

			// Draw full screen pass
			// Pass 0 is procedural, Pass 1 is blitter (we'll use procedural for compatibility)
			Blitter.BlitTexture(
				context.cmd,
				data.sourceTexture,
				new Vector4(1, 1, 0, 0),
				data.material,
				0
			);
		}

		void ConfigureHDROutputParams(
			UniversalCameraData cameraData,
			bool isHDROutput,
			out Vector4 params1,
			out Vector4 params2
		)
		{
			if (!isHDROutput)
			{
				// LDR output - use default values
				params1 = new Vector4(0.005f, 1000f, 300f, 1f / 300f);
				params2 = new Vector4(0, 0, 300f, 0);
				return;
			}

		// HDR output - configure based on display settings
		// In URP, we need to query HDR output settings from the camera or global settings
		var minNits = 0.005f;
		var maxNits = 1000f;
		var paperWhite = 300f;
		var gamut = ColorGamut.sRGB;

			// Try to get HDR output settings if available
#if UNITY_2023_1_OR_NEWER
			if (HDROutputSettings.main != null && HDROutputSettings.main.active)
			{
				minNits = HDROutputSettings.main.minToneMapLuminance;
				maxNits = HDROutputSettings.main.maxToneMapLuminance;
				paperWhite = HDROutputSettings.main.paperWhiteNits;
				gamut = HDROutputSettings.main.displayColorGamut;
			}
#endif

			// Apply overrides if enabled
			MelonTonemapping.GetHDROutParams(
				minNits,
				maxNits,
				paperWhite,
				gamut,
				m_VolumeComponent.OverrideHDRSettings.value,
				m_VolumeComponent.MinNits.value,
				m_VolumeComponent.MaxNits.value,
				m_VolumeComponent.PaperWhite.value,
				out params1,
				out params2
			);
		}

		static bool IsHDRFormat(GraphicsFormat format)
		{
			return format == GraphicsFormat.B10G11R11_UFloatPack32
				|| format == GraphicsFormat.R16G16B16A16_SFloat
				|| format == GraphicsFormat.R32G32B32A32_SFloat
				|| format == GraphicsFormat.R16G16B16A16_UNorm;
		}

		public void Dispose()
		{
			// Cleanup if needed
		}
	}
}

#endif
