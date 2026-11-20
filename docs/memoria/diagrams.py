"""Diagramas SVG de la memoria de Late (estilo sobrio, colores de la plantilla thePower/Prometeo)."""
from datetime import date

INK = "#1d2b2c"
TEAL = "#1f3a3d"
LINE = "#5b6f70"
SOFT = "#eef3f2"
LIME = "#b9e35a"
QUIET = "#5b6668"
FONT = "font-family='Arial, Helvetica, sans-serif'"


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def svg(w, h, body, title):
    defs = f"""<defs>
<marker id='a-open' viewBox='0 0 10 10' refX='9' refY='5' markerWidth='8' markerHeight='8' orient='auto-start-reverse'><path d='M0 0L10 5L0 10' fill='none' stroke='{LINE}' stroke-width='1.4'/></marker>
<marker id='a-tri' viewBox='0 0 12 12' refX='11' refY='6' markerWidth='11' markerHeight='11' orient='auto-start-reverse'><path d='M0 0L12 6L0 12z' fill='white' stroke='{LINE}' stroke-width='1.2'/></marker>
<marker id='a-fill' viewBox='0 0 10 10' refX='9' refY='5' markerWidth='7' markerHeight='7' orient='auto-start-reverse'><path d='M0 0L10 5L0 10z' fill='{LINE}'/></marker>
</defs>"""
    return (f"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {w} {h}' width='100%' role='img' aria-label='{esc(title)}' {FONT}>"
            f"{defs}{body}</svg>")


def text(x, y, s, size=11, weight="normal", fill=INK, anchor="start", italic=False, extra=""):
    st = " font-style='italic'" if italic else ""
    return f"<text x='{x}' y='{y}' font-size='{size}' font-weight='{weight}' fill='{fill}' text-anchor='{anchor}'{st} {extra}>{esc(s)}</text>"


def line(x1, y1, x2, y2, kind="assoc", label=None, lx=None, ly=None):
    dash = " stroke-dasharray='5 4'" if kind in ("impl", "dep", "dash") else ""
    marker = {"assoc": "url(#a-open)", "impl": "url(#a-tri)", "dep": "url(#a-open)", "dash": "url(#a-fill)", "flow": "url(#a-fill)", "none": ""}[kind]
    m = f" marker-end='{marker}'" if marker else ""
    out = f"<line x1='{x1}' y1='{y1}' x2='{x2}' y2='{y2}' stroke='{LINE}' stroke-width='1.3'{dash}{m}/>"
    if label:
        out += text(lx, ly, label, 10, fill=QUIET, anchor="middle")
    return out


def path(d, kind="flow", label=None, lx=None, ly=None, both=False):
    dash = " stroke-dasharray='5 4'" if kind in ("dash", "dep") else ""
    start = " marker-start='url(#a-fill)'" if both else ""
    out = f"<path d='{d}' fill='none' stroke='{LINE}' stroke-width='1.3'{dash}{start} marker-end='url(#a-fill)'/>"
    if label:
        out += text(lx, ly, label, 10, fill=QUIET, anchor="middle")
    return out


# ---------- UML ----------

LH = 16  # alto de línea en los compartimentos


def uml(x, y, w, name, stereo=None, attrs=(), meths=()):
    head = 38 if stereo else 25
    ah = 8 + LH * len(attrs) if attrs else 12
    mh = 8 + LH * len(meths) if meths else 12
    h = head + ah + mh
    o = [f"<rect x='{x}' y='{y}' width='{w}' height='{h}' rx='3' fill='white' stroke='{TEAL}' stroke-width='1.3'/>",
         f"<rect x='{x}' y='{y}' width='{w}' height='{head}' rx='3' fill='{SOFT}' stroke='{TEAL}' stroke-width='1.3'/>"]
    cy = y + 16
    if stereo:
        o.append(text(x + w / 2, cy, f"«{stereo}»", 10.5, fill=QUIET, anchor="middle", italic=True))
        cy += 14
    o.append(text(x + w / 2, cy + 2, name, 13, "bold", TEAL, "middle"))
    yy = y + head
    o.append(f"<line x1='{x}' y1='{yy + ah}' x2='{x + w}' y2='{yy + ah}' stroke='{TEAL}' stroke-width='1'/>")
    for i, a in enumerate(attrs):
        o.append(text(x + 8, yy + 17 + LH * i, a, 11.5, fill=INK))
    for i, m in enumerate(meths):
        o.append(text(x + 8, yy + ah + 17 + LH * i, m, 11.5, fill=INK))
    return "".join(o), dict(x=x, y=y, w=w, h=h)


