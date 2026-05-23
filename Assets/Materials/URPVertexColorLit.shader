Shader "Custom/URPVertexColorLit"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Color("Color (Unity Shortcut)", Color) = (1, 1, 1, 1)
        _AmbientColor("Ambient Color", Color) = (0.28, 0.28, 0.28, 1)
        _HeadlightIntensity("Headlight Intensity", Range(0, 2)) = 0.55
        _MainLightIntensity("Main Light Intensity", Range(0, 2)) = 0.35
        _UseVertexColor("Use Vertex Color", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
            };

            float4 _BaseColor;
            float4 _AmbientColor;
            float _HeadlightIntensity;
            float _MainLightIntensity;
            float _UseVertexColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normal = normalize(IN.normalWS);
                float3 positionWS = IN.positionWS;
                
                // 1. Headlight (Light aligned with the camera view direction)
                float3 viewDir = normalize(GetCameraPositionWS() - positionWS);
                float headlight = max(0.0, dot(normal, viewDir));
                
                // 2. Standard Scene Main Light
                Light mainLight = GetMainLight();
                float diffuse = max(0.0, dot(normal, mainLight.direction));
                
                // Blend: main light + dynamic headlight + ambient
                float3 lighting = (mainLight.color * diffuse * _MainLightIntensity) + 
                                  (float3(1.0, 1.0, 1.0) * headlight * _HeadlightIntensity) + 
                                  _AmbientColor.rgb;
                
                // Retrieve color
                float4 colorSample = _BaseColor;
                if (_UseVertexColor > 0.5)
                {
                    float4 vertexColor = IN.color;
                    if (!(vertexColor.a == 0.0 && vertexColor.r == 0.0 && vertexColor.g == 0.0 && vertexColor.b == 0.0))
                    {
                        colorSample *= vertexColor;
                    }
                }
                
                return float4(colorSample.rgb * lighting, colorSample.a);
            }
            ENDHLSL
        }
    }
}
