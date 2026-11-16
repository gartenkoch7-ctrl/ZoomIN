namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// Shader bytecode compiled during the build (tools/ZoomIN.ShaderCompiler, M14 step 4) and embedded
/// in this assembly. Before, every pipeline start compiled ~570 ms of HLSL per monitor.
/// </summary>
internal static class ShaderSource
{
    /// <summary>Embedded resource name of one compiled shader.</summary>
    public static string ResourceName(string fileName, string entryPoint, string profile) =>
        $"ZoomIN.Shaders.{fileName}.{entryPoint}.{profile}.cso";

    /// <exception cref="InvalidOperationException">
    /// The shader is not listed as ShaderEntry in ZoomIN.Rendering.csproj (ShaderBytecodeTests prevents that).
    /// </exception>
    public static byte[] Load(string fileName, string entryPoint, string profile)
    {
        var name = ResourceName(fileName, entryPoint, profile);
        using var stream = typeof(ShaderSource).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Shader bytecode '{name}' not embedded; add a ShaderEntry to ZoomIN.Rendering.csproj.");
        var bytecode = new byte[stream.Length];
        stream.ReadExactly(bytecode);
        return bytecode;
    }
}
