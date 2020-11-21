Shader "Custom/POM"
{
    Properties {
        // normal map texture on the material,
        // default to dummy "flat surface" normalmap
        [KeywordEnum(PLAIN, NORMAL, BUMP, POM, POM_SHADOWS)] MODE("Overlay mode", Float) = 0
        
        _NormalMap("Normal Map", 2D) = "bump" {}
        _MainTex("Texture", 2D) = "grey" {}
        _HeightMap("Height Map", 2D) = "white" {}
        _MaxHeight("Max Height", Range(0.0001, 0.02)) = 0.01
        _StepLength("Step Length", Float) = 0.000001
        _MaxStepCount("Max Step Count", Int) = 64
        
        _Reflectivity("Reflectivity", Range(1, 100)) = 0.5
    }
    
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "UnityLightingCommon.cginc"
    
    inline float LinearEyeDepthToOutDepth(float z)
    {
        return (1 - _ZBufferParams.w * z) / (_ZBufferParams.z * z);
    }

    struct v2f {
        float3 worldPos : TEXCOORD0;
        half3 worldSurfaceNormal : TEXCOORD4;
        // texture coordinate for the normal map
        float2 uv : TEXCOORD5;
        float4 clip : SV_POSITION;
        half3 tangent: TANGENT;
        half3 bitangent: BITANGENT;
        half3 wNormal: WNORMAL;
        half3 wtangent: WTANGENT;
        half3 wbitangent: WBITANGENT;
    };


    // normal map texture from shader properties
    sampler2D _NormalMap;
    sampler2D _MainTex;
    sampler2D _HeightMap;
    
    // Vertex shader now also gets a per-vertex tangent vector.
    // In Unity tangents are 4D vectors, with the .w component used to indicate direction of the bitangent vector.
    v2f vert (float4 vertex : POSITION, float3 normal : NORMAL, float4 tangent : TANGENT, float2 uv : TEXCOORD0)
    {
        v2f o;
        o.clip = UnityObjectToClipPos(vertex);
        o.worldPos = mul(unity_ObjectToWorld, vertex).xyz;
        half3 wNormal = UnityObjectToWorldNormal(normal);
        half3 wTangent = UnityObjectToWorldDir(tangent.xyz);
        
        o.uv = uv;
        half3 bitangent = cross(normal, tangent) * tangent.w;
        // ага, кончено
//        half3 M = texture(_MainTex, uv);
//        o.worldSurfaceNormal = M.x * tangent + M.y * bitangent + M.z * normal;
        o.worldSurfaceNormal = normal;
        o.tangent = tangent;
        o.bitangent = cross(normal, tangent);
        o.wNormal = wNormal;
        o.wtangent = wTangent;
        o.wbitangent = cross(wNormal, wTangent);
        // compute bitangent from cross product of normal and tangent and output it
        
        return o;
    }
    
    // The maximum depth in which the ray can go.
    uniform float _MaxHeight;
    // Step size
    uniform float _StepLength;
    // Count of steps
    uniform int _MaxStepCount;
    
    float _Reflectivity;

    void frag (in v2f i, out half4 outColor : COLOR, out float outDepth : DEPTH)
    {
        float2 uv = i.uv;
        
        float3 worldViewDir = normalize(i.worldPos.xyz - _WorldSpaceCameraPos.xyz);
//worldSurfaceNormal
//#if MODE_BUMP
        // Change UV according to the Parallax Offset Mapping
//        half3 newViewDir = normalize(float3(
//            dot(i.tangent, worldViewDir),
//            dot(i.bitangent, worldViewDir),
//            dot(i.worldSurfaceNormal, worldViewDir)));

        half3 newViewDir = -normalize(float3(
            dot(i.wtangent, worldViewDir),
            dot(i.wbitangent, worldViewDir),
            dot(i.wNormal, worldViewDir)));
        
        half height = (1-tex2D(_HeightMap, uv).x);
        // последний вектор направляет кирпичи в нужную сторону, иначе как ни умножай, не туда растут
        half2 p = (newViewDir.xy / newViewDir.z * (height * _MaxHeight)) * half2(1, -1);
        uv = uv - p;
    
//#endif   
    
        float depthDif = 0;
#if MODE_POM | MODE_POM_SHADOWS    
        // Change UV according to Parallax Occclusion Mapping

        

        
#endif
        
        float3 worldLightDir = normalize(_WorldSpaceLightPos0.xyz);
        float shadow = 0;
#if MODE_POM_SHADOWS
        // Calculate soft shadows according to Parallax Occclusion Mapping, assign to shadow
#endif

        
//#if !MODE_PLAIN
//          half3 normal = UnpackNormal(tex2D(_NormalMap, uv));

        float3 M = UnpackNormal(tex2D(_NormalMap, uv));
        half3 normal = M.x * i.wtangent + M.y * i.wbitangent + M.z * i.wNormal;
//        half3 normal = M.x * i.tangent + M.y * i.bitangent + M.z * i.worldSurfaceNormal;
//#endif
        
//        half3 normal = i.worldSurfaceNormal;
        // Diffuse lightning
        half cosTheta = max(0, dot(normal, worldLightDir));
        half3 diffuseLight = max(0, cosTheta) * _LightColor0 * max(0, 1 - shadow);
        
        // Specular lighting (ad-hoc)
        half specularLight = pow(max(0, dot(worldViewDir, reflect(worldLightDir, normal))), _Reflectivity) * _LightColor0 * max(0, 1 - shadow); 

        // Ambient lighting
        half3 ambient = ShadeSH9(half4(UnityObjectToWorldNormal(normal), 1));

        // Return resulting color
        float3 texColor = tex2D(_MainTex, uv);
        outColor = half4((diffuseLight + specularLight + ambient) * texColor, 0);
        outDepth = LinearEyeDepthToOutDepth(LinearEyeDepth(i.clip.z));
    }
    ENDCG
    
    SubShader
    {    
        Pass
        {
            Name "MAIN"
            Tags { "LightMode" = "ForwardBase" }
        
            ZTest Less
            ZWrite On
            Cull Back
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_local MODE_PLAIN MODE_NORMAL MODE_BUMP MODE_POM MODE_POM_SHADOWS
            ENDCG
            
        }
    }
}