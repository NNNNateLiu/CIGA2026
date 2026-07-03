#if !defined(UNDERWATER_INCLUDED)
#define UNDERWATER_INCLUDED

// submergence
TEXTURE2D(Water_CameraSubmergenceTexture);
SAMPLER(samplerWater_CameraSubmergenceTexture);

bool IsUnderWater(float2 uv)
{
    bool underwater = true;
    
    float submergence = SAMPLE_TEXTURE2D(Water_CameraSubmergenceTexture,
                samplerWater_CameraSubmergenceTexture, uv).r;
    //float safetyMargin = 0.025;
    //if (submergence - 0.5 > safetyMargin)
    //    underwater = false;
    
    float rawDepth = SampleSceneDepth(uv);
    float4 positionCS = float4(uv * 2 - 1, rawDepth, 1);
    float4 positionVS = mul(Water_InverseProjectionMatrix, positionCS);
    positionVS /= positionVS.w;
    float3 viewDir = -mul(Water_InverseViewMatrix, float4(positionVS.xyz, 0)).xyz;
    float viewDist = length(positionVS);
    viewDir /= viewDist;
    //float4 positionWS = mul(Water_InverseViewMatrix, positionVS);

    //safetyMargin *= saturate((viewDir.y * 1.3 + 1) * 0.5);
    //clip(-(submergence - 0.5 > safetyMargin));
    
    //viewDir.y > 0 : look into the water
    float safetyMargin = viewDir.y > 0 ? 0.03 : 0.001;
    if (submergence - 0.5 > safetyMargin)
        underwater = false;
    
    return underwater;
}

//return edge fade alpha
float IsWaterLine(float2 uv, float margin)
{
    float submergence = SAMPLE_TEXTURE2D(Water_CameraSubmergenceTexture,
                samplerWater_CameraSubmergenceTexture, uv).r;
    float submergeValue = submergence - 0.5;
    float waterLineMargin = abs(margin);
    if (abs(submergeValue) < waterLineMargin)
        return saturate((waterLineMargin - abs(submergeValue)) / waterLineMargin);
    
    return -1;
}

half3 ApplyCaustics(half2 screenUV)
{
    float2 UV = screenUV;

    // Sample the depth from the Camera depth texture.
#if UNITY_REVERSED_Z
    real depth = SampleSceneDepth(UV);
#else
    // Adjust Z to match NDC for OpenGL ([-1, 1])
    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
#endif

    // Reconstruct the world space positions.
    float3 worldPosition = ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);
    
    //shadow
    float backGroundShadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(worldPosition));
    
    float2 depthRange = float2(-50, -1);
    float decayRange = 5;
    
    float worldY = worldPosition.y;
    
    //caustics intensity
    float distanceToMin = abs(worldY - depthRange.x);
    float distanceToMax = abs(worldY - depthRange.y);
    
    float intensity = (worldY > depthRange.x && worldY < depthRange.y) ? saturate(min(distanceToMin, distanceToMax) / decayRange) : 0.0;
    intensity *= backGroundShadow;
    float scale = lerp(1, 0.2, saturate(-worldY / 500.0));
    
    float4 causticsUV_ST1 = float4(0.1, 0.1, 0.2, 0);
    float4 causticsUV_ST2 = float4(0.1, 0.1, 0, 0.3);
    
    float causticsSpeed1 = 0.05;
    float causticsSpeed2 = 0.05;
    
    float2 causticsUV1 = worldPosition.xz * scale * causticsUV_ST1.xy + causticsUV_ST1.zw;
    causticsUV1 += causticsSpeed1 * _Time.y;
    
    float2 causticsUV2 = worldPosition.xz * scale * causticsUV_ST2.xy + causticsUV_ST2.zw;
    causticsUV2 += causticsSpeed2 * _Time.y;
    
    float4 causticsColor1 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, causticsUV1);
    float4 causticsColor2 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, causticsUV2);
    
    return min(causticsColor1.xyz, causticsColor2.xyz) * intensity * 2;
}

#endif