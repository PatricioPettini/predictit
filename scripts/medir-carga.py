# -*- coding: utf-8 -*-
"""Mide el requisito de carga del ADR 0008 (H-42).

    python scripts/medir-carga.py

El ADR compromete 500 activos y 5.000 incidencias con respuesta por debajo de
los dos segundos. Estaba afirmado y no medido, que es la peor forma de tener un
requisito no funcional: nadie sabe si se cumple hasta el día que no se cumple.

Cómo mide:

1. Crea una organización aparte con el volumen que pide el ADR.
2. Entra como un usuario de esa organización y cronometra los endpoints que
   recorren tablas grandes.
3. Borra la organización, pase lo que pase.

La organización es aparte y no la de demostración por dos motivos. No hay que
ensuciar los datos que se muestran en la defensa, y el aislamiento por
organización se aplica en el acceso a datos: medir contra una organización con
volumen real es además la prueba de que ese filtro escala.

Requiere el contenedor levantado y la API andando.
"""
from __future__ import print_function

import datetime as dt
import io
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
API = os.environ.get('PREDICTIT_API', 'http://localhost:8081')

CONTENEDOR = 'predictit-sql'
SQLCMD = '/opt/mssql-tools18/bin/sqlcmd'
PASSWORD = os.environ.get('MSSQL_SA_PASSWORD', 'PredictIT_2026!')

# Identificadores fijos, para poder limpiar aunque el script se corte.
ORG = 'AF000000-0000-0000-0000-0000000000CA'
USUARIO = 'BF000000-0000-0000-0000-0000000000CA'
LOGIN = 'carga.medicion'
CLAVE = 'Carga.2026'

# El objetivo del ADR 0008.
EQUIPOS = 500
INCIDENCIAS = 5000
OBJETIVO_MS = 2000

# El ADR 0008 compromete 20 usuarios concurrentes con p95 < 2 s. Cada uno
# recorre cuatro pantallas, y se repite el barrido tres veces para que la
# medicion no dependa de un pico puntual.
USUARIOS_CONCURRENTES = 20
VUELTAS = 3

INFORME = os.path.join(RAIZ, 'docs', 'tecnico', 'CARGA.md')


def sql(base, consulta):
    # -I habilita QUOTED_IDENTIFIER, que sqlcmd deja apagado en -Q. Sin eso,
    # cualquier DELETE sobre una tabla con indice en columna calculada falla
    # con el Msg 1934, que no dice nada de lo que realmente pasa.
    r = subprocess.run(
        ['docker', 'exec', CONTENEDOR, SQLCMD, '-S', 'localhost', '-U', 'sa',
         '-P', PASSWORD, '-C', '-b', '-I', '-d', base, '-h', '-1', '-W', '-Q',
         'SET NOCOUNT ON; ' + consulta],
        capture_output=True, text=True, encoding='utf-8', errors='replace')

    if r.returncode != 0:
        raise SystemExit('sqlcmd falló:\n%s' % (r.stderr or r.stdout)[-1500:])
    return r.stdout


