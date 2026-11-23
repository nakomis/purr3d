// A colour per project without storing one: hash the prefix to a hue. string.GetHashCode is randomised per
// process in .NET, so it would change between runs; FNV-1a doesn't.

namespace Purr3D.Core
{
    public static class ProjectHue
    {
        /// <summary>A hue in [0, 1) for <c>Color.HSVToRGB</c>. The same prefix always gets the same hue.</summary>
        public static float For(string prefix)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var c in prefix ?? "")
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                // Low bits of FNV-1a mix poorly for short strings, so fold the high half in first.
                hash ^= hash >> 16;
                return (hash % 3600) / 3600f;
            }
        }
    }
}
