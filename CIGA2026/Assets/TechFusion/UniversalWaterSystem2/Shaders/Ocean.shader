Shader "TechFusion/UniversalWaterSystem/Ocean"
{
    Properties
    {
        //_FoamAlbedo("Foam", 2D) = "white" {}
		//_FoamBubble("Bubble", 2D) = "white" {}
        //_ContactFoamTexture("Contact Foam", 2D) = "white" {}
    }

    SubShader
    {
        Pass
        {
			Tags { "LightMode" = "OceanMesh" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OceanMainVert
            #pragma fragment OceanMainFrag

			#pragma multi_compile_fog
			#pragma multi_compile _ MID CLOSE

			#define _REFLECTION_PLANARREFLECTION
			#define _MAIN_LIGHT_SHADOWS
			#define _MAIN_LIGHT_SHADOWS_CASCADE
			//#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
		
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			
		
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float viewDepth     : TEXCOORD1;
                float4 positionNDC  : TEXCOORD2;
                float2 worldUV      : TEXCOORD3;
                #ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
                float4 shadowCoord  : TEXCOORD4;
                #endif
				float4 additionalData: TEXCOORD5;	// x = distance to surface, y = distance to surface
				float4 lodScales	: TEXCOORD6;
				float3 worldUVDeviders : TEXCOORD7;
            };

			#include "Sampling.hlsl"
			#include "Cascade.hlsl"
			#include "Foam.hlsl"
			#include "Surface.hlsl"

            Varyings OceanMainVert(Attributes input)
            {
                Varyings output;

				output.positionWS = mul(unity_ObjectToWorld, input.positionOS).xyz;
				output.worldUV = output.positionWS.xz;

				float3 viewVector = output.positionWS - _WorldSpaceCameraPos;
				float viewDist = length(viewVector);
	
				float lod_c0 = min(LengthScale0 / viewDist, 1);
				float lod_c1 = min(LengthScale1 / viewDist, 1);
				float lod_c2 = min(LengthScale2 / viewDist, 1);

				output.worldUVDeviders = float3(LengthScale0, LengthScale1, LengthScale2);

				float3 displacement = 0;
				float largeWavesBias = 0;

				displacement = SampleDisplacement(output.worldUV, output.worldUVDeviders, float3(lod_c0, lod_c1, lod_c2));

				largeWavesBias = displacement.y;
				output.lodScales = float4(lod_c0, lod_c1, lod_c2, (max((displacement.y + _SSSBase) * _SSSScale, 0)) * _SSSStrength);

				output.positionWS += displacement;

				half4 screenUV = ComputeScreenPos(TransformWorldToHClip(output.positionWS));
				screenUV.xyz /= screenUV.w;

				//Todo Dynamic displacement
				output.positionWS += SampleWaveDisplacement(output.worldUV).xyz * 0.5;

				float3 positionOS = TransformWorldToObject(output.positionWS);
				VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
				output.viewDepth = -positionInputs.positionVS.z;
				output.positionNDC = positionInputs.positionNDC;
				output.positionHCS = positionInputs.positionCS;
#ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
				output.shadowCoord = GetShadowCoord(positionInputs);
#endif
				output.additionalData = AdditionalData(output.positionWS, output.lodScales.w);
				return output;
            }

			half4 OceanMainFrag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
			{
				half2 screenUV = input.positionNDC.xy / input.positionNDC.w;
				bool isUnderwater = !isFrontFace;
	
				float3 worldNormal = SampleNormal(input.worldUV, input.worldUVDeviders, input.lodScales.xyz);
				worldNormal += SampleWaveNormal(input.worldUV, 0).xyz;
				worldNormal = normalize(worldNormal);
				float3 foamLODScales = 1;//input.lodScales.xyz; //todo

				float3 viewDir = _WorldSpaceCameraPos - input.positionWS;
				float viewDist = length(viewDir);
				viewDir = viewDir / viewDist;
	
				float coverage = SampleFoamCoverage(input.worldUV, input.worldUVDeviders, foamLODScales) + ContactFoam(input.worldUV, screenUV, input.viewDepth);
				//coverage = saturate(coverage);
				
				float normalFadeFactor = 0;// saturate(viewDist / _NormalFadeFar);
				float3 normal = lerp(worldNormal, float3(0, 1, 0), normalFadeFactor);
				
				float foamFadeFactor = saturate(viewDist / _FoamFadeFar);
				coverage = lerp(coverage, 0, foamFadeFactor);
	
				float2 waveFoam = SampleWaveFoam(input.worldUV);
				//coverage += waveFoam.x;
	
				half4 foam = float4(GetFoamAlbedo(input.worldUV, coverage, waveFoam.x), 1);
				float4 oceanColor = WaterShading(screenUV, input.positionWS, 
									#ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
									input.shadowCoord,
									#endif
									normal, viewDir, input.additionalData, foam, isUnderwater);

				// Fog
				float viewZ = input.viewDepth;
				if (!isUnderwater)
				{
					//todo : atmospheric fog
					#if (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
					float nearToFarZ = max(viewZ - _ProjectionParams.y, 0);
					half fogFactor = ComputeFogFactorZ0ToFar(nearToFarZ);
					#else
					half fogFactor = 0;
					#endif

					oceanColor.rgb = MixFog(oceanColor.rgb, fogFactor);
				}
				else
				{
					// underwater
					Light mainLight = GetMainLight();
					float3 fogColor = UnderwaterFogColor(viewDir, mainLight.direction, _WorldSpaceCameraPos.y);
					oceanColor.rgb = UnderwaterColor(fogColor, oceanColor.rgb, viewDist);
				}

				return oceanColor;
			}
            ENDHLSL
        }
    }
}