def crear_volumen():
    """Organización con el volumen del ADR, en dos INSERT masivos."""
    print('Creando la organización de medición…')

    # El hash es el de «Carga.2026» con el mismo formato que usa el sistema:
    # PBKDF2-HMAC-SHA256, con las iteraciones por defecto del servicio. Se genera acá y no se copia de
    # otro usuario para no depender de una contraseña de la semilla.
    hash_clave = generar_hash(CLAVE)

    # Se reactiva si ya existe de una corrida anterior: no se puede borrar
    # porque la bitácora lo referencia (ver `limpiar`).
    sql('PredictIT_Servicio', """
        DELETE FROM dbo.Usuario_Rol WHERE id_usuario = '%(u)s';

        IF EXISTS (SELECT 1 FROM dbo.Usuario WHERE id_usuario = '%(u)s')
            UPDATE dbo.Usuario
               SET activo = 1, bloqueado = 0, intentos_fallidos = 0,
                   [password] = '%(hash)s', id_organizacion = '%(org)s'
             WHERE id_usuario = '%(u)s';
        ELSE
            INSERT INTO dbo.Usuario (id_usuario, nombre, apellido, email, username,
                                     [password], id_organizacion)
            VALUES ('%(u)s', 'Medición', 'De carga', 'carga@predictit.test',
                    '%(login)s', '%(hash)s', '%(org)s');

        INSERT INTO dbo.Usuario_Rol (id_usuario, id_rol)
        SELECT '%(u)s', id_rol FROM dbo.Rol WHERE nombre = 'Administrador';
        """ % {'u': USUARIO, 'login': LOGIN, 'hash': hash_clave, 'org': ORG})

    sql('PredictIT_Negocio', """
        DELETE FROM dbo.Incidencia WHERE id_organizacion = '%(org)s';
        DELETE FROM dbo.Equipo     WHERE id_organizacion = '%(org)s';
        DELETE FROM dbo.Organizacion WHERE id_organizacion = '%(org)s';

        INSERT INTO dbo.Organizacion (id_organizacion, razon_social, nombre_corto,
                                      cuit, id_plan, activa)
        SELECT '%(org)s', 'Medición de carga', 'Carga', '30-00000000-0',
               (SELECT TOP 1 id_plan FROM dbo.PlanComercial ORDER BY abono_mensual DESC), 1;

        -- Los equipos, en una sola sentencia. Un bucle de 500 INSERT tarda
        -- más en el ida y vuelta que en escribir.
        WITH numeros AS (
            SELECT TOP (%(equipos)d) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
            FROM sys.all_objects a CROSS JOIN sys.all_objects b)
        INSERT INTO dbo.Equipo (id_equipo, id_organizacion, codigo, id_tipo_equipo,
                                id_estado_equipo, id_ubicacion, marca, modelo,
                                numero_serie, fecha_alta, fecha_adquisicion, criticidad)
        SELECT NEWID(), '%(org)s',
               'CARGA-' + RIGHT('0000' + CAST(n AS varchar), 4),
               (SELECT TOP 1 id_tipo_equipo FROM dbo.TipoEquipo ORDER BY nombre),
               (SELECT TOP 1 id_estado_equipo FROM dbo.EstadoEquipo WHERE operativo = 1),
               NULL, 'Marca', 'Modelo', 'SN-CARGA-' + CAST(n AS varchar),
               DATEADD(day, -n, GETDATE()), DATEADD(day, -n - 30, GETDATE()),
               1 + (n %% 4)
        FROM numeros;
        """ % {'org': ORG, 'equipos': EQUIPOS})

    sql('PredictIT_Negocio', """
        WITH numeros AS (
            SELECT TOP (%(inc)d) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
            FROM sys.all_objects a CROSS JOIN sys.all_objects b),
        equipos AS (
            SELECT id_equipo, ROW_NUMBER() OVER (ORDER BY codigo) AS fila
            FROM dbo.Equipo WHERE id_organizacion = '%(org)s')
        INSERT INTO dbo.Incidencia (id_incidencia, id_organizacion, numero, id_equipo,
                                    titulo, descripcion, id_estado_incidencia,
                                    id_prioridad, fecha, id_usuario_reportante)
        SELECT NEWID(), '%(org)s', n,
               (SELECT id_equipo FROM equipos WHERE fila = 1 + (n %% %(equipos)d)),
               'Incidencia de medición ' + CAST(n AS varchar),
               'Generada por scripts/medir-carga.py',
               (SELECT TOP 1 id_estado_incidencia FROM dbo.EstadoIncidencia ORDER BY orden),
               (SELECT TOP 1 id_prioridad FROM dbo.PrioridadIncidencia ORDER BY nivel),
               DATEADD(hour, -n, GETDATE()), '%(usuario)s'
        FROM numeros;
        """ % {'org': ORG, 'inc': INCIDENCIAS, 'equipos': EQUIPOS, 'usuario': USUARIO})

    equipos = contar('Equipo')
    incidencias = contar('Incidencia')
    print('  %d equipos, %d incidencias' % (equipos, incidencias))
    return equipos, incidencias


def contar(tabla):
    salida = sql('PredictIT_Negocio',
                 "SELECT COUNT(*) FROM dbo.%s WHERE id_organizacion = '%s';" % (tabla, ORG))
    for linea in salida.split('\n'):
        if linea.strip().isdigit():
            return int(linea.strip())
    return 0


