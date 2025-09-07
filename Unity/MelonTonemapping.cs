#region Header

// **    Copyright (C) 2023 Nicolas Reinhard, @LTMX. All rights reserved.
// **    Github Profile: https://github.com/LTMX
// **    Repository : https://github.com/LTMX/Unity.Athena

#endregion

#if UNITY_HDRP

namespace Melon_Tonemapper.Unity
{
	using System;
	using UnityEngine;
	using UnityEngine.Rendering;
	using UnityEngine.Rendering.HighDefinition;

	[Serializable]
	[SupportedOnRenderPipeline(typeof(HDRenderPipelineAsset))]
	sealed class MelonTonemapping : CustomPostProcessVolumeComponent, IPostProcessComponent
	{
		public BoolParameter isActive = new(true, true);

		[Tooltip("Default to 0.15")]
		public ClampedFloatParameter WhiteIntensity = new(0.15f, 0, 1, true);

		[Tooltip("Default to 0.64")]
		public ClampedFloatParameter Contrast = new(0.64f, 0, 1, true);

		Material m_Material;

		public override CustomPostProcessInjectionPoint injectionPoint =>
			CustomPostProcessInjectionPoint.AfterPostProcess;

		public bool IsActive()
		{
			return m_Material != null && isActive.value;
		}

		public override void Setup()
		{
			m_Material = CoreUtils.CreateEngineMaterial("FullScreen/MelonTonemapping");
		}

		public override void Render(
			CommandBuffer cmd,
			HDCamera camera,
			RTHandle source,
			RTHandle destination
		)
		{
			Debug.Assert(m_Material != null);

			m_Material.SetFloat(ShaderIDs.Contrast, Contrast.value);
			m_Material.SetFloat(ShaderIDs.WhiteIntensity, WhiteIntensity.value);
			m_Material.SetTexture("_MainTex", source);
			//_MainTex is a hacky way to explicitly include support for dynamic resolution, as unity's documentation suggests
			//can't care enough to look at their source codes to for a non-hacky way - it works

			HDUtils.DrawFullScreen(cmd, m_Material, destination);
		}

		public override void Cleanup()
		{
			CoreUtils.Destroy(m_Material);
		}

		internal class ShaderIDs
		{
			// public static readonly int k_InputTexture = Shader.PropertyToID("_InputTexture");
			public static readonly int Contrast = Shader.PropertyToID("_Contrast");
			public static readonly int WhiteIntensity = Shader.PropertyToID("_WhiteIntensity");
		}
	}
}

#endif
