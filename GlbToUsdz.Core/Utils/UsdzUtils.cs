using System.Numerics;

namespace GlbToUsdz.Core.Utils;

internal static class UsdzUtils
{
    internal static string ToUsdString(this IEnumerable<Vector3> vertices) => $"[{string.Join(",", vertices.Select(ToUsdString))}]";
   internal static string ToUsdString(this IEnumerable<Vector2> vertices) => $"[{string.Join(",", vertices.Select(ToUsdString))}]";
    internal static string ToUsdString(this IEnumerable<uint> indices) => $"[{string.Join(", ", indices)}]";

    internal static string ToUsdString(this Vector3 p) => $"({p.X:F7}, {p.Y:F7}, {p.Z:F7})";
    internal static string ToUsdString(this Vector2 p) => $"({p.X:F7}, {p.Y:F7})";
    internal static string ToXYZUsdString(this Vector4 p) => $"({p.X:F7}, {p.Y:F7}, {p.Z:F7})";
    internal static string ToUsdString(this Matrix4x4 m) => $"( ({m.M11}, {m.M12}, {m.M13}, {m.M14}), ({m.M21}, {m.M22}, {m.M23}, {m.M24}), ({m.M31}, {m.M32}, {m.M33}, {m.M34}), ({m.M41}, {m.M42}, {m.M43}, {m.M44}) )";
}
