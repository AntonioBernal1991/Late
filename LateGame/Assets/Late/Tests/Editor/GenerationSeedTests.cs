using Late.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Late.Tests
{
    /// <summary>
    /// R01F01T01P01: la misma semilla genera el mismo laberinto. Aquí se prueba la pieza que lo
    /// garantiza: la semilla global y el generador aleatorio de cada módulo.
    /// </summary>
    public class GenerationSeedTests
    {
        private static readonly Vector2 ModuleWorldSize = new Vector2(13 * 1.2f, 13 * 1.2f);

        private static MapSettings SettingsWithSeed(int seed)
        {
            var settings = ScriptableObject.CreateInstance<MapSettings>();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("_seed").intValue = seed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }

        private static int[] Sequence(PathGenerationContext context, int count = 50)
        {
            var values = new int[count];
            for (int i = 0; i < count; i++) values[i] = context.Random.Next();
            return values;
        }

        [Test]
        public void MismaSemilla_MismaSemillaBase()
        {
            MapSettings a = SettingsWithSeed(45);
            MapSettings b = SettingsWithSeed(45);
            try
            {
                Assert.AreEqual(a.BaseSeed, b.BaseSeed);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void SemillasDistintas_SemillasBaseDistintas()
        {
            MapSettings a = SettingsWithSeed(45);
            MapSettings b = SettingsWithSeed(46);
            try
            {
                Assert.AreNotEqual(a.BaseSeed, b.BaseSeed);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void MismoModulo_MismasDecisiones()
        {
            var position = new Vector3(0f, 0f, 2 * (ModuleWorldSize.y + 1.2f));
            var first = new PathGenerationContext(12345, position, 1.2f, ModuleWorldSize);
            var second = new PathGenerationContext(12345, position, 1.2f, ModuleWorldSize);

            CollectionAssert.AreEqual(Sequence(first), Sequence(second));
        }

        [Test]
        public void ModulosDistintos_DecisionesDistintas()
        {
            var first = new PathGenerationContext(12345, Vector3.zero, 1.2f, ModuleWorldSize);
            var second = new PathGenerationContext(12345, new Vector3(0f, 0f, ModuleWorldSize.y + 1.2f), 1.2f, ModuleWorldSize);

            CollectionAssert.AreNotEqual(Sequence(first), Sequence(second));
        }

        [Test]
        public void CambiarElHuecoEntreModulos_NoCambiaElLaberinto()
        {
            // El mismo módulo (dos a la derecha, tres hacia delante) con hueco 1,2 y con hueco 0.
            Vector3 PositionWithSpacing(float spacing) =>
                new Vector3(2 * (ModuleWorldSize.x + spacing), 0f, 3 * (ModuleWorldSize.y + spacing));

            var normal = new PathGenerationContext(777, PositionWithSpacing(1.2f), 1.2f, ModuleWorldSize);
            var noGap = new PathGenerationContext(777, PositionWithSpacing(0f), 0f, ModuleWorldSize);

            CollectionAssert.AreEqual(Sequence(normal), Sequence(noGap));
        }
    }
}
