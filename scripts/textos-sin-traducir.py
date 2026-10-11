# -*- coding: utf-8 -*-
"""Encuentra los textos del frontend que no pasan por el diccionario (H-72).

    python scripts/textos-sin-traducir.py             # informe
    python scripts/textos-sin-traducir.py --aplicar   # reemplaza los que tienen clave
    python scripts/textos-sin-traducir.py --verificar # sale != 0 si crecieron

El marco de la aplicación estaba traducido —menú, login, títulos— y el cuerpo
de cada pantalla seguía en castellano fijo escrito en el JSX. El diccionario ya
tenía cargadas casi todas las claves con su inglés; lo que faltaba era usarlas.

Cómo decide qué es un texto de la interfaz:

- el contenido de un nodo JSX (`<h2>Equipos</h2>`),
- el valor de los atributos que el usuario lee (`placeholder`, `title`,
  `aria-label`, `label`),

y sólo si tiene una letra acentuada, una eñe, una palabra del castellano, o si
el texto ya figura tal cual en el diccionario. Esto último importa:
«Responsable» o «Riesgo» no tienen acento ni artículo, y sin esa regla pasaban
de largo. Un identificador, una clase de CSS o una ruta no entran.

Es una heurística, así que no reemplaza nada que no tenga una clave exacta en
el diccionario: lo que no puede resolver lo lista para que lo mire una persona.

`--verificar` es la red de seguridad: no exige cero —puede aparecer un literal
que no valga la pena traducir— sino que el número no suba respecto de la línea
de base.
"""
from __future__ import print_function

import bisect
import io
import json
import os
import re
import sys
import unicodedata

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
FUENTE = os.path.join(RAIZ, 'frontend', 'src')
SEED = os.path.join(RAIZ, 'db', '05-seed-traducciones.sql')
LINEA_BASE = os.path.join(AQUI, '_textos-sin-traducir.json')

# Atributos cuyo valor se muestra. `alt` no está: describe la imagen para quien
# no la ve, y traducirlo sin ver el contexto sale mal más veces de las que sale
# bien.
ATRIBUTOS = ('placeholder', 'title', 'aria-label', 'label',
             # Props propias que llevan texto visible: el encabezado de cada
             # pantalla recibe `titulo` y `contexto`, y los grupos de opciones
             # reciben `etiqueta`. Faltaban, y por eso el titulo de nueve
             # pantallas nunca se habia traducido.
             'titulo', 'contexto', 'etiqueta', 'detalle')

# Palabras que delatan castellano en un texto sin acentos ni eñes.
#
# La segunda mitad son las de los textos cortos de tiempo y cantidad —«hace 3
# d», «ayer», «1 mes»—. Sin ellas la heurística los dejaba pasar: no tienen
# acento ni artículo, y son justamente los que más se repiten en las tablas.
# Aparecieron mirando la pantalla en inglés y no en el informe del script, que
# es la razón por la que la lista está acá y no en una nota.
CASTELLANO = re.compile(
    r'\b(el|la|los|las|un|una|de|del|que|con|sin|por|para|desde|hasta|'
    r'no|hay|este|esta|todos|todas|cada|entre|sobre|se|su|sus|al|es|son|'
    r'hace|hoy|ayer|mes|meses|dia|dias|anio|anios|hora|horas|minuto|minutos|'
    r'nunca|solo|todo|nada|algo|mas|menos|ahora|antes|luego)\b',
    re.IGNORECASE)

ACENTOS = re.compile('[áéíóúüñÁÉÍÓÚÜÑ¿¡]')

# Un texto que parece un rótulo aunque no tenga acento ni artículo: dos palabras
# de letras seguidas, o una sola que empieza en mayúscula.
#
# Esta regla se agregó después y por una medición, no por prolijidad. Con la
# interfaz puesta en inglés quedaban en castellano «Estado operativo», «Datos
# administrativos», «Configurar reglas», «Fallos seguidos» y una treintena más,
# y el script daba cero: ninguno tiene acento, ni eñe, ni una palabra de la
# lista, ni figuraba en el diccionario. El recuento en cero decía que no faltaba
# nada y faltaban cuarenta.
PROSA = re.compile(u'^[A-Za-zÁÉÍÓÚÑáéíóúñü]{2,}$')
INICIAL = re.compile(r'^[A-ZÁÉÍÓÚÑ][\wÁÉÍÓÚÑáéíóúñü]{2,}$')

