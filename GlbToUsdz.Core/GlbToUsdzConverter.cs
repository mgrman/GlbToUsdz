
using GlbToUsdz.Core;
using SharpGLTF.Schema2;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;

// usda samples https://github.com/ft-lab/sample_usd/blob/main/readme.md
// gltf samples https://github.com/KhronosGroup/glTF-Sample-Models/
// usdz samples https://developer.apple.com/augmented-reality/quick-look/

// usd spec https://openusd.org/release/spec_usdpreviewsurface.html
public static class GlbToUsdzConverter
{
    public static byte[] ConvertToUsdz(ModelRoot modelRoot)
    {
        var builder = new GlbToUsdzBuilder();
        builder.AddModel(modelRoot, Matrix4x4.Identity);

        using var ms=new MemoryStream();

        Task.Run(async () => await builder.WriteUsdzAsync(ms)).Wait();

        return ms.ToArray();
    }
}