def generar_hash(clave):
    """PBKDF2-HMAC-SHA256 con el formato del sistema."""
    import base64
    import hashlib
    import os as sistema

    sal = sistema.urandom(16)
    derivada = hashlib.pbkdf2_hmac('sha256', clave.encode('utf-8'), sal, 120000, 32)
    return 'PBKDF2$120000$%s$%s' % (base64.b64encode(sal).decode(),
                                    base64.b64encode(derivada).decode())


def limpiar():
    print('Borrando la organización de medición…')
    sql('PredictIT_Negocio', """
        DELETE FROM dbo.Incidencia   WHERE id_organizacion = '%(org)s';
        DELETE FROM dbo.Equipo       WHERE id_organizacion = '%(org)s';
        DELETE FROM dbo.Organizacion WHERE id_organizacion = '%(org)s';
        """ % {'org': ORG})
    # El usuario NO se borra: se desactiva y se le quitan los roles.
    #
    # No es una concesión, es una consecuencia del diseño. El login de la
    # medición dejó una entrada en la bitácora, la bitácora la referencia por
    # clave foránea, y la bitácora no se puede borrar —un disparador rechaza
    # el DELETE con el error 50001—. Un usuario que hizo algo alguna vez no se
    # puede eliminar del sistema, y por eso el sistema ofrece dar de baja y no
    # borrar.
    sql('PredictIT_Servicio', """
        DELETE FROM dbo.Usuario_Rol WHERE id_usuario = '%(u)s';
        UPDATE dbo.Usuario SET activo = 0 WHERE id_usuario = '%(u)s';
        """ % {'u': USUARIO})


def pedir(ruta, token=None, cuerpo=None):
    """Devuelve (milisegundos, código, longitud)."""
    datos = json.dumps(cuerpo).encode('utf-8') if cuerpo is not None else None
    pedido = urllib.request.Request(API + ruta, data=datos, method='POST' if datos else 'GET')
    if datos:
        pedido.add_header('Content-Type', 'application/json')
    if token:
        pedido.add_header('Authorization', 'Bearer ' + token)

    inicio = time.perf_counter()
    try:
        with urllib.request.urlopen(pedido, timeout=60) as r:
            contenido = r.read()
            codigo = r.status
    except urllib.error.HTTPError as e:
        contenido = e.read()
        codigo = e.code

    return (time.perf_counter() - inicio) * 1000.0, codigo, len(contenido)


def medir(token):
    """Cronometra cada endpoint. Se descarta la primera y se toma la mediana."""
    ENDPOINTS = [
        ('Listado de activos, primera página', '/api/equipos?porPagina=25'),
        ('Listado de activos, 500 de una', '/api/equipos?porPagina=1000'),
        ('Listado de incidencias, primera página', '/api/incidencias?porPagina=25'),
        ('Listado de incidencias, 1.000 de una', '/api/incidencias?porPagina=1000'),
        ('Tablero', '/api/prediccion/dashboard'),
        ('Panel predictivo', '/api/prediccion/panel'),
        ('Historial del parque', '/api/historial'),
    ]

    filas = []
    for nombre, ruta in ENDPOINTS:
        # La primera llamada paga la compilación del plan de consulta y el
        # arranque en frío del pool: se descarta o el número mide otra cosa.
        pedir(ruta, token)

        medidas = sorted(pedir(ruta, token) for _ in range(5))
        mediana_ms, codigo, tamano = medidas[2]
        peor_ms = medidas[-1][0]

        filas.append((nombre, ruta, codigo, mediana_ms, peor_ms, tamano))
        print('  %-42s %6.0f ms  (peor %6.0f)  %s'
              % (nombre, mediana_ms, peor_ms, codigo))

    return filas