def hconn(src, dst, kind="assoc"):
    """Conector horizontal entre dos cajas con alturas solapadas (a media altura del solape)."""
    y = (max(src["y"], dst["y"]) + min(src["y"] + src["h"], dst["y"] + dst["h"])) / 2
    if src["x"] < dst["x"]:
        return line(src["x"] + src["w"], y, dst["x"], y, kind)
    return line(src["x"], y, dst["x"] + dst["w"], y, kind)


def vconn(src, dst, kind="assoc", x=None):
    """Conector vertical entre dos cajas con anchos solapados."""
    if x is None:
        x = (max(src["x"], dst["x"]) + min(src["x"] + src["w"], dst["x"] + dst["w"])) / 2
    if src["y"] < dst["y"]:
        return line(x, src["y"] + src["h"], x, dst["y"], kind)
    return line(x, src["y"], x, dst["y"] + dst["h"], kind)


def bottom(*boxes):
    return max(b["y"] + b["h"] for b in boxes)


def class_gen():
    b = []
    s, imap = uml(10, 10, 230, "IMapGenerator", "interface", ["Settings : MapSettings", "Pacer : GenerationPacer"], ["+ EnqueueNextModule()"]); b.append(s)
    s, gen = uml(270, 10, 230, "MapGenerator3D", "MonoBehaviour", ["- _settings : MapSettings", "- _queue : ModuleQueue", "+ MazeRoot : Transform"], ["- Start()", "+ EnqueueNextModule()"]); b.append(s)
    s, ms = uml(530, 10, 220, "MapSettings", "ScriptableObject", ["- _seed, _moduleCount : int", "- _cavesEnabled, _caveSize", "+ BaseSeed : int"], ["+ TileLocalPosition()"]); b.append(s)
    y2 = bottom(imap, gen, ms) + 48
    s, pg = uml(10, y2, 230, "PathGenerator", None, ["- _baseSeed : int"], ["+ GeneratePath()", "- GenerateBranch()"]); b.append(s)
    s, mg = uml(270, y2, 230, "ModuleGenerator", None, ["- _queue : ModuleQueue", "- _pathGenerator"], ["+ Generate(moduleCount)", "- GenerateModule()"]); b.append(s)
    s, mq = uml(530, y2, 220, "ModuleQueue", None, ["- _queue : Queue<ModuleInfo>"], ["+ Enqueue() / Dequeue()", "+ IsTooClose()"]); b.append(s)
    y3 = bottom(pg, mg, mq) + 48
    s, mc = uml(270, y3, 230, "MeshCombiner", "static", [], ["+ CombineMeshesByMaterial()"]); b.append(s)
    s, mo = uml(530, y3, 220, "MapOptimizer", "static · editor", [], ["+ Optimize(root, options)", "+ MapStats.Measure()"]); b.append(s)
    b += [hconn(gen, imap, "impl"), hconn(gen, ms), vconn(gen, mg), hconn(mg, pg), hconn(mg, mq), vconn(mg, mc),
          vconn(pg, imap), hconn(mo, mc, "dep")]
    return svg(760, bottom(mc, mo) + 10, "".join(b), "Diagrama de clases del generador procedural")


