Shader "ColorTag/PaintStamp"
{
    Properties
    {
        _MainTex ("Canvas", 2D) = "white" {}
        _StampUV ("Stamp UV", Vector) = (0.5, 0.5, 0, 0)
        _StampRadius ("Stamp Radius", Float) = 0.02
        _StampColor ("Stamp Color", Color) = (1, 1, 1, 1)
        // 1 = 일반 붓(브러시): 이미 칠해진(알파=1) 픽셀은 건드리지 않음
        // 0 = 라운드 확정 재도색: 잠금을 무시하고 항상 덮어씀
        _RespectLock ("Respect Lock", Float) = 1
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
            float4 _StampColor;
            float _RespectLock;

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

                bool alreadyLocked = existing.a >= 0.999;
                if (_RespectLock > 0.5 && alreadyLocked)
                    return existing; // 일반 붓 모드에서는 이미 칠해진 픽셀을 보호

                return fixed4(_StampColor.rgb, 1); // 새로 칠하고 알파를 1로 고정(잠금)
            }
            ENDCG
        }
    }
}
