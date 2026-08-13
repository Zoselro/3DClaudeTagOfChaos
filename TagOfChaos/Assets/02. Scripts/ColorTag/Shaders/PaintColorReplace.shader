Shader "ColorTag/PaintColorReplace"
{
    Properties
    {
        _MainTex ("Canvas", 2D) = "white" {}
        _OldColor ("Old Color", Color) = (1, 1, 1, 1)
        _NewColor ("New Color", Color) = (1, 1, 1, 1)
        _Tolerance ("Match Tolerance", Float) = 0.01
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _OldColor;
            float4 _NewColor;
            float _Tolerance;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 술래 전용: 캔버스 전체에서 원래 슬롯 색과 같은 픽셀만 변형 색으로 치환 (6.3)
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 existing = tex2D(_MainTex, i.uv);

                float d = distance(existing.rgb, _OldColor.rgb);
                if (d <= _Tolerance)
                    return fixed4(_NewColor.rgb, existing.a);

                return existing;
            }
            ENDCG
        }
    }
}
