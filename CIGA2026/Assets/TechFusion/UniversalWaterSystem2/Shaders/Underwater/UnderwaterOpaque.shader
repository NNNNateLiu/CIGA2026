Shader"Hidden/UniversalWaterSystem/UnderwaterOpaque"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #pragma exclude_renderers gles

        #define MID
        #define CLOSE

#define _MAIN_LIGHT_SHADOWS
			#define _MAIN_LIGHT_SHADOWS_CASCADE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
        #include "../FullscreenVert.hlsl"
        #include "../Sampling.hlsl"
        #include "../Material.hlsl"
        #include "../Volume.hlsl"
        #include "Underwater.hlsl"

        TEXTURE2D(Water_UnderWaterOpaqueTexture);
        SAMPLER(samplerWater_UnderWaterOpaqueTexture);

        half4 UnderwaterOpaqueFrag(Varyings input) : SV_Target
        {
            if (!IsUnderWater(input.uv))
            {
                clip(-1);
            }
    
            float rawDepth = SampleSceneDepth(input.uv);
            float4 positionCS = float4(input.uv * 2 - 1, rawDepth, 1);
            float4 positionVS = mul(Water_InverseProjectionMatrix, positionCS);
            positionVS /= positionVS.w;
            float3 viewDir = -mul(Water_InverseViewMatrix, float4(positionVS.xyz, 0)).xyz;
            float viewDist = length(positionVS);
            viewDir /= viewDist;
            float4 positionWS = mul(Water_InverseViewMatrix, positionVS);
    
            float skyboxMask = Linear01Depth(rawDepth, _ZBufferParams) > 0.99 ? 1 : 0;
            if (viewDir.y < -0.1)
            {
                clip(-skyboxMask);
            }
    
            Light mainLight = GetMainLight();
            float3 backgroundColor = SAMPLE_TEXTURE2D_X(Water_UnderWaterOpaqueTexture, samplerWater_UnderWaterOpaqueTexture, UnityStereoTransformScreenSpaceTex(input.uv)).rgb;
            backgroundColor += ApplyCaustics(input.uv);
            float3 fogColor = UnderwaterFogColor(viewDir, mainLight.direction, _WorldSpaceCameraPos.y);
            float3 color = UnderwaterColor(fogColor, backgroundColor, viewDist);
            return float4(color, 1);
        }

        ENDHLSL

        Pass
        {
            Name "Underwater Opaque"
            Cull Off ZWrite Off ZTest Always

            HLSLPROGRAM
            #pragma vertex ProceduralFullscreenVert
            #pragma fragment UnderwaterOpaqueFrag
            #pragma target 3.5
            ENDHLSL
        }
    }
}