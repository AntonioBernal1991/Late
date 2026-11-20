"""Monta la memoria de Late: HTML con diagramas, capturas, numeración e índices paginados, y la imprime a PDF.
Uso: python build.py <salida.pdf>   (horas reales opcionales en hours.json: {"6": 14, "7": 18, ...})
"""
import base64, html, json, os, re, subprocess, sys
import diagrams

HERE = os.path.dirname(os.path.abspath(__file__))

SHOTS = os.path.join(HERE, "capturas")
REPO = r"C:\Users\Usuario\Desktop\GitHub\Unity\Late"
OUT_PDF = sys.argv[1]

CSS = """
@page { size: A4; margin: 24mm 18mm 18mm 18mm; }
* { box-sizing: border-box; }
html { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
body { margin: 0; font-family: Arial, Helvetica, sans-serif; font-size: 10.3pt; line-height: 1.45; color: #1d2b2c; }
p { margin: 0 0 7pt; text-align: justify; }
.page { break-before: page; }
h1 { font-size: 19pt; font-weight: 400; text-transform: uppercase; letter-spacing: .4px; color: #1f3a3d;
     margin: 0 0 12pt; padding-bottom: 5pt; border-bottom: 3px solid #b9e35a; }
h2 { font-size: 13pt; color: #1f3a3d; margin: 14pt 0 6pt; break-after: avoid; }
h3 { font-size: 11.3pt; color: #1f3a3d; margin: 12pt 0 5pt; break-after: avoid; }
h3.idx { font-size: 12pt; margin-top: 4pt; }
h4 { margin: 0 0 6pt; font-size: 11pt; color: #1f3a3d; text-transform: uppercase; letter-spacing: .4px; }
ul { margin: 0 0 8pt; padding-left: 16pt; } li { margin-bottom: 4pt; text-align: justify; }
code { font-family: Consolas, monospace; font-size: 9.2pt; background: #eef3f2; padding: 0 3px; border-radius: 2px; }
table { width: 100%; border-collapse: collapse; margin: 6pt 0 2pt; font-size: 9pt; break-inside: auto; }
tr { break-inside: avoid; }
th { background: #eef3f2; color: #1f3a3d; text-align: left; font-weight: bold; }
th, td { border: 1px solid #c9d3d1; padding: 4pt 6pt; vertical-align: top; }
td.ok { color: #3d7a12; font-weight: bold; white-space: nowrap; }
td.wip { color: #a35a00; font-weight: bold; white-space: nowrap; }
table.num td:not(:first-child), table.num th:not(:first-child) { text-align: right; white-space: nowrap; }
table.num2 td:nth-child(2), table.num2 th:nth-child(2) { text-align: left; white-space: normal; }
table.dec { font-size: 8.8pt; } table.dec td:first-child { width: 27%; font-weight: bold; color: #1f3a3d; } table.dec td:nth-child(2) { width: 38%; }
table.req2 td:nth-child(odd) { font-weight: bold; color: #1f3a3d; width: 40px; background: #f6f9f4; }
tr.total td { font-weight: bold; background: #f6f9f4; }
td.pend { background: #fff3c4; color: #7a5a00; font-style: italic; }
.cap { text-align: center; font-size: 8.6pt; font-style: italic; color: #5b6668; margin: 2pt 0 12pt; }
figure { margin: 8pt 0 10pt; break-inside: avoid; }
figure > svg { display: block; }
figcaption { text-align: center; font-size: 8.6pt; font-style: italic; color: #5b6668; margin-top: 4pt; }
table.rftp td:first-child { font-family: Consolas, monospace; font-size: 8.5pt; white-space: nowrap; }
table.rftp tr.r td { background: #eef3f2; font-weight: bold; color: #1f3a3d; }
table.rftp tr.f td:first-child { padding-left: 14pt; }
table.rftp tr.t td:first-child { padding-left: 24pt; }
table.rftp tr.p td:first-child { padding-left: 34pt; color: #5b6668; }
table.rftp tr.p td:nth-child(2) { color: #3f4b4c; font-style: italic; }
table.cu { font-size: 9.2pt; margin-top: 10pt; }
table.cu th { background: #1f3a3d; color: white; font-size: 9.6pt; }
table.cu td { width: 50%; }
.abstract { display: grid; grid-template-columns: 1fr 1fr; gap: 16pt; }
.abstract > div { border: 1px solid #c9d3d1; background: #f8faf9; padding: 10pt 12pt; }
.abstract p { font-size: 9.6pt; }
.phones { display: grid; grid-template-columns: repeat(4, 1fr); gap: 10pt; margin: 6pt 6pt 0; }
.phones.two { grid-template-columns: repeat(2, 150px); justify-content: center; gap: 30pt; }
figure.phone { margin: 0; }
figure.phone .screen { border: 5px solid #1b1d1e; border-radius: 14px; overflow: hidden; aspect-ratio: 1080 / 2100; background: #000; }
figure.phone img { display: block; width: 100%; }
figure.phone figcaption { font-style: normal; color: #1d2b2c; font-size: 8.4pt; }
p.cap.fig { margin-top: 6pt; }
.wide { display: grid; grid-template-columns: 1fr 1fr; gap: 10pt; margin: 8pt 0 0; }
.wide figure { margin: 0; } .wide img { display: block; width: 100%; border: 1px solid #c9d3d1; }
.wide figcaption { font-style: normal; color: #1d2b2c; font-size: 8.4pt; }
figure.phone.photo .screen { aspect-ratio: auto; border-width: 4px; }
.tech { display: grid; grid-template-columns: 46px 1fr; gap: 4pt 12pt; margin-bottom: 9pt; break-inside: avoid; }
.badge { width: 44px; height: 44px; border-radius: 10px; color: white; font-weight: bold; font-size: 12pt; display: flex; align-items: center; justify-content: center; }
.tech b { color: #1f3a3d; }
.tech p { margin: 0; font-size: 9.6pt; }
.refs p { margin: 0 0 2pt; text-align: left; font-size: 9.6pt; }
.refs p.use { margin-top: 9pt; font-style: italic; color: #5b6668; font-size: 9pt; }
.small { font-size: 9pt; color: #5b6668; }
.mk { font-size: 1px; color: #ffffff; }
.toc-row { display: flex; align-items: baseline; font-size: 10pt; margin: 2.5pt 0; }
.toc-row .t { white-space: nowrap; }
.toc-row .dots { flex: 1; border-bottom: 1px dotted #9aa7a6; margin: 0 4pt; transform: translateY(-3pt); }
.toc-row.l1 { font-weight: bold; margin-top: 6pt; }
.toc-row.l2 { padding-left: 14pt; } .toc-row.l3 { padding-left: 28pt; font-size: 9.5pt; }
.toc-row.fig, .toc-row.tab { font-size: 9.3pt; }
.cover { height: 252mm; display: flex; flex-direction: column; }
.cover-logo { width: 74%; margin: 22mm auto 0; }
.cover-title { text-align: center; margin-top: 26mm; }
.cover-kicker { text-align: center; text-transform: uppercase; letter-spacing: 1.5px; font-size: 9.5pt; color: #5b6668; }
.cover-name { border: 0; font-size: 46pt; font-weight: bold; letter-spacing: 10px; margin: 4pt 0; padding: 0; text-align: center; }
.cover-sub { text-align: center; font-size: 13pt; color: #1f3a3d; }
.cover-course { text-align: center; font-size: 11pt; margin-top: 10pt; }
.cover-people { margin-top: auto; margin-left: auto; width: 62%; text-align: right; }
.cover-people p { text-align: right; margin: 0 0 6pt; font-size: 11pt; }
.cover-people span { display: block; font-size: 8pt; text-transform: uppercase; letter-spacing: 1px; color: #5b6668; }
"""