def class_game():
    b = []
    s, gm = uml(10, 10, 230, "GameManager", "MonoBehaviour", ["- runTuningsByIndex", "+ RunFinished : event"], ["+ StartRun()", "- OnEndReached()"]); b.append(s)
    s, lm = uml(270, 10, 240, "LevelManager", "MonoBehaviour", ["+ StoreFactory", "- _store : IProgressStore"], ["+ IsUnlocked(runIndex)", "+ ReloadProgress()"]); b.append(s)
    s, rp = uml(540, 10, 210, "RunProgression", "static", [], ["+ HighestUnlockedAfter()", "+ IsUnlocked()"]); b.append(s)
    y2 = bottom(gm, lm, rp) + 48
    s, mem = uml(10, y2 + 20, 230, "InMemoryProgressStore", None, ["- _results : List<RunResult>"], []); b.append(s)
    s, ips = uml(270, y2, 240, "IProgressStore", "interface", [], ["+ LoadHighestUnlockedRun()", "+ SaveHighestUnlockedRun()", "+ SaveRunResult(RunResult)"]); b.append(s)
    y3 = bottom(ips, mem) + 48
    s, sq = uml(270, y3, 240, "SqliteProgressStore", None, ["- _db : SQLiteConnection"], ["+ LoadPendingResults()", "+ ApplyAccountProgress()", "+ LoadSetting() / SaveSetting()"]); b.append(s)
    s, boot = uml(10, y3 + 10, 230, "LocalDataBootstrap", "static", ["+ DatabasePath : string"], ["+ OpenStore()"]); b.append(s)
    s, ipr = uml(540, y3 - 40, 210, "IPendingResultsStore", "interface", [], ["+ LoadPendingResults()", "+ RemovePendingResults()"]); b.append(s)
    s, iss = uml(540, bottom(ipr) + 20, 210, "ISettingsStore", "interface", [], ["+ LoadSetting()", "+ SaveSetting()"]); b.append(s)
    b += [hconn(gm, lm), hconn(lm, rp), vconn(lm, ips), hconn(mem, ips, "impl"), vconn(sq, ips, "impl"),
          hconn(sq, ipr, "impl"), hconn(sq, iss, "impl"), hconn(boot, sq)]
    return svg(760, bottom(sq, boot, iss) + 10, "".join(b), "Diagrama de clases del juego y los datos locales")


def class_online():
    b = []
    s, os_ = uml(10, 30, 250, "OnlineService", "MonoBehaviour", ["+ Session : AccountSession", "+ Api : LateApiClient"], ["+ SyncNowAsync()", "+ LoginAsync() / RegisterAsync()", "+ LogoutAsync()"]); b.append(s)
    s, om = uml(10, bottom(os_) + 48, 250, "OnlineMenu", "MonoBehaviour", ["- _accountPanel, _rankingPanel"], ["- Update(): cuenta obligatoria", "- Submit()", "- OpenRanking()"]); b.append(s)
    s, acc = uml(290, 30, 210, "AccountSession", None, ["+ Token, UserName"], ["+ IsLoggedIn : bool", "+ SignIn() / SignOut()"]); b.append(s)
    s, up = uml(290, bottom(acc) + 24, 210, "PendingResultsUploader", None, ["- _store, _api"], ["+ SyncAsync(token)"]); b.append(s)
    s, api = uml(290, bottom(up) + 34, 210, "LateApiClient", "IRunsApi", [], ["+ LoginAsync()", "+ SubmitRunsAsync()", "+ GetRankingAsync()", "+ GetProgressAsync()"]); b.append(s)
    sx, sw = 545, 205
    s, au = uml(sx, 46, sw, "AuthController", None, [], ["+ Register()", "+ Login()"]); b.append(s)
    s, ru = uml(sx, bottom(au) + 12, sw, "RunsController", None, [], ["+ Submit()"]); b.append(s)
    s, le = uml(sx, bottom(ru) + 12, sw, "LevelsController", None, [], ["+ GetLevels()", "+ GetRanking()"]); b.append(s)
    s, pr = uml(sx, bottom(le) + 12, sw, "ProgressController", None, [], ["+ Get()"]); b.append(s)
    s, dbx = uml(sx, bottom(pr) + 34, sw, "LateDbContext", "DbContext", ["Users, Levels, Runs"], []); b.append(s)
    h = max(bottom(dbx), bottom(om), bottom(api)) + 14
    b.insert(0, f"<rect x='{sx - 12}' y='6' width='{sw + 24}' height='{h - 10}' rx='6' fill='none' stroke='{LINE}' stroke-dasharray='5 4'/>")
    b.append(text(sx + sw / 2, 30, "Servidor · Late.Api", 12, "bold", TEAL, "middle"))
    b.append(text(10, 20, "Cliente · Late.Online", 12, "bold", TEAL))
    b += [hconn(os_, acc), line(260, os_["y"] + 95, 290, up["y"] + 30, "assoc"), line(260, os_["y"] + os_["h"] - 10, 290, api["y"] + 30, "assoc"),
          vconn(om, os_), vconn(up, api), vconn(pr, dbx)]
    yy = api["y"] + 40
    b.append(line(500, yy, sx - 12, yy, "dash"))
    b.append(text((500 + sx - 12) / 2, yy - 6, "HTTPS", 10, fill=QUIET, anchor="middle"))
    b.append(text(sx + sw / 2 + 8, bottom(pr) + 21, "todos los controladores", 10, fill=QUIET))
    return svg(760, h, "".join(b), "Diagrama de clases de la parte online")


