// 적외선 투시(Request1009Plan.md §25.5 F): 다른 물체에 가려진 부분만 빛나는 테두리 색으로 그린다(ZTest Greater). 가려지지 않은 부분은 원래 재질 그대로.
Shader "TagOfChaos/XRaySilhouette"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.85, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2
        _Fill ("Fill", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha One
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _RimPower;
            half _Fill;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 viewDir : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half rim = 1.0 - saturate(abs(dot(normalize(i.normal), normalize(i.viewDir))));
                half a = saturate(_Fill + pow(rim, _RimPower));
                return fixed4(_Color.rgb, a * _Color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
