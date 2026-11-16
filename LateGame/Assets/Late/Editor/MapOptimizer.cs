using System.Collections.Generic;
using Late.Generation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Late.EditorTools
{
    /// <summary>
    /// Optimiza un nivel ya ajustado a mano sin cambiar cómo se ve ni cómo se juega.
    ///
    /// Trabaja sobre los «cubos simples» (solo Transform, MeshFilter, MeshRenderer y, como mucho, un
    /// BoxCollider sólido). Todo lo demás (llave, puerta, meta, inicio, prefabs anidados, scripts) se
    /// deja tal cual. Con los cubos simples:
    /// 1. Quita los duplicados: cubos con la misma malla y material en la misma posición.
    /// 2. Quita las caras que nunca se ven: las que tocan de lleno la cara de otro cubo (la tapa del
    ///    suelo bajo una pared) y, si se pide, las de abajo, porque la cámara nunca baja del suelo.
    /// 3. Une lo que queda en una malla por material y por módulo, para que el culling por módulos
    ///    (<c>MapDistanceCuller</c>) siga funcionando.
    /// 4. Conserva todos los colisionadores: el cubo con colisionador se queda sin renderer, así que
    ///    el recorrido y los choques no cambian.
    ///
    /// No guarda nada en disco: devuelve las mallas creadas para que quien llame decida dónde guardarlas.
    /// </summary>
    public static class MapOptimizer
    {
        /// <summary>Sufijo de las mallas unidas. Distinto de <see cref="MeshCombiner.CombinedSuffix"/>,
        /// porque el horneado borra esas al guardar y estas tienen que guardarse.</summary>
        public const string OptimizedSuffix = "_Opt";

        // Posiciones redondeadas a 1 mm para comparar sin errores de coma flotante.
        private const float Snap = 1000f;

        public struct Options
        {
            /// <summary>Quitar los cubos repetidos en la misma posición.</summary>
            public bool RemoveDuplicates;

            /// <summary>Quitar las caras que miran hacia abajo. El jugador siempre está por encima del suelo.</summary>
            public bool RemoveBottomFaces;

            public static Options Default => new Options { RemoveDuplicates = true, RemoveBottomFaces = true };
        }

        public class Result
        {
            public MapStats Before;
            public MapStats After;
            public int DuplicatesRemoved;
            public int TrianglesRemoved;
            public readonly List<Mesh> CreatedMeshes = new List<Mesh>();
        }

        /// <summary>Optimiza la jerarquía de <paramref name="root"/> en el sitio.</summary>
        public static Result Optimize(GameObject root, Options options)
        {
            var result = new Result { Before = MapStats.Measure(root) };
            Matrix4x4 worldToRoot = root.transform.worldToLocalMatrix;

            List<PlainCube> cubes = CollectPlainCubes(root.transform, worldToRoot);
            if (options.RemoveDuplicates) result.DuplicatesRemoved = MarkDuplicates(cubes);

            HashSet<FaceKey> hiddenFaces = FindTouchingFaces(cubes);

            // Agrupar por padre (módulo) y material.
            var groups = new Dictionary<(Transform, Material), List<PlainCube>>();
            foreach (PlainCube cube in cubes)
            {
                if (cube.IsDuplicate) continue;
                var key = (cube.Transform.parent, cube.Material);
                if (!groups.TryGetValue(key, out List<PlainCube> list))
                {
                    list = new List<PlainCube>();
                    groups[key] = list;
                }
                list.Add(cube);
            }

            foreach (KeyValuePair<(Transform, Material), List<PlainCube>> group in groups)
            {
                Mesh mesh = BuildGroupMesh(group.Value, group.Key.Item1, hiddenFaces, options, ref result.TrianglesRemoved);
                if (mesh == null) continue;
                CreateOptimizedObject(group.Key.Item1, group.Key.Item2, group.Value[0], mesh);
                result.CreatedMeshes.Add(mesh);
            }

            StripPlainCubes(cubes);

            result.After = MapStats.Measure(root);
            return result;
        }

        // ---------- Qué es un cubo simple ----------

        private class PlainCube
        {
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Material Material;
            public Mesh Mesh;
            public bool HasSolidCollider;
            public bool IsDuplicate;

            /// <summary>Malla en el espacio del nivel (posición, rotación y escala).</summary>
            public Matrix4x4 ToRoot;

            /// <summary>True si es la caja estándar sin rotar: entonces se pueden quitar caras.</summary>
            public bool IsAxisAlignedBox;
            public Vector3 Center;
            public Vector3 HalfSize;
        }

        private static List<PlainCube> CollectPlainCubes(Transform root, Matrix4x4 worldToRoot)
        {
            var cubes = new List<PlainCube>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                PlainCube cube = TryGetPlainCube(t, worldToRoot);
                if (cube != null) cubes.Add(cube);
            }
            return cubes;
        }

        private static PlainCube TryGetPlainCube(Transform t, Matrix4x4 worldToRoot)
        {
            GameObject go = t.gameObject;
            if (!go.activeInHierarchy && !IsActiveInsideRoot(t)) return null;
            if (t.childCount > 0) return null;
            if (go.name == MeshCombiner.FloorColliderName) return null;
            if (go.name.EndsWith(OptimizedSuffix)) return null;
            if (IsPartOfNestedPrefab(go)) return null;

            MeshFilter filter = null;
            MeshRenderer renderer = null;
            BoxCollider box = null;
            foreach (Component component in go.GetComponents<Component>())
            {
                switch (component)
                {
                    case Transform _: break;
                    case MeshFilter f: filter = f; break;
                    case MeshRenderer r: renderer = r; break;
                    case BoxCollider b when box == null: box = b; break;
                    default: return null; // script, trigger extra, luz... no se toca
                }
            }

            if (filter == null || renderer == null || filter.sharedMesh == null) return null;
            if (!renderer.enabled || renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial == null) return null;
            if (filter.sharedMesh.subMeshCount != 1) return null;
            if (box != null && box.isTrigger) return null; // las zonas de disparo tienen lógica asociada

            var cube = new PlainCube
            {
                Transform = t,
                Filter = filter,
                Renderer = renderer,
                Material = renderer.sharedMaterial,
                Mesh = filter.sharedMesh,
                HasSolidCollider = box != null && box.enabled,
                ToRoot = worldToRoot * t.localToWorldMatrix
            };
            FillBoxShape(cube);
            return cube;
        }

        // En una prefab cargada para editar, la raíz está en una escena de previsualización y
        // activeInHierarchy funciona; esta comprobación cubre jerarquías creadas a mano en los tests.
        private static bool IsActiveInsideRoot(Transform t)
        {
            for (Transform current = t; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf) return false;
            }
            return true;
        }

        private static bool IsPartOfNestedPrefab(GameObject go)
        {
#if UNITY_EDITOR
            return UnityEditor.PrefabUtility.IsPartOfPrefabInstance(go);
#else
            return false;
#endif
        }

        private static void FillBoxShape(PlainCube cube)
        {
            Matrix4x4 m = cube.ToRoot;
            const float eps = 1e-4f;
            bool noRotation =
                Mathf.Abs(m.m01) < eps && Mathf.Abs(m.m02) < eps &&
                Mathf.Abs(m.m10) < eps && Mathf.Abs(m.m12) < eps &&
                Mathf.Abs(m.m20) < eps && Mathf.Abs(m.m21) < eps &&
                m.m00 > 0f && m.m11 > 0f && m.m22 > 0f;

            Mesh mesh = cube.Mesh;
            bool isBox = mesh.vertexCount == 24 && mesh.GetIndexCount(0) == 36 && IsBoxMesh(mesh);

            cube.IsAxisAlignedBox = noRotation && isBox;
            if (!cube.IsAxisAlignedBox) return;

            Bounds b = mesh.bounds;
            cube.Center = m.MultiplyPoint3x4(b.center);
            cube.HalfSize = Vector3.Scale(b.extents, new Vector3(m.m00, m.m11, m.m22));
        }

        private static readonly Dictionary<Mesh, bool> BoxMeshCache = new Dictionary<Mesh, bool>();

        /// <summary>La caja estándar: 6 caras planas, cada una con 4 vértices de la misma normal sobre una cara del bounds.</summary>
        private static bool IsBoxMesh(Mesh mesh)
        {
            if (BoxMeshCache.TryGetValue(mesh, out bool cached)) return cached;

            bool ok = mesh.isReadable;
            if (ok)
            {
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Bounds b = mesh.bounds;
                ok = normals.Length == vertices.Length;
                for (int i = 0; ok && i < vertices.Length; i++)
                {
                    int axis = DominantAxis(normals[i]);
                    float sign = Mathf.Sign(normals[i][axis]);
                    float expected = b.center[axis] + sign * b.extents[axis];
                    ok = Mathf.Abs(vertices[i][axis] - expected) < 1e-4f;
                }
            }

            BoxMeshCache[mesh] = ok;
            return ok;
        }

        // ---------- 1. Duplicados ----------

        private static int MarkDuplicates(List<PlainCube> cubes)
        {
            var seen = new Dictionary<(Mesh, Material, Vector3Int, Vector3Int, Vector3Int), PlainCube>();
            int count = 0;
            foreach (PlainCube cube in cubes)
            {
                Matrix4x4 m = cube.ToRoot;
                var key = (cube.Mesh, cube.Material,
                    SnapVector(m.GetColumn(3)),
                    SnapVector(m.GetColumn(0)),
                    SnapVector(m.GetColumn(1) + m.GetColumn(2) * 7f)); // rotación y escala en una sola clave

                if (seen.ContainsKey(key))
                {
                    cube.IsDuplicate = true;
                    count++;
                }
                else
                {
                    seen[key] = cube;
                }
            }
            return count;
        }

        // ---------- 2. Caras que tocan otra cara ----------

        /// <summary>
        /// Una cara está tapada si otra caja tiene la cara opuesta en el mismo plano, con el mismo
        /// centro y al menos el mismo tamaño. Es el caso de la tapa del suelo bajo cada pared.
        /// Con el hueco entre cubos (tileSpacing > 1) los laterales nunca se tocan, así que se conservan.
        /// </summary>
        private static HashSet<FaceKey> FindTouchingFaces(List<PlainCube> cubes)
        {
            var faces = new Dictionary<FaceKey, Vector2>();
            foreach (PlainCube cube in cubes)
            {
                if (cube.IsDuplicate || !cube.IsAxisAlignedBox) continue;
                for (int axis = 0; axis < 3; axis++)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        FaceKey key = FaceKey.Of(cube, axis, sign);
                        Vector2 size = FaceSize(cube, axis);
                        if (!faces.TryGetValue(key, out Vector2 existing) || Covers(size, existing))
                        {
                            faces[key] = size;
                        }
                    }
                }
            }

            var hidden = new HashSet<FaceKey>();
            foreach (PlainCube cube in cubes)
            {
                if (cube.IsDuplicate || !cube.IsAxisAlignedBox) continue;
                for (int axis = 0; axis < 3; axis++)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        FaceKey key = FaceKey.Of(cube, axis, sign);
                        if (faces.TryGetValue(key.Opposite, out Vector2 other) && Covers(other, FaceSize(cube, axis)))
                        {
                            hidden.Add(key);
                        }
                    }
                }
            }
            return hidden;
        }

        private static Vector2 FaceSize(PlainCube cube, int axis)
        {
            int u = (axis + 1) % 3, v = (axis + 2) % 3;
            return new Vector2(cube.HalfSize[u], cube.HalfSize[v]);
        }

        private static bool Covers(Vector2 a, Vector2 b) => a.x >= b.x - 1e-4f && a.y >= b.y - 1e-4f;

        private readonly struct FaceKey : System.IEquatable<FaceKey>
        {
            private readonly int _axis;
            private readonly int _sign;
            private readonly Vector3Int _center; // centro de la cara, redondeado

            private FaceKey(int axis, int sign, Vector3Int center)
            {
                _axis = axis;
                _sign = sign;
                _center = center;
            }

            public static FaceKey Of(PlainCube cube, int axis, int sign)
            {
                Vector3 c = cube.Center;
                c[axis] += sign * cube.HalfSize[axis];
                return new FaceKey(axis, sign, SnapVector(c));
            }

            public FaceKey Opposite => new FaceKey(_axis, -_sign, _center);

            public bool Equals(FaceKey other) => _axis == other._axis && _sign == other._sign && _center == other._center;
            public override bool Equals(object obj) => obj is FaceKey other && Equals(other);
            public override int GetHashCode() => (_center.GetHashCode() * 31 + _axis) * 3 + _sign;
        }

        // ---------- 3. Unir mallas ----------

        private static Mesh BuildGroupMesh(List<PlainCube> cubes, Transform parent, HashSet<FaceKey> hiddenFaces,
            Options options, ref int trianglesRemoved)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Matrix4x4 worldToParent = parent.worldToLocalMatrix;

            foreach (PlainCube cube in cubes)
            {
                Mesh mesh = cube.Mesh;
                Vector3[] mv = mesh.vertices;
                Vector3[] mn = mesh.normals;
                Vector4[] mt = mesh.tangents;
                Vector2[] muv = mesh.uv;
                int[] mtri = mesh.GetTriangles(0);

                Matrix4x4 toParent = worldToParent * cube.Transform.localToWorldMatrix;
                Matrix4x4 normalMatrix = toParent.inverse.transpose;
                var remap = new Dictionary<int, int>();

                for (int i = 0; i < mtri.Length; i += 3)
                {
                    if (cube.IsAxisAlignedBox && IsFaceHidden(cube, mn, mtri[i], hiddenFaces, options))
                    {
                        trianglesRemoved++;
                        continue;
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        int src = mtri[i + k];
                        if (!remap.TryGetValue(src, out int dst))
                        {
                            dst = vertices.Count;
                            remap[src] = dst;
                            vertices.Add(toParent.MultiplyPoint3x4(mv[src]));
                            normals.Add(mn.Length > 0 ? normalMatrix.MultiplyVector(mn[src]).normalized : Vector3.up);
                            if (mt.Length > 0)
                            {
                                Vector3 tan = toParent.MultiplyVector(new Vector3(mt[src].x, mt[src].y, mt[src].z)).normalized;
                                tangents.Add(new Vector4(tan.x, tan.y, tan.z, mt[src].w));
                            }
                            uvs.Add(muv.Length > 0 ? muv[src] : Vector2.zero);
                        }
                        triangles.Add(dst);
                    }
                }
            }

            if (triangles.Count == 0) return null;

            var result = new Mesh
            {
                name = $"{parent.name}_{cubes[0].Material.name}{OptimizedSuffix}",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            result.SetVertices(vertices);
            result.SetNormals(normals);
            if (tangents.Count == vertices.Count) result.SetTangents(tangents);
            result.SetUVs(0, uvs);
            result.SetTriangles(triangles, 0);
            result.RecalculateBounds();
            result.UploadMeshData(false);
            return result;
        }

        private static bool IsFaceHidden(PlainCube cube, Vector3[] meshNormals, int vertexIndex,
            HashSet<FaceKey> hiddenFaces, Options options)
        {
            Vector3 n = meshNormals[vertexIndex];
            int axis = DominantAxis(n);
            int sign = n[axis] > 0f ? 1 : -1;

            // La normal está en el espacio de la malla; sin rotación, el eje y el signo se conservan.
            if (options.RemoveBottomFaces && axis == 1 && sign < 0) return true;
            return hiddenFaces.Contains(FaceKey.Of(cube, axis, sign));
        }

        private static void CreateOptimizedObject(Transform parent, Material material, PlainCube template, Mesh mesh)
        {
            // Sin el nombre del módulo delante: MapDistanceCuller toma por módulo todo lo que empiece por «Module_».
            var go = new GameObject(material.name + OptimizedSuffix);
            go.transform.SetParent(parent, false);
            go.layer = template.Transform.gameObject.layer;
            go.tag = template.Transform.gameObject.tag;
#if UNITY_EDITOR
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(go,
                UnityEditor.GameObjectUtility.GetStaticEditorFlags(template.Transform.gameObject));
#endif

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            MeshRenderer source = template.Renderer;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            renderer.lightProbeUsage = source.lightProbeUsage;
            renderer.reflectionProbeUsage = source.reflectionProbeUsage;
        }

        // ---------- 4. Limpiar los cubos originales ----------

        private static void StripPlainCubes(List<PlainCube> cubes)
        {
            foreach (PlainCube cube in cubes)
            {
                if (cube.HasSolidCollider)
                {
                    // El colisionador se queda donde estaba; solo sobra lo visual.
                    Object.DestroyImmediate(cube.Renderer);
                    Object.DestroyImmediate(cube.Filter);
                }
                else
                {
                    Object.DestroyImmediate(cube.Transform.gameObject);
                }
            }
        }

        // ---------- Utilidades ----------

        private static int DominantAxis(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            return x >= y && x >= z ? 0 : (y >= z ? 1 : 2);
        }

        private static Vector3Int SnapVector(Vector3 v) =>
            new Vector3Int(Mathf.RoundToInt(v.x * Snap), Mathf.RoundToInt(v.y * Snap), Mathf.RoundToInt(v.z * Snap));
    }

    /// <summary>Cifras de un nivel para comparar antes y después de optimizar.</summary>
    public struct MapStats
    {
        public int GameObjects;
        public int Renderers;
        public int Vertices;
        public int Triangles;
        public int Colliders;

        public static MapStats Measure(GameObject root)
        {
            var stats = new MapStats();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                stats.GameObjects++;
            }

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                stats.Renderers++;
                stats.Vertices += filter.sharedMesh.vertexCount;
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                {
                    stats.Triangles += (int)(filter.sharedMesh.GetIndexCount(i) / 3);
                }
            }

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider.enabled) stats.Colliders++;
            }
            return stats;
        }
    }
}
