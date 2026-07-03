using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.GraphicsBuffer;

namespace UniversalWaterSystem
{
    public class DynamicWaves
    {
        private int cascadeCount = 1;
        
        private RenderTexture displacements;

        private RenderTexture[] foams = { null, null, null };
        private const int genFoamID = 0;
        private int lastFoamID = 1;
        private int thisFoamID = 2;

        //private const string k_FoamBlendShaderName = "Hidden/UniversalWaterSystem/FoamBlend";
        private Shader foamBlendShader = null;
        private Material foamBlendMaterial = null;

        private RenderTextureFormat displacementFormat = RenderTextureFormat.ARGBHalf;

        //texture resolution
        private int resolution = -1;

        private bool needToReadWriteTextureData = false;
        
        public const int MAX_LOD_COUNT = 15;

        private Vector4[] paramCascadePosScales = new Vector4[MAX_LOD_COUNT + 1];
        private Vector4[] paramCascadeSize = new Vector4[MAX_LOD_COUNT + 1];


        //didn't initialised at first frame
        private Vector4[] paramPrevCascadePosScales = new Vector4[MAX_LOD_COUNT + 1];
        private Vector4[] paramPrevCascadeSize = new Vector4[MAX_LOD_COUNT + 1];

        public DynamicWaves()
        {
        }

        public void Init(int count)
        {
            cascadeCount = count;

            InitData();
        }

        public static RenderTexture CreateCascadeDataTextures(int count, RenderTextureDescriptor desc, string name, bool needToReadWriteTextureData)
        {
            RenderTexture result = new RenderTexture(desc);
            result.wrapMode = TextureWrapMode.Clamp;
            result.antiAliasing = 1;
            result.filterMode = FilterMode.Bilinear;
            result.anisoLevel = 0;
            result.useMipMap = false;
            result.name = name;
            result.dimension = TextureDimension.Tex2DArray;
            result.volumeDepth = count;
            result.enableRandomWrite = needToReadWriteTextureData;
            result.Create();
            return result;
        }
        
        void InitData()
        {
            if (foamBlendMaterial == null)
            {
                if (foamBlendShader == null)
                {
                    //foamBlendShader = Shader.Find(k_FoamBlendShaderName);
                    foamBlendShader = Water.Instance.waterResources.foamBlendShader;
                    if (foamBlendShader == null)
                    {
                        Debug.LogError("Shader Not Exist! -- foamBlendShader");
                        return;
                    }

                    foamBlendMaterial = CoreUtils.CreateEngineMaterial(foamBlendShader);
                }
            }

            var resolution = Water.Instance.CascadeResolution;
            var desc = new RenderTextureDescriptor(resolution, resolution, displacementFormat, 0);
            displacements = CreateCascadeDataTextures(cascadeCount, desc, "Dynamic Displacement Data", needToReadWriteTextureData);

            var descFoam = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.RG16, 0);
            foams[0] = CreateCascadeDataTextures(cascadeCount, descFoam, "Dynamic Foam Current", needToReadWriteTextureData);
            foams[1] = CreateCascadeDataTextures(cascadeCount, descFoam, "Dynamic Foam Last 0", needToReadWriteTextureData);
            foams[2] = CreateCascadeDataTextures(cascadeCount, descFoam, "Dynamic Foam Last 1", needToReadWriteTextureData);

            Shader.SetGlobalTexture("Water_DynamicDisplacement", displacements);
        }

        public void UpdateData()
        {
            int width = Water.Instance.CascadeResolution;
            
            if (resolution == -1)
            {
                resolution = width;
            }
            else if (width != resolution)
            {
                displacements.Release();
                displacements.width = displacements.height = resolution;
                displacements.Create();

                resolution = width;
            }

            var lt = Water.Instance.cascadeTransform;
            for (int i = 0; i < cascadeCount; i++)
            {
                paramCascadePosScales[i] = new Vector4(
                    lt._renderData[i]._posSnapped.x, 
                    lt._renderData[i]._posSnapped.z,
                    Water.Instance.CalcLodScale(i), 
                    0f);

                paramCascadeSize[i] = new Vector4(
                    lt._renderData[i]._texelWidth, 
                    lt._renderData[i]._textureRes, 
                    1f, 
                    1f / lt._renderData[i]._textureRes);
            }

            // Duplicate the last element as the shader accesses element {slice index + 1] in a few situations. This way going
            // off the end of this parameter is the same as going off the end of the texture array with our clamped sampler.
            // Never use this last lod - it exists to give 'something' but should not be used
            paramCascadePosScales[cascadeCount] = paramCascadePosScales[cascadeCount - 1];
            paramCascadeSize[cascadeCount] = paramCascadeSize[cascadeCount - 1];
            paramCascadeSize[cascadeCount].z = 0f;

            thisFoamID = lastFoamID;
            lastFoamID = lastFoamID % 2 + 1; // ((n - 1) +  1) % 2 + 1
        }

