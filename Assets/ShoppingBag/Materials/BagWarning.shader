Shader "XRGroup10/ShoppingBag/ContactWarning"
{
    Properties { _Color ("Warning color", Color) = (1,0.015,0.005,1) }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" }
        Pass
        {
            // Draw the same mesh after its base material; depth bias avoids coplanar flicker.
            // Depth test preserves real occlusion. No scale inflation or geometry obstruction.
            Cull Back ZWrite Off ZTest LEqual Offset -1, -1 Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); return o;
            }
            fixed4 frag(v2f i) : SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return _Color; }
            ENDCG
        }
    }
}
