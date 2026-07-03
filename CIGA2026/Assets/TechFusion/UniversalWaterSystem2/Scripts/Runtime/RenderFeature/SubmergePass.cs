using System.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UniversalWaterSystem
{
    public class SubmergePass : ScriptableRenderPass
    {
        private Material submergeMaterial;
        private RTHandle submergenceRT;

        public SubmergePass()
        {
        }

        private Material GetMaterial()
        {
            if (submergeMaterial == null)
            {
                if (Water.Instance != null &&
                    Water.Instance.waterResources != null &&
                    Water.Instance.waterResources.submergeShader != null)
                {
                    submergeMaterial = new Material(Water.Instance.waterResources.submergeShader);
                }
                else
                {
                    Debug.LogError("SubmergeShader is not exist!");
                }
            }

            return submergeMaterial;
        }

        private bool CheckEnable()
        {
            if (Water.Instance == null) return false;
            if (!Water.Instance.underwaterEnable) return false;

            GetMaterial();
            if (submergeMaterial == null) return false;

            return true;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (NeedsReAlloc(submergenceRT, cameraTextureDescriptor))
            {
                if (submergenceRT != null) RTHandles.Release(submergenceRT);
                submergenceRT = RTHandles.Alloc(cameraTextureDescriptor.width, cameraTextureDescriptor.height, cameraTextureDescriptor.volumeDepth, DepthBits.None, cameraTextureDescriptor.graphicsFormat, FilterMode.Bilinear, TextureWrapMode.Clamp, TextureDimension.Tex2D, name: "SubmergenceTarget");
            }
            cmd.SetGlobalTexture(GlobalShaderVariables.Misc.SubmergenceTexture, submergenceRT);
            ConfigureTarget(submergenceRT);
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

            CommandBuffer cmd = CommandBufferPool.Get("Underwater Submerge");
            SetupCameraGlobals(cmd, camera);

            DrawPreceduralFullscreenQuad(cmd, submergenceRT,
                RenderBufferLoadAction.DontCare, GetMaterial(), 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void ClearRT()
        {
            RTHandles.Release(submergenceRT);
        }

        void DrawPreceduralFullscreenQuad(CommandBuffer cmd, RenderTargetIdentifier target,
            RenderBufferLoadAction loadAction, Material material, int pass)
        {
            cmd.SetRenderTarget(target, loadAction, RenderBufferStoreAction.Store);
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
