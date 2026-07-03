using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UniversalWaterSystem
{
    public class OceanMeshPass : ScriptableRenderPass
    {
        private readonly static ShaderTagId OceanShaderTagId = new ShaderTagId("OceanMesh");
        private readonly static ProfilingSampler profilingSampler = new ProfilingSampler("Ocean Mesh");
        private FilteringSettings filteringSettings;

        public OceanMeshPass()
        {
            filteringSettings = new FilteringSettings(RenderQueueRange.all);
        }

        private bool CheckEnable()
        {
            if (Water.Instance == null) return false;
            return true;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!CheckEnable()) return;

            CameraData cameraData = renderingData.cameraData;
            Camera camera = cameraData.camera;

            DrawingSettings drawingSettings = new DrawingSettings(OceanShaderTagId,
                new SortingSettings(camera));

            CommandBuffer cmd = CommandBufferPool.Get();
            //SetupGlobalKeywords(cmd);
            //SetupCameraGlobals(cmd, camera);
            context.ExecuteCommandBuffer(cmd);

            using (new ProfilingScope(cmd, profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                drawingSettings.perObjectData = PerObjectData.LightProbe;
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            }
            context.ExecuteCommandBuffer(cmd);


            CommandBufferPool.Release(cmd);
        }

        //private void SetupCameraGlobals(CommandBuffer cmd, Camera cam)
        //{
        //    cmd.SetGlobalMatrix(GlobalShaderVariables.Misc.InverseViewMatrix, cam.cameraToWorldMatrix);
        //    cmd.SetGlobalMatrix(GlobalShaderVariables.Misc.InverseProjectionMatrix,
        //        GL.GetGPUProjectionMatrix(cam.projectionMatrix, false).inverse);
        //}

        //private void SetupGlobalKeywords(CommandBuffer cmd)
        //{
        //    SetGlobalKeyword(cmd, "OCEAN_TRANSPARENCY_ENABLED", _settings.transparency);
        //    SetGlobalKeyword(cmd, "OCEAN_UNDERWATER_ENABLED", _settings.underwaterEffect);
        //}

        //private void SetGlobalKeyword(CommandBuffer cmd, string keyword, bool b)
        //{
        //    if (b)
        //        cmd.EnableShaderKeyword(keyword);
        //    else
        //        cmd.DisableShaderKeyword(keyword);
        //}
    }
}
