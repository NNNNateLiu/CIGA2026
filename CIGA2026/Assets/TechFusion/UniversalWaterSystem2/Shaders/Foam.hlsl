#if !defined(FOAM_INCLUDED)
#define FOAM_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float RemapValue(float origFrom, float origTo, float targetFrom, float targetTo, float value)
{
	return lerp(targetFrom, targetTo, (value - origFrom) / (origTo - origFrom));
}

float3 GetFoamAlbedo(float2 worldUV, float coverage, float shoreline)
{
	//float2 uv = TRANSFORM_TEX((i.worldUV + Noise(i.worldUV)*0.2), _FoamAlbedo);
	
    //return coverage;
    float2 uv = worldUV * 0.05;
	
    float4 foamBubble = SAMPLE_TEXTURE2D(_FoamBubble, sampler_FoamBubble, uv * 0.5);
	
    float foamTime = 1 - saturate(coverage + shoreline) /* * 0.65*/;
    float4 foamMasks = SAMPLE_TEXTURE2D(_FoamAlbedo, sampler_FoamAlbedo, uv);
	//return foamMasks.r;
	float microDistanceField = foamMasks.r;
	float temporalNoise = foamMasks.g;
	float foamNoise = saturate(foamMasks.b);
	float macroDistanceField = foamMasks.a;

	foamTime = saturate(foamTime);
	foamTime = pow(foamTime, 10.0);//4

	// Time offsets
	float microDistanceFieldInfluenceMin = 0.05;
	float microDistanceFieldInfluenceMax = 0.6;
	float MicroDistanceFieldInfluence = lerp(microDistanceFieldInfluenceMin, microDistanceFieldInfluenceMax, foamTime);
	foamTime += (2.0*(1.0f - microDistanceField) - 1.0) * MicroDistanceFieldInfluence;

	float temporalNoiseInfluenceMin = 0.1;
	float temporalNoiseInfluenceMax = 0.2;
	float temporalNoiseInfluence = -lerp(temporalNoiseInfluenceMin, temporalNoiseInfluenceMax, foamTime);
	foamTime += (2.0 * temporalNoise - 1.0) * temporalNoiseInfluence;

	foamTime = saturate(foamTime);
    foamTime = RemapValue(0.0, 1.0, 0.0, 1.8, foamTime); //RemapValue(0.0, 1.0, 0.0, 2.2, foamTime);
	foamTime = saturate(foamTime);

	// sharpness
	float sharpnessMin = 0.1;
	float sharpnessMax = 5.0;
	float sharpness = lerp(sharpnessMax, sharpnessMin, foamTime);
	sharpness = max(0.0f, sharpness);

    float alpha = RemapValue(foamTime, 1.0f, 0.0f, 1.0f, macroDistanceField);
	alpha *= sharpness;
	alpha = saturate(alpha);

	// detail in alpha
	float distanceFieldInAlpha = lerp(macroDistanceField, microDistanceField, 0.5f) * 0.45f;
	distanceFieldInAlpha = 1.0f - distanceFieldInAlpha;
	float noiseInAlpha = pow(foamNoise, 0.3f);

	// fade
	float fadeOverTime = pow(1.0 - foamTime, /*2*/5);

    float3 albedo = (alpha * distanceFieldInAlpha * noiseInAlpha * fadeOverTime * 2) + foamBubble.r * shoreline;

	return albedo;
}

//todo params
float ContactFoam(float2 worldUV, float2 screenUV, float viewDepth)
{
	float depthDiff = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams)
		- viewDepth;

    float2 uv = worldUV * 1;
    float3 contactTexture = SAMPLE_TEXTURE2D(_ContactFoamTexture, sampler_ContactFoamTexture, uv).rgb;

	float contactValue = dot(contactTexture, float3(1, 1, 1));
	float distanceFactor = (1 - saturate(abs(depthDiff) / 40));

	contactValue = _ContactFoam * contactValue * pow(distanceFactor, 20);
	return contactValue;
}

#endif