Shader "Apartment/Balcony River Window"
{
    Properties { _MainTex ("Shing Mun River at dusk", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            struct Vertex
            {
                float4 position : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 sightline : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Vertex v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.position);
                // Unity selects the current eye before computing the world-space sightline.
                float3 world = mul(unity_ObjectToWorld, v.position).xyz;
                o.sightline = world - _WorldSpaceCameraPos;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // The north-facing opening looks along +Z. Sample a distant view through
                // its physical aperture so head movement reveals more of the landscape.
                float3 ray = normalize(i.sightline);
                float horizontal = max(length(ray.xz), 0.0001);
                float front = ray.z / horizontal;
                float shoulder = max(0.10 - abs(front - 0.80), 0.0);
                float depth = horizontal * (max(front, 0.80) + 2.5 * shoulder * shoulder);
                float u = 0.5 + 0.4 * ray.x / depth;
                float height = (0.4 * 4.0 / 3.0) * ray.y / depth;
                float excess = max(abs(height) - 0.34, 0.0);
                float v = 0.5 + sign(height) * (min(abs(height), 0.34) + 0.16 * excess / (0.16 + excess));
                return tex2D(_MainTex, float2(u, v));
            }
            ENDCG
        }
    }
}
