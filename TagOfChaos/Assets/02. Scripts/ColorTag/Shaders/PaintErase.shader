Shader "ColorTag/PaintErase"
{
    Properties
    {
        _MainTex ("Canvas", 2D) = "white" {}
        _StampUV ("Stamp UV", Vector) = (0.5, 0.5, 0, 0)
        _StampRadius ("Stamp Radius", Float) = 0.02
        _StampColor ("Stamp Color", Color) = (1, 1, 1, 1) // 지우개는 색을 쓰지 않지만 PlayerPaintCanvas.ApplyStamp()가 공통으로 설정하므로 선언만 유지
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
            float4 _StampUV;
            float _StampRadius;

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

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 existing = tex2D(_MainTex, i.uv);

                float dist = distance(i.uv, _StampUV.xy);
                if (dist > _StampRadius)
                    return existing; // 스탬프 범위 밖 -> 기존 캔버스 그대로 통과

                return fixed4(0, 0, 0, 0); // 잠금 무시하고 항상 투명(미도색)으로 되돌림
            }
            ENDCG
        }
    }
}
