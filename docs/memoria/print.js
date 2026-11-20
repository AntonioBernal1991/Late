// Imprime un HTML a PDF con Edge headless por el protocolo de DevTools (cabecera, pie y nº de página).
// Uso: node print.js <entrada.html> <salida.pdf>
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const os = require('os');

const [, , input, output] = process.argv;
const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
// Puerto distinto en cada ejecución: una instancia que se quedara colgada no interfiere con la siguiente.
const PORT = 9400 + Math.floor(Math.random() * 500);
const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'late-pdf-'));

const header = `<div style="width:100%;font-family:Arial,sans-serif;padding:0 18mm;display:flex;justify-content:space-between;align-items:flex-end;color:#1f3a3d;">
  <span style="font-size:8px;color:#5b6668;">Late · Memoria de Trabajo de Fin de Grado · Curso 2026-2027</span>
  <span style="font-size:13px;letter-spacing:5px;font-weight:300;">PROMETEO</span></div>`;
const footer = `<div style="width:100%;font-family:Arial,sans-serif;padding:0 18mm;display:flex;justify-content:space-between;font-size:8px;color:#5b6668;">
  <span>Antonio Bernal · CFGS DAM · thePower FP Oficial</span>
  <span>Página <span class="pageNumber"></span> de <span class="totalPages"></span></span></div>`;

const sleep = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  const edge = spawn(EDGE, ['--headless=new', '--disable-gpu', `--remote-debugging-port=${PORT}`, `--user-data-dir=${profile}`, 'about:blank'], { stdio: 'ignore' });
  try {
    let targets;
    for (let i = 0; i < 50; i++) {
      try { targets = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json(); if (targets.some(t => t.type === 'page')) break; } catch { }
      await sleep(200);
    }
    const page = targets.find(t => t.type === 'page');
    const ws = new WebSocket(page.webSocketDebuggerUrl);
    await new Promise(r => ws.addEventListener('open', r));
    let id = 0; const pending = new Map(); const events = [];
    ws.addEventListener('message', e => {
      const m = JSON.parse(e.data);
      if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); }
      else if (m.method) events.push(m.method);
    });
    const send = (method, params = {}) => new Promise(r => { const i = ++id; pending.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
    await send('Page.enable');
    await send('Page.navigate', { url: 'file:///' + path.resolve(input).replace(/\\/g, '/') });
    for (let i = 0; i < 300 && !events.includes('Page.loadEventFired'); i++) await sleep(100);
    await sleep(800);
    const res = await send('Page.printToPDF', {
      printBackground: true, displayHeaderFooter: true, headerTemplate: header, footerTemplate: footer,
      preferCSSPageSize: true, transferMode: 'ReturnAsBase64'
    });
    if (res.error) throw new Error(JSON.stringify(res.error));
    fs.writeFileSync(output, Buffer.from(res.result.data, 'base64'));
    ws.close();
    console.log('PDF:', output);
  } finally {
    // edge.kill() solo cierra el proceso principal; los procesos hijos de Edge se quedarían vivos.
    try { require('child_process').execSync(`taskkill /PID ${edge.pid} /T /F`, { stdio: 'ignore' }); } catch { }
  }
})().catch(e => { console.error(e); process.exit(1); });
