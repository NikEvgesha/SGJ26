Shader "LittlePlanet/DepthGradientWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.35, 0.9, 1.0, 1)
        _DeepColor ("Deep Color", Color) = (0.02, 0.08, 0.32, 1)
        _DepthMaxDistance ("Depth Max Distance", Float) = 1.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "DepthGradientWater"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthMaxDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.positionWS = positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = SampleSceneDepth(uv);
                float3 sceneWS = ComputeWorldSpacePosition(uv, sceneDepth, UNITY_MATRIX_I_VP);
                float3 waterCenterWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));

                float waterRadius = distance(input.positionWS, waterCenterWS);
                float surfaceRadius = distance(sceneWS, waterCenterWS);
                float waterDepth = max(0.0, waterRadius - surfaceRadius);
                float depth01 = saturate(waterDepth / max(0.0001, _DepthMaxDistance));

                half3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
