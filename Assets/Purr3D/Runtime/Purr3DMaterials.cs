using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// The materials for things built in code: <c>Assets/Purr3D/Resources/Purr3D/Shell.mat</c> (URP Lit) and
    /// <c>Unlit.mat</c> (URP Unlit).
    ///
    /// Why not <c>Shader.Find</c> or a primitive's own material? In the editor both work. In a build, a primitive gets
    /// Unity's built-in default material, which URP draws magenta, and <c>Shader.Find</c> finds nothing, because
    /// a build strips every shader no asset references. Anything in a <c>Resources</c> folder always goes into a
    /// build, with its shader, so these always work (PURR3D-14).
    /// </summary>
    public static class Purr3DMaterials
    {
        private static Material _shell, _unlit;

        /// <summary>Lit, pale grey: walls, floors and zones (tinted per renderer with a MaterialPropertyBlock).</summary>
        public static Material Shell => _shell != null ? _shell : _shell = Load("Shell");

        /// <summary>Unlit, white: icons and glowing things (tinted or textured with a MaterialPropertyBlock, or copied).</summary>
        public static Material Unlit => _unlit != null ? _unlit : _unlit = Load("Unlit");

        private static Material Load(string name)
        {
            var material = Resources.Load<Material>("Purr3D/" + name);
            if (material == null) Debug.LogError($"[Purr] missing Assets/Purr3D/Resources/Purr3D/{name}.mat: code-built things will be magenta in a build");
            return material;
        }
    }
}
