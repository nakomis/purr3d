using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// A box whose texture repeats every <c>tileMetres</c>, however big the box is (PURR3D-15).
    ///
    /// A scaled Unity cube stretches one copy of the texture over each face: a 40 m wall would get one 40 m panel.
    /// Here each face's UVs are its position in metres divided by the tile size, measured in the parent's space, so a
    /// 40 m wall shows twenty 2 m tiles and two blocks side by side line up seamlessly. The tangents are rebuilt
    /// from those UVs, which the normal map needs to know which way is "up" on each face.
    /// </summary>
    public static class TiledBox
    {
        /// <summary>
        /// Makes the box as a child of <paramref name="parent"/>, centred at <paramref name="centre"/> (local) with
        /// the given <paramref name="size"/>. It has a BoxCollider unless <paramref name="collide"/> is false.
        /// </summary>
        public static GameObject Create(string name, Transform parent, Vector3 centre, Vector3 size, float tileMetres,
            Material material, bool collide = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            go.AddComponent<MeshFilter>().sharedMesh = Mesh(name, centre, size, tileMetres);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collide) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        // Each face: its outward normal, then the axes its texture's u and v run along.
        private static readonly (Vector3 normal, Vector3 u, Vector3 v)[] Faces =
        {
            (Vector3.right, Vector3.forward, Vector3.up),
            (Vector3.left, Vector3.forward, Vector3.up),
            (Vector3.up, Vector3.right, Vector3.forward),
            (Vector3.down, Vector3.right, Vector3.forward),
            (Vector3.forward, Vector3.right, Vector3.up),
            (Vector3.back, Vector3.right, Vector3.up),
        };

        private static Mesh Mesh(string name, Vector3 centre, Vector3 size, float tileMetres)
        {
            var half = size / 2f;
            var tile = Mathf.Max(0.01f, tileMetres);
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];

            for (var f = 0; f < Faces.Length; f++)
            {
                var (n, u, v) = Faces[f];
                var faceCentre = Vector3.Scale(n, half);
                var du = Vector3.Scale(u, half);
                var dv = Vector3.Scale(v, half);
                var corners = new[] { faceCentre - du - dv, faceCentre - du + dv, faceCentre + du + dv, faceCentre + du - dv };

                for (var c = 0; c < 4; c++)
                {
                    var i = f * 4 + c;
                    vertices[i] = corners[c];
                    normals[i] = n;
                    var world = centre + corners[c]; // in the parent's space, so neighbouring blocks line up
                    uvs[i] = new Vector2(Vector3.Dot(world, u), Vector3.Dot(world, v)) / tile;
                }

                // Unity draws the side a triangle's clockwise winding faces; pick the order that faces outwards.
                var b = f * 4;
                var outwards = Vector3.Dot(Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]), n) > 0f;
                var order = outwards ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
                for (var k = 0; k < 6; k++) triangles[f * 6 + k] = b + order[k];
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