# ---------- Casos de uso ----------

def actor(cx, y, name):
    return (f"<circle cx='{cx}' cy='{y}' r='9' fill='none' stroke='{TEAL}' stroke-width='1.4'/>"
            f"<path d='M{cx} {y + 9}V{y + 38}M{cx - 16} {y + 20}H{cx + 16}M{cx} {y + 38}L{cx - 13} {y + 58}M{cx} {y + 38}L{cx + 13} {y + 58}' stroke='{TEAL}' stroke-width='1.4' fill='none'/>"
            + text(cx, y + 74, name, 11, "bold", TEAL, "middle"))


def ellipse(cx, cy, label, sub):
    return (f"<ellipse cx='{cx}' cy='{cy}' rx='88' ry='26' fill='{SOFT}' stroke='{TEAL}' stroke-width='1.2'/>"
            + text(cx, cy - 2, label, 10.5, "bold", TEAL, "middle") + text(cx, cy + 12, sub, 10, fill=INK, anchor="middle"))


def usecases():
    b = [f"<rect x='225' y='18' width='520' height='360' rx='8' fill='none' stroke='{LINE}'/>", text(240, 38, "Late", 12, "bold", TEAL)]
    b.append(actor(105, 46, "Diseñador"))
    b.append(actor(105, 200, "Jugador"))
    b.append(ellipse(370, 70, "CU-01", "Crear un nivel"))
    b.append(ellipse(370, 160, "CU-04", "Registrarse / entrar"))
    b.append(ellipse(370, 250, "CU-02", "Jugar un nivel"))
    b.append(ellipse(370, 340, "CU-05", "Consultar el ranking"))
    b.append(ellipse(620, 250, "CU-03", "Llave y puerta"))
    b.append(line(125, 70, 282, 70, "none"))
    b.append(line(125, 225, 284, 166, "none"))
    b.append(line(125, 232, 282, 250, "none"))
    b.append(line(125, 240, 284, 334, "none"))
    b.append(line(370, 224, 370, 187, "dash"))
    b.append(text(378, 210, "«include»", 9.5, fill=QUIET, italic=True))
    b.append(path("M458 340H500V160H459", "dash"))
    b.append(text(508, 300, "«include»", 9.5, fill=QUIET, italic=True))
    b.append(line(532, 250, 459, 250, "dash"))
    b.append(text(495, 243, "«extend»", 9.5, fill=QUIET, italic=True, anchor="middle"))
    return svg(760, 392, "".join(b), "Diagrama de casos de uso")


# ---------- E/R ----------

