/**
 * Captura las pantallas del sistema para los manuales y el documento del TFI.
 *
 *     npm run capturas
 *
 * Se automatiza en lugar de recortar ventanas a mano por dos razones. La
 * primera es que las capturas quedan todas del mismo tamaño y con el mismo
 * recorte, que es lo que hace que un manual parezca un manual. La segunda es
 * que cuando una pantalla cambia se vuelven a sacar todas con un comando, y
 * una captura vieja en un manual es peor que no tener captura.
 *
 * Requiere el sistema levantado (docker compose up -d).
 */
import { mkdir, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';
import puppeteer from 'puppeteer';

const BASE = process.env.PREDICTIT_URL ?? 'http://localhost:8080';
const SALIDA = path.resolve(import.meta.dirname, '../../docs/capturas');

// 1440x900 es la resolución de trabajo más común en las notebooks del sector, y
// con deviceScaleFactor 2 la captura entra nítida en una página impresa.
const ANCHO = 1440;
const ALTO = 900;
const ESCALA = 2;

const USUARIOS = {
  admin: { usuario: 'admin', clave: 'Admin.2026' },
  tecnico: { usuario: 'tecnico1', clave: 'Tecnico.2026' },
  solicitante: { usuario: 'solicitante', clave: 'Usuario.2026' },
};

/**
 * Las pantallas a capturar, en el orden en que aparecen en los manuales.
 * `espera` es un texto que tiene que estar en la pantalla antes de disparar la
 * captura: sin eso se captura el esqueleto de carga en lugar del contenido.
 */
const PANTALLAS = [
  // El login es una tarjeta chica sobre un fondo vacio: a 1440x900 la
  // captura sale con mas fondo que pantalla. Se la saca en una ventana mas
  // angosta, que es ademas como se ve en una notebook real.
  { id: '01-login', ruta: '/login', perfil: null, espera: 'Iniciar sesi',
    titulo: 'Pantalla de inicio de sesión', ventana: { ancho: 900, alto: 620 } },
  { id: '02-dashboard', ruta: '/dashboard', perfil: 'admin', espera: 'Equipos',
    titulo: 'Tablero de control del administrador' },
  { id: '03-activos', ruta: '/activos', perfil: 'admin', espera: 'Activos',
    titulo: 'Listado del parque de equipos' },
  { id: '04-equipo-detalle', ruta: '/activos', perfil: 'admin', espera: 'Activos',
    titulo: 'Ficha de un equipo con su historial', buscarEquipoConHistorial: true,
    clic: 'tbody tr a' },
  { id: '05-incidencias', ruta: '/incidencias', perfil: 'admin', espera: 'Incidencias',
    titulo: 'Listado de incidencias' },
  { id: '06-incidencia-nueva', ruta: '/incidencias/nueva', perfil: 'tecnico',
    espera: 'incidencia', titulo: 'Formulario de reporte de una incidencia' },
  { id: '07-incidencia-detalle', ruta: '/incidencias', perfil: 'tecnico', espera: 'Incidencias',
    titulo: 'Detalle de una incidencia con la recomendación de asignación',
    clic: 'tbody tr a' },
  { id: '08-mantenimientos', ruta: '/mantenimientos', perfil: 'admin', espera: 'Mantenimiento',
    titulo: 'Registro de mantenimientos' },
  { id: '26-agenda', ruta: '/mantenimientos/agenda', perfil: 'admin', espera: 'Agenda',
    titulo: 'Agenda de mantenimiento: lo vencido y lo que viene' },
  { id: '09-predictivo', ruta: '/analisis', perfil: 'admin', espera: 'riesgo',
    titulo: 'Análisis predictivo: equipos por nivel de riesgo' },
  { id: '10-reglas', ruta: '/configuracion/reglas', perfil: 'admin', espera: 'Regla',
    titulo: 'Configuración de las reglas del motor predictivo' },
  { id: '27-planes', ruta: '/configuracion/planes', perfil: 'admin', espera: 'Plan',
    titulo: 'Planes de mantenimiento preventivo' },
  { id: '28-facturacion', ruta: '/configuracion/facturacion', perfil: 'admin',
    espera: 'Factura', titulo: 'Facturación del servicio: lo cobrado y lo pendiente' },
  { id: '11-reportes', ruta: '/reportes', perfil: 'admin', espera: 'Reporte',
    titulo: 'Generación de reportes en PDF' },
  { id: '12-usuarios', ruta: '/usuarios', perfil: 'admin', espera: 'Usuario',
    titulo: 'Administración de usuarios y permisos' },
  { id: '13-bitacora', ruta: '/configuracion/bitacora', perfil: 'admin', espera: 'Bit',
    titulo: 'Consulta de la bitácora de auditoría' },
  { id: '14-integracion-ia', ruta: '/configuracion/ia', perfil: 'admin', espera: 'proveedor',
    titulo: 'Configuración del proveedor de inteligencia artificial' },
  { id: '15-respaldos', ruta: '/configuracion/respaldos', perfil: 'admin', espera: 'Respaldo',
    titulo: 'Respaldos y restauración de las bases' },
  { id: '16-mis-equipos', ruta: '/mis-equipos', perfil: 'solicitante', espera: 'equipo',
    titulo: 'Vista del usuario solicitante sobre sus equipos' },
  { id: '17-historial', ruta: '/historial', perfil: 'tecnico', espera: 'Historial',
    titulo: 'Historial de intervenciones' },
  { id: '18-organizacion', ruta: '/configuracion', perfil: 'admin', espera: 'Organiza',
    titulo: 'Datos de la organización y plan contratado' },
  { id: '19-notificaciones', ruta: '/configuracion/notificaciones', perfil: 'admin',
    espera: 'Notificaci', titulo: 'Notificaciones: la sección declarada fuera de alcance' },
  { id: '20-integraciones', ruta: '/configuracion/integraciones', perfil: 'admin',
    espera: 'Integraci', titulo: 'Integraciones: la sección declarada fuera de alcance' },
  { id: '24-errores', ruta: '/configuracion/errores', perfil: 'admin',
    espera: 'rror', titulo: 'Los errores del sistema, con su traza' },
  { id: '25-apariencia', ruta: '/configuracion/apariencia', perfil: 'admin',
    espera: 'ema', titulo: 'Apariencia: los dos temas, la densidad y el idioma' },
  { id: '21-mis-pedidos', ruta: '/mis-pedidos', perfil: 'solicitante', espera: 'pedido',
    titulo: 'Los pedidos del usuario solicitante y en qué estado están' },
  { id: '22-reportar', ruta: '/reportar', perfil: 'solicitante', espera: 'equipo',
    titulo: 'Reportar un problema: elegir el equipo, el primero de los tres pasos' },
  { id: '23-pedido-detalle', ruta: '/mis-pedidos', perfil: 'solicitante', espera: 'pedido',
    titulo: 'El detalle de un pedido, contado sin el vocabulario del taller',
    clic: '.tarjeta.pedido' },
];

function ejecutable() {
  // Se reusa el Chrome del sistema antes que descargar uno: son 150 MB que ya
  // estan en la maquina.
  const candidatos = [
    process.env.PUPPETEER_EXECUTABLE_PATH,
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
  ].filter(Boolean);
  return candidatos.find((c) => existsSync(c));
}

async function entrar(pagina, perfil) {
  const { usuario, clave } = USUARIOS[perfil];
  await pagina.goto(BASE + '/login', { waitUntil: 'networkidle2' });
  await pagina.waitForSelector('input');
  const campos = await pagina.$$('input');
  await campos[0].type(usuario);
  await campos[1].type(clave);
  await pagina.click('button[type=submit]');
  // El front guarda el token y redirige por router, sin navegacion del browser:
  // se espera a que la ruta deje de ser /login.
  await pagina.waitForFunction(() => !location.pathname.includes('login'), { timeout: 20000 });

  // La preferencia guardada le gana a la cabecera, así que se fija también acá.
  await pagina.evaluate(() => localStorage.setItem('predictit.idioma', 'es-AR'));
}

async function salir(pagina) {
  // En about:blank el origen es opaco y leer localStorage tira SecurityError.
  // Hay que estar en el origen de la app para poder limpiarle la sesion.
  if (!pagina.url().startsWith(BASE)) {
    await pagina.goto(BASE + '/login', { waitUntil: 'domcontentloaded' });
  }
  await pagina.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
}

async function esperarTexto(pagina, texto) {
  try {
    await pagina.waitForFunction(
      (t) => document.body.innerText.toLowerCase().includes(t.toLowerCase()),
      { timeout: 12000 }, texto);
  } catch {
    console.warn('  · no aparecio "' + texto + '"; se captura lo que haya');
  }
}

const esperar = (ms) => new Promise((r) => setTimeout(r, ms));

/**
 * Las tarjetas que miden menos que lo que tienen adentro.
 *
 * Es el unico control de esta clase que se puede hacer: necesita un navegador
 * de verdad, porque depende del calculo de layout. Nace de un caso concreto.
 * `.cuerpo` es un flex en columna, sus hijos se encogian cuando el contenido
 * no entraba en la pantalla, y una tarjeta -que lleva `overflow: hidden` para
 * recortar sus esquinas- tiene minimo automatico cero. El desglose del riesgo
 * quedo con 2 px de alto y 400 px adentro: la captura mostraba una tarjeta
 * vacia con su titulo, y asi entro al documento sin que nada avisara.
 *
 * Se deja un margen de 4 px: un borde redondeado o un `line-height` pueden
 * dar diferencias de sub-pixel que no son un colapso.
 */
async function tarjetasAplastadas(pagina) {
  return pagina.evaluate(() => {
    const malas = [];
    for (const t of document.querySelectorAll('.tarjeta')) {
      const alto = t.getBoundingClientRect().height;
      const contenido = [...t.children]
        .reduce((suma, h) => suma + h.getBoundingClientRect().height, 0);
      if (contenido > alto + 4) {
        const titulo = t.querySelector('h2')?.textContent?.trim() || '(sin titulo)';
        malas.push(titulo + ' mide ' + Math.round(alto) + ' px y tiene '
                   + Math.round(contenido) + ' px adentro');
      }
    }
    return malas;
  });
}

const main = async () => {
  await mkdir(SALIDA, { recursive: true });

  const navegador = await puppeteer.launch({
    executablePath: ejecutable(),
    headless: 'shell',
    defaultViewport: { width: ANCHO, height: ALTO, deviceScaleFactor: ESCALA },
    args: ['--hide-scrollbars'],
  });

  const pagina = await navegador.newPage();
  // Sin esto la hora del sistema entra en la captura y dos capturas sacadas con
  // un minuto de diferencia se ven distintas sin haber cambiado nada.
  await pagina.emulateTimezone('America/Argentina/Buenos_Aires');

  // El documento está en castellano y las capturas también tienen que estarlo.
  // Sin esto, Chrome sin cabeza pide en-US, la API le manda el diccionario en
  // inglés y las veinticinco capturas salen en inglés. No se notaba mientras
  // faltaban las traducciones: la interfaz mostraba el castellano de reserva.
  await pagina.setExtraHTTPHeaders({ 'Accept-Language': 'es-AR,es;q=0.9' });

  let perfilActual = 'ninguno';
  const hechas = [];
  const colapsos = [];

  for (const p of PANTALLAS) {
    process.stdout.write(p.id + ' ... ');

    if (p.perfil !== perfilActual) {
      await salir(pagina);
      if (p.perfil) await entrar(pagina, p.perfil);
      perfilActual = p.perfil;
    }

    await pagina.setViewport({
      width: p.ventana?.ancho ?? ANCHO,
      height: p.ventana?.alto ?? ALTO,
      deviceScaleFactor: ESCALA,
    });

    await pagina.goto(BASE + p.ruta, { waitUntil: 'networkidle2' });
    await esperarTexto(pagina, p.espera);

    if (p.buscarEquipoConHistorial) {
      // La ficha del primer equipo del listado sale vacia: el orden es
      // alfabetico y el primero no tiene por que tener historial. Se le
      // pregunta a la API por un equipo que si lo tenga y se lo filtra, asi la
      // captura muestra la pantalla con datos sin fijar un id de la semilla.
      const codigo = await pagina.evaluate(async () => {
        const r = await fetch('/api/incidencias?porPagina=1', {
          headers: { Authorization: 'Bearer ' + sessionStorage.getItem('predictit.token') },
        });
        const j = await r.json();
        return j.items?.[0]?.codigoEquipo ?? null;
      });

      if (!codigo) throw new Error('no hay incidencias cargadas: no se puede mostrar un historial');

      const busqueda = await pagina.$('input[type=search], input');
      await busqueda.type(codigo);
      await pagina.waitForFunction(
        (c) => document.querySelectorAll('tbody tr').length === 1
               && document.body.innerText.includes(c),
        { timeout: 10000 }, codigo);
    }

    if (p.clic) {
      // La ficha de detalle no tiene URL fija: se llega desde el listado, y la
      // fila entera no navega. El enlace del final de la fila si.
      const destino = await pagina.$(p.clic);
      if (!destino) throw new Error('no se encontro ' + p.clic + ' en ' + p.ruta);
      await destino.click();
      await pagina.waitForFunction((r) => location.pathname !== r,
                                   { timeout: 15000 }, p.ruta);
      await esperar(1600);
    }

    // Se deja respirar a las animaciones de entrada; si no, los graficos
    // aparecen a media transicion y las barras salen cortadas.
    await esperar(900);

    await pagina.screenshot({ path: path.join(SALIDA, p.id + '.png'), fullPage: false });
    hechas.push({ id: p.id, titulo: p.titulo, archivo: p.id + '.png', perfil: p.perfil });

    const aplastadas = await tarjetasAplastadas(pagina);
    if (aplastadas.length) {
      colapsos.push(p.id + ': ' + aplastadas.join(' | '));
      console.log('ok (con tarjetas aplastadas)');
    } else {
      console.log('ok');
    }
  }

  if (colapsos.length) {
    console.error('\nTarjetas aplastadas:');
    for (const c of colapsos) console.error('  - ' + c);
    console.error('\nUna tarjeta mas baja que su contenido no se ve rota en la captura:');
    console.error('se ve como una tarjeta vacia, y la captura entra al documento igual.');
    process.exitCode = 1;
  }

  // El indice lo leen los generadores de Word para poner el epigrafe de cada
  // figura sin que ese texto se escriba dos veces.
  await writeFile(path.join(SALIDA, 'indice.json'),
    JSON.stringify({ generado: new Date().toISOString(), ancho: ANCHO, alto: ALTO,
                     escala: ESCALA, pantallas: hechas }, null, 2), 'utf8');

  await navegador.close();
  console.log('\n' + hechas.length + ' capturas en docs/capturas/');
};

main().catch((e) => { console.error(e); process.exit(1); });
