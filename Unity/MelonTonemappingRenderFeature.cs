#region Header

// **    Copyright (C) 2023 Nicolas Reinhard, @LTMX. All rights reserved.
// **    Github Profile: https://github.com/LTMX
// **    Repository : https://github.com/LTMX/Unity.Athena

#endregion

#if UNITY_URP

namespace Melon_Tonemapper.Unity
{
	using UnityEngine;
	using UnityEngine.Rendering;
	using UnityEngine.Rendering.Universal;

	public class MelonTonemappingRenderFeature : ScriptableRendererFeature
	{
		MelonTonemappingPass m_Pass;
		Material m_Material;

		public override void Create()
		{
			// Create the pass and set it to run before post-processing
			// This ensures it runs before Unity's built-in tonemapping
			m_Pass = new MelonTonemappingPass
			{
				renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
			};
		}

		public override void AddRenderPasses(
			ScriptableRenderer renderer,
			ref RenderingData renderingData
		)
		{
			// Skip if the camera is not a game or scene view camera
			if (
				renderingData.cameraData.cameraType != CameraType.Game
				&& renderingData.cameraData.cameraType != CameraType.SceneView
			)
			{
				return;
			}

			// Get the volume component
			var stack = VolumeManager.instance.stack;
			var melonTone = stack.GetComponent<MelonTonemapping>();

			// Only add the pass if the volume component is active
			if (melonTone == null || !melonTone.IsActive())
			{
				return;
			}

			// Create material if needed
			if (m_Material == null)
			{
				var shader = Shader.Find("FullScreen/MelonTonemappingURP");
				if (shader == null)
				{
					Debug.LogError(
						"[MelonTonemappingRenderFeature] Could not find shader 'FullScreen/MelonTonemappingURP'. "
							+ "Make sure the shader is in a Resources folder."
					);
					return;
				}
				m_Material = CoreUtils.CreateEngineMaterial(shader);
			}

			// Setup and enqueue the pass
			m_Pass.Setup(m_Material, melonTone);
			renderer.EnqueuePass(m_Pass);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				CoreUtils.Destroy(m_Material);
				m_Pass?.Dispose();
			}
		}
	}
}

#endif
