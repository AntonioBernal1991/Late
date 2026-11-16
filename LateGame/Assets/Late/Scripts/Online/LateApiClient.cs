using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Late.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Late.Online
{
    /// <summary>
    /// Resultado de una llamada a la API. Si falla, <see cref="ErrorKey"/> es la clave del mensaje
    /// para el jugador (ver Strings) y <see cref="Status"/> el código HTTP (0 = sin conexión).
    /// </summary>
    public readonly struct ApiResult<T>
    {
        public int Status { get; }
        public T Data { get; }
        public string ErrorKey { get; }
        public bool Ok => ErrorKey == null;
        public bool IsNetworkError => Status == 0;

        private ApiResult(int status, T data, string errorKey)
        {
            Status = status;
            Data = data;
            ErrorKey = errorKey;
        }

        public static ApiResult<T> Success(int status, T data) => new ApiResult<T>(status, data, null);
        public static ApiResult<T> Failure(int status, string errorKey) => new ApiResult<T>(status, default, errorKey);
    }

    /// <summary>Lo que necesita el envío de partidas; separado para probarlo sin servidor.</summary>
    public interface IRunsApi
    {
        Task<ApiResult<SubmitRunsResponse>> SubmitRunsAsync(string token, SubmitRunsRequest request);
    }

    /// <summary>Cliente de la API REST de Late (server/). Los DTO son los de shared/, los mismos que usa el servidor.</summary>
    public class LateApiClient : IRunsApi
    {
        public static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",
        };

        // Un único HttpClient para toda la app (recomendación de .NET: reutiliza conexiones).
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        private readonly string _baseUrl;

        public LateApiClient(string baseUrl) => _baseUrl = baseUrl.TrimEnd('/');

        public string BaseUrl => _baseUrl;

        public Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request) =>
            SendAsync<AuthResponse>(HttpMethod.Post, "/api/auth/register", request, null, isLogin: true);

        public Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request) =>
            SendAsync<AuthResponse>(HttpMethod.Post, "/api/auth/login", request, null, isLogin: true);

        public Task<ApiResult<SubmitRunsResponse>> SubmitRunsAsync(string token, SubmitRunsRequest request) =>
            SendAsync<SubmitRunsResponse>(HttpMethod.Post, "/api/runs", request, token);

        public Task<ApiResult<ProgressResponse>> GetProgressAsync(string token) =>
            SendAsync<ProgressResponse>(HttpMethod.Get, "/api/progress", null, token);

        public Task<ApiResult<List<LevelDto>>> GetLevelsAsync() =>
            SendAsync<List<LevelDto>>(HttpMethod.Get, "/api/levels", null, null);

        public Task<ApiResult<RankingResponse>> GetRankingAsync(int levelOrder, int top = 10) =>
            SendAsync<RankingResponse>(HttpMethod.Get, $"/api/levels/{levelOrder}/ranking?top={top}", null, null);

        private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object body, string token, bool isLogin = false)
        {
            using (var request = new HttpRequestMessage(method, _baseUrl + path))
            {
                if (body != null)
                {
                    request.Content = new StringContent(JsonConvert.SerializeObject(body, Json), Encoding.UTF8, "application/json");
                }
                if (token != null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                HttpResponseMessage response;
                string text;
                try
                {
                    response = await Http.SendAsync(request).ConfigureAwait(false);
                    text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException || e is OperationCanceledException)
                {
                    // Sin red, servidor apagado o tiempo de espera agotado.
                    return ApiResult<T>.Failure(0, "error.network");
                }

                int status = (int)response.StatusCode;
                response.Dispose();
                if (status >= 200 && status < 300)
                {
                    try
                    {
                        return ApiResult<T>.Success(status, JsonConvert.DeserializeObject<T>(text, Json));
                    }
                    catch (JsonException)
                    {
                        return ApiResult<T>.Failure(status, "error.unknown");
                    }
                }
                return ApiResult<T>.Failure(status, ErrorKeyFor(status, text, isLogin));
            }
        }

        /// <summary>Traduce la respuesta de error de la API a un mensaje para el jugador.</summary>
        public static string ErrorKeyFor(int status, string body, bool isLogin)
        {
            switch (status)
            {
                case 401: return isLogin ? "error.credentials" : "error.sessionExpired";
                case 409: return isLogin ? "error.taken" : "error.unknown";
                case 429: return "error.tooMany";
                case 400:
                    // ValidationProblemDetails: { "errors": { "UserName": [...], ... } }
                    try
                    {
                        if (JObject.Parse(body)["errors"] is JObject errors)
                        {
                            if (errors["UserName"] != null) return "error.userName";
                            if (errors["Email"] != null) return "error.email";
                            if (errors["Password"] != null) return "error.password";
                        }
                    }
                    catch (JsonException)
                    {
                        // Respuesta sin JSON: error genérico.
                    }
                    return "error.unknown";
                default:
                    return "error.unknown";
            }
        }
    }
}