# Lo que la regla de arriba levanta y no es un texto de interfaz: un comando de
# SQL que la pantalla de respaldos muestra a propósito tal cual se ejecuta, el
# identificador de un modelo, el prefijo de una clave de API, un código de
# equipo de ejemplo y un tipo de TypeScript.
TECNICO = re.compile(
    r'^(BACKUP|RESTORE|SELECT|INSERT|UPDATE|DELETE|MERGE)\b'
    r'|^claude-|^sk-ant-|^Promise\b'
    # Codigos de equipo de la vista previa: TRES-LETRAS-TRES-DIGITOS.
    r'|^[A-Z]{2,4}-[A-Z]{3}-\d{3}$')


def parece_rotulo(texto):
    t = ' '.join(texto.split())
    if TECNICO.search(t):
        return False
    palabras = [w for w in re.split(r'[\s·—-]+', t) if w]
    if len(palabras) >= 2:
        return sum(1 for w in palabras if PROSA.match(w)) >= 2
    return bool(INICIAL.match(t))

# Una hora armada dentro de una plantilla —`${dia}T12:00:00`— no empieza por
# el patrón, así que se busca en cualquier posición y no sólo al principio.
HORA_ARMADA = re.compile(r'\d{2}:\d{2}')

# Texto que no es de la interfaz aunque lo parezca.
IGNORAR = re.compile(r'^[\s\d\W]*$|^[a-z][a-zA-Z0-9]*$|^https?:|^/|^#'
                     r'|^[a-z]{2}-[A-Z]{2}$'
                     # Una ruta de import: `../idioma/hace` tiene «hace».
                     r'|^\.{1,2}/'
                     # Un pedazo de código que quedó entre dos comillas.
                     r'|^\s*\+\s')

# Al permitir saltos de línea, el `>...<` de un nodo JSX también abarca código
# que quedó entre un `>` y un `<` de otra cosa: el cierre de una expresión, un
# comentario, una comparación. Estos signos no aparecen en un texto que se
# muestra, y sí en código.
# El punto y coma cuenta como codigo solo al cerrar una sentencia. Suelto en
# medio de una frase es puntuacion, y descartaba parrafos enteros: el de
# «como calibrar las reglas» quedaba sin traducir por tener uno.
CODIGO = re.compile(r'//|=>|&&|\|\||;\s*($|[})])|\(\)|\breturn\b|\bconst\b',
                    re.M)


def normalizado(texto):
    """Para comparar textos: sin acentos, sin puntuación de borde, en minúscula."""
    t = unicodedata.normalize('NFD', texto.strip().lower())
    t = ''.join(c for c in t if unicodedata.category(c) != 'Mn')
    return re.sub(r'\s+', ' ', t).strip(' .:·…')


def es_de_la_interfaz(texto, conocidos=(), rotulos=True):
    t = texto.strip()
    if len(t) < 3 or IGNORAR.match(t):
        return False

    # Un texto que ya contiene una llamada es una traducción hecha, no un
    # literal: pasa cuando el reemplazo cae dentro del rango de otra
    # coincidencia y el detector se lee a sí mismo.
    if "t('" in t or CODIGO.search(t) or HORA_ARMADA.search(t):
        return False

    if normalizado(t) in conocidos:
        return True

    if ACENTOS.search(t) or CASTELLANO.search(t):
        return True

    # `rotulos` distingue el JSX del codigo. Adentro de una expresion una
    # palabra suelta con mayuscula es casi siempre un identificador o el lado
    # derecho de una comparacion, no un texto: aplicar ahi la regla ancha
    # levantaba ciento treinta falsos positivos y ninguno era traducible.
    return bool(rotulos and parece_rotulo(t))


