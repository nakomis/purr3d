using System;
using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// A trigger volume that reports the player (anything tagged <c>Player</c>, which Starter Assets'
    /// PlayerArmature is) walking into it. Doors and the room's exit make their own in code with <see cref="Create"/>.
    /// </summary>
    public sealed class PlayerTrigger : MonoBehaviour
    {
        public const string PlayerTag = "Player";

        public event Action<PlayerTrigger, GameObject> Entered;

        /// <summary>A child trigger box of <paramref name="size"/> centred at <paramref name="localCentre"/>.</summary>
        public static PlayerTrigger Create(Transform parent, string name, Vector3 localCentre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;

            // A kinematic body makes the trigger fire whatever the player moves with (CharacterController or not).
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            return go.AddComponent<PlayerTrigger>();
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = PlayerOf(other);
            if (player != null) Entered?.Invoke(this, player);
        }

        private static GameObject PlayerOf(Collider other)
        {
            var t = other.transform;
            while (t != null)
            {
                if (t.CompareTag(PlayerTag)) return t.gameObject;
                t = t.parent;
            }
            return null;
        }
    }
}
