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

			// ── 漩涡参数（最多4个，由 WaterVortex.cs 统一写入）──────────
			float4 _VortexParamsArr[4]; // xy=中心XZ, z=outerRadius, w=innerRadius
			float4 _VortexAnimArr[4];  // x=深度(m), y=旋转角(rad), z=方向(1/-1), w=激活
			int    _VortexCount;

			// ── 海啸参数（由 Tsunami.cs 每帧写入）────────────────────────
			float4 _TsunamiParams; // x=波前投影, y=浪高, z=波宽, w=阶段
			float4 _TsunamiDir;   // x=dirX, y=dirZ, z=退潮深度, w=激活强度
			float4 _TsunamiAnim;  // x=已行进, y=总行进, z=衰减t, w=时间

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

				// ── 漩涡顶点位移（多实例循环）─────────────────────────────
				for (int vi = 0; vi < _VortexCount; vi++)
				{
					float4 vParams = _VortexParamsArr[vi];
					float4 vAnim   = _VortexAnimArr[vi];
					if (vAnim.w < 0.001) continue;

					float2 toV    = output.worldUV - vParams.xy;
					float  dist   = max(length(toV), 0.001);
					float  outer  = max(vParams.z, 0.001);
					float  depth  = vAnim.x;
					float  active = vAnim.w;

					float radialT = saturate(dist / outer);
					float fade    = smoothstep(1.05, 0.65, radialT);
					float funnelT = pow(max(1.0 - radialT, 0.0), 2.0);

					float baseAngle  = atan2(toV.y, toV.x) + vAnim.y * vAnim.z;
					float ridgeK     = 6.28318 * 4.0 / outer;
					float ridgePhase = baseAngle - dist * ridgeK;
					float ridgeWave  = sin(ridgePhase) * 0.5 + 0.5;
					float ridgeEnv   = pow(max(1.0 - radialT, 0.0), 0.6) * fade;

					output.positionWS.y  += (-funnelT * depth * fade + ridgeWave * ridgeEnv * depth * 0.08) * active;

					float2 radial2  = toV / dist;
					float2 tangent2 = float2(-radial2.y, radial2.x) * vAnim.z;
					output.positionWS.xz += tangent2 * funnelT * fade * depth * 0.45 * active;
				}

				// ── 海啸顶点位移 ───────────────────────────────────────────
				if (_TsunamiDir.w > 0.001)
				{
					float2 dir2      = normalize(_TsunamiDir.xy);
					float  proj      = dot(output.positionWS.xz, dir2);
					float  waveFront = _TsunamiParams.x;
					float  halfW     = max(_TsunamiParams.z * 0.5, 0.001);
					float  waveH     = _TsunamiParams.y;
					float  withdrawY = -_TsunamiDir.z;

					float distToFront = proj - (waveFront - halfW * 0.5);
					float gaussian    = exp(-pow(distToFront / halfW, 2.0));
					float waveY       = waveH * gaussian;
					float ahead       = saturate((waveFront - proj) / max(halfW, 0.001));
					waveY *= (1.0 - ahead * 0.7);

					output.positionWS.y  += (withdrawY + waveY) * _TsunamiDir.w;

					float slope = -distToFront / (halfW * halfW) * 2.0 * gaussian;
					output.positionWS.xz += dir2 * slope * waveH * 0.3 * _TsunamiDir.w;
				}

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

				// ── 漩涡法线 + 泡沫（多实例循环）────────────────────────────
				for (int vi2 = 0; vi2 < _VortexCount; vi2++)
				{
					float4 vParams = _VortexParamsArr[vi2];
					float4 vAnim   = _VortexAnimArr[vi2];
					if (vAnim.w < 0.001) continue;

					float2 toV    = input.positionWS.xz - vParams.xy;
					float  dist   = max(length(toV), 0.001);
					float  outer  = max(vParams.z, 0.001);
					float  depth  = vAnim.x;
					float  active = vAnim.w;
					float  radialT = saturate(dist / outer);
					float  fade2   = smoothstep(1.05, 0.65, radialT);

					float baseAngle2  = atan2(toV.y, toV.x) + vAnim.y * vAnim.z;
					float ridgeK2     = 6.28318 * 4.0 / outer;
					float ridgePhase  = baseAngle2 - dist * ridgeK2;
					float ridgeWave   = sin(ridgePhase);
					float ridgePhase2 = baseAngle2 * 1.7 - dist * ridgeK2 * 1.3;
					float ridgeCombined = saturate((ridgeWave + sin(ridgePhase2) * 0.4) * 0.37 + 0.5);
					float ridgeEnv = pow(max(1.0 - radialT, 0.0), 0.5) * fade2;

					float2 radialDir  = toV / dist;
					float2 tangentDir = float2(-radialDir.y, radialDir.x) * vAnim.z;

					float funnelSlope = 2.0 * (1.0 - radialT) * depth / outer;
					normal.xz += radialDir  * funnelSlope          * active * fade2 * 0.4;
					normal.xz += tangentDir * cos(ridgePhase)      * ridgeEnv * 1.5 * active;
					normal.xz += radialDir  * ridgeWave            * ridgeEnv * 0.8 * active;

					coverage += smoothstep(0.72, 0.90, ridgeCombined) * ridgeEnv * active * 0.7;
					coverage += smoothstep(0.82, 0.90, radialT)
					          * smoothstep(0.95, 0.88, radialT) * active * fade2 * 0.25;
				}
				normal = normalize(normal);

				// ── 海啸泡沫 ───────────────────────────────────────────────
				if (_TsunamiDir.w > 0.001)
				{
					float2 dir2      = normalize(_TsunamiDir.xy);
					float  proj      = dot(input.positionWS.xz, dir2);
					float  waveFront = _TsunamiParams.x;
					float  halfW     = max(_TsunamiParams.z * 0.5, 0.001);

					float distToFront = proj - (waveFront - halfW * 0.5);
					float gaussian    = exp(-pow(distToFront / halfW, 2.0));

					float crestFoam = smoothstep(0.4, 0.9, gaussian) * _TsunamiDir.w * 6.0;
					float breakFoam = smoothstep(0.0, halfW * 0.3, distToFront)
					                * smoothstep(halfW * 1.5, halfW * 0.6, distToFront)
					                * _TsunamiDir.w * 4.0;
					coverage += crestFoam + breakFoam;
				}

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
