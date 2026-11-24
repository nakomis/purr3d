using StarterAssets;
using UnityEditor;
using UnityEngine;

namespace Purr3D.Editor
{
    /// <summary>
    /// Tools → Purr3D → Swap In Our Player Controller: replaces Starter Assets' ThirdPersonController on the selected
    /// player with Purr3D's <see cref="PlayerController"/>, carrying every setting across (speeds, ground layers,
    /// the camera target, the footstep sounds). It's one Undo step.
    ///
    /// The two classes share field names, so each of the old component's serialised properties is copied onto the
    /// property of the same name on the new one. (A JSON round trip doesn't work: the JSON is wrapped in the source
    /// class's name, so nothing matches on a different class.)
    /// </summary>
    public static class PlayerControllerSwap
    {
        private const string Menu = "Tools/Purr3D/Swap In Our Player Controller";

        [MenuItem(Menu)]
        private static void Swap()
        {
            var player = Selection.activeGameObject;
            var old = player.GetComponent<ThirdPersonController>();

            Undo.SetCurrentGroupName("Swap in PlayerController");
            var ours = Undo.AddComponent<PlayerController>(player);

            var from = new SerializedObject(old);
            var to = new SerializedObject(ours);
            var copied = 0;
            var property = from.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false; // top-level fields only; arrays and structs are copied whole
                if (property.propertyPath == "m_Script" || to.FindProperty(property.propertyPath) == null) continue;
                to.CopyFromSerializedProperty(property);
                copied++;
            }
            to.ApplyModifiedProperties();

            Undo.DestroyObjectImmediate(old);
            EditorUtility.SetDirty(player);

            if (ours.CinemachineCameraTarget == null)
                Debug.LogWarning($"[Purr] {player.name}: Player Controller has no Cinemachine Camera Target. Drag PlayerCameraRoot into it.");
            Debug.Log($"[Purr] {player.name}: ThirdPersonController → PlayerController, {copied} settings carried over.");
        }

        [MenuItem(Menu, true)]
        private static bool CanSwap() =>
            Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<ThirdPersonController>() != null;
    }
}
