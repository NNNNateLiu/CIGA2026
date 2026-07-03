Shader"Hidden/UniversalWaterSystem/FoamBlend"
{
	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Material.hlsl"
	#include "Cascade.hlsl"

	Texture2DArray Water_CascadeFoamCurrent;
	Texture2DArray Water_CascadeFoamHistory;

	int Water_CascadeIndex;

	struct Attributes
	{
		float4 positionHCS   : POSITION;
		float2 uv           : TEXCOORD0;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};

	struct Varyings
	{
		float4  positionCS  : SV_POSITION;
		float2  uv          : TEXCOORD0;
		UNITY_VERTEX_OUTPUT_STEREO
	};

	Varyings VertDefault(Attributes input)
	{
		Varyings output;
		UNITY_SETUP_INSTANCE_ID(input);
		//UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

		// Note: The pass is setup with a mesh already in CS
		// Therefore, we can just output vertex position
		output.positionCS = float4(input.positionHCS.xyz, 1.0);

		#if UNITY_UV_STARTS_AT_TOP
			output.positionCS.y *= -1;
		#endif

		output.uv = input.uv;

		// Add a small epsilon to avoid artifacts when reconstructing the normals
		output.uv += 1.0e-6;

		return output;
	}

	float4 SampleCurrentFoam(float2 worldXZ)
	{
		float3 uv = WorldToUV(worldXZ);
		return Water_CascadeFoamCurrent.SampleLevel(sampler_linear_clamp, uv, 0) * UVBoundFlag(uv.xy);
}

	float4 SampleHistoryFoam(float2 worldXZ)
	{
		float3 uv = WorldToPrevUV(worldXZ);
		return Water_CascadeFoamHistory.SampleLevel(sampler_linear_clamp, uv, 0) * UVBoundFlag(uv.xy);
	}

	half4 FoamBlend(Varyings input) : SV_Target
	{
		//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		const int kernelSize = 5;
		float2 kernel[kernelSize] = {
			float2(-1, 0),
			float2(1, 0),
			float2(0, 0),
			float2(0, 1),
			float2(0, -1)
		};

		float weight[kernelSize] = {
			0.1,
			0.1,
			0.6,
			0.1,
			0.1
		};

		float2 uv = input.uv;
		float2 worldXZ = UVToWorld(uv, Water_CascadeIndex);

		float4 foamCurrent = SampleCurrentFoam(worldXZ);

		float4 foamHistory = float4(0, 0, 0, 0);
		for (int j = 0; j < kernelSize; j++)
		{
			foamHistory += SampleHistoryFoam(worldXZ + kernel[j] * 0.3) * weight[j];
		}

		float foamGen = foamCurrent.x;
		float foamHistoryFade = max(foamHistory.x - Water_Time.y * 0.28, 0);
		return half4(foamGen * 0.6 + foamHistoryFade, foamGen, 0, 0);
	}
	ENDHLSL

	SubShader
	{
		Tags{ "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Foam Blend"
			ZTest Always
			ZWrite Off
			Cull Off

			HLSLPROGRAM
				#pragma vertex VertDefault
				#pragma fragment FoamBlend
			ENDHLSL
		}
	}
}
