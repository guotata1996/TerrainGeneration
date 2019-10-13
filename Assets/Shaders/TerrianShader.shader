    Shader "Custom/Terrian" {
        Properties {
            _GroundColor ("Ground", color) = (0,1,0.5,0)
            _SnowColor("Snow", color) = (1,1,1,0)
            _CliffColor("Cliff", color) = (0.5,0.4,0.1,0)
            _CliffNess("CliffNess", Range(0,2)) = 1
            _WaterColor("Water", color) = (0,0.3,0.6)
            _WaterLevel("WaterLevel", 2D) = "black" {}
        }
        SubShader {
            Tags { "RenderType"="Opaque" }
            LOD 300
            
            CGPROGRAM
            #pragma surface surf BlinnPhong addshadow fullforwardshadows

            sampler2D _DispTex;
            float _Displacement;

            struct Input {
                float3 worldPos;
                float3 worldNormal;
                float2 uv_WaterLevel;
                INTERNAL_DATA
            };

            fixed4 _GroundColor, _SnowColor, _CliffColor, _WaterColor;
            float _CliffNess;
            sampler2D _WaterLevel;

            void surf (Input IN, inout SurfaceOutput o) {
                fixed4 height_color = lerp(_GroundColor, _SnowColor, saturate(IN.worldPos.y * 0.01));
                fixed4 land = lerp(_CliffColor, height_color, pow(abs(IN.worldNormal.y),_CliffNess));
                fixed4 c = lerp(land, _WaterColor, tex2D(_WaterLevel, IN.uv_WaterLevel).r);
                o.Albedo = c.rgb;
                o.Specular = 0.2;
                o.Gloss = 1.0;
            }
            ENDCG
        }
        FallBack "Diffuse"
    }