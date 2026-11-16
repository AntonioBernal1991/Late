using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Late.Data;
using Late.Shared;

namespace Late.Online
{
    public enum SyncStatus
    {
        /// <summary>No había nada pendiente o se envió todo.</summary>
        Done,
        NotLoggedIn,
        Offline,
        SessionExpired,
        ServerError,
    }

    public readonly struct SyncOutcome
    {
        public SyncStatus Status { get; }

        /// <summary>Partidas que el servidor confirmó y se borraron de pendientes.</summary>
        public int Sent { get; }

        public SyncOutcome(SyncStatus status, int sent)
        {
            Status = status;
            Sent = sent;
        }
    }

    /// <summary>
    /// Envía al servidor las partidas guardadas en SQLite (R06F01: «enviar los pendientes al recuperar
    /// la conexión»). Solo se borran de pendientes cuando el servidor responde bien; si se corta la
    /// conexión a mitad, se reenvían y el servidor descarta las repetidas.
    /// </summary>
    public class PendingResultsUploader
    {
        /// <summary>Máximo que acepta la API por petición.</summary>
        public const int BatchSize = 100;

        private readonly IPendingResultsStore _store;
        private readonly IRunsApi _api;

        public PendingResultsUploader(IPendingResultsStore store, IRunsApi api)
        {
            _store = store;
            _api = api;
        }

        public async Task<SyncOutcome> SyncAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return new SyncOutcome(SyncStatus.NotLoggedIn, 0);

            int sent = 0;
            var removed = new HashSet<int>();
            while (true)
            {
                List<PendingResult> batch = _store.LoadPendingResults().Take(BatchSize).ToList();
                if (batch.Count == 0) return new SyncOutcome(SyncStatus.Done, sent);

                // Si algo ya enviado sigue ahí, el borrado no funcionó: parar en vez de reenviar sin fin.
                if (batch.Any(p => removed.Contains(p.Id))) return new SyncOutcome(SyncStatus.ServerError, sent);

                var request = new SubmitRunsRequest { Runs = batch.Select(ToSubmission).ToList() };
                ApiResult<SubmitRunsResponse> result = await _api.SubmitRunsAsync(token, request);

                if (!result.Ok)
                {
                    SyncStatus status = result.IsNetworkError ? SyncStatus.Offline
                        : result.Status == 401 ? SyncStatus.SessionExpired
                        : SyncStatus.ServerError;
                    return new SyncOutcome(status, sent);
                }

                // Aceptadas, ya guardadas y rechazadas: ninguna necesita volver a enviarse.
                _store.RemovePendingResults(batch.Select(p => p.Id));
                removed.UnionWith(batch.Select(p => p.Id));
                sent += batch.Count;
            }
        }

        public static RunSubmission ToSubmission(PendingResult pending) => new RunSubmission
        {
            LevelOrder = pending.Result.RunIndex,
            ElapsedSeconds = pending.Result.ElapsedSeconds,
            BeatTheMusic = pending.Result.BeatTheMusic,
            PlayedAtUtc = pending.DateUtc,
        };
    }
}
