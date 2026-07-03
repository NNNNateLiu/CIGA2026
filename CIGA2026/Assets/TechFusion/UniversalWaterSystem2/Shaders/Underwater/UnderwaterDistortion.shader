Shader"Hidden/UniversalWaterSystem/UnderwaterDistortion"
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

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
        #include "../FullscreenVert.hlsl"
        #include "../Sampling.hlsl"
        #include "../Material.hlsl"
        #include "../Volume.hlsl"
        #include "Underwater.hlsl"

        TEXTURE2D(Water_UnderWaterBackgroundTexture);
        SAMPLER(samplerWater_UnderWaterBackgroundTexture);

        TEXTURE2D(Water_ScreenNoise);
        SAMPLER(samplerWater_ScreenNoise);

        half4 UnderwaterDistortionFrag(Varyings input) : SV_Target
        {
            if (!IsUnderWater(input.uv))
            {
                clip(-1);
            }
    
            float noise = SAMPLE_TEXTURE2D_X(Water_ScreenNoise, samplerWater_ScreenNoise, UnityStereoTransformScreenSpaceTex(input.uv * 4 + Water_Time.x * 0.5)).r;
            float2 distortionUV = input.uv;
    
            distortionUV.x += cos(noise) * 0.005;
            distortionUV.y += sin(noise) * 0.005;
    
            float3 distortionColor = SAMPLE_TEXTURE2D_X(Water_UnderWaterBackgroundTexture, samplerWater_UnderWaterBackgroundTexture, UnityStereoTransformScreenSpaceTex(distortionUV)).rgb;
            return float4(distortionColor, 1);
        }

        ENDHLSL

        Pass
        {
            Name "Underwater Distortion"
            Cull Off ZWrite Off ZTest Always

            HLSLPROGRAM
            #pragma vertex ProceduralFullscreenVert
            #pragma fragment UnderwaterDistortionFrag
            #pragma target 3.5
            ENDHLSL
        }
    }
}