// Unlit vertex-colour sky dome / sun disc. Drawn behind everything, no fog.
Shader "BB/Sky"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Background+10" "RenderType"="Background" "IgnoreProjector"="True" }
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.col = v.color * _Color; return o; }
            fixed4 frag (v2f i) : SV_Target { return fixed4(i.col.rgb, 1); }
            ENDCG
        }
    }
}
