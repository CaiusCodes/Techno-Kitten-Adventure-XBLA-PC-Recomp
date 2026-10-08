using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class EffectCompiler
{
    public static string[] CompilerPrefixArguments { get; set; } = [];
    private static readonly string[] BodyHashes = [
        "F3D88F8E896A96AF42D1E52DDF46338E3C8FD58261BDB19B3F1ED2CC2F2C344D",
        "4BD02AA92DCC31AE5EB6D6122724161C30E6209E159A2BE44531E7A0B67EF28C",
        "EF5CC9464CDCE9B12F8E32DEBC649B68AD82ABB17B0C5C46CD5A339E04060A3B",
        "3A1ADCFBB7FF010E28DEE27C81DAE267CA58A4EF3FFE3C15C0DD3D20F0137D77",
        "EF4D6B6FC101A2FC4A472CAB6888B3F6CC31273EE9BEEBE5F76D83C5CFF33C75"];

    public static int Compile(string bodies, string translated, string compiler, string output)
    {
        if (Directory.Exists(output)) throw new IOException("Output already exists.");
        Directory.CreateDirectory(output);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(translated, "shader-manifest.json")));
        var report = new List<object>();
        for (var index = 0; index < 5; index++)
        {
            var name = "effect" + index;
            var original = File.ReadAllBytes(Path.Combine(bodies, name + ".xnb.body"));
            if (Convert.ToHexString(SHA256.HashData(original)) != BodyHashes[index]) throw new InvalidDataException("Unsupported original effect.");
            var metadata = manifest.RootElement[index];
            if (metadata.GetProperty("file").GetString() != name + ".xnb.body") throw new InvalidDataException("Manifest order mismatch.");
            var effectOffset = metadata.GetProperty("effect_body_offset").GetInt32();
            var effect = original.AsSpan(effectOffset).ToArray();
            var declarationEnd = checked(24 + (int)U32(effect, 20));
            var raw = File.ReadAllText(Path.Combine(translated, name + ".xenos.hlsl"));
            var main = raw.IndexOf("void main(", StringComparison.Ordinal);
            if (main < 0) throw new InvalidDataException("Missing translated shader main.");
            var code = raw[raw.IndexOf('{', main)..];
            var declarations = new StringBuilder();
            var defaults = new List<object>();
            foreach (var parameter in metadata.GetProperty("parameters").EnumerateArray())
            {
                var parameterName = parameter.GetProperty("name").GetString()!;
                if (parameter.GetProperty("register_set").GetInt32() == 3)
                {
                    if (parameterName != "TextureSampler") throw new InvalidDataException("Unsupported sampler.");
                    continue;
                }
                var columns = parameter.GetProperty("columns").GetInt32();
                if (parameter.GetProperty("rows").GetInt32() != 1 || columns is < 1 or > 4) throw new InvalidDataException("Unsupported parameter shape.");
                var needle = Encoding.ASCII.GetBytes(parameterName + '\0');
                var at = effect.AsSpan(24, declarationEnd - 24).IndexOf(needle) + 24;
                var valueAt = at - 4 - 4 * columns;
                if (at < 24 || U32(effect, at - 4) != needle.Length || U32(effect, valueAt - 8) != columns || U32(effect, valueAt - 4) != 1 || U32(effect, valueAt - 28) != 3)
                    throw new InvalidDataException("Cannot verify original effect default: " + parameterName);
                var values = Enumerable.Range(0, columns).Select(i => BitConverter.Int32BitsToSingle((int)U32(effect, valueAt + i * 4))).ToArray();
                var type = columns == 1 ? "float" : "float" + columns;
                declarations.AppendLine($"{type} {parameterName} = {type}({string.Join(", ", values.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))});");
                var padded = columns == 4 ? parameterName : $"float4({parameterName}, {string.Join(", ", Enumerable.Repeat("0.0", 4 - columns))})";
                code = Regex.Replace(code, @"\b" + Regex.Escape(parameterName) + @"\b", "(" + padded + ")");
                defaults.Add(new { name = parameterName, effect_value_offset = valueAt, values });
            }
            // Xenos has a loop-register stack. Distinct local counters preserve
            // nesting even in older HLSL compilers with legacy for-loop scoping.
            var loopIndex = 0;
            code = Regex.Replace(code, @"for \(aL = 0; aL < (i\d+\.x); aL\+\+\)", match =>
            {
                var counter = "tkaLoop" + loopIndex++;
                return $"for (int {counter} = 0; {counter} < {match.Groups[1].Value}; {counter}++)";
            });
            code = code.Replace("int aL = 0;", "", StringComparison.Ordinal);
            if (Regex.IsMatch(code, @"\baL\b")) throw new InvalidDataException("Unsupported dynamic Xbox loop-register use.");
            // D3D11 links registers as well as semantics. MonoGame SpriteEffect
            // outputs position v0, color v1, UV v2.xy. XenosRecomp's generic
            // declaration orders UV before color; retaining that order samples
            // a constant vertex color as UV and collapses the stage to one color.
            if (!code.Contains("float4 r0 = iTexCoord0;", StringComparison.Ordinal)) throw new InvalidDataException("Unexpected shader UV input initialization.");
            code = code.Replace("float4 r0 = iTexCoord0;", "float4 r0 = float4(iTexCoord0, 0.0, 0.0);", StringComparison.Ordinal);
            var source = Header + declarations + "\nvoid main(in float4 iPos : SV_Position, in float4 iColor0 : COLOR0, in float2 iTexCoord0 : TEXCOORD0, out float4 oC0 : SV_Target0)\n" + code + "\ntechnique OriginalEffect { pass Pass1 { PixelShader = compile ps_4_0 main(); } }\n";
            var fx = Path.GetFullPath(Path.Combine(output, name + ".fx"));
            var mgfx = Path.GetFullPath(Path.Combine(output, name + ".mgfxo"));
            File.WriteAllText(fx, source);
            var start = new ProcessStartInfo(Path.GetFullPath(compiler)) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in CompilerPrefixArguments) start.ArgumentList.Add(argument);
            start.ArgumentList.Add(fx); start.ArgumentList.Add(mgfx); start.ArgumentList.Add("/Profile:DirectX_11");
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Console.Write(stdout.GetAwaiter().GetResult()); Console.Error.Write(stderr.GetAwaiter().GetResult());
            if (process.ExitCode != 0) throw new InvalidOperationException("Effect compiler failed: " + name);
            var compiled = File.ReadAllBytes(mgfx);
            using var target = new BinaryWriter(File.Create(Path.Combine(output, name + ".xnb")));
            target.Write(Encoding.ASCII.GetBytes("XNBw")); target.Write((byte)5); target.Write((byte)1);
            target.Write(10 + effectOffset + compiled.Length);
            target.Write(original, 0, effectOffset - 4); target.Write(compiled.Length); target.Write(compiled);
            report.Add(new { name, original_body_sha256 = BodyHashes[index], translated_sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))),
                compiled_sha256 = Convert.ToHexString(SHA256.HashData(compiled)), input_layout = "SV_Position=v0.xyzw; COLOR0=v1.xyzw; TEXCOORD0=v2.xy", defaults });
        }
        File.WriteAllText(Path.Combine(output, "conversion-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
    // Generic D3D11 bindings only. The actual shader instructions and constants
    // are generated from the user's package into private storage, never shipped.
    private const string Header = """
        #define FLT_MIN asfloat(0xff7fffff)
        #define FLT_MAX asfloat(0x7f7fffff)
        #define SPEC_CONSTANT_ALPHA_TEST 1
        #define g_AlphaThreshold 0.0
        #define g_SpecConstants() 0
        #define TextureSampler_Texture2DDescriptorIndex 0
        #define TextureSampler_SamplerDescriptorIndex 0
        struct CubeMapData { float unused; };
        Texture2D SpriteTexture;
        sampler TextureSampler = sampler_state { Texture = <SpriteTexture>; };
        float4 tfetch2D(uint resourceIndex, uint samplerIndex, float2 uv, float2 pixelOffset)
        {
            uint width, height;
            SpriteTexture.GetDimensions(width, height);
            return SpriteTexture.Sample(TextureSampler, uv + pixelOffset / float2(width, height));
        }
        float select(bool condition, float yesValue, float noValue) { return condition ? yesValue : noValue; }

        """;
}
