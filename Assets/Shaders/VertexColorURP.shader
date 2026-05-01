Shader "SGJ26/Terraforming/Vertex Color URP"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation", Range(0, 2)) = 1.2
        _Contrast ("Contrast", Range(0, 2)) = 1.12
        _Exposure ("Exposure", Range(0, 2)) = 1.05
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.55
        _LightStrength ("Light Strength", Range(0, 2)) = 1.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Saturation;
                half _Contrast;
                half _Exposure;
                half _AmbientStrength;
                half _LightStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half3 ambient = SampleSH(normalize(input.normalWS)) * _AmbientStrength;
                half3 direct = mainLight.color * (0.25h + ndotl * 0.85h) * _LightStrength;
                half3 color = input.color.rgb * (ambient + direct) * _Exposure;
                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                color = lerp(luminance.xxx, color, _Saturation);
                color = (color - 0.5h) * _Contrast + 0.5h;
                return half4(saturate(color), input.color.a);
            }
            ENDHLSL
        }
    }
}
