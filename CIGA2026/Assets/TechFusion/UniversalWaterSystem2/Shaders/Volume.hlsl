#ifndef VOLUME_INCLUDED
#define VOLUME_INCLUDED

#include "Material.hlsl"

half3 Scattering(half depth)
{
	return SAMPLE_TEXTURE2D(_AbsorptionScatteringRamp, sampler_AbsorptionScatteringRamp, half2(depth, 0.375h)).rgb;
}

half3 Absorption(half depth)
{
	return SAMPLE_TEXTURE2D(_AbsorptionScatteringRamp, sampler_AbsorptionScatteringRamp, half2(depth, 0.0h)).rgb;
}


float3 UnderwaterFogColor(float3 viewDir, float3 lightDir, float depth)
{
    float3 underwaterBaseColor = Water_UnderBaseColor.rgb; //float3(0, 0.92, 1) * 0.1;
    float3 sssColor = Water_UnderSSSColor.rgb;//float3(0, 0.1, 0.12);
    
    float depthAttenuation = min(0, abs(depth) * 0.02);
    
    //ambient
    float3 ambient = underwaterBaseColor * max(0.1, saturate(2 - viewDir.y - depthAttenuation));
    
    //sss
    float sssFactor = pow(saturate(dot(lightDir, -viewDir)), 5) * pow(max(0, 1 - viewDir.y - depthAttenuation), 3) * 5;

    //combine
    float3 color = ambient + sssColor * sssFactor;
    
    return color;
}

float3 UnderwaterColor(float3 fogColor, float3 backgroundColor, float viewDist)
{
    return lerp(backgroundColor, fogColor, saturate(viewDist / _MaxDepth));
}

#endif // VOLUME_INCLUDED