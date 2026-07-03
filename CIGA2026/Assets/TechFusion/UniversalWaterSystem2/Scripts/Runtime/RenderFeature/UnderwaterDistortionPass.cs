using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UniversalWaterSystem
{
    public class UnderwaterDistortionPass : ScriptableRenderPass
    {
        private Material underwaterDistortionMaterial;
        private Material waterLineMaterial;

        private RenderTargetIdentifier underwaterBackground;
        private static readonly int underwaterBackgroundID = Shader.PropertyToID("UnderwaterBackground");

        private Vector4 underwaterBackgroundTexel = Vector4.zero;

        private RenderTargetIdentifier underwaterBackgroundBlur;
        private static readonly int underwaterBackgroundBlurID = Shader.PropertyToID("UnderwaterBackgroundBlur");

        public UnderwaterDistortionPass()
        {
        }

        private Material GetDistortionMaterial()
        {
            if (underwaterDistortionMaterial == null)
            {
                //underwaterDistortionMaterial = new Material(Shader.Find("Hidden/UniversalWaterSystem/UnderwaterDistortion"));
                if (Water.Instance != null &&
                    Water.Instance.waterResources != null &&
                    Water.Instance.waterResources.underwaterDistortionShader != null)
                {
                    underwaterDistortionMaterial = new Material(Water.Instance.waterResources.underwaterDistortionShader);
                }
                else
                {
                    Debug.LogError("UnderwaterDistortionShader is not exist!");
                }
            }

            return underwaterDistortionMaterial;
        }

        private Material GetWaterLineMaterial()
        {
            if (waterLineMaterial == null)
            {
                //waterLineMaterial = new Material(Shader.Find("Hidden/UniversalWaterSystem/WaterLine"));
                if (Water.Instance != null &&
                    Water.Instance.waterResources != null &&
                    Water.Instance.waterResources.waterLineShader != null)
                {
                    waterLineMaterial = new Material(Water.Instance.waterResources.waterLineShader);
                }
                else
                {
                    Debug.LogError("WaterLineShader is not exist!");
                }
            }

            return waterLineMaterial;
        }

        private bool CheckEnable()
        {
            if (Water.Instance == null) return false;
            if (!Water.Instance.underwaterEnable) return false;

            GetDistortionMaterial();
            GetWaterLineMaterial();
            if (underwaterDistortionMaterial == null ||
                waterLineMaterial == null) return false;

            return true;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            float scale = 1;

            cmd.GetTemporaryRT(underwaterBackgroundID, (int)(cameraTextureDescriptor.width * scale), (int)(cameraTextureDescriptor.height * scale), 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
            underwaterBackground = new RenderTargetIdentifier(underwaterBackgroundID);

            
            underwaterBackgroundTexel.x = 1 / (cameraTextureDescriptor.width * scale);
            underwaterBackgroundTexel.y = 1 / (cameraTextureDescriptor.height * scale);

            cmd.GetTemporaryRT(underwaterBackgroundBlurID, (int)(cameraTextureDescriptor.width * scale), (int)(cameraTextureDescriptor.height * scale), 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
            underwaterBackgroundBlur = new RenderTargetIdentifier(underwaterBackgroundBlurID);
        }

        //private void SetupCameraGlobals(CommandBuffer cmd, Camera cam)
        //{
        //    cmd.SetGlobalMatrix(GlobalShaderVariables.Misc.InverseViewMatrix, cam.cameraToWorldMatrix);
        //    cmd.SetGlobalMatrix(GlobalShaderVariables.Misc.InverseProjectionMatrix,
        //        GL.GetGPUProjectionMatrix(cam.projectionMatrix, false).inverse);
        //}

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!CheckEnable()) return;

            CameraData cameraData = renderingData.cameraData;
            Camera camera = cameraData.camera;

            CommandBuffer cmd = CommandBufferPool.Get("Underwater Distortion");
            //SetupCameraGlobals(cmd, camera);

            cmd.Blit(cameraData.renderer.cameraColorTarget, underwaterBackground, 0, 0);
            cmd.SetGlobalTexture("Water_UnderWaterBackgroundTexture", underwaterBackgroundID);
            cmd.SetGlobalVector("UnderWaterBackground_TexelSize", underwaterBackgroundTexel);

            //distortion
            DrawPreceduralFullscreenQuad(cmd, cameraData.renderer.cameraColorTarget,
                GetDistortionMaterial(), 0);

            //blur
            DrawPreceduralFullscreenQuad(cmd, underwaterBackgroundBlur,
                GetWaterLineMaterial(), 1);
            //cmd.Blit(underwaterBackground, underwaterBackgroundBlur, 0, 0);

            cmd.SetGlobalTexture("Water_UnderWaterBackgroundBlurTexture", underwaterBackgroundBlur);
            //waterline
            DrawPreceduralFullscreenQuad(cmd, cameraData.renderer.cameraColorTarget,
                GetWaterLineMaterial(), 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(underwaterBackgroundID);
        }

        void DrawPreceduralFullscreenQuad(CommandBuffer cmd, RenderTargetIdentifier target,
            Material material, int pass)
        {
            cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, -1);
            cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Quads, 4, 1, null);
        }
    }
}
