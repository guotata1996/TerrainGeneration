Shader "Unlit/SmoothenShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float4 c1 = tex2D(_MainTex, i.uv + float2(-0.002, -0.002));
                float4 c2 = tex2D(_MainTex, i.uv + float2(-0.002, 0));
                float4 c3 = tex2D(_MainTex, i.uv + float2(-0.002, 0.002));
                float4 c4 = tex2D(_MainTex, i.uv + float2(0, -0.002));
                float4 c5 = tex2D(_MainTex, i.uv + float2(0, 0));
                float4 c6 = tex2D(_MainTex, i.uv + float2(0, 0.002));
                float4 c7 = tex2D(_MainTex, i.uv + float2(0.002, -0.002));
                float4 c8 = tex2D(_MainTex, i.uv + float2(0.002, 0));
                float4 c9 = tex2D(_MainTex, i.uv + float2(0.002, 0.002));
                return (c1+c2+c3+c4+c5*2+c6+c7+c8+c9)*0.1;
            }
            ENDCG
        }
    }
}
