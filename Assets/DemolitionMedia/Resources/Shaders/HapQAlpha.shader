Shader "DemolitionMedia/HapQAlpha"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_AlphaTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			#define UNITY_COLORSPACE_LINEAR !UNITY_COLORSPACE_GAMMA

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
			
			sampler2D _MainTex;
			sampler2D _AlphaTex;

			float4 frag (v2f i) : SV_Target
			{
				float4 CoCgSY = tex2D(_MainTex, i.uv);
				float alpha = tex2D(_AlphaTex, i.uv).r;

				CoCgSY += float4(-0.50196078431373, -0.50196078431373, 0.0, 0.0);
    
				float scale = (CoCgSY.z * (255.0 / 8.0)) + 1.0;
    
				float Co = CoCgSY.x / scale;
				float Cg = CoCgSY.y / scale;
				float Y = CoCgSY.w;

				float3 rgb = float3(Y + Co - Cg, Y + Cg, Y - Co - Cg);
#if UNITY_COLORSPACE_LINEAR
				rgb = GammaToLinearSpace(rgb);
#endif

				return float4(rgb, alpha);
			}
			ENDCG
		}
	}
}
