Shader"Hidden/UniversalWaterSystem/WaterLine"
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

        TEXTURE2D(Water_UnderWaterBackgroundBlurTexture);
        SAMPLER(samplerWater_UnderWaterBackgroundBlurTexture);

        float4 UnderWaterBackground_TexelSize;

        float3 SampleUnderwaterBackground(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(Water_UnderWaterBackgroundTexture, samplerWater_UnderWaterBackgroundTexture, UnityStereoTransformScreenSpaceTex(uv)).rgb;
        }

        float3 SampleUnderwaterBackgroundBlur(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(Water_UnderWaterBackgroundBlurTexture, samplerWater_UnderWaterBackgroundBlurTexture, UnityStereoTransformScreenSpaceTex(uv)).rgb;
        }

        half4 WaterLineFrag(Varyings input) : SV_Target
        {
            float value = IsWaterLine(input.uv, 0.015);//0.025
            if (value < 0)
            {
                clip(-1);
            }
    
            float3 originColor = SampleUnderwaterBackground(input.uv);
            float3 blurColor = SampleUnderwaterBackgroundBlur(input.uv);
            float3 edgeColor = blurColor * float3(0.8, 0.92, 1) * 0.8;
    return float4(lerp(blurColor, edgeColor, value), 1);
}

        half4 EdgeHBlurFrag(Varyings input) : SV_Target
        {
            float2 texelSize = UnderWaterBackground_TexelSize.xy;
    
            float3 colorC = SampleUnderwaterBackground(input.uv);
    float3 colorL = SampleUnderwaterBackground(input.uv - 2 * half2(texelSize.x, 0));
    float3 colorR = SampleUnderwaterBackground(input.uv + 2 * half2(texelSize.x, 0));
            float3 colorLL = SampleUnderwaterBackground(input.uv - 4 * half2(texelSize.x, 0));
            float3 colorRR = SampleUnderwaterBackground(input.uv + 4 * half2(texelSize.x, 0));
            
            half weight[3] = { 0.4026, 0.2442, 0.0545 };
    
            float3 color = colorC * weight[0] + 
            colorL * weight[1] + 
            colorR * weight[1] + 
            colorLL * weight[2] +
            colorRR * weight[2];
    
    return float4(color, 1);

        }

        ENDHLSL

        Pass
        {
            Name "Water Line"
            Cull Off ZWrite Off ZTest Always

            HLSLPROGRAM
            #pragma vertex ProceduralFullscreenVert
            #pragma fragment WaterLineFrag
            #pragma target 3.5
            ENDHLSL
        }

        Pass
        {
            Name"Edge HBlur"
            Cull Off ZWrite Off ZTest Always

            HLSLPROGRAM
            #pragma vertex ProceduralFullscreenVert
            #pragma fragment EdgeHBlurFrag
            #pragma target 3.5
            ENDHLSL
        }
    }
}