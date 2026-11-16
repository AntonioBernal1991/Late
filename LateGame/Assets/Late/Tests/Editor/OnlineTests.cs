using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Late.Data;
using Late.Game;
using Late.Online;
using Late.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Late.Tests
{
    /// <summary>R06F01 en el cliente: envío de pendientes, sesión y formato de la API.</summary>
    public class OnlineTests
    {
        // ---------- Dobles de prueba ----------

        private class FakePendingStore : IPendingResultsStore
        {
            public readonly List<PendingResult> Pending = new List<PendingResult>();
            public bool IgnoreRemovals;

            public IReadOnlyList<PendingResult> LoadPendingResults() => Pending.ToList();

            public void RemovePendingResults(IEnumerable<int> ids)
            {
                if (IgnoreRemovals) return;
                var set = new HashSet<int>(ids);
                Pending.RemoveAll(p => set.Contains(p.Id));
            }
        }

        private class FakeRunsApi : IRunsApi
        {
            public readonly List<SubmitRunsRequest> Requests = new List<SubmitRunsRequest>();
            public Func<int, ApiResult<SubmitRunsResponse>> Respond =
                _ => ApiResult<SubmitRunsResponse>.Success(200, new SubmitRunsResponse());

            public Task<ApiResult<SubmitRunsResponse>> SubmitRunsAsync(string token, SubmitRunsRequest request)
            {
                Requests.Add(request);
                return Task.FromResult(Respond(Requests.Count));
            }
        }

        private class FakeSettings : ISettingsStore
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public string LoadSetting(string key, string defaultValue = null) => Values.TryGetValue(key, out string v) ? v : defaultValue;
            public void SaveSetting(string key, string value)
            {
                if (value == null) Values.Remove(key);
                else Values[key] = value;
            }
        }

        private static readonly DateTime Day = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);

        private static FakePendingStore StoreWith(int count)
        {
            var store = new FakePendingStore();
            for (int i = 1; i <= count; i++)
            {
                store.Pending.Add(new PendingResult(i, new RunResult(i % 2, 50f + i, i % 3 == 0), Day.AddSeconds(i)));
            }
            return store;
        }

        private static SyncOutcome Sync(PendingResultsUploader uploader, string token = "token") =>
            uploader.SyncAsync(token).GetAwaiter().GetResult();

        // ---------- Envío de pendientes ----------

        [Test]
        public void SinSesion_NoEnviaNada()
        {
            FakePendingStore store = StoreWith(3);
            var api = new FakeRunsApi();

            SyncOutcome outcome = Sync(new PendingResultsUploader(store, api), token: null);

            Assert.AreEqual(SyncStatus.NotLoggedIn, outcome.Status);
            Assert.AreEqual(0, api.Requests.Count);
            Assert.AreEqual(3, store.Pending.Count);
        }

        [Test]
        public void EnviaTodoEnLotesDe100YLoBorra()
        {
            FakePendingStore store = StoreWith(250);
            var api = new FakeRunsApi();

            SyncOutcome outcome = Sync(new PendingResultsUploader(store, api));

            Assert.AreEqual(SyncStatus.Done, outcome.Status);
            Assert.AreEqual(250, outcome.Sent);
            CollectionAssert.AreEqual(new[] { 100, 100, 50 }, api.Requests.Select(r => r.Runs.Count));
            Assert.AreEqual(0, store.Pending.Count);
        }

        [Test]
        public void SinConexion_LasPendientesSeQuedan()
        {
            FakePendingStore store = StoreWith(150);
            var api = new FakeRunsApi
            {
                // El primer lote llega; en el segundo se corta la conexión.
                Respond = call => call == 1
                    ? ApiResult<SubmitRunsResponse>.Success(200, new SubmitRunsResponse())
                    : ApiResult<SubmitRunsResponse>.Failure(0, "error.network")
            };

            SyncOutcome outcome = Sync(new PendingResultsUploader(store, api));

            Assert.AreEqual(SyncStatus.Offline, outcome.Status);
            Assert.AreEqual(100, outcome.Sent);
            Assert.AreEqual(50, store.Pending.Count);
        }

        [Test]
        public void TokenCaducado_SesionCaducadaYNoSeBorraNada()
        {
            FakePendingStore store = StoreWith(5);
            var api = new FakeRunsApi { Respond = _ => ApiResult<SubmitRunsResponse>.Failure(401, "error.sessionExpired") };

            SyncOutcome outcome = Sync(new PendingResultsUploader(store, api));

            Assert.AreEqual(SyncStatus.SessionExpired, outcome.Status);
            Assert.AreEqual(5, store.Pending.Count);
        }

        [Test]
        public void SiElBorradoFalla_NoReenviaSinFin()
        {
            FakePendingStore store = StoreWith(5);
            store.IgnoreRemovals = true;
            var api = new FakeRunsApi();

            SyncOutcome outcome = Sync(new PendingResultsUploader(store, api));

            Assert.AreEqual(SyncStatus.ServerError, outcome.Status);
            Assert.AreEqual(1, api.Requests.Count);
        }

        [Test]
        public void LaPartidaSeEnviaConSusDatos()
        {
            var pending = new PendingResult(7, new RunResult(1, 61.25f, true), Day.AddMilliseconds(345));

            RunSubmission submission = PendingResultsUploader.ToSubmission(pending);

            Assert.AreEqual(1, submission.LevelOrder);
            Assert.AreEqual(61.25, submission.ElapsedSeconds, 1e-4);
            Assert.IsTrue(submission.BeatTheMusic);
            Assert.AreEqual(Day.AddMilliseconds(345), submission.PlayedAtUtc);
        }

        // ---------- Sesión ----------

        [Test]
        public void LaSesionSeGuardaYSeRecupera()
        {
            var settings = new FakeSettings();
            DateTime now = Day;
            var session = new AccountSession(settings, () => now);

            session.SignIn(new AuthResponse { Token = "abc", UserName = "ana", ExpiresAtUtc = Day.AddDays(30) });
            var reopened = new AccountSession(settings, () => now);

            Assert.IsTrue(reopened.IsLoggedIn);
            Assert.AreEqual("abc", reopened.Token);
            Assert.AreEqual("ana", reopened.UserName);
            Assert.AreEqual(Day.AddDays(30), reopened.ExpiresAtUtc);
        }

        [Test]
        public void SesionCaducada_ComoSiNoHubieraEntrado()
        {
            var settings = new FakeSettings();
            DateTime now = Day;
            var session = new AccountSession(settings, () => now);
            session.SignIn(new AuthResponse { Token = "abc", UserName = "ana", ExpiresAtUtc = Day.AddDays(30) });

            now = Day.AddDays(31);

            Assert.IsFalse(session.IsLoggedIn);
        }

        [Test]
        public void CerrarSesion_BorraLoGuardado()
        {
            var settings = new FakeSettings();
            var session = new AccountSession(settings, () => Day);
            session.SignIn(new AuthResponse { Token = "abc", UserName = "ana", ExpiresAtUtc = Day.AddDays(30) });

            session.SignOut();

            Assert.IsFalse(session.IsLoggedIn);
            Assert.AreEqual(0, settings.Values.Count);
            Assert.IsFalse(new AccountSession(settings, () => Day).IsLoggedIn);
        }

        // ---------- Formato de la API ----------

        [Test]
        public void ElJsonEnviadoEsElQueEsperaLaApi()
        {
            var request = new SubmitRunsRequest
            {
                Runs = { new RunSubmission { LevelOrder = 0, ElapsedSeconds = 52.3, BeatTheMusic = true, PlayedAtUtc = Day.AddMilliseconds(123) } }
            };

            JObject json = JObject.Parse(JsonConvert.SerializeObject(request, LateApiClient.Json));
            var run = (JObject)json["runs"][0];

            Assert.AreEqual(0, (int)run["levelOrder"]);
            Assert.AreEqual(52.3, (double)run["elapsedSeconds"], 1e-9);
            Assert.AreEqual(true, (bool)run["beatTheMusic"]);
            Assert.AreEqual("2026-11-16T10:00:00.123Z", run["playedAtUtc"].ToString(Formatting.None).Trim('"'));
        }

        [Test]
        public void SeLeeLaRespuestaDeLaApi()
        {
            // Respuesta real del servidor (System.Text.Json, camelCase).
            const string body = "{\"levelOrder\":0,\"levelName\":\"Run0\",\"entries\":[{\"position\":1,\"userName\":\"demo\",\"bestSeconds\":49.87,\"achievedAtUtc\":\"2026-11-16T10:05:00Z\"}]}";

            var ranking = JsonConvert.DeserializeObject<RankingResponse>(body, LateApiClient.Json);

            Assert.AreEqual("Run0", ranking.LevelName);
            Assert.AreEqual("demo", ranking.Entries[0].UserName);
            Assert.AreEqual(49.87, ranking.Entries[0].BestSeconds, 1e-9);
            Assert.AreEqual(new DateTime(2026, 11, 16, 10, 5, 0, DateTimeKind.Utc), ranking.Entries[0].AchievedAtUtc);
            Assert.AreEqual(DateTimeKind.Utc, ranking.Entries[0].AchievedAtUtc.Kind);
        }

        [TestCase(401, "", true, "error.credentials")]
        [TestCase(401, "", false, "error.sessionExpired")]
        [TestCase(409, "", true, "error.taken")]
        [TestCase(429, "", true, "error.tooMany")]
        [TestCase(400, "{\"errors\":{\"Password\":[\"Entre 8 y 128 caracteres.\"]}}", true, "error.password")]
        [TestCase(400, "{\"errors\":{\"UserName\":[\"...\"]}}", true, "error.userName")]
        [TestCase(400, "no es json", true, "error.unknown")]
        [TestCase(500, "", false, "error.unknown")]
        public void ErroresDeLaApi_MensajeParaElJugador(int status, string body, bool isLogin, string expected)
        {
            Assert.AreEqual(expected, LateApiClient.ErrorKeyFor(status, body, isLogin));
        }

        [Test]
        public void ServidorApagado_ErrorDeConexion()
        {
            // Un puerto donde no escucha nadie.
            var client = new LateApiClient("http://127.0.0.1:9");

            ApiResult<RankingResponse> result = Task.Run(() => client.GetRankingAsync(0)).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
            Assert.IsTrue(result.IsNetworkError);
            Assert.AreEqual("error.network", result.ErrorKey);
        }
    }
}
