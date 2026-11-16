using System;
using System.Threading.Tasks;
using Late.Online;
using Late.Shared;
using NUnit.Framework;

namespace Late.Tests
{
    /// <summary>
    /// Juego y servidor juntos: el cliente de Unity contra la API real levantada con Docker
    /// (docker compose up en docker/). Si la API no está en marcha, las pruebas se omiten.
    /// </summary>
    [Category("Online")]
    public class LiveApiTests
    {
        private const string Url = "http://localhost:5080";
        private LateApiClient _client;

        private static T Run<T>(Func<Task<T>> call) => Task.Run(call).GetAwaiter().GetResult();

        [OneTimeSetUp]
        public void CheckServer()
        {
            _client = new LateApiClient(Url);
            if (Run(() => _client.GetLevelsAsync()).IsNetworkError)
            {
                Assert.Ignore($"La API no responde en {Url}: arranca docker compose para estas pruebas.");
            }
        }

        [Test]
        public void RegistrarseEnviarYVerElRanking()
        {
            string name = "unity" + Guid.NewGuid().ToString("N").Substring(0, 8);
            ApiResult<AuthResponse> auth = Run(() => _client.RegisterAsync(new RegisterRequest
            {
                UserName = name, Email = name + "@late.test", Password = "contraseña-de-prueba",
            }));
            Assert.IsTrue(auth.Ok, auth.ErrorKey);

            // Un tiempo imposible de batir para salir primero en el ranking de Run1.
            var run = new RunSubmission { LevelOrder = 1, ElapsedSeconds = 0.01, BeatTheMusic = true, PlayedAtUtc = DateTime.UtcNow };
            var request = new SubmitRunsRequest { Runs = { run } };
            ApiResult<SubmitRunsResponse> first = Run(() => _client.SubmitRunsAsync(auth.Data.Token, request));
            ApiResult<SubmitRunsResponse> again = Run(() => _client.SubmitRunsAsync(auth.Data.Token, request));

            Assert.AreEqual(1, first.Data.Accepted);
            Assert.AreEqual(1, again.Data.AlreadyStored);

            // Ganar Run1 deja desbloqueado el siguiente en la cuenta, se juegue donde se juegue.
            ApiResult<ProgressResponse> progress = Run(() => _client.GetProgressAsync(auth.Data.Token));
            Assert.IsTrue(progress.Ok, progress.ErrorKey);
            Assert.AreEqual(2, progress.Data.HighestUnlockedLevel);

            ApiResult<RankingResponse> ranking = Run(() => _client.GetRankingAsync(1));
            Assert.IsTrue(ranking.Ok);
            Assert.IsTrue(ranking.Data.Entries.Exists(e => e.UserName == name), "El jugador debería estar en el ranking");
        }

        [Test]
        public void ContraseñaIncorrecta_MensajeDeCredenciales()
        {
            ApiResult<AuthResponse> result = Run(() => _client.LoginAsync(new LoginRequest
            {
                UserNameOrEmail = "nadie-" + Guid.NewGuid().ToString("N"), Password = "contraseña-de-prueba",
            }));

            Assert.AreEqual(401, result.Status);
            Assert.AreEqual("error.credentials", result.ErrorKey);
        }
    }
}
