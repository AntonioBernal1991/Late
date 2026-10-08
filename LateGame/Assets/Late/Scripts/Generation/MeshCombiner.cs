using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Late.Generation
{
    /// <summary>
    /// Une las mallas de los cubos de un módulo en una malla por material. Pasa de cientos de
    /// draw calls por módulo a una o dos, que es lo que permite mantener los FPS en móvil.
    /// Los cubos originales conservan su colisionador; solo se oculta su renderer.
    /// </summary>
    public static class MeshCombiner
    {
        /// <summary>Nombre del colisionador invisible del suelo, que no se combina.</summary>
        public const string FloorColliderName = "BasePlane";

        /// <summary>Sufijo de los objetos que crea el combinador.</summary>
        public const string CombinedSuffix = "_Combined";

        public static void CombineMeshesByMaterial(GameObject parent)
        {
            var instancesByMaterial = new Dictionary<Material, List<CombineInstance>>();
            var renderersToHide = new List<MeshRenderer>();
            Matrix4x4 worldToParent = parent.transform.worldToLocalMatrix;

            foreach (MeshFilter meshFilter in parent.GetComponentsInChildren<MeshFilter>())
            {
                if (meshFilter.sharedMesh == null) continue;
                if (meshFilter.gameObject.name == FloorColliderName) continue;
                if (meshFilter.gameObject.name.Contains(CombinedSuffix)) continue;

                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                if (meshRenderer == null || meshRenderer.sharedMaterial == null) continue;

                Material material = meshRenderer.sharedMaterial;
                if (!instancesByMaterial.TryGetValue(material, out List<CombineInstance> instances))
                {
                    instances = new List<CombineInstance>();
                    instancesByMaterial[material] = instances;
                }

                instances.Add(new CombineInstance
                {
                    mesh = meshFilter.sharedMesh,
                    transform = worldToParent * meshFilter.transform.localToWorldMatrix
                });
                renderersToHide.Add(meshRenderer);
            }

            // Con 0 o 1 mallas no se gana nada combinando.
            if (renderersToHide.Count <= 1)
            {
                return;
            }

            foreach (MeshRenderer meshRenderer in renderersToHide)
            {
                meshRenderer.enabled = false;
            }

            foreach (KeyValuePair<Material, List<CombineInstance>> entry in instancesByMaterial)
            {
                CreateCombinedObject(parent.transform, entry.Key, entry.Value);
            }
        }

        private static void CreateCombinedObject(Transform parent, Material material, List<CombineInstance> instances)
        {
            var combinedObject = new GameObject($"{parent.name}_{material.name}{CombinedSuffix}");
            combinedObject.transform.parent = parent;
            combinedObject.transform.localPosition = Vector3.zero;
            combinedObject.transform.localRotation = Quaternion.identity;

            // Índices de 32 bits: con muchos cubos se superan los 65.535 vértices.
            // Recalcular los bounds evita que el frustum culling haga desaparecer trozos.
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(instances.ToArray(), true, true);
            mesh.RecalculateBounds();

            combinedObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer meshRenderer = combinedObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            // Las sombras de miles de cubos son lo más caro del frame; el juego no las necesita.
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
    }
}
