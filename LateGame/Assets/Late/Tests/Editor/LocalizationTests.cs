using System.Linq;
using Late.Localization;
using NUnit.Framework;
using UnityEngine;
using L10n = Late.Localization.Localization;

namespace Late.Tests
{
    /// <summary>R07: el juego en español e inglés.</summary>
    public class LocalizationTests
    {
        private Language _previous;

        [SetUp]
        public void SetUp()
        {
            _previous = L10n.Current;
            L10n.Saver = null;
        }

        [TearDown]
        public void TearDown()
        {
            L10n.Saver = null;
            L10n.Load(_previous);
        }

        [Test]
        public void TodasLasClavesEstanEnLosDosIdiomas()
        {
            string[] missing = Strings.Keys
                .Where(key => string.IsNullOrWhiteSpace(Strings.Get(key, Language.Spanish))
                              || string.IsNullOrWhiteSpace(Strings.Get(key, Language.English)))
                .ToArray();

            CollectionAssert.IsEmpty(missing);
        }

        [Test]
        public void LosHuecosCoincidenEnLosDosIdiomas()
        {
            foreach (string key in Strings.Keys)
            {
                int es = Strings.Get(key, Language.Spanish).Split('{').Length;
                int en = Strings.Get(key, Language.English).Split('{').Length;
                Assert.AreEqual(es, en, $"«{key}» tiene distintos huecos {{0}} en cada idioma");
            }
        }

        [Test]
        public void CambiarDeIdioma_CambiaLosTextosYSeGuarda()
        {
            Language? saved = null;
            int changes = 0;
            void OnChanged() => changes++;
            L10n.Load(Language.Spanish);
            L10n.Saver = language => saved = language;
            L10n.Changed += OnChanged;
            try
            {
                Assert.AreEqual("busca la llave", L10n.Get("key.look"));

                L10n.Current = Language.English;

                Assert.AreEqual("look 4 the key", L10n.Get("key.look"));
                Assert.AreEqual(Language.English, saved);
                Assert.AreEqual(1, changes);
            }
            finally
            {
                L10n.Changed -= OnChanged;
            }
        }

        [Test]
        public void TextoConHuecos()
        {
            L10n.Load(Language.Spanish);
            Assert.AreEqual("conectado como ana", L10n.Format("account.loggedAs", "ana"));
        }

        [Test]
        public void ClaveInexistente_SeVeEntreCorchetes()
        {
            Assert.AreEqual("[no.existe]", L10n.Get("no.existe"));
        }

        [Test]
        public void IdiomaPorDefecto_ElDelSistema()
        {
            Assert.AreEqual(Language.Spanish, L10n.FromSystem(SystemLanguage.Spanish));
            Assert.AreEqual(Language.Spanish, L10n.FromSystem(SystemLanguage.Catalan));
            Assert.AreEqual(Language.English, L10n.FromSystem(SystemLanguage.English));
            Assert.AreEqual(Language.English, L10n.FromSystem(SystemLanguage.Japanese));
        }

        [Test]
        public void CodigoDeIdioma_IdaYVuelta()
        {
            foreach (Language language in new[] { Language.Spanish, Language.English })
            {
                Assert.IsTrue(L10n.TryParse(L10n.Code(language), out Language parsed));
                Assert.AreEqual(language, parsed);
            }
            Assert.IsFalse(L10n.TryParse("fr", out _));
            Assert.IsFalse(L10n.TryParse(null, out _));
        }
    }
}