def er():
    b = []
    def entity(x, name, attrs):
        o = f"<rect x='{x}' y='40' width='140' height='44' fill='{SOFT}' stroke='{TEAL}' stroke-width='1.4'/>" + text(x + 70, 67, name, 13, "bold", TEAL, "middle")
        for i, (a, pk) in enumerate(attrs):
            deco = " text-decoration='underline'" if pk else ""
            o += f"<text x='{x + 70}' y='{110 + 15 * i}' font-size='10.5' fill='{INK}' text-anchor='middle'{deco}>{esc(a)}</text>"
        return o
    def diamond(cx, label):
        return (f"<polygon points='{cx - 50},62 {cx},36 {cx + 50},62 {cx},88' fill='white' stroke='{TEAL}' stroke-width='1.3'/>"
                + text(cx, 66, label, 11, fill=INK, anchor="middle"))
    b.append(entity(20, "USUARIO", [("id", True), ("nombre", False), ("email", False), ("password_hash", False), ("fecha_alta", False)]))
    b.append(entity(310, "PARTIDA", [("id", True), ("tiempo_seg", False), ("gano_musica", False), ("fecha", False)]))
    b.append(entity(600, "NIVEL", [("id", True), ("nombre", False), ("orden", False)]))
    b.append(diamond(235, "juega"))
    b.append(diamond(525, "es de"))
    for x1, x2 in ((160, 185), (285, 310), (450, 475), (575, 600)):
        b.append(f"<line x1='{x1}' y1='62' x2='{x2}' y2='62' stroke='{TEAL}' stroke-width='1.3'/>")
    b.append(text(170, 54, "1", 11, "bold", TEAL, "middle"))
    b.append(text(298, 54, "N", 11, "bold", TEAL, "middle"))
    b.append(text(462, 54, "N", 11, "bold", TEAL, "middle"))
    b.append(text(588, 54, "1", 11, "bold", TEAL, "middle"))
    b.append(text(380, 200, "Un usuario juega muchas partidas; cada partida pertenece a un nivel. Atributo subrayado: clave primaria.", 10, fill=QUIET, anchor="middle"))
    return svg(760, 214, "".join(b), "Diagrama entidad-relación")


# ---------- Base de datos ----------

def table(x, y, w, name, rows):
    h = 24 + 18 * len(rows)
    o = [f"<rect x='{x}' y='{y}' width='{w}' height='{h}' rx='3' fill='white' stroke='{TEAL}' stroke-width='1.2'/>",
         f"<rect x='{x}' y='{y}' width='{w}' height='24' rx='3' fill='{TEAL}'/>",
         text(x + 10, y + 16, name, 11.5, "bold", "white")]
    for i, (col, typ, key) in enumerate(rows):
        ry = y + 24 + 18 * i
        if i:
            o.append(f"<line x1='{x}' y1='{ry}' x2='{x + w}' y2='{ry}' stroke='#d6dedd'/>")
        o.append(text(x + 10, ry + 13, col, 10, "bold" if key == "PK" else "normal", INK))
        o.append(text(x + w - 46, ry + 13, typ, 9.5, fill=QUIET, anchor="end"))
        if key:
            o.append(text(x + w - 8, ry + 13, key, 9, "bold", "#3d7a2a" if key == "PK" else "#a35a00", "end"))
    return "".join(o), dict(x=x, y=y, w=w, h=h)


