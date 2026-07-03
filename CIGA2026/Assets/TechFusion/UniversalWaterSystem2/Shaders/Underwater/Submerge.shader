Shader"Hidden/UniversalWaterSystem/Submerge"
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

        float SubmergenceFrag(Varyings input) : SV_Target
        {
            float4 positionCS = float4(input.uv * 2 - 1, UNITY_NEAR_CLIP_VALUE, 1);
            float4 positionVS = mul(Water_InverseProjectionMatrix, positionCS);
            positionVS = positionVS / positionVS.w;
            float4 positionWS = mul(Water_InverseViewMatrix, positionVS);
    
            float3 viewVector = positionWS - _WorldSpaceCameraPos;
            float viewDist = length(viewVector);
    
            float lod_c0 = min(LengthScale0 * 1 / viewDist, 1);
            float lod_c1 = min(LengthScale1 * 1 / viewDist, 1);
            float lod_c2 = min(LengthScale2 * 1 / viewDist, 1);

            float3 worldUVDeviders = float3(LengthScale0, LengthScale1, LengthScale2);
            float waterHeight = SampleHeight(positionWS.xz, worldUVDeviders, float3(lod_c0, lod_c1, lod_c2));
            return positionWS.y - waterHeight + 0.5;
        }
        ENDHLSL

        Pass
        {
            Name "Camera Submergence"
            Cull Off ZWrite Off ZTest Always

            HLSLPROGRAM
            #pragma vertex ProceduralFullscreenVert
            #pragma fragment SubmergenceFrag
            #pragma target 3.5
            ENDHLSL
        }
    }
}