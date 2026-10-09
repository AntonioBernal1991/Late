using System.Collections.Generic;
using UnityEngine;

namespace Late.Gameplay
{
    /// <summary>
    /// Esconde la llave en un sitio distinto cada partida. Los hijos de este objeto son los posibles
    /// escondites; al empezar se instancia la llave en uno de ellos al azar.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyHider : MonoBehaviour
    {
        [SerializeField] private GameObject keyPrefab;

        [Header("Colocación")]
        [Tooltip("Usa la rotación del escondite para la llave.")]
        [SerializeField] private bool useSpawnRotation = true;
        [Tooltip("Cuelga la llave del escondite en la jerarquía.")]
        [SerializeField] private bool parentToSpawnPoint = false;

        [Header("Azar")]
        [Tooltip("Si es >= 0, el escondite es siempre el mismo para esta semilla. Si es < 0, cambia cada partida.")]
        [SerializeField] private int fixedSeed = -1;

        [Header("Depuración")]
        [SerializeField] private bool logSpawn = false;
        [Tooltip("Avisa si falta el prefab o no hay escondites.")]
        [SerializeField] private bool logWarnings = false;
        [SerializeField] private bool drawGizmos = true;

        private GameObject _spawnedInstance;

        private void Start()
        {
            SpawnKey();
        }

        [ContextMenu("Spawn Key Now")]
        public void SpawnKey()
        {
            if (!Application.isPlaying) return;

            if (keyPrefab == null)
            {
                if (logWarnings) Debug.LogWarning("[KeyHider] Falta asignar el prefab de la llave.", this);
                return;
            }

            List<Transform> points = GetSpawnPoints();
            if (points.Count == 0)
            {
                if (logWarnings) Debug.LogWarning("[KeyHider] No hay escondites: añade hijos a este objeto.", this);
                return;
            }

            if (_spawnedInstance != null) Destroy(_spawnedInstance);

            Transform point = points[PickIndex(points.Count)];
            Quaternion rotation = useSpawnRotation ? point.rotation : Quaternion.identity;
            _spawnedInstance = Instantiate(keyPrefab, point.position, rotation, parentToSpawnPoint ? point : null);
            _spawnedInstance.name = keyPrefab.name;

            if (logSpawn) Debug.Log($"[KeyHider] Llave en '{point.name}'.", this);
        }

        private int PickIndex(int count)
        {
            if (count <= 1) return 0;
            return fixedSeed >= 0 ? new System.Random(fixedSeed).Next(0, count) : Random.Range(0, count);
        }

        private List<Transform> GetSpawnPoints()
        {
            var points = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.gameObject.activeInHierarchy) points.Add(child);
            }
            return points;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            Gizmos.color = new Color(0f, 1f, 0.85f, 0.7f);
            foreach (Transform child in transform)
            {
                Gizmos.DrawWireSphere(child.position, 0.15f);
                Gizmos.DrawLine(child.position, child.position + child.forward * 0.4f);
            }
        }
#endif
    }
}
