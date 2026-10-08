using UnityEngine;

namespace Late.Core
{
    /// <summary>
    /// Pool de GameObjects reutilizables.
    /// </summary>
    public interface IObjectPool
    {
        GameObject GetObject();
        void ReturnObject(GameObject obj);
    }
}
