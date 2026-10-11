/**
 * Qué queda en castellano cuando la interfaz está en inglés.
 *
 *     npm run idioma
 *
 * El script `scripts/textos-sin-traducir.py` mira el código y decide con una
 * heurística si un literal es un texto de interfaz. Es útil pero es ciega a lo
 * que no parece castellano: «Estado operativo», «Fallos seguidos» y treinta y
 * ocho más pasaron de largo durante meses porque no tienen acento, ni eñe, ni
 * un artículo. El recuento decía cero y faltaban cuarenta.
 *
 * Esto lo mide en vez de deducirlo: abre el sistema en castellano, lo abre en
 * inglés, y compara el texto de cada pantalla. Lo que sale idéntico en los dos
 * idiomas o es un dato —el código de un equipo, el nombre de una persona, un
 * estado del catálogo— o es un texto que no pasa por el diccionario.
 *
 * Para separar una cosa de la otra no hay lista a mano: se escuchan las
 * respuestas de la API y se guardan todas las cadenas que trae. Un texto que
 * la API mandó es un dato; uno que no aparece en ninguna respuesta y está
 * igual en los dos idiomas es un literal sin traducir.
 *
 * Requiere el sistema levantado. Sale != 0 si encuentra alguno.
 */
import { existsSync } from 'node:fs';
import puppeteer from 'puppeteer';

const BASE = process.env.PREDICTIT_URL ?? 'http://localhost:5173';

const RUTAS = ['/dashboard', '/activos', '/incidencias', '/mantenimientos',
  '/mantenimientos/agenda', '/analisis', '/historial', '/reportes', '/usuarios',
  '/configuracion', '/configuracion/reglas', '/configuracion/planes',
  '/configuracion/facturacion',
  '/configuracion/ia', '/configuracion/bitacora', '/configuracion/errores',
  '/configuracion/apariencia'];

// Lo que aparece igual en los dos idiomas y esta bien que asi sea.
const IGUAL_EN_LOS_DOS = new Set([
  'PredictIT', 'Dashboard', 'CUIT', 'IA', 'OK', 'PP', 'S/N', 'admin',
  'Error', 'No', 'Total', 'Router', 'Monitor', 'Notebook', 'UPS', 'Switch',
]);

const ejecutable = () => [
  process.env.PUPPETEER_EXECUTABLE_PATH,
  'C:\Program Files\Google\Chrome\Application\chrome.exe',
  'C:\Program Files (x86)\Google\Chrome\Application\chrome.exe',
].filter(Boolean).find((c) => existsSync(c));

const esperar = (ms) => new Promise((r) => setTimeout(r, ms));

/** Todas las cadenas de un JSON, a cualquier profundidad. */
function cadenas(valor, salida) {
  if (typeof valor === 'string') salida.add(valor.trim());
  else if (Array.isArray(valor)) valor.forEach((v) => cadenas(v, salida));
  else if (valor && typeof valor === 'object') Object.values(valor).forEach((v) => cadenas(v, salida));
  return salida;
}

const navegador = await puppeteer.launch({
  executablePath: ejecutable(),
  headless: 'shell',
  defaultViewport: { width: 1600, height: 1000 },
  args: ['--hide-scrollbars'],
});

const pagina = await navegador.newPage();
await pagina.emulateTimezone('America/Argentina/Buenos_Aires');

// Todo lo que la API devolvio es un dato, no un texto de la interfaz.
//
// Se guarda de dos formas. El conjunto de cadenas sirve para la comparacion
// exacta, y el cuerpo crudo para lo que viene partido o escapado adentro de
// otro campo -el nombre de una regla dentro del motivo de una alerta, por
// ejemplo-: sin eso quedaban afuera cuatro nombres de catalogo.
const deLaApi = new Set();
const cuerpos = [];
pagina.on('response', async (r) => {
  if (!r.url().includes('/api/')) return;
  try {
    const crudo = await r.text();
    cuerpos.push(crudo);
    cadenas(JSON.parse(crudo), deLaApi);
  } catch {
    // Una respuesta que no es JSON no aporta datos que comparar.
  }
});

await pagina.goto(BASE + '/login', { waitUntil: 'networkidle2' });
await pagina.waitForSelector('input');
const campos = await pagina.$$('input');
await campos[0].type('admin');
await campos[1].type('Admin.2026');
await pagina.click('button[type=submit]');
await pagina.waitForFunction(() => !location.pathname.includes('login'), { timeout: 20000 });

async function textos(idioma) {
  await pagina.evaluate((l) => localStorage.setItem('predictit.idioma', l), idioma);
  const porRuta = {};
  for (const ruta of RUTAS) {
    await pagina.goto(BASE + ruta, { waitUntil: 'networkidle2' });
    await esperar(1000);
    porRuta[ruta] = await pagina.evaluate(() => {
      const vistos = [];
      const paseo = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
      let nodo;
      while ((nodo = paseo.nextNode())) {
        const texto = nodo.textContent.trim();
        if (texto) vistos.push(texto);
      }
      return vistos;
    });
  }
  return porRuta;
}

const es = await textos('es-AR');
const en = await textos('en-US');
await navegador.close();

// Un texto de una sola palabra sin letras, o que empieza con un numero, es un
// dato aunque la API no lo haya mandado tal cual: una fecha, un porcentaje.
const NO_ES_TEXTO = /^[\s\d\W]*$|^\d/;

const sospechosos = new Map();
for (const ruta of RUTAS) {
  const enIngles = new Set(en[ruta] ?? []);
  for (const texto of new Set(es[ruta] ?? [])) {
    if (!enIngles.has(texto)) continue;
    if (NO_ES_TEXTO.test(texto) || IGUAL_EN_LOS_DOS.has(texto)) continue;
    // Lo que la API mando es un dato: el codigo de un equipo, el nombre de una
    // persona, un estado del catalogo.
    if (deLaApi.has(texto)) continue;
    if (cuerpos.some((c) => c.includes(texto))) continue;
    if (!sospechosos.has(texto)) sospechosos.set(texto, new Set());
    sospechosos.get(texto).add(ruta);
  }
}

if (sospechosos.size === 0) {
  console.log('Ningun texto de interfaz quedo en castellano con el idioma en ingles.');
  process.exit(0);
}

console.log('Textos iguales en los dos idiomas que la API no mando (%d):', sospechosos.size);
for (const [texto, rutas] of [...sospechosos].sort()) {
  console.log('  %s   ·   %s', JSON.stringify(texto), [...rutas].join(' '));
}
process.exit(1);