def db():
    b = [f"<rect x='8' y='8' width='744' height='212' rx='6' fill='none' stroke='{LINE}' stroke-dasharray='5 4'/>",
         text(20, 26, "MySQL 8.4 · servidor", 11, "bold", TEAL),
         f"<rect x='8' y='236' width='744' height='150' rx='6' fill='none' stroke='{LINE}' stroke-dasharray='5 4'/>",
         text(20, 254, "SQLite · dispositivo del jugador", 11, "bold", TEAL)]
    s, u = table(20, 36, 230, "usuarios", [("id", "INT", "PK"), ("nombre", "VARCHAR(30)", "UQ"), ("email", "VARCHAR(100)", "UQ"), ("password_hash", "VARCHAR(255)", ""), ("fecha_alta", "DATETIME(3)", "")]); b.append(s)
    s, p = table(275, 36, 230, "partidas", [("id", "INT", "PK"), ("usuario_id", "INT", "FK"), ("nivel_id", "INT", "FK"), ("tiempo_seg", "DECIMAL(6,2)", ""), ("gano_musica", "TINYINT(1)", ""), ("fecha", "DATETIME(3)", "")]); b.append(s)
    s, n = table(530, 36, 210, "niveles", [("id", "INT", "PK"), ("nombre", "VARCHAR(30)", ""), ("orden", "INT", "UQ")]); b.append(s)
    b.append(text(390, 200, "UNIQUE (usuario_id, nivel_id, fecha)", 9.5, fill=QUIET, anchor="middle", italic=True))
    b.append(line(250, 69, 275, 87, "none"))
    b.append(text(257, 64, "1", 10, "bold", TEAL)); b.append(text(266, 94, "N", 10, "bold", TEAL))
    b.append(line(530, 69, 505, 105, "none"))
    b.append(text(518, 64, "1", 10, "bold", TEAL)); b.append(text(510, 118, "N", 10, "bold", TEAL))
    s, _ = table(20, 266, 230, "progreso", [("nivel_id", "INTEGER", "PK"), ("desbloqueado", "INTEGER", ""), ("mejor_tiempo", "REAL", "")]); b.append(s)
    s, _ = table(275, 266, 230, "pendientes", [("id", "INTEGER", "PK"), ("nivel_id", "INTEGER", ""), ("tiempo_seg", "REAL", ""), ("gano_musica", "INTEGER", ""), ("fecha", "TEXT (ISO 8601)", "")]); b.append(s)
    s, _ = table(530, 266, 210, "ajustes", [("clave", "TEXT", "PK"), ("valor", "TEXT", "")]); b.append(s)
    return svg(760, 394, "".join(b), "Diagrama de la base de datos")


# ---------- Arquitectura ----------

def box(x, y, w, h, title, lines=(), accent=False):
    fill = "#f1f8e4" if accent else "white"
    stroke = "#6d9b1f" if accent else TEAL
    o = f"<rect x='{x}' y='{y}' width='{w}' height='{h}' rx='8' fill='{fill}' stroke='{stroke}' stroke-width='{1.8 if accent else 1.2}'/>"
    o += text(x + 14, y + 22, title, 12.5, "bold", TEAL)
    for i, l in enumerate(lines):
        o += text(x + 14, y + 39 + 15 * i, l, 10.5, fill=QUIET)
    return o


def arq():
    b = [box(24, 64, 220, 72, "Juego en Unity", ["Android y Windows", "partidas, cuenta, ranking"], True),
         box(24, 200, 220, 72, "SQLite local", ["progreso y ajustes", "partidas pendientes"]),
         f"<rect x='344' y='44' width='396' height='200' rx='8' fill='#f6f8f8' stroke='{LINE}' stroke-dasharray='5 4'/>",
         text(360, 66, "Servidor en Docker", 12, "bold", TEAL),
         box(360, 90, 165, 72, "API REST", ["ASP.NET Core 10", "JWT, progreso, ranking"]),
         box(575, 90, 150, 72, "MySQL 8.4", ["usuarios, niveles", "y partidas"]),
         text(360, 226, "docker compose · puerto 5080", 10.5, fill=QUIET),
         path("M244 114H360", both=True), text(302, 106, "HTTPS · JSON", 10, fill=QUIET, anchor="middle"),
         path("M525 126H575"), text(550, 118, "EF Core", 10, fill=QUIET, anchor="middle"),
         path("M134 136V200", both=True), text(142, 172, "SQLite-net", 10, fill=QUIET)]
    return svg(760, 290, "".join(b), "Diagrama de arquitectura de la solución")


# ---------- Navegación ----------

