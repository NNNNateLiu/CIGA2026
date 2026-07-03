using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UniversalWaterSystem
{
    public class UnderwaterOpaquePass : ScriptableRenderPass
    {
        private Material underwaterOpaqueMaterial;

        private RTHandle underwaterOpaqueRT;

        public UnderwaterOpaquePass()
        {
        }

        private Material GetMaterial()
        {
            if (underwaterOpaqueMaterial == null)
            {
                if (Water.Instance != null &&
                    Water.Instance.waterResources != null &&
                    Water.Instance.waterResources.submergeShader != null)
                {
                    underwaterOpaqueMaterial = new Material(Water.Instance.waterResources.underwaterOpaqueShader);
                }
                else
                {
                    Debug.LogError("UnderwaterOpaqueShader is not exist!");
                }
            }

            return underwaterOpaqueMaterial;
        }

        private bool CheckEnable()
        {
            if (Water.Instance == null) return false;
            if (!Water.Instance.underwaterEnable) return false;

            GetMaterial();
            if (underwaterOpaqueMaterial == null) return false;

            return true;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (NeedsReAlloc(underwaterOpaqueRT, cameraTextureDescriptor))
            {
                if (underwaterOpaqueRT != null) RTHandles.Release(underwaterOpaqueRT);
                underwaterOpaqueRT = RTHandles.Alloc(cameraTextureDescriptor.width, cameraTextureDescriptor.height, cameraTextureDescriptor.volumeDepth, DepthBits.None, cameraTextureDescriptor.graphicsFormat, FilterMode.Bilinear, TextureWrapMode.Clamp, TextureDimension.Tex2D, name: "UnderwaterOpaque");
            }

            cmd.SetGlobalTexture("Water_UnderWaterOpaqueTexture", underwaterOpaqueRT);
            ConfigureTarget(underwaterOpaqueRT);
        }

        private void SetupCameraGlobals(CommandBuffer cmd, Camera cam)
        {
            cmd.SetGlobalMatrix(GlobalShaderVariables.Misc.InverseViewMatrix, cam.cameraToWorldMatrix);
            cmd.SetGlobalMatrix(GlobalShaderVariables.Misc.InverseProjectionMatrix,
                GL.GetGPUProjectionMatrix(cam.projectionMatrix, false).inverse);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!CheckEnable()) return;

            CameraData cameraData = renderingData.cameraData;
            Camera camera = cameraData.camera;

            CommandBuffer cmd = CommandBufferPool.Get("Underwater Opaque");
            SetupCameraGlobals(cmd, camera);

            cmd.Blit(cameraData.renderer.cameraColorTarget, underwaterOpaqueRT, 0, 0);

            DrawPreceduralFullscreenQuad(cmd, cameraData.renderer.cameraColorTarget,
                GetMaterial(), 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void ClearRT()
        {
            RTHandles.Release(underwaterOpaqueRT);
        }

        void DrawPreceduralFullscreenQuad(CommandBuffer cmd, RenderTargetIdentifier target,
            Material material, int pass)
        {
            cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, -1);
            cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Quads, 4, 1, null);
        }

        bool NeedsReAlloc(RTHandle handle, in RenderTextureDescriptor descriptor)
        {
            if (handle == null || handle.rt == null)
            {
                return true;
            }

            if ((handle.rt.width != descriptor.width || handle.rt.height != descriptor.height))
            {
                return true;
            }

            if (handle.rt.descriptor.dimension != descriptor.dimension)
            {
                return true;
            }

            return false;
        }
    }
}
