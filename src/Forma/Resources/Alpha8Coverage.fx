#if SM6
#define VS_SHADERMODEL vs_6_0
#define PS_SHADERMODEL ps_6_0
#define PS_OUTPUT SV_TARGET
#elif OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#define PS_OUTPUT COLOR0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#define PS_OUTPUT COLOR0
#endif

float4x4 MatrixTransform;

#if SM6
Texture2D Texture : register(t0);
SamplerState TextureSampler : register(s0);
#define SAMPLE_TEXTURE(texture, sampler, coordinate) texture.Sample(sampler, coordinate)
#else
Texture2D Texture;
sampler TextureSampler = sampler_state
{
    Texture = <Texture>;
};
#define SAMPLE_TEXTURE(texture, sampler, coordinate) tex2D(sampler, coordinate)
#endif

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TextureCoordinate : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinate : TEXCOORD0;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TextureCoordinate = input.TextureCoordinate;
    return output;
}

float4 MainPS(VertexShaderOutput input) : PS_OUTPUT
{
    float4 sample = SAMPLE_TEXTURE(Texture, TextureSampler, input.TextureCoordinate);
    float coverage = sample.a < 0.999 ? sample.a : sample.r;
    return input.Color * coverage;
}

technique Alpha8Coverage
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}