def nav():
    W, H = 140, 48
    rowA, rowB = 110, 240
    c1, c2, c3, c4 = 94, 274, 454, 634
    def nbox(cx, cy, label, accent=False):
        return (f"<rect x='{cx - W / 2}' y='{cy - H / 2}' width='{W}' height='{H}' rx='8' fill='{'#f1f8e4' if accent else 'white'}' stroke='{'#6d9b1f' if accent else TEAL}' stroke-width='{1.8 if accent else 1.2}'/>"
                + text(cx, cy + 4, label, 12, "bold" if accent else "normal", TEAL if accent else INK, "middle"))
    b = [nbox(c1, rowA, "Cuenta", True), nbox(c2, rowA, "Portada"), nbox(c3, rowA, "Menú de niveles"), nbox(c4, rowA, "Ranking"),
         nbox(c3, rowB, "Nivel (Run)"), nbox(c2, rowB, "Pantalla final"),
         path(f"M{c1 + W / 2} {rowA}H{c2 - W / 2}"), path(f"M{c2 + W / 2} {rowA}H{c3 - W / 2}"),
         path(f"M{c3 + W / 2} {rowA - 6}H{c4 - W / 2}", both=True),
         path(f"M{c3} {rowA + H / 2}V{rowB - H / 2}"), path(f"M{c3 - W / 2} {rowB}H{c2 + W / 2}"),
         path(f"M{c2} {rowB + H / 2}V300H544V{rowA + 16}H{c3 + W / 2}"),
         path(f"M{c3} {rowA - H / 2}V62H{c1}V{rowA - H / 2}", "dash"),
         text((c1 + c2) / 2, rowA - 8, "entrar", 10, fill=QUIET, anchor="middle"),
         text((c2 + c3) / 2, rowA - 8, "tocar", 10, fill=QUIET, anchor="middle"),
         text(c3 + 8, (rowA + rowB) / 2 + 4, "jugar", 10, fill=QUIET),
         text((c2 + c3) / 2, rowB - 8, "meta", 10, fill=QUIET, anchor="middle"),
         text(409, 318, "toca y sigue", 10, fill=QUIET, anchor="middle"),
         text(c2, 56, "cerrar sesión", 10, fill=QUIET, anchor="middle")]
    return svg(760, 330, "".join(b), "Diagrama de flujo de navegación")


# ---------- Red ----------

def net():
    b = [box(20, 46, 150, 58, "Móvil Android", ["Late.apk"]),
         box(20, 178, 150, 58, "PC con Windows", ["Late.exe"]),
         f"<ellipse cx='255' cy='75' rx='52' ry='28' fill='{SOFT}' stroke='{TEAL}' stroke-width='1.2'/>",
         text(255, 79, "Internet", 11.5, "bold", TEAL, "middle"),
         box(330, 46, 140, 58, "Cloudflare", ["túnel HTTPS"]),
         f"<rect x='500' y='14' width='250' height='276' rx='8' fill='#f6f8f8' stroke='{LINE}' stroke-dasharray='5 4'/>",
         text(515, 34, "PC de desarrollo · Docker", 11.5, "bold", TEAL),
         box(515, 48, 220, 50, "cloudflared", ["conexión saliente"]),
         box(515, 124, 220, 58, "API · contenedor :8080", ["publicada en el puerto 5080"], True),
         box(515, 210, 220, 58, "MySQL 8.4 · :3306", ["solo red interna de Docker"]),
         path("M170 75H203", both=True), path("M307 75H330", both=True), path("M470 73H515", both=True),
         path("M625 98V124", both=True), path("M625 182V210", both=True),
         path("M170 207H480V153H515", both=True), text(325, 200, "localhost:5080 o red local", 10, fill=QUIET, anchor="middle"),
         f"<rect x='20' y='262' width='460' height='42' rx='8' fill='none' stroke='{LINE}' stroke-dasharray='5 4'/>",
         text(36, 288, "Producción prevista: VPS con dominio y HTTPS (mismo docker-compose)", 10.5, fill=QUIET)]
    return svg(760, 318, "".join(b), "Diagrama de red")


