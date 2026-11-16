using System.IO;
using Late.Game;
using UnityEngine;

namespace Late.Data
{
    /// <summary>
    /// Al arrancar, hace que <see cref="LevelManager"/> guarde el progreso en SQLite.
    /// La base de datos vive en <c>Application.persistentDataPath</c>, que en Android es la carpeta
    /// privada de la app y se conserva entre sesiones y actualizaciones.
    /// </summary>
    public static class LocalDataBootstrap
    {
        public const string DatabaseFileName = "late.db";

        private static SqliteProgressStore _store;

        public static string DatabasePath => Path.Combine(Application.persistentDataPath, DatabaseFileName);

        /// <summary>El almacén abierto, o null si todavía no se ha usado.</summary>
        public static SqliteProgressStore Store => _store;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            LevelManager.StoreFactory = GetOrOpenStore;
            Application.quitting -= Close;
            Application.quitting += Close;
        }

        private static IProgressStore GetOrOpenStore()
        {
            if (_store != null) return _store;
            try
            {
                _store = new SqliteProgressStore(DatabasePath);
                return _store;
            }
            catch (System.Exception e)
            {
                // Sin base de datos el juego sigue funcionando; solo se pierde el progreso al cerrar.
                Debug.LogError($"[LocalData] No se pudo abrir '{DatabasePath}': {e.Message}. Se usa el progreso en memoria.");
                return new InMemoryProgressStore();
            }
        }

        /// <summary>Cierra la conexión (al salir del juego o antes de borrar el archivo desde el editor).</summary>
        public static void Close()
        {
            _store?.Dispose();
            _store = null;
        }
    }
}