TECH = [
    ("U", "#222c2e", "Unity 2022.3 LTS", "Motor de videojuegos multiplataforma con editor visual, versión de soporte extendido.", "Juego, escenas, generador procedural, herramientas del editor (Hornear, Optimizar, Idiomas, Builds) y builds para Android y Windows."),
    ("C#", "#5c2d91", "C#", "Lenguaje orientado a objetos de la plataforma .NET.", "Todo el código: juego, editor, API y pruebas. Los contratos de shared/ se compilan igual en Unity y en el servidor."),
    ("A", "#3d8a3a", "Android SDK e IL2CPP", "Herramientas de compilación de Android y compilador de C# a código nativo de Unity.", "Build del APK. IL2CPP mejora el rendimiento en el móvil; se fuerza el permiso de Internet porque Unity no detecta HttpClient."),
    ("SQ", "#0f6fa8", "SQLite y SQLite-net", "Base de datos embebida en un único archivo y su ORM para Unity (licencia MIT).", "Progreso, partidas pendientes y ajustes en el dispositivo, sin servidor y sin conexión."),
    ("{}", "#444444", "Newtonsoft.Json", "Serializador JSON; paquete oficial de Unity.", "Convierte a JSON los mensajes que el juego intercambia con la API."),
    ("NET", "#512bd4", "ASP.NET Core 10", "Framework web de Microsoft, versión LTS.", "API REST con controladores, autenticación JWT, límite de intentos y documentación Swagger. Se eligió .NET 10 porque .NET 8 dejó de tener soporte el 10 de noviembre de 2026."),
    ("EF", "#6b3fa0", "Entity Framework Core 10", "ORM de .NET con migraciones; proveedor oficial MySql.EntityFrameworkCore.", "Modelo de datos, consultas del ranking y del progreso, y migraciones de la base de datos. Pomelo aún no es compatible con EF Core 10."),
    ("My", "#00618a", "MySQL 8.4", "Sistema gestor de bases de datos relacional.", "Usuarios, niveles y partidas en el servidor."),
    ("D", "#1d63ed", "Docker y Docker Compose", "Contenedores y su orquestación local.", "Levanta la API y MySQL con un solo comando; las migraciones se aplican al arrancar y MySQL solo es accesible desde la red interna."),
    ("JWT", "#d63aff", "JWT y PBKDF2", "Token de sesión firmado (HMAC-SHA256) y algoritmo de hash de contraseñas con sal.", "Sesión de 30 días sin guardar la contraseña; en la base de datos solo está su hash."),
    ("UT", "#222c2e", "Unity Test Framework", "NUnit integrado en el editor de Unity.", "62 pruebas del juego en modo editor, dos de ellas contra la API real."),
    ("xU", "#5e2750", "xUnit y Testcontainers", "Framework de pruebas de .NET y contenedores desechables para pruebas.", "34 pruebas de integración de la API contra un MySQL real que se crea y se destruye en cada ejecución."),
    ("Git", "#f05133", "Git, GitHub y Git LFS", "Control de versiones, repositorio remoto y almacenamiento de archivos grandes.", "Historial con un commit por fase; música, texturas, modelos y niveles en LFS."),
    ("CI", "#2088ff", "GitHub Actions y GameCI", "Integración continua en la nube y acciones específicas para Unity.", "Prevista para la entrega final: compilar y probar el servidor y el juego en cada cambio."),
    ("CF", "#f38020", "Cloudflare Tunnel", "Túnel HTTPS gratuito hacia un servidor local.", "Probar el juego en un Android real con datos móviles contra el servidor del PC."),
    ("TMP", "#3a3a3a", "TextMesh Pro y JetBrains Mono", "Sistema de texto de Unity y fuente monoespaciada (SIL Open Font License).", "Textos del menú y de la interfaz online con estética de terminal."),
]


