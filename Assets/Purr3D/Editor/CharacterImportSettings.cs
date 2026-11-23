using UnityEditor;

namespace Purr3D.Editor
{
    /// <summary>
    /// Import settings for everything under <c>Assets/Purr3D/Characters/</c>, so a character imports the same way
    /// every time and on every machine, rather than depending on clicks in the Inspector.
    ///
    /// Models: a Humanoid rig with its own avatar, so the Starter Assets animations retarget onto it. Materials
    /// come from the FBX's own description, which URP turns into Lit materials using the FBX's embedded textures.
    ///
    /// Textures: anything named <c>*normal*</c> is a normal map; anything named <c>*metallic*</c>, <c>*roughness*</c>
    /// or <c>*mask*</c> holds data, not colour, so it's read as linear.
    /// </summary>
    public sealed class CharacterImportSettings : AssetPostprocessor
    {
        private const string Root = "Assets/Purr3D/Characters/";

        // Bump to make Unity reimport characters after changing these rules.
        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;      // Mixamo T-pose has nothing worth keeping
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;    // a generated mesh has none
            importer.globalScale = 1f;             // the prep script already made him 1.80 m
            importer.useFileScale = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

            if (name.Contains("normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (name.Contains("metallic") || name.Contains("roughness") || name.Contains("mask"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
            }
            importer.maxTextureSize = 2048;
        }
    }
}
