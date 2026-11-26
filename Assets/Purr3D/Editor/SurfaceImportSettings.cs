using UnityEditor;
using UnityEngine;

namespace Purr3D.Editor
{
    /// <summary>
    /// Import settings for the MaterialCraft maps in <c>Assets/Purr3D/Surfaces/Textures/</c>, written by
    /// <c>scripts/export-surfaces.py</c> (PURR3D-15).
    ///
    /// <list type="bullet">
    /// <item><c>*_normal</c>: a normal map. Imported as colour instead, every normal tilts the same way (Dante's
    /// shaded face, PURR3D-13).</item>
    /// <item><c>*_metallicsmoothness</c> and <c>*_ambientocclusion</c>: data, not colour, so read as linear.
    /// sRGB would bend the numbers on their way into the shader.</item>
    /// <item><c>*_basecolor</c>: colour, so sRGB (the default).</item>
    /// </list>
    /// All of them repeat, because the surfaces tile, and get trilinear, anisotropic filtering, because floors are
    /// seen at a glancing angle.
    /// </summary>
    public sealed class SurfaceImportSettings : AssetPostprocessor
    {
        private const string Root = "Assets/Purr3D/Surfaces/";

        // Bump to make Unity reimport the surfaces after changing these rules.
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

            importer.textureType = name.EndsWith("_normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = name.EndsWith("_basecolor");
            importer.alphaSource = name.EndsWith("_metallicsmoothness")
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
        }
    }
}