def tech_html():
    out = []
    for badge, color, name, desc, use in TECH:
        out.append(f"<div class='tech'><div class='badge' style='background:{color}'>{html.escape(badge)}</div>"
                   f"<div><p><b>{html.escape(name)}</b></p><p>{html.escape(desc)}</p><p><i>Uso en el proyecto:</i> {html.escape(use)}</p></div></div>")
    return "".join(out)


def gitlog_html():
    raw = subprocess.run(["git", "-C", REPO, "log", "--reverse", "--format=%ad|%h|%s", "--date=format:%d/%m/%Y"],
                         capture_output=True, text=True, encoding="utf-8").stdout.strip().splitlines()
    return "".join(f"<tr><td>{d}</td><td><code>{h}</code></td><td>{html.escape(s)}</td></tr>" for d, h, s in (l.split("|", 2) for l in raw))


def signed(n):
    return f"{n:+g}".replace("-", "−")


def hours_fill(doc):
    """Horas reales de las fases 6 a 10 desde hours.json:
    {"horas": {"6": 14, ...}, "en_curso": ["10"], "desviaciones": "texto del análisis"}."""
    path = os.path.join(HERE, "hours.json")
    data = json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {}
    hours, ongoing = data.get("horas", {}), set(data.get("en_curso", []))
    est = {"6": 15, "7": 20, "8": 40, "9": 30, "10": 30}
    total, complete = 75, True
    for k, e in est.items():
        if k in hours:
            h = hours[k]; total += h
            if k in ongoing:
                doc = doc.replace("{{H" + k + "}}", f"{h:g} (en curso)").replace("{{D" + k + "}}", f"quedan ~{e - h:g}")
            else:
                doc = doc.replace("{{H" + k + "}}", f"{h:g}").replace("{{D" + k + "}}", signed(h - e))
        else:
            complete = False
            doc = doc.replace("{{H" + k + "}}", "pendiente").replace("{{D" + k + "}}", "—")
    if complete:
        doc = doc.replace("{{HT}}", f"{total:g}" + (" (en curso)" if ongoing else ""))
        doc = doc.replace("{{DT}}", "—" if ongoing else signed(total - 213))
        doc = doc.replace(" class=\"pend\"", "")
        doc = doc.replace("{{DEVIATION}}", data.get("desviaciones", f"En total el proyecto ha requerido {total:g} horas frente a las 213 estimadas ({signed(total - 213)} h)."))
    else:
        doc = doc.replace("{{HT}}", "pendiente").replace("{{DT}}", "—")
        doc = doc.replace("{{DEVIATION}}", "<span style='background:#fff3c4'>Pendiente: completar las horas reales de las fases 6 a 10 para cerrar la totalización y el análisis de desviaciones.</span>")
    return doc


