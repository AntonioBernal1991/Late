using System;
using System.Globalization;
using Late.Data;
using Late.Shared;

namespace Late.Online
{
    /// <summary>
    /// Sesión del jugador: el token JWT y su nombre. Se guarda en la tabla de ajustes de SQLite para no
    /// tener que entrar cada vez que se abre el juego.
    /// </summary>
    public class AccountSession
    {
        public const string TokenKey = "cuenta.token";
        public const string UserNameKey = "cuenta.nombre";
        public const string ExpiresKey = "cuenta.caduca";

        private readonly ISettingsStore _store;
        private readonly Func<DateTime> _utcNow;

        public event Action Changed;

        public string Token { get; private set; }
        public string UserName { get; private set; }
        public DateTime ExpiresAtUtc { get; private set; }

        /// <summary>Hay token y no ha caducado. Si caduca, se trata como sesión cerrada.</summary>
        public bool IsLoggedIn => Token != null && _utcNow() < ExpiresAtUtc;

        /// <param name="store">Dónde guardar la sesión; null para no guardarla (si SQLite no está disponible).</param>
        public AccountSession(ISettingsStore store, Func<DateTime> utcNow = null)
        {
            _store = store;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            Load();
        }

        private void Load()
        {
            if (_store == null) return;
            string token = _store.LoadSetting(TokenKey);
            string expires = _store.LoadSetting(ExpiresKey);
            if (token == null || !DateTime.TryParse(expires, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime expiresAt))
            {
                return;
            }
            Token = token;
            UserName = _store.LoadSetting(UserNameKey);
            ExpiresAtUtc = expiresAt;
        }

        public void SignIn(AuthResponse auth)
        {
            Token = auth.Token;
            UserName = auth.UserName;
            ExpiresAtUtc = DateTime.SpecifyKind(auth.ExpiresAtUtc, DateTimeKind.Utc);
            _store?.SaveSetting(TokenKey, Token);
            _store?.SaveSetting(UserNameKey, UserName);
            _store?.SaveSetting(ExpiresKey, ExpiresAtUtc.ToString("o", CultureInfo.InvariantCulture));
            Changed?.Invoke();
        }

        /// <summary>Cierra la sesión. Las partidas pendientes se quedan y se enviarán con la próxima cuenta que entre.</summary>
        public void SignOut()
        {
            Token = null;
            UserName = null;
            ExpiresAtUtc = default;
            _store?.SaveSetting(TokenKey, null);
            _store?.SaveSetting(UserNameKey, null);
            _store?.SaveSetting(ExpiresKey, null);
            Changed?.Invoke();
        }
    }
}