# ---------- Gantt contra Git ----------

def gantt():
    d0, d1 = date(2026, 9, 22), date(2026, 11, 20)
    x0, x1 = 290, 730
    X = lambda d: x0 + (d - d0).days / (d1 - d0).days * (x1 - x0)
    phases = [
        ("1. Análisis y planificación", date(2026, 9, 25), date(2026, 10, 8)),
        ("2. Generador procedural", date(2026, 10, 8), date(2026, 10, 9)),
        ("3. Jugabilidad y niveles", date(2026, 10, 9), date(2026, 10, 9)),
        ("4. Rendimiento en móvil", date(2026, 10, 9), date(2026, 10, 9)),
        ("5. Refactorización y Git", date(2026, 10, 8), date(2026, 10, 9)),
        ("6. Optimización y pruebas", date(2026, 11, 16), date(2026, 11, 16)),
        ("7. Datos locales (SQLite)", date(2026, 11, 16), date(2026, 11, 16)),
        ("8. Servidor, MySQL y Docker", date(2026, 11, 16), date(2026, 11, 16)),
        ("9. Login, ranking e idiomas", date(2026, 11, 16), date(2026, 11, 16)),
        ("10. Pruebas, CI, builds, memoria", date(2026, 11, 16), date(2026, 11, 16)),
    ]
    top, rh = 58, 27
    bottom = top + rh * len(phases)
    b = []
    t = d0
    from datetime import timedelta
    months = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"]
    while t <= d1:
        x = X(t)
        b.append(f"<line x1='{x:.1f}' y1='{top - 8}' x2='{x:.1f}' y2='{bottom}' stroke='#e2e8e7'/>")
        b.append(text(round(x, 1), top - 14, f"{t.day} {months[t.month - 1]}", 11, fill=QUIET, anchor="middle"))
        t += timedelta(days=7)
    for i, (name, a, z) in enumerate(phases):
        y = top + rh * i + rh / 2
        b.append(text(20, y + 4, name, 12, fill=INK))
        if a == z:
            x = X(a)
            b.append(f"<path d='M{x:.1f} {y - 7}l7 7l-7 7l-7 -7z' fill='{TEAL}'/>")
        else:
            b.append(f"<rect x='{X(a):.1f}' y='{y - 6}' width='{max(X(z) - X(a), 6):.1f}' height='12' rx='3' fill='{TEAL}'/>")
    for d, label in ((date(2026, 10, 9), "Entrega parcial · 9 oct"), (date(2026, 11, 17), "Esta entrega · 17 nov")):
        x = X(d)
        b.append(f"<line x1='{x:.1f}' y1='{top - 6}' x2='{x:.1f}' y2='{bottom + 8}' stroke='#6d9b1f' stroke-width='1.5' stroke-dasharray='4 3'/>")
        b.append(text(round(x, 1), bottom + 22, label, 11.5, "bold", "#4d7a12", "middle"))
    b.append(text(20, top - 14, "Fase (fechas de sus commits)", 10, "bold", TEAL))
    return svg(760, bottom + 34, "".join(b), "Diagrama de Gantt contrastado con Git")


# ---------- Logo de portada ----------

def logo():
    return (f"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 560 150' width='100%' {FONT}>"
            f"<path d='M10 18H92V78A41 41 0 0 1 10 78Z' fill='{TEAL}'/>"
            f"<path d='M48 26H118V52H104V40H62V96A30 30 0 0 0 92 126H100V140H92A44 44 0 0 1 48 96Z' fill='{LIME}'/>"
            f"<text x='138' y='96' font-size='86' font-weight='bold' fill='{TEAL}' letter-spacing='-3'>thePower</text>"
            f"<text x='142' y='136' font-size='34' fill='{TEAL}'>FP Oficial</text></svg>")


ALL = {"arq": arq, "usecases": usecases, "class_gen": class_gen, "class_game": class_game, "class_online": class_online,
       "er": er, "db": db, "nav": nav, "net": net, "gantt": gantt, "logo": logo}