def build(pages=None):
    doc = open(os.path.join(HERE, "content.html"), encoding="utf-8").read()
    for name, fn in diagrams.ALL.items():
        doc = doc.replace("{{SVG:" + name + "}}", fn())
    def img(m):
        return f"<div class='screen'>{embed(m.group(1))}</div>"
    def embed(name):
        for ext, mime in ((".png", "image/png"), (".jpg", "image/jpeg")):
            path = os.path.join(SHOTS, name + ext)
            if os.path.exists(path):
                data = base64.b64encode(open(path, "rb").read()).decode()
                return f"<img src='data:{mime};base64,{data}'/>"
        raise FileNotFoundError(name)
    doc = re.sub(r"\{\{IMG:([\w]+)\}\}", img, doc)
    doc = re.sub(r"\{\{PIC:([\w]+)\}\}", lambda m: embed(m.group(1)), doc)
    doc = doc.replace("{{TECH}}", tech_html()).replace("{{GITLOG}}", gitlog_html())
    doc = hours_fill(doc)

    entries = []  # (kind, level, text, key)
    counters = {"FIG": 0, "TAB": 0}
    def caption(m):
        kind, cap = m.group(1), m.group(2)
        counters[kind] += 1
        n = counters[kind]
        key = f"{kind.lower()}{n}"
        label = ("Ilustración" if kind == "FIG" else "Tabla") + f" {n}: {cap}"
        entries.append((kind.lower(), 0, label, key))
        return f"<span class='mk'>QQ{key}QQ</span>{html.escape(label)}"
    # Títulos e ilustraciones/tablas en orden de aparición
    hcount = [0]
    def heading(m):
        hcount[0] += 1
        key = f"h{hcount[0]}"
        level = int(m.group(1))
        entries.append(("h", level, re.sub("<[^>]+>", "", m.group(2)), key))
        return f"<h{level} class=\"toc\"><span class='mk'>QQ{key}QQ</span>{m.group(2)}</h{level}>"
    token = re.compile(r"<h([123]) class=\"toc\">(.*?)</h\1>|\{\{(FIG|TAB):(.*?)\}\}")
    def any_token(m):
        if m.group(1):
            return heading(m)
        class M: pass
        mm = M(); mm.group = lambda i: {1: m.group(3), 2: m.group(4)}[i]
        return caption(mm)
    doc = token.sub(any_token, doc)

    page = lambda k: str(pages.get(k, "?")) if pages else "00"
    def rows(kinds):
        out = []
        for kind, level, txt, key in entries:
            if kind not in kinds:
                continue
            cls = f"l{level}" if kind == "h" else kind
            out.append(f"<div class='toc-row {cls}'><span class='t'>{html.escape(txt)}</span><span class='dots'></span><span class='p'>{page(key)}</span></div>")
        return "".join(out)
    doc = doc.replace("{{TOC}}", rows({"h"})).replace("{{LOT}}", rows({"tab"})).replace("{{LOF}}", rows({"fig"}))
    page_html = f"<!doctype html><html lang='es'><head><meta charset='utf-8'><title>Late · Memoria de TFG</title><style>{CSS}</style></head><body>{doc}</body></html>"
    path = os.path.join(HERE, "memoria.html")
    open(path, "w", encoding="utf-8").write(page_html)
    return path, [e[3] for e in entries]


