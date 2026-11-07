using Vortice.D3DCompiler;

namespace ZoomIN.ShaderCompiler;

/// <summary>
/// Usage: ZoomIN.ShaderCompiler &lt;source folder&gt; &lt;output folder&gt; File.hlsl|Entry|profile ...
/// Writes one &lt;File.hlsl&gt;.&lt;Entry&gt;.&lt;profile&gt;.cso per shader; ZoomIN.Rendering embeds them.
/// Exit code 1 with the compiler messages on stderr when a shader does not compile.
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: ZoomIN.ShaderCompiler <source folder> <output folder> File.hlsl|Entry|profile ...");
            return 2;
        }

        var source = args[0];
        var output = args[1];
        Directory.CreateDirectory(output);
        foreach (var shader in args.Skip(2))
        {
            var parts = shader.Split('|');
            if (parts.Length != 3)
            {
                Console.Error.WriteLine($"Expected File.hlsl|Entry|profile, got '{shader}'.");
                return 2;
            }

            var (file, entry, profile) = (parts[0], parts[1], parts[2]);
            try
            {
                var code = File.ReadAllText(Path.Combine(source, file));

                // Must match the flags the shaders were tested with (OfflineRenderTests and friends).
                var bytecode = Compiler.Compile(code, entry, file, profile, ShaderFlags.OptimizationLevel3);
                File.WriteAllBytes(Path.Combine(output, $"{file}.{entry}.{profile}.cso"), bytecode.ToArray());
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"{file}({entry}, {profile}): error: {ex.Message}");
                return 1;
            }
        }

        return 0;
    }
}
