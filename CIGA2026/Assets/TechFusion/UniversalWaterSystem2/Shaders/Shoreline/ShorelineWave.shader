Shader"TechFusion/UniversalWaterSystem/ShorelineWaveShader"
{
	Properties
	{
		_MainTex("Shore Wave", 2D) = "white" {}
		//_VariationTex("Variation", 2D) = "white" {}
		_VariationDetailTex("Variation Detail", 2D) = "white" {}

		// Blend mode values
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend mode", Float) = 0.0
		// Blend mode values
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend mode", Float) = 0.0
		// Will set "_INVERT_ON" shader keyword when set
		[Toggle] _Invert("Invert?", Float) = 0
	}

	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off
		Blend[_SrcBlend][_DstBlend]
		Cull Off
		LOD 100

		Pass
		{
			Name "Shoreline Wave"
			Tags{"LightMode" = "ShorelineDisp"}
			HLSLPROGRAM
			#pragma vertex ShorelineVertex
			#pragma fragment ShorelineFragment
			#pragma shader_feature _INVERT_ON

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				uint   vid		  : SV_VertexID;
				float3 positionOS : POSITION;
				float3 normalOS   : NORMAL;
				float4 tangentOS  : TANGENT;
				half4 color       : COLOR;
				float2 uv         : TEXCOORD0;
			};

			struct Varyings
			{
				float2 uv : TEXCOORD0;
				//half4 normal : TEXCOORD1;    // xyz: normal, w: viewDir.x
				//half4 tangent : TEXCOORD2;    // xyz: tangent, w: viewDir.y
				//half4 bitangent : TEXCOORD3;    // xyz: binormal, w: viewDir.z
				half3 waveFrontDirection : TEXCOORD1;
				half4 color : TEXCOORD4;
				float4 worldPosition : TEXCOORD5; //xyz: pos, w:alpha
				float4 vertex : SV_POSITION;
			};

			struct FragmentOutput
			{
				half4 disp : SV_Target0;
				half2 foam : SV_Target1;
			};

			Texture2D _MainTex;
			SamplerState sampler_linear_clamp;
			Texture2D<float4> _VariationDetailTex;
			float4 _VariationDetailTex_TexelSize;

			//float VariationID;
			//float Wave_Value;
			//float Wave_Phase;
			//float Wave_Amplitude;
			//float4 ShorelineVariable; //x:VariationID y:Wave_Value z:Wave_Amplitude// w:phase
			float4 WaveVariable; //x:Wave_Value y:Wave_Amplitude z:Foam Strength
			float4 VariationVariable; //x:VariationID y:UV multi z:strength
			float4 FoamSpaceVariable; //x:center y:factor z:exp
			float4 FoamTimeVariable; //x:center y:factor z:exp

			StructuredBuffer<float3> CurvePoints;
			int CurvePointCount;
			float WaveFade;
			float ShorelineWidth;

			void ShorelineMesh(uint vid, inout float3 positionOS, inout float3 curveTangent)
			{
				int curveID = vid / 2;
				float3 tangentOS = 0;
				if (curveID > 0)
					tangentOS += CurvePoints[curveID] - CurvePoints[curveID - 1];
				if (curveID < CurvePointCount - 1)
					tangentOS += CurvePoints[curveID + 1] - CurvePoints[curveID];
				tangentOS = normalize(tangentOS);

				curveTangent = float3(-tangentOS.z, 0, tangentOS.x);

				positionOS = CurvePoints[curveID] + curveTangent * ShorelineWidth * (0.5 - vid % 2);
			}

			Varyings ShorelineVertex(Attributes input)
			{
				Varyings output = (Varyings)0;

				float3 positionOS = 0;
				float3 curveTangent = 0;
				ShorelineMesh(input.vid, positionOS, curveTangent);
				VertexPositionInputs vertexPosition = GetVertexPositionInputs(positionOS);

				output.waveFrontDirection = curveTangent;
				//output.vertex = TransformWorldToHClip(positionOS); //vertexPosition.positionCS;
				output.vertex = vertexPosition.positionCS;
				output.uv = input.uv;
				output.color = input.color;
				output.worldPosition.xyz = positionOS;//vertexPosition.positionWS;
				output.worldPosition.xyz = vertexPosition.positionWS;
				output.worldPosition.w = input.tangentOS.w * WaveFade;

				return output;
			}

			//u: 01 uv
			//y: int num
			float GetVariationDetail(float u, float y)
			{
				int width = int(_VariationDetailTex_TexelSize.z); // Get the size of the texture
				float uTexCoord = u * width; // Calculate floating-point texcoords

				uint uTexCoordsFloor = uint(uTexCoord); // Calculate floor of texcoords
				uint uTexCoordsCeil = uTexCoordsFloor + 1; // Calculate ceil of texcoords

				float frac = uTexCoord - uTexCoordsFloor; // Calculate fraction

				float4 valueFloor = _VariationDetailTex.Load(uint3(uTexCoordsFloor, y, 0));
				float4 valueCeil = _VariationDetailTex.Load(uint3(uTexCoordsCeil, y, 0));

				return lerp(valueFloor, valueCeil, frac);
			}

			float2 GetShoreWaveUV(float2 ObjUV)
			{
				float u = ObjUV.x;
				float variationDetail = GetVariationDetail(frac(ObjUV.y * VariationVariable.y), VariationVariable.x);
				float detail = saturate(1 - variationDetail) * VariationVariable.z; //detail should all end at 1
				float v = saturate(WaveVariable.x * (1 + detail) - detail);
				return float2(u, v);
			}

			float Range01Flag(float t)
			{
				return max(sign(0.5 - abs(t - 0.5)), 0);
			}

			float3 GetDisplacement(float2 WaveUV, float3 tangent)
			{
				half4 dispData = _MainTex.SampleLevel(sampler_linear_clamp, float3(WaveUV, 0), 0) * 2 - 1;
				float3 rotatedTangent = TransformObjectToWorldDir(tangent);
				half3 displacement = (dispData.x * rotatedTangent + dispData.y * half3(0, 1, 0)) * WaveVariable.y;// +dispData.y * half3(0, 1, 0) * ShorelineVariable.z;
				return displacement/* * Range01Flag(WaveUV.y)*/;
			}

			float GetFoam(float2 uv)
			{
				float spaceFactor = pow((1 - saturate(abs(uv.x - FoamSpaceVariable.x) * FoamSpaceVariable.y)), FoamSpaceVariable.z);
				float timeFactor = pow((1 - saturate(abs(uv.y - FoamTimeVariable.x) * FoamTimeVariable.y)), FoamTimeVariable.z);

				return spaceFactor * timeFactor * WaveVariable.z;
			}

			FragmentOutput ShorelineFragment(Varyings input)
			{
				FragmentOutput output;

				float2 uv = GetShoreWaveUV(input.uv);
	
				float dispAlpha = input.worldPosition.w;
				output.disp = half4(GetDisplacement(uv, input.waveFrontDirection) * dispAlpha, 0);
				output.foam = half2(GetFoam(uv) * dispAlpha, 0);

				return output;
			}
			ENDHLSL
		}
	}
}
