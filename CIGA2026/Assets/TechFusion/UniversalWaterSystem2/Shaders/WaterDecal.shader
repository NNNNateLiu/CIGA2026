Shader"TechFusion/UniversalWaterSystem/WaterDecal"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_MaskTex ("Mask", 2D) = "white" {}
		// Blend mode values
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend mode", Float) = 0.0
		// Blend mode values
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend mode", Float) = 0.0
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off
		//ZTest Off
		Blend[_SrcBlend][_DstBlend]
		LOD 100

		Pass
		{
			Name"Water Decal"
			HLSLPROGRAM
			#pragma vertex WaterDecalVertex
			#pragma fragment WaterDecalFragment
			//#pragma multi_compile _ MID CLOSE
			#define CLOSE
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Sampling.hlsl"
			#include "Cascade.hlsl"

			struct Attributes
			{
				float3 positionOS : POSITION;
				float3 normalOS : NORMAL;
    			float4 tangentOS : TANGENT;
				half4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				half4 color : TEXCOORD1;
};

			sampler2D _MainTex;
			sampler2D _MaskTex;
			
			Varyings WaterDecalVertex(Attributes input)
			{
				Varyings output = (Varyings)0;

				float3 positionWS = mul(unity_ObjectToWorld, input.positionOS).xyz;
				positionWS.y = 0;
				positionWS += float3(0, 0.2, 0);
				float2 worldUV = positionWS.xz;

				float3 viewVector = positionWS - _WorldSpaceCameraPos;
				float viewDist = length(viewVector);

				float worldScale = 1;
				float lodScale = 1;
				float lod_c0 = min(lodScale * LengthScale0 / viewDist, 1);
				float lod_c1 = min(lodScale * LengthScale1 / viewDist, 1);
				float lod_c2 = min(lodScale * LengthScale2 / viewDist, 1);

				float3 worldUVDeviders = worldScale * float3(LengthScale0, LengthScale1, LengthScale2);

				float3 displacement = 0;
				displacement = SampleDisplacement(worldUV, worldUVDeviders, float3(lod_c0, lod_c1, lod_c2));
				positionWS += displacement;
				//Todo Dynamic displacement
				positionWS += SampleWaveDisplacement(worldUV).xyz * 0.5;

				float3 positionOS = TransformWorldToObject(positionWS);
				VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
				output.uv = input.uv;
				output.positionHCS = positionInputs.positionCS;
				output.color = input.color;
				return output;
			}
			
			half4 WaterDecalFragment(Varyings input) : SV_Target
			{
				half4 col = tex2D(_MainTex, input.uv) * tex2D(_MaskTex, input.uv) * input.color.a;
				half4 comp = col;

				return comp;
			}
			ENDHLSL
		}
	}
}