def diccionario():
    """Las claves del seed con su castellano, para poder buscar por texto."""
    sql = io.open(SEED, encoding='utf-8').read()
    porTexto = {}

    # ('clave', 'castellano', 'inglés'). Las claves son camelCase —ia.queDatos—
    # así que la clase de caracteres incluye mayúsculas: sin eso leía 113 de 181
    # y daba por no traducibles textos que ya tenían clave.
    for m in re.finditer(r"\('([A-Za-z0-9._]+)',\s*'((?:[^']|'')*)'", sql):
        clave, es = m.group(1), m.group(2).replace("''", "'")
        porTexto.setdefault(normalizado(es), clave)

    return porTexto


def literales(ruta, conocidos=()):
    """Los textos de interfaz de un archivo, con su posición exacta."""
    codigo = io.open(ruta, encoding='utf-8').read()
    encontrados = []

    # 1) Contenido de nodos JSX: entre > y <, sin llaves ni etiquetas adentro.
    #    Se permiten saltos de línea porque el texto de un <Link> o de un <p>
    #    largo suele estar en su propio renglón, indentado.
    for m in re.finditer(r'>([^<>{}]{3,}?)<', codigo, re.S):
        if es_de_la_interfaz(m.group(1), conocidos):
            encontrados.append((m.start(1), m.end(1), m.group(1), 'nodo'))

    # 2) Atributos que se leen. Se toma el atributo entero y no sólo su valor:
    #    en JSX el reemplazo va sin comillas —placeholder={t(...)}— y dejarlas
    #    convierte la llamada en el texto literal «{t('clave', 'texto')}».
    for atributo in ATRIBUTOS:
        for m in re.finditer(r'\b(%s)="([^"]{3,})"' % atributo, codigo):
            if es_de_la_interfaz(m.group(2), conocidos):
                encontrados.append((m.start(), m.end(), m.group(2), atributo))

    # Lo que ya está adentro de un t(...) no cuenta. El rango se calcula
    # contando paréntesis y no con un margen fijo: con un margen de doscientos
    # caracteres se descartaban también los literales de al lado, y los
    # encabezados «Tipo» y «Estado» quedaban sin traducir por tener una llamada
    # unas líneas más arriba.
    yaTraducido = []
    for m in re.finditer(r"\bt\(\s*'", codigo):
        # Se empieza a contar en el nombre de la función y no donde termina
        # la coincidencia: con `t(` en un renglón y el texto en el siguiente,
        # el conteo arrancaba pasado el paréntesis de apertura y el primer `(`
        # del propio texto —«(10.2.3)»— cerraba el rango antes de tiempo. El
        # efecto era contar como pendiente un texto que ya estaba traducido.
        nivel, i = 0, m.start()
        while i < len(codigo):
            if codigo[i] == '(':
                nivel += 1
            elif codigo[i] == ')':
                nivel -= 1
                if nivel == 0:
                    break
            i += 1
        yaTraducido.append((m.start(), i))

    encontrados = [e for e in encontrados
                   if not any(a <= e[0] <= b for a, b in yaTraducido)]

    # Y dos coincidencias que se pisan tampoco: el contenido de un nodo puede
    # caer adentro del rango de un atributo. Reemplazar las dos deja la segunda
    # escrita encima de la primera y el archivo roto. Gana la más larga.
    encontrados.sort(key=lambda e: (e[0], -(e[1] - e[0])))
    sinSolape, hasta = [], -1
    for e in encontrados:
        if e[0] >= hasta:
            sinSolape.append(e)
            hasta = e[1]

    return codigo, sinSolape


