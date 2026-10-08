using System.Collections.Generic;
using UnityEngine;

namespace Late.Core
{
    /// <summary>
    /// Pool de instancias de un prefab. Evita crear y destruir miles de cubos durante la generación.
    /// Los objetos devueltos se desactivan, no se destruyen.
    /// </summary>
    public class ObjectPool : IObjectPool
    {
        private readonly Queue<GameObject> _pool = new Queue<GameObject>();
        private readonly GameObject _prefab;
        private readonly Transform _parent;

        /// <param name="parent">Dónde se crean las instancias. Conviene darlo para que vivan en la escena correcta.</param>
        public ObjectPool(GameObject prefab, int initialSize, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;

            for (int i = 0; i < initialSize; i++)
            {
                GameObject obj = Object.Instantiate(prefab, parent);
                obj.SetActive(false);
                _pool.Enqueue(obj);
            }
        }

        public GameObject GetObject()
        {
            if (_pool.Count > 0)
            {
                GameObject obj = _pool.Dequeue();
                obj.SetActive(true);
                return obj;
            }

            return Object.Instantiate(_prefab, _parent);
        }

        public void ReturnObject(GameObject obj)
        {
            obj.SetActive(false);
            _pool.Enqueue(obj);
        }
    }
}
