using UnityEditor;
using UnityEngine;

namespace Purr3D.Editor
{
    /// <summary>
    /// Import settings for animation clips (Mixamo downloads, say) under <c>Assets/Purr3D/Animations/</c>, so a
    /// clip plays on Dante the same way every time, without clicking through the Rig and Animation tabs.
    ///
    /// Rig: Humanoid, borrowing Dante's avatar, so the clip retargets onto him (it was recorded on Mixamo's
    /// skeleton). Nothing else from the file is wanted: no mesh materials, cameras or lights.
    ///
    /// Clips: no looping, and the root's rotation, height and position are all baked into the pose. The
    /// CharacterController moves the player; the clip only moves the body relative to it. Otherwise a flip would
    /// try to carry the character off with it, or sink them through the floor.
    /// </summary>
    public sealed class AnimationImportSettings : AssetPostprocessor
    {
        public const string Root = "Assets/Purr3D/Animations/";
        private const string AvatarSource = "Assets/Purr3D/Characters/Dante/Dante.fbx";

        // Bump to make Unity reimport clips after changing these rules.
        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;

            importer.animationType = ModelImporterAnimationType.Human;
            var avatar = DanteAvatar();
            if (avatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
            }
            else
            {
                Debug.LogWarning($"[Purr] {assetPath}: no avatar in {AvatarSource}, so this clip gets its own.");
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;

            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = false;
                clip.lockRootRotation = true;      // Root Transform Rotation: Bake Into Pose
                clip.lockRootHeightY = true;       // Root Transform Position (Y): Bake Into Pose
                clip.lockRootPositionXZ = true;    // Root Transform Position (XZ): Bake Into Pose
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        private static Avatar DanteAvatar()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AvatarSource))
            {
                if (asset is Avatar avatar) return avatar;
            }
            return null;
        }
    }
}