def medir_concurrencia(token):
    """Los 20 usuarios simultáneos que compromete el ADR 0008.

    La medición secuencial de arriba dice que las consultas aguantan el
    volumen; no dice nada sobre cuántos clientes soporta a la vez, que es un
    número distinto y el que el ADR compromete. Con un solo cliente no se ve
    la contención del pool de conexiones ni la cola del servidor.

    Se usa el mismo token para los veinte hilos: lo que se mide es el
    servidor, no el login. Y se mide el p95 y no el promedio porque un
    promedio de 200 ms con una cola de 4 s esconde justamente el caso que
    importa.
    """
    from concurrent.futures import ThreadPoolExecutor

    # El recorrido de un usuario real, no un solo endpoint repetido: entra al
    # tablero, mira el listado y abre el análisis.
    RECORRIDO = [
        '/api/prediccion/dashboard',
        '/api/equipos?porPagina=25',
        '/api/incidencias?porPagina=25',
        '/api/prediccion/panel',
    ]

    def usuario(_):
        tiempos = []
        for ruta in RECORRIDO:
            ms, codigo, _tam = pedir(ruta, token)
            tiempos.append((ms, codigo))
        return tiempos

    # Una vuelta en frío que no se cuenta, igual que en la medición secuencial.
    usuario(0)

    inicio = time.perf_counter()
    with ThreadPoolExecutor(max_workers=USUARIOS_CONCURRENTES) as pool:
        resultados = list(pool.map(usuario, range(USUARIOS_CONCURRENTES * VUELTAS)))
    total_s = time.perf_counter() - inicio

    tiempos = sorted(ms for vuelta in resultados for ms, _ in vuelta)
    errores = sum(1 for vuelta in resultados for _, c in vuelta if c != 200)

    p95 = tiempos[int(len(tiempos) * 0.95) - 1]
    print('  %d usuarios · %d peticiones · p95 %.0f ms · peor %.0f ms · %d errores'
          % (USUARIOS_CONCURRENTES, len(tiempos), p95, tiempos[-1], errores))

    return {
        'usuarios': USUARIOS_CONCURRENTES,
        'peticiones': len(tiempos),
        'mediana': tiempos[len(tiempos) // 2],
        'p95': p95,
        'peor': tiempos[-1],
        'errores': errores,
        'segundos': total_s,
        'cumple': p95 < OBJETIVO_MS and errores == 0,
    }


def escribir(filas, equipos, incidencias, carga):
    cumple = all(f[3] < OBJETIVO_MS for f in filas) and carga['cumple']
    peor = max(f[4] for f in filas)

    L = ['# Medición del requisito de carga', '',
         '> PredictIT · Trabajo Final de Ingeniería · Patricio Pettini',
         '',
         'El ADR 0008 compromete **%d activos y %d incidencias con respuesta por '
         'debajo de los %d ms**, y **%d usuarios concurrentes con p95 por debajo '
         'de ese mismo número**. Este documento es la medición de las dos cosas, '
         'y se genera corriendo `scripts/medir-carga.py`.'
         % (EQUIPOS, INCIDENCIAS, OBJETIVO_MS, USUARIOS_CONCURRENTES),
         '',
         '## Cómo se midió', '',
         'Se crea una organización aparte con el volumen que pide el ADR, se entra '
         'con un usuario de esa organización y se cronometran los endpoints que '
         'recorren tablas grandes. Al terminar se borra.',
         '',
         'La organización es aparte de la de demostración por dos motivos: no ensuciar '
         'los datos que se muestran en la defensa, y porque el aislamiento por '
         'organización se aplica en el acceso a datos, así que medir contra una '
         'organización cargada es además la prueba de que ese filtro escala.',
         '',
         'De cada endpoint se descarta la primera llamada —paga la compilación del '
         'plan de consulta y el arranque en frío del pool— y de las cinco siguientes '
         'se informa la mediana y la peor.',
         '',
         '## Resultado', '',
         '| | |', '|---|---|',
         '| Fecha | %s |' % dt.datetime.now().strftime('%d/%m/%Y %H:%M'),
         '| Equipos | %d |' % equipos,
         '| Incidencias | %d |' % incidencias,
         '| Objetivo | %d ms |' % OBJETIVO_MS,
         '| Peor medición | %.0f ms |' % peor,
         '| **Cumple** | **%s** |' % ('sí' if cumple else 'no'),
         '',
         'Tabla: Tiempo de respuesta por endpoint, con el volumen del ADR 0008',
         '',
         '| Qué se pidió | Endpoint | Código | Mediana | Peor |',
         '|---|---|---|---|---|']

    for nombre, ruta, codigo, mediana, peor_ms, _ in filas:
        L.append('| %s | `%s` | %d | %.0f ms | %.0f ms |'
                 % (nombre, ruta, codigo, mediana, peor_ms))

    L += ['',
          '## Lo que apareció al limpiar', '',
          'La primera versión de la medición terminaba borrando el usuario que había '
          'usado, y el borrado falló: la bitácora lo referencia por clave foránea, y '
          'la bitácora no se puede borrar porque un disparador rechaza el DELETE.',
          '',
          'No es un defecto: es la consecuencia buscada de tener un registro de '
          'auditoría inmutable. Un usuario que hizo algo alguna vez no se puede '
          'eliminar del sistema, y por eso el sistema ofrece **dar de baja** y no '
          'borrar. La medición ahora desactiva el usuario en lugar de borrarlo.',
          '',
          '## Concurrencia', '',
          'El ADR 0008 compromete además **%d usuarios concurrentes con p95 por '
          'debajo de los %d ms**. Con un solo cliente no se ve la contención del '
          'pool de conexiones ni la cola del servidor, que es donde aparece el '
          'problema cuando aparece.' % (carga['usuarios'], OBJETIVO_MS),
          '',
          'Cada usuario simulado recorre cuatro pantallas —tablero, activos, '
          'incidencias y análisis— en lugar de golpear un endpoint solo, y el '
          'barrido se repite %d veces. Se informa el p95 y no el promedio: un '
          'promedio de 200 ms con una cola de cuatro segundos esconde justamente '
          'el caso que importa.' % VUELTAS,
          '',
          'Tabla: Tiempo de respuesta con %d usuarios concurrentes' % carga['usuarios'],
          '',
          '| | |', '|---|---|',
          '| Usuarios simultáneos | %d |' % carga['usuarios'],
          '| Peticiones | %d |' % carga['peticiones'],
          '| Mediana | %.0f ms |' % carga['mediana'],
          '| **p95** | **%.0f ms** |' % carga['p95'],
          '| Peor | %.0f ms |' % carga['peor'],
          '| Respuestas con error | %d |' % carga['errores'],
          '| **Cumple** | **%s** |' % ('sí' if carga['cumple'] else 'no'),
          '',
          '## Qué NO mide esto', '',
          'Una base local sin latencia de red, en la misma máquina que la API. No '
          'mide el efecto de una base en otro servidor, ni una carga sostenida de '
          'horas, ni escrituras concurrentes sobre la misma incidencia.',
          '',
          'Decirlo importa: se puede afirmar que el volumen y los veinte usuarios '
          'del ADR se cumplen en esta configuración, y no se puede afirmar nada '
          'sobre un despliegue distribuido.',
          '']

    io.open(INFORME, 'w', encoding='utf-8', newline='\n').write('\n'.join(L))
    return cumple


def main():
    try:
        equipos, incidencias = crear_volumen()

        print('Entrando como el usuario de medición…')
        ms, codigo, _ = pedir('/api/auth/login', cuerpo={'username': LOGIN,
                                                         'contrasena': CLAVE})
        if codigo != 200:
            raise SystemExit('El login de medición devolvió %d.' % codigo)

        pedido = urllib.request.Request(
            API + '/api/auth/login', method='POST',
            data=json.dumps({'username': LOGIN, 'contrasena': CLAVE}).encode('utf-8'))
        pedido.add_header('Content-Type', 'application/json')
        with urllib.request.urlopen(pedido, timeout=30) as r:
            token = json.loads(r.read())['token']

        print('Midiendo, un cliente por vez…')
        filas = medir(token)

        print('Midiendo con %d usuarios a la vez…' % USUARIOS_CONCURRENTES)
        carga = medir_concurrencia(token)

        cumple = escribir(filas, equipos, incidencias, carga)

        print('\n%s el objetivo de %d ms con %d equipos, %d incidencias y %d '
              'usuarios concurrentes.'
              % ('CUMPLE' if cumple else 'NO CUMPLE', OBJETIVO_MS, equipos,
                 incidencias, USUARIOS_CONCURRENTES))
        return 0 if cumple else 1

    finally:
        limpiar()


if __name__ == '__main__':
    sys.exit(main())
