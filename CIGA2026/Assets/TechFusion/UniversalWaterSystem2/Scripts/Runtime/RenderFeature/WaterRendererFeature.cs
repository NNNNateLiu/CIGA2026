using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UniversalWaterSystem
{
    public class WaterRendererFeature : ScriptableRendererFeature
    {
        private OceanMeshPass oceanMeshPass;
        private SubmergePass submergePass;
        private UnderwaterOpaquePass underwaterOpaquePass;
        private UnderwaterDistortionPass underwaterDistortionPass;

        public override void Create()
        {
            oceanMeshPass = new OceanMeshPass();
            submergePass = new SubmergePass();
            underwaterOpaquePass = new UnderwaterOpaquePass();
            underwaterDistortionPass = new UnderwaterDistortionPass();

            name = "Water Effect";
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            submergePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            renderer.EnqueuePass(submergePass);

            underwaterOpaquePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            renderer.EnqueuePass(underwaterOpaquePass);

            oceanMeshPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            renderer.EnqueuePass(oceanMeshPass);

            underwaterDistortionPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            renderer.EnqueuePass(underwaterDistortionPass);
        }

        private void OnDisable()
        {
            submergePass.ClearRT();
            underwaterOpaquePass.ClearRT();
        }
    }
}
