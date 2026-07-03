#ifndef LIGHTING_INCLUDED
#define LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#define SHADOW_ITERATIONS 4

half CalculateFresnelTerm(half3 normalWS, half3 viewDirectionWS)
{
    return saturate(pow(1.0 - dot(normalWS, viewDirectionWS), 2)) * saturate(pow(1.0 - dot(float3(0, 1, 0), viewDirectionWS), 1.5)); //todo temp, use up vector
}

//Soft Shadows
half SoftShadows(float3 screenUV, float3 positionWS, half3 viewDir, half depth)
{
#ifdef _MAIN_LIGHT_SHADOWS
    half2 jitterUV = screenUV.xy * _ScreenParams.xy/* * _DitherPattern_TexelSize.xy*/;
	half shadowAttenuation = 0;

	float loopDiv = 1.0 / SHADOW_ITERATIONS;
	half depthFrac = depth * loopDiv;
	half3 lightOffset = -viewDir * depthFrac;
	for (uint i = 0u; i < SHADOW_ITERATIONS; ++i)
    {
#ifndef _STATIC_SHADER
        jitterUV += frac(half2(_Time.x, -_Time.z));
#endif
        float3 jitterTexture = 0;//SAMPLE_TEXTURE2D(_DitherPattern, sampler_DitherPattern, jitterUV + i * _ScreenParams.xy).xyz * 2 - 1;
	    half3 j = jitterTexture.xzy * depthFrac * i * 0.1;
	    float3 lightJitter = (positionWS + j) + (lightOffset * (i + jitterTexture.y));
	    shadowAttenuation += SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture, TransformWorldToShadowCoord(lightJitter));
	}
    return BEYOND_SHADOW_FAR(TransformWorldToShadowCoord(positionWS * 1.1)) ? 1.0 : shadowAttenuation * loopDiv;
#else
    return 1;
#endif
}

///////////////////////////////////////////////////////////////////////////////
//                           Reflection Modes                                //
///////////////////////////////////////////////////////////////////////////////

half3 SampleReflections(bool isUnderwater, half3 normalWS, half3 viewDirectionWS, half2 screenUV, half roughness)
{
    half3 reflection = 0;
    half2 refOffset = 0;

	half3 reflectVector = reflect(-viewDirectionWS, normalWS);
	reflection = GlossyEnvironmentReflection(reflectVector, 0, 1);
#ifdef _REFLECTION_PLANARREFLECTION
	// get the perspective projection
	float2 p11_22 = float2(unity_CameraInvProjection._11, unity_CameraInvProjection._22) * 10;
	// conver the uvs into view space by "undoing" projection
	float3 viewDir = -(float3((screenUV * 2 - 1) / p11_22, -1));

	half3 viewNormal = mul(normalWS, (float3x3)GetWorldToViewMatrix()).xyz;
	//half3 reflectVectorPlanar = reflect(-viewDir, viewNormal);

	half2 reflectionUV = screenUV + normalWS.zx * half2(0.02, 0.15) * 3;
	half3 planarReflection = SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTexture, sampler_ScreenTextures_linear_clamp, reflectionUV, 0.0/*6 * roughness*/).rgb;//planar reflection
	
	float t = lerp(0, 0.4, dot(normalWS, float3(0, 1, 0)));
	reflection = lerp(reflection, planarReflection, t);
#endif

	if (isUnderwater)
	{
		reflection = half3(0, 0, 0);
	}

    return reflection;
}

#endif // LIGHTING_INCLUDED