        void LateUpdateData()
        {
            var lt = Water.Instance.cascadeTransform;
            for (int i = 0; i <= cascadeCount; i++)
            {
                paramPrevCascadePosScales[i] = paramCascadePosScales[i];
                paramPrevCascadeSize[i] = paramCascadeSize[i];
            }
        }

        void BlendFoam(CommandBuffer cmd, int depth, RenderTargetIdentifier currentMap, RenderTargetIdentifier historyMap, RenderTargetIdentifier target)
        {
            cmd.SetGlobalTexture(GlobalShaderVariables.Simulation.CascadeFoamCurrent, currentMap);
            cmd.SetGlobalTexture(GlobalShaderVariables.Simulation.CascadeFoamHistory, historyMap);
            cmd.SetGlobalInt(GlobalShaderVariables.Simulation.CascadeIndex, depth);
            cmd.SetRenderTarget(target, 0, CubemapFace.Unknown, depth);
            cmd.DrawMesh(UnityEngine.Rendering.Universal.RenderingUtils.fullscreenMesh, Matrix4x4.identity, foamBlendMaterial, 0, (int)0);
        }

        public void BuildCommandBuffer(CommandBuffer cmd)
        {
            RenderTargetIdentifier[] dispAndFoams = new RenderTargetIdentifier[] { displacements, foams[genFoamID] };

            for (int i = cascadeCount - 1; i >= 0; i--)
            {
                //displacement and foam
                //cmd.SetRenderTarget(displacements, displacements, 0, CubemapFace.Unknown, i);
                cmd.SetRenderTarget(dispAndFoams, displacements, 0, CubemapFace.Unknown, i);
                cmd.ClearRenderTarget(false, true, new Color(0f, 0f, 0f, 0f));
                SubmitDynamicParticleDraws(i, cmd);
                SubmitShorelineDraws(i, cmd);
            }

            for (int i = cascadeCount - 1; i >= 0; i--)
            {
                BlendFoam(cmd, i, foams[genFoamID], foams[lastFoamID], foams[thisFoamID]);
            }

            cmd.SetGlobalTexture(GlobalShaderVariables.Simulation.CascadeFoam, foams[thisFoamID]);
        }

        void SubmitDynamicParticleDraws(int id, CommandBuffer cmd)
        {
            var lt = Water.Instance.cascadeTransform;

            lt.SetViewProjectionMatrices(id, cmd);

            foreach (var particle in Water.Instance.GetWaveParticles())
            {
                cmd.DrawRenderer(particle.GetComponent<ParticleSystemRenderer>(), particle.GetComponent<ParticleSystemRenderer>().sharedMaterial, 0, 0);
            }
        }

        void SubmitShorelineDraws(int id, CommandBuffer cmd)
        {
            var lt = Water.Instance.cascadeTransform;

            lt.SetViewProjectionMatrices(id, cmd);

            foreach (ShorelineWaveGenerator swg in Water.Instance.shorelineGenerators)
            {
                swg.SubmitDraws(cmd);
            }
        }

        public void SetGlobalShaderVariables()
        {
            Shader.SetGlobalVector("Water_Time", new Vector4(Time.timeSinceLevelLoad, Time.deltaTime, 0, 0));
            Shader.SetGlobalVectorArray("Water_CascadePosScale", paramCascadePosScales);
            Shader.SetGlobalVectorArray("Water_CascadeSize", paramCascadeSize);
            Shader.SetGlobalVectorArray("Water_PrevCascadePosScale", paramPrevCascadePosScales);
            Shader.SetGlobalVectorArray("Water_PrevCascadeSize", paramPrevCascadeSize);

            LateUpdateData();
        }
    }
}
