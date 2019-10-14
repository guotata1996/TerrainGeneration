    Shader "Custom/Terrian" {
        Properties {
            _GroundColor ("Ground", color) = (0,1,0.5,0)
            _RockColor("Rock", color) = (1,1,1,0)
            _CliffColor("Cliff", color) = (0.5,0.4,0.1,0)
            _WaterColor("Water", color) = (0,0.3,0.5)
            _SnowColor("Snow", color) = (1,1,1,0)
            _LakeColor("Lake", color) = (0, 0.1, 0.8)
            _WaterLevel("WaterLevel", 2D) = "black" {}
            _Lakes("Lakes", 2D) = "black" {}
            _CliffNess("CliffNess", Range(0,2)) = 1
            _RockLine("rockline", Range(0,1)) = 0.62
            _glacierSnowLine("loSnowline", Range(0,1)) = 0.7
            _normalSnowLine("hiSnowline", Range(0,1)) = 0.76
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
                float2 uv_Lakes;
                INTERNAL_DATA
            };

            fixed4 _GroundColor, _RockColor, _CliffColor, _WaterColor, _LakeColor, _SnowColor;
            float _CliffNess, _RockLine, _glacierSnowLine, _normalSnowLine;
            sampler2D _WaterLevel, _Lakes;

            void surf (Input IN, inout SurfaceOutput o) {
                float h = IN.worldPos.y * 0.01;
                if (tex2D(_Lakes, IN.uv_Lakes).r > 0.01){
                    float3 surroundingColor = lerp(_WaterColor, _SnowColor, saturate((h - _glacierSnowLine) / (_normalSnowLine - _glacierSnowLine)));
                    float3 centerColor = lerp(_LakeColor, _SnowColor, saturate((h - _glacierSnowLine) / (_normalSnowLine - _glacierSnowLine)));
                    float3 depthColor = lerp(surroundingColor, centerColor, saturate((tex2D(_Lakes, IN.uv_Lakes).g - h) * 50));
                    o.Albedo = depthColor;
                    o.Specular = 0.2;
                    o.Gloss = 1.0;
                }
                else{
                    float waterlevel = tex2D(_WaterLevel, IN.uv_WaterLevel).r;
                    fixed4 height_color = lerp(_GroundColor, _RockColor, saturate(h / _RockLine));
                    fixed4 land = lerp(_CliffColor, height_color, pow(abs(IN.worldNormal.y),_CliffNess));

                    float snowLineByState = lerp(_normalSnowLine, _glacierSnowLine, waterlevel);  // if waterlevel is high, snow line drops
                    fixed4 waterOrSnowColor = lerp(_WaterColor, _SnowColor, saturate((h - _glacierSnowLine) / (snowLineByState - _glacierSnowLine)));
                    fixed4 c = lerp(land, waterOrSnowColor, waterlevel); //TODO: offset waterlevel uv
                    o.Albedo = c.rgb;
                    o.Specular = 0.2;
                    o.Gloss = waterlevel;
                }
            }
            ENDCG
        }
        FallBack "Diffuse"
    }