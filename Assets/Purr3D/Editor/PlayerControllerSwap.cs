using StarterAssets;
using UnityEditor;
using UnityEngine;

namespace Purr3D.Editor
{
    /// <summary>
    /// Tools → Purr3D → Swap In Our Player Controller: replaces Starter Assets' ThirdPersonController on the selected
    /// player with Purr3D's <see cref="PlayerController"/>, carrying every setting across (speeds, ground layers,
    /// the camera target, the footstep sounds). The two classes share field names, so a JSON round trip copies
    /// them. It's one Undo step.
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
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(old), ours);
            Undo.DestroyObjectImmediate(old);
            EditorUtility.SetDirty(player);
            Debug.Log($"[Purr] {player.name}: ThirdPersonController → PlayerController, settings carried over.");
        }

        [MenuItem(Menu, true)]
        private static bool CanSwap() =>
            Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<ThirdPersonController>() != null;
    }
}
