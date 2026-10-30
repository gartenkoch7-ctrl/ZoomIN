using Vortice.D3DCompiler;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// Compiles embedded HLSL at startup with the D3DCompiler that ships with Windows.
/// Takes a few milliseconds and needs no SDK tools in the build.
/// </summary>
internal static class ShaderSource
{
    public static ReadOnlyMemory<byte> Compile(string fileName, string entryPoint, string profile)
    {
        var source = Load(fileName);
        return Compiler.Compile(source, entryPoint, fileName, profile, ShaderFlags.OptimizationLevel3);
    }

    private static string Load(string fileName)
    {
        var name = "ZoomIN.Shaders." + fileName;
        using var stream = typeof(ShaderSource).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded shader '{name}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
