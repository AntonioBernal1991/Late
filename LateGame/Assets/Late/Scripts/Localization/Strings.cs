using System.Collections.Generic;

namespace Late.Localization
{
    /// <summary>
    /// Todos los textos del juego en español e inglés. Una prueba comprueba que ninguna clave
    /// tenga un idioma sin traducir.
    /// </summary>
    public static class Strings
    {
        // clave → (español, inglés)
        private static readonly Dictionary<string, (string Es, string En)> Table = new Dictionary<string, (string, string)>
        {
            // ---------- Menú y escenas ----------
            ["menu.tree"] = (
                "/L A T E\n├── niveles\r\n│   ├── \n│   ├── \n│   ├── \n│   ├── \n│   └── \n├── ajustes\r\n│   ├── audio\r\n│   └── vídeo\r\n└── observador.log\r\n",
                "/L A T E\n├── run\r\n│   ├── \n│   ├── \n│   ├── \n│   ├── \n│   └── \n├── settings\r\n│   ├── audio\r\n│   └── video\r\n└── observer.log\r\n"),
            // Los botones de la escena tienen el ancho del texto inglés: el español no debe ser más largo.
            ["tap.getin"] = ("toca y entra -->", "tap to get in -->"),
            ["tap.continue"] = ("toca y sigue -->", "tap to continue -->"),
            ["end.completed"] = ("   Proceso completado", "   Process Completed"),
            ["end.terminated"] = ("   Proceso terminado", "   Process Terminated"),
            ["end.good"] = ("Suficiente", "Good Enough"),
            ["end.bad"] = ("Demasiado tarde", "Too Bad"),
            ["key.look"] = ("busca la llave", "look 4 the key"),

            // ---------- Barra online ----------
            ["online.account"] = ("> cuenta", "> account"),
            ["online.ranking"] = ("> ranking", "> ranking"),
            ["online.back"] = ("<-- volver", "<-- back"),

            // ---------- Cuenta ----------
            ["account.title"] = ("/L A T E/cuenta", "/L A T E/account"),
            ["account.mode.login"] = ("entrar", "log in"),
            ["account.mode.register"] = ("crear cuenta", "sign up"),
            ["account.field.user"] = ("nombre", "username"),
            ["account.field.userOrEmail"] = ("nombre o email", "username or email"),
            ["account.field.email"] = ("email", "email"),
            ["account.field.password"] = ("contraseña", "password"),
            ["account.submit.login"] = ("> entrar", "> log in"),
            ["account.submit.register"] = ("> crear cuenta", "> sign up"),
            ["account.loggedAs"] = ("conectado como {0}", "logged in as {0}"),
            ["account.pending"] = ("partidas sin enviar: {0}", "runs not sent yet: {0}"),
            ["account.syncNow"] = ("> enviar ahora", "> send now"),
            ["account.logout"] = ("> cerrar sesión", "> log out"),
            ["account.working"] = ("conectando...", "connecting..."),
            ["account.welcome"] = ("hola, {0}", "hello, {0}"),
            ["account.synced"] = ("enviadas: {0}", "sent: {0}"),
            ["account.required"] = ("necesitas una cuenta para jugar: tu progreso se guarda en ella y lo recuperas en cualquier móvil",
                                    "you need an account to play: your progress is saved in it and follows you to any phone"),
            ["account.logoutPending"] = ("hay partidas sin enviar: conéctate para enviarlas antes de cerrar sesión",
                                         "some runs haven't been sent: get online to send them before logging out"),

            // ---------- Ranking ----------
            ["ranking.title"] = ("/L A T E/ranking", "/L A T E/ranking"),
            ["ranking.loading"] = ("cargando...", "loading..."),
            ["ranking.empty"] = ("nadie ha ganado a la música aquí todavía", "nobody has beaten the music here yet"),
            ["ranking.you"] = ("(tú)", "(you)"),

            // ---------- Errores ----------
            ["error.network"] = ("sin conexión con el servidor", "can't reach the server"),
            ["error.credentials"] = ("usuario o contraseña incorrectos", "wrong username or password"),
            ["error.taken"] = ("ese nombre o email ya existe", "that username or email is taken"),
            ["error.tooMany"] = ("demasiados intentos, espera un minuto", "too many attempts, wait a minute"),
            ["error.userName"] = ("nombre: 3-30 letras, números, _ . -", "username: 3-30 letters, digits, _ . -"),
            ["error.email"] = ("email no válido", "invalid email"),
            ["error.password"] = ("contraseña: al menos 8 caracteres", "password: at least 8 characters"),
            ["error.sessionExpired"] = ("la sesión ha caducado, entra otra vez", "session expired, please log in again"),
            ["error.unknown"] = ("error del servidor ({0})", "server error ({0})"),

            // ---------- Idioma ----------
            ["language.toggle"] = ("[es] en", "es [en]"),
        };

        public static IEnumerable<string> Keys => Table.Keys;

        public static string Get(string key, Language language)
        {
            if (!Table.TryGetValue(key, out (string Es, string En) entry)) return null;
            return language == Language.Spanish ? entry.Es : entry.En;
        }
    }
}