def print_pdf(html_path, pdf_path):
    try:
        subprocess.run(["node", os.path.join(HERE, "print.js"), html_path, pdf_path], check=True)
    finally:
        # Edge en modo headless deja procesos sueltos; se cierran solo los de nuestro perfil temporal (late-pdf-*).
        subprocess.run(["powershell", "-NoProfile", "-Command",
                        "Get-CimInstance Win32_Process -Filter \"Name='msedge.exe'\" | "
                        "Where-Object { $_.CommandLine -match 'late-pdf-' } | "
                        "ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"],
                       capture_output=True)


def find_pages(pdf_path, keys):
    from pypdf import PdfReader
    reader = PdfReader(pdf_path)
    texts = [p.extract_text() or "" for p in reader.pages]
    pages = {}
    for k in keys:
        mark = f"QQ{k}QQ".upper()
        for i, t in enumerate(texts):
            if mark in t.replace(" ", "").replace("\n", "").upper():
                pages[k] = i + 1
                break
    return pages, len(texts)


if __name__ == "__main__":
    tmp_pdf = os.path.join(HERE, "pass1.pdf")
    html_path, keys = build()
    print_pdf(html_path, tmp_pdf)
    pages, n = find_pages(tmp_pdf, keys)
    missing = [k for k in keys if k not in pages]
    print(f"páginas: {n}; marcas encontradas {len(pages)}/{len(keys)}; sin encontrar: {missing}")
    html_path, _ = build(pages)
    print_pdf(html_path, OUT_PDF)
    pages2, n2 = find_pages(OUT_PDF, keys)
    print("índice estable:", pages2 == pages, "páginas finales:", n2)
