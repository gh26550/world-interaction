Shader "WorldInteraction/AnisotropicGaussian"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION;float3 scale:NORMAL;float4 rotation:TANGENT;float4 color:COLOR;float2 corner:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
            float3 rotateQ(float3 v,float4 q){return v+2*cross(q.xyz,cross(q.xyz,v)+q.w*v);}
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p=mul(UNITY_MATRIX_MV,v.vertex).xyz;
                float4 clipPos=mul(UNITY_MATRIX_P,float4(p,1));
                float z=max(-p.z,0.001);
                float3 jx=float3(UNITY_MATRIX_P._m00/z,0,UNITY_MATRIX_P._m00*p.x/(z*z));
                float3 jy=float3(0,UNITY_MATRIX_P._m11/z,UNITY_MATRIX_P._m11*p.y/(z*z));
                float3 ax=mul((float3x3)UNITY_MATRIX_MV,rotateQ(float3(v.scale.x,0,0),v.rotation));
                float3 ay=mul((float3x3)UNITY_MATRIX_MV,rotateQ(float3(0,v.scale.y,0),v.rotation));
                float3 az=mul((float3x3)UNITY_MATRIX_MV,rotateQ(float3(0,0,v.scale.z),v.rotation));
                float3 dx=float3(dot(jx,ax),dot(jx,ay),dot(jx,az));
                float3 dy=float3(dot(jy,ax),dot(jy,ay),dot(jy,az));
                float a=dot(dx,dx)+1e-8,b=dot(dx,dy),c=dot(dy,dy)+1e-8;
                float l00=sqrt(a),l10=b/l00,l11=sqrt(max(c-l10*l10,1e-8));
                float2 offset=float2(l00*v.corner.x,l10*v.corner.x+l11*v.corner.y);
                clipPos.xy+=offset*clipPos.w;
                if(p.z>=-0.01)clipPos=float4(2,2,2,1);
                o.pos=clipPos;o.uv=v.corner;o.color=v.color;return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float alpha=i.color.a*exp(-0.5*dot(i.uv,i.uv));clip(alpha-0.0039);
                return float4(i.color.rgb,min(alpha,.99));
            }
            ENDCG
        }
    }
}
