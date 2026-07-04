Shader "Nate/VortexSurface"
{
    Properties
    {
        _ShallowColor   ("浅水色",   Color) = (0.05, 0.35, 0.6, 0.75)
        _DeepColor      ("深水色",   Color) = (0.0,  0.08, 0.2, 1.0)
        _FoamColor      ("泡沫色",   Color) = (0.9,  0.95, 1.0, 1.0)

        _NormalTex      ("法线贴图", 2D)    = "bump" {}
        _NormalTex2     ("法线贴图2",2D)    = "bump" {}
        _NormalStrength ("法线强度", Range(0,2)) = 0.8

        _Smoothness     ("光滑度",   Range(0,1)) = 0.92
        _Metallic       ("金属度",   Range(0,1)) = 0.0

        // 漩涡参数（由 C# 脚本每帧写入）
        _VortexAngle    ("漩涡旋转角(rad)", Float) = 0
        _VortexDepth    ("漩涡深度",        Float) = 6
        _InnerRadius    ("内径",            Float) = 4
        _OuterRadius    ("外径",            Float) = 30

        // 泡沫
        _FoamThreshold  ("泡沫阈值（深度）", Range(0,4)) = 0.8
        _FoamSpeed      ("泡沫流速",         Range(0,3)) = 1.2

        [HideInInspector] _SrcBlend ("__src", Float) = 5   // SrcAlpha
        [HideInInspector] _DstBlend ("__dst", Float) = 10  // OneMinusSrcAlpha
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent-10"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VortexSurface"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off          // 双面，内壁也可见

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // ── 属性 ────────────────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                half4  _ShallowColor;
                half4  _DeepColor;
                half4  _FoamColor;
                float  _NormalStrength;
                float  _Smoothness;
                float  _Metallic;
                float  _VortexAngle;
                float  _VortexDepth;
                float  _InnerRadius;
                float  _OuterRadius;
                float  _FoamThreshold;
                float  _FoamSpeed;
                float4 _NormalTex_ST;
                float4 _NormalTex2_ST;
            CBUFFER_END

            TEXTURE2D(_NormalTex);   SAMPLER(sampler_NormalTex);
            TEXTURE2D(_NormalTex2);  SAMPLER(sampler_NormalTex2);

            // ── 顶点输入 / 输出 ──────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float2 uv          : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float4 screenPos   : TEXCOORD5;
                // 极坐标 r（到漩涡中心距离），存在 uv2.x
                float  radialT     : TEXCOORD6;
                float  fogFactor   : TEXCOORD7;
            };

            // ── 顶点着色器 ───────────────────────────────────────────────────
            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                // 世界坐标（由 C# 写入了带凹陷的顶点，这里直接用 OS）
                VertexPositionInputs posIn = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nmIn  = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = posIn.positionCS;
                OUT.positionWS  = posIn.positionWS;
                OUT.normalWS    = nmIn.normalWS;
                OUT.tangentWS   = nmIn.tangentWS;
                OUT.bitangentWS = nmIn.bitangentWS;
                OUT.screenPos   = ComputeScreenPos(posIn.positionCS);

                // 极坐标 t：0 = 中心，1 = 外缘
                float2 flat = float2(IN.positionOS.x, IN.positionOS.z);
                float  r    = length(flat);
                OUT.radialT = saturate((r - _InnerRadius) / (_OuterRadius - _InnerRadius));

                // UV 随漩涡角旋转，越内层转越快（内层 mult=3，外层 mult=1）
                float mult     = lerp(3.0, 1.0, OUT.radialT);
                float rotAngle = _VortexAngle * mult;
                float c = cos(rotAngle), s = sin(rotAngle);
                float2 rotatedXZ = float2(
                    flat.x * c - flat.y * s,
                    flat.x * s + flat.y * c);
                OUT.uv = rotatedXZ * 0.05; // 缩放到合适 UV 频率

                OUT.fogFactor = ComputeFogFactor(posIn.positionCS.z);
                OUT.screenPos = ComputeScreenPos(posIn.positionCS);
                return OUT;
            }

            // ── 工具：螺旋UV（沿切线方向平移，产生漩涡流动感）────────────
            float2 SpiralUV(float2 baseUV, float radialT, float time, float speed)
            {
                float swirl = (1.0 - radialT) * 2.0; // 中心更扭曲
                return baseUV + float2(swirl * speed * time * 0.03,
                                       swirl * speed * time * 0.02);
            }

            // ── 片元着色器 ───────────────────────────────────────────────────
            half4 Frag(Varyings IN) : SV_Target
            {
                float  t    = IN.radialT;        // 0=中心,1=边缘
                float  time = _Time.y;

                // ── 法线采样（两层叠加 + 时间偏移）
                float2 uv1 = SpiralUV(IN.uv,               t, time,  _FoamSpeed);
                float2 uv2 = SpiralUV(IN.uv * 1.4 + 0.37, t, time, -_FoamSpeed * 0.7);

                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex,  sampler_NormalTex,  uv1), _NormalStrength);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex2, sampler_NormalTex2, uv2), _NormalStrength * 0.6);
                half3 nm = normalize(n1 + n2);

                // 切线空间 → 世界空间
                float3x3 tbn    = float3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                float3   nWS    = normalize(mul(nm, tbn));

                // ── 深度差：用于泡沫 & 透明度
                // 用屏幕坐标采样场景深度
                float2 screenUV  = IN.screenPos.xy / IN.screenPos.w;
                float  sceneRawZ = SampleSceneDepth(screenUV);
                float  sceneZ    = LinearEyeDepth(sceneRawZ, _ZBufferParams);
                float  surfZ     = IN.screenPos.w;
                float  depthDiff = sceneZ - surfZ;

                // ── 颜色混合：浅水 / 深水
                float depthT = saturate(depthDiff * 0.15 + (1.0 - t) * 0.5);
                half4 waterColor = lerp(_ShallowColor, _DeepColor, depthT);

                // ── 泡沫：靠近内壁 + 靠近外缘时出现
                float foamInner  = 1.0 - smoothstep(0.0, 0.18, t);           // 中心边缘
                float foamOuter  = smoothstep(0.75, 1.0, t);                  // 外缘
                float foamDepth  = 1.0 - smoothstep(0.0, _FoamThreshold, depthDiff); // 交界线
                float foamMask   = saturate(foamInner + foamOuter + foamDepth * 0.4);

                // 泡沫纹理：用法线 x 分量模拟细碎泡沫
                float foamNoise  = saturate(n1.x * 0.5 + 0.5 + n2.y * 0.3);
                float foam       = foamMask * foamNoise;

                half4 color = lerp(waterColor, _FoamColor, foam * 0.85);

                // ── 透明度：中心更透明（看起来像深渊）
                color.a = lerp(0.5, waterColor.a, t);

                // ── PBR 光照（简化：主光高光）
                Light mainLight = GetMainLight();
                float3 viewDir  = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 halfDir  = normalize(mainLight.direction + viewDir);
                float  NdotH    = saturate(dot(nWS, halfDir));
                float  spec     = pow(NdotH, 64.0 * _Smoothness + 4.0) * _Smoothness;
                color.rgb += mainLight.color * spec * 0.6;

                // 漫反射
                float NdotL = saturate(dot(nWS, mainLight.direction));
                color.rgb  *= (NdotL * 0.5 + 0.5);   // 半兰伯特，内壁也有光

                // 雾
                color.rgb = MixFog(color.rgb, IN.fogFactor);

                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