def reemplazo(clave, texto, donde):
    """Cómo se escribe el reemplazo según dónde estaba el texto."""
    limpio = ' '.join(texto.split()).replace("'", "\\'")
    llamada = "{t('%s', '%s')}" % (clave, limpio)

    if donde == 'nodo':
        # Se conservan los espacios y los saltos de los bordes: en JSX separan
        # el texto de lo que venga pegado al lado.
        izquierda = texto[:len(texto) - len(texto.lstrip())]
        derecha = texto[len(texto.rstrip()):]
        return izquierda + llamada + derecha

    # Un atributo se reemplaza entero, sin las comillas.
    return '%s=%s' % (donde, llamada)


def rangos_de_llamadas(codigo):
    """Dónde empieza y termina cada t(...) y cada p(...), contando paréntesis.

    `p` es el helper de plural: `p(n, 'clave', '1 equipo', '{n} equipos')`. Su
    castellano es el texto de reserva de las dos formas, igual que el segundo
    argumento de `t`, así que lo de adentro tampoco es un pendiente.
    """
    rangos = []
    for m in re.finditer(r"\bt\(\s*'|\bp\(\s*\w", codigo):
        # Se empieza a contar en el nombre de la función y no donde termina
        # la coincidencia: con `t(` en un renglón y el texto en el siguiente,
        # el conteo arrancaba pasado el paréntesis de apertura y el primer `(`
        # del propio texto —«(10.2.3)»— cerraba el rango antes de tiempo. El
        # efecto era contar como pendiente un texto que ya estaba traducido.
        nivel, i = 0, m.start()
        while i < len(codigo):
            if codigo[i] == '(':
                nivel += 1
            elif codigo[i] == ')':
                nivel -= 1
                if nivel == 0:
                    break
            i += 1
        rangos.append((m.start(), i))
    return rangos


# Lineas donde un literal en castellano no es un pendiente:
#
# - `clave: 'nav.activos', texto: 'Activos'` — el castellano es el texto de
#   reserva de un `t()` que ocurre al dibujar. La lista se declara fuera de
#   todo componente y por eso la clave viaja al lado.
# - `['En observación', 'var(--medio-barra)']` — una tabla de color indexada
#   por el nombre del estado: es un dato, no un texto.
# - `throw new Error(...)` — un error de programación que ve quien programa,
#   no la persona que usa el sistema.
# - `className={cond ? 'paso con-riesgo' : 'paso'}` -- un nombre de clase
#   CSS nunca se muestra, aunque este escrito en castellano.
# - un comentario.
RESUELTO = re.compile(
    r"\bclave\w*\s*:"
    r"|className="
    r"|'var\(--"
    r"|new Error\("
    r"|^\s*(?://|\*|\{/\*)"
    r"|[!=]==\s*'"
    r"|^\s*\['[a-z]+\.[A-Za-z]+',"
    # Una tabla de reserva: `'dash.equiposActivos': 'Equipos activos',`. El
    # castellano esta ahi para que la pantalla se lea si el diccionario no
    # cargo, igual que el segundo argumento de `t()`.
    r"|^\s*'[a-z]+\.[A-Za-z]+':")

# La clave y su texto de reserva pueden estar en renglones distintos.
CON_CLAVE = re.compile(r"\bclave\w*\s*:")


def en_expresiones(ruta, conocidos=()):
    """Literales en castellano que están dentro de código, no en el JSX.

    Son los fragmentos de plantilla y los ternarios que quedaron sin pasar por
    el diccionario. Se cuentan pero **no se reemplazan**, y el motivo es
    concreto: buena parte de esos literales no se muestran sino que se
    comparan —`estado === 'En reparación'`— y cambiarlos por una llamada al
    diccionario rompería la lógica en silencio. Distinguir uno de otro pide
    leer el código, no una expresión regular.

    Lo que ya está resuelto no se cuenta: ver `RESUELTO`. Un recuento que
    incluye lo hecho no baja nunca, y un número que no baja se ignora.
    """
    codigo = io.open(ruta, encoding='utf-8').read()
    rangos = rangos_de_llamadas(codigo)

    lineas = codigo.split('\n')
    inicios, pos = [], 0
    for l in lineas:
        inicios.append(pos)
        pos += len(l) + 1

    encontrados = []
    for m in re.finditer(r"'([^'\n]{4,})'|`([^`\n]{4,})`", codigo):
        texto = m.group(1) or m.group(2)
        if any(a <= m.start() <= b for a, b in rangos):
            continue
        if not es_de_la_interfaz(texto, conocidos, rotulos=False):
            continue

        i = bisect.bisect_right(inicios, m.start()) - 1
        # Se miran también los renglones de alrededor: en una lista de varias
        # líneas la clave y su texto quedan en renglones distintos.
        vecindad = '\n'.join(lineas[max(0, i - 2):i + 2])
        if RESUELTO.search(lineas[i]) or CON_CLAVE.search(vecindad):
            continue

        encontrados.append(texto)

    return encontrados


