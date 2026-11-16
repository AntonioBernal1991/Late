using System.Collections.Generic;
using System.Linq;
using Late.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Late.Tests
{
    /// <summary>
    /// R03F02: el optimizador reduce lo que se dibuja sin cambiar el recorrido.
    /// Cada caja estándar tiene 12 triángulos (6 caras de 2).
    /// </summary>
    public class MapOptimizerTests
    {
        private const int TrianglesPerCube = 12;
        private static readonly MapOptimizer.Options KeepBottoms =
            new MapOptimizer.Options { RemoveDuplicates = true, RemoveBottomFaces = false };

        private GameObject _root;
        private Material _wall;
        private Material _ground;
        private readonly List<Mesh> _createdMeshes = new List<Mesh>();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Run");
            _wall = new Material(Shader.Find("Standard")) { name = "Grass" };
            _ground = new Material(Shader.Find("Standard")) { name = "Ground" };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_wall);
            Object.DestroyImmediate(_ground);
            foreach (Mesh mesh in _createdMeshes) Object.DestroyImmediate(mesh);
            _createdMeshes.Clear();
        }

        private Transform Module(string name, Vector3 position)
        {
            var module = new GameObject(name).transform;
            module.SetParent(_root.transform, false);
            module.localPosition = position;
            return module;
        }

        private static GameObject Cube(Transform parent, Vector3 localPosition, Material material, bool collider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "TerrainCube(Clone)";
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
            return cube;
        }

        private MapOptimizer.Result Optimize(MapOptimizer.Options options)
        {
            MapOptimizer.Result result = MapOptimizer.Optimize(_root, options);
            _createdMeshes.AddRange(result.CreatedMeshes);
            return result;
        }

        private static List<Bounds> ColliderBounds(GameObject root) =>
            root.GetComponentsInChildren<Collider>(true).Where(c => c.enabled).Select(c => c.bounds)
                .OrderBy(b => b.center.x).ThenBy(b => b.center.y).ThenBy(b => b.center.z).ToList();

        [Test]
        public void ParedSobreSuelo_QuitaLasCarasQueSeTocan()
        {
            Transform module = Module("Module_1", Vector3.zero);
            Cube(module, new Vector3(0f, 0f, 0f), _wall, collider: true);
            Cube(module, new Vector3(0f, -1f, 0f), _ground, collider: false);

            MapOptimizer.Result result = Optimize(KeepBottoms);

            // La base de la pared y la tapa del suelo: 2 caras, 4 triángulos.
            Assert.AreEqual(2 * TrianglesPerCube - 4, result.After.Triangles);
        }

        [Test]
        public void ConCarasInferiores_TambienQuitaLaBaseDelSuelo()
        {
            Transform module = Module("Module_1", Vector3.zero);
            Cube(module, new Vector3(0f, 0f, 0f), _wall, collider: true);
            Cube(module, new Vector3(0f, -1f, 0f), _ground, collider: false);

            MapOptimizer.Result result = Optimize(MapOptimizer.Options.Default);

            Assert.AreEqual(2 * TrianglesPerCube - 6, result.After.Triangles);
        }

        [Test]
        public void CubosSeparadosPorElHueco_ConservanLosLaterales()
        {
            Transform module = Module("Module_1", Vector3.zero);
            Cube(module, new Vector3(0f, 0f, 0f), _wall, collider: false);
            Cube(module, new Vector3(1.2f, 0f, 0f), _wall, collider: false);

            MapOptimizer.Result result = Optimize(KeepBottoms);

            Assert.AreEqual(2 * TrianglesPerCube, result.After.Triangles);
        }

        [Test]
        public void CubosDuplicados_SeQuedaUno()
        {
            Transform module = Module("Module_1", Vector3.zero);
            Cube(module, Vector3.zero, _wall, collider: false);
            Cube(module, Vector3.zero, _wall, collider: false);

            MapOptimizer.Result result = Optimize(KeepBottoms);

            Assert.AreEqual(1, result.DuplicatesRemoved);
            Assert.AreEqual(TrianglesPerCube, result.After.Triangles);
        }

        [Test]
        public void LosColisionadoresNoCambian()
        {
            Transform module = Module("Module_1", new Vector3(10f, 0f, 5f));
            for (int x = 0; x < 4; x++)
            {
                Cube(module, new Vector3(x * 1.2f, 0f, 0f), _wall, collider: true);
                Cube(module, new Vector3(x * 1.2f, -1f, 0f), _ground, collider: false);
            }
            List<Bounds> before = ColliderBounds(_root);

            MapOptimizer.Result result = Optimize(MapOptimizer.Options.Default);

            CollectionAssert.AreEqual(before, ColliderBounds(_root));
            Assert.AreEqual(result.Before.Colliders, result.After.Colliders);
        }

        [Test]
        public void UnaMallaPorMaterialYModulo()
        {
            Transform first = Module("Module_1", Vector3.zero);
            Transform second = Module("Module_2", new Vector3(0f, 0f, 20f));
            foreach (Transform module in new[] { first, second })
            {
                for (int x = 0; x < 3; x++)
                {
                    Cube(module, new Vector3(x * 1.2f, 0f, 0f), _wall, collider: false);
                    Cube(module, new Vector3(x * 1.2f, -1f, 2.4f), _ground, collider: false);
                }
            }

            MapOptimizer.Result result = Optimize(KeepBottoms);

            Assert.AreEqual(4, result.After.Renderers);
            Assert.AreEqual(2, first.childCount);
            Assert.AreEqual(2, second.childCount);
        }

        [Test]
        public void LaMallaUnidaOcupaLoMismoQueLosCubos()
        {
            Transform module = Module("Module_1", new Vector3(3f, 0f, 7f));
            Cube(module, new Vector3(0f, 0f, 0f), _wall, collider: false);
            Cube(module, new Vector3(2.4f, 0f, 1.2f), _wall, collider: false);
            Bounds expected = new Bounds(module.TransformPoint(new Vector3(1.2f, 0f, 0.6f)), new Vector3(3.4f, 1f, 2.2f));

            Optimize(KeepBottoms);

            MeshRenderer combined = module.GetComponentInChildren<MeshRenderer>();
            Assert.AreEqual(expected.center.x, combined.bounds.center.x, 1e-3f);
            Assert.AreEqual(expected.center.z, combined.bounds.center.z, 1e-3f);
            Assert.AreEqual(expected.size.x, combined.bounds.size.x, 1e-3f);
            Assert.AreEqual(expected.size.z, combined.bounds.size.z, 1e-3f);
        }

        [Test]
        public void ObjetosConLogica_NoSeTocan()
        {
            Transform module = Module("Module_1", Vector3.zero);
            GameObject door = Cube(module, Vector3.zero, _wall, collider: true);
            door.name = "DoorCube";
            door.AddComponent<Rigidbody>(); // cualquier componente extra lo deja fuera
            GameObject trigger = Cube(module, new Vector3(1.2f, 0f, 0f), _wall, collider: true);
            trigger.GetComponent<BoxCollider>().isTrigger = true;

            Optimize(KeepBottoms);

            Assert.IsNotNull(door.GetComponent<MeshRenderer>());
            Assert.IsNotNull(trigger.GetComponent<MeshRenderer>());
            Assert.AreEqual(2, module.childCount);
        }
    }
}