def main():
    aplicar = '--aplicar' in sys.argv
    verificar = '--verificar' in sys.argv

    porTexto = diccionario()
    conClave, sinClave, cambiados, enExpresiones = [], [], 0, 0

    for carpeta, _, archivos in os.walk(FUENTE):
        if 'pruebas' in carpeta:
            continue
        for archivo in sorted(archivos):
            if not archivo.endswith('.tsx'):
                continue

            ruta = os.path.join(carpeta, archivo)
            codigo, textos = literales(ruta, porTexto)
            relativo = os.path.relpath(ruta, RAIZ).replace('\\', '/')
            nuevo = codigo

            # De atrás para adelante, para que las posiciones no se corran.
            for ini, fin, texto, donde in sorted(textos, reverse=True):
                clave = porTexto.get(normalizado(texto))
                if clave:
                    conClave.append((relativo, clave, ' '.join(texto.split())))
                    if aplicar:
                        nuevo = nuevo[:ini] + reemplazo(clave, texto, donde) + nuevo[fin:]
                else:
                    sinClave.append((relativo, ' '.join(texto.split())))

            if aplicar and nuevo != codigo:
                io.open(ruta, 'w', encoding='utf-8', newline='\n').write(nuevo)
                cambiados += 1

            enExpresiones += len(en_expresiones(ruta, porTexto))

    total = len(conClave) + len(sinClave)

    if aplicar:
        print('Reemplazados %d textos en %d archivos.' % (len(conClave), cambiados))
        print('Quedan %d sin clave en el diccionario.' % len(sinClave))
    else:
        print('Textos de interfaz fuera del diccionario: %d' % total)
        print('  con clave ya cargada (se pueden reemplazar): %d' % len(conClave))
        print('  sin clave (hay que agregarla al seed):       %d' % len(sinClave))

    print('\nLiterales en expresiones de código: %d' % enExpresiones)
    if enExpresiones:
        print('  Plantillas y ternarios que todavía no pasan por el diccionario.')
        print('  No se reemplazan solos: hay que leer cada uno, porque un literal')
        print('  que se compara rompería la lógica sin que falle nada.')

    if sinClave:
        print('\nSin clave, por archivo:')
        porArchivo = {}
        for archivo, texto in sinClave:
            porArchivo.setdefault(archivo, []).append(texto)
        for archivo in sorted(porArchivo):
            print('  %s (%d)' % (archivo, len(porArchivo[archivo])))
            for texto in porArchivo[archivo][:8]:
                print('      %s' % texto[:78])

    if verificar:
        base = 10 ** 9
        if os.path.exists(LINEA_BASE):
            base = json.load(io.open(LINEA_BASE, encoding='utf-8'))['total']
        if total > base:
            print('\nSubieron de %d a %d. Un texto nuevo se escribe con '
                  "t('clave', 'texto')." % (base, total))
            return 1
        print('\nSin crecimiento contra la línea de base (%d).' % base)

    if '--guardar' in sys.argv:
        io.open(LINEA_BASE, 'w', encoding='utf-8', newline='\n').write(
            json.dumps({'total': total}, ensure_ascii=False, indent=2) + '\n')
        print('Línea de base fijada en %d.' % total)

    return 0


if __name__ == '__main__':
    sys.exit(main())
