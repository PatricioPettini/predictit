# -*- coding: utf-8 -*-
"""Verifica que la suite no deje residuo en la base (H-44).

    python scripts/residuo-de-las-pruebas.py

Cuenta las filas de las tablas que las pruebas de integración tocan, corre la
suite entera, y vuelve a contar. Si algún número cambió, la suite dejó algo
atrás.

Importa porque una prueba que deja filas contamina a la siguiente: la que
después cuenta equipos empieza a contar los que dejó la anterior, y el día que
falla nadie sabe si es un defecto del sistema o basura de otra prueba. Y porque
la base de esta máquina es la misma que se muestra en la demostración.

Requiere el contenedor levantado.
"""
from __future__ import print_function

import os
import subprocess
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOTNET = r'C:\Program Files\dotnet\dotnet.exe'

CONTENEDOR = 'predictit-sql'
SQLCMD = '/opt/mssql-tools18/bin/sqlcmd'
PASSWORD = os.environ.get('MSSQL_SA_PASSWORD', 'PredictIT_2026!')

# La bitácora NO está en esta lista, y no es un olvido. Es un registro de
# solo-agregado: un disparador rechaza cualquier UPDATE o DELETE con el error
# 50001, así que ni las pruebas ni nadie pueden borrar de ahí. Que crezca
# cuando la suite intenta veinte logins fallidos es el comportamiento correcto
# —esos intentos pasaron de verdad— y exigirle que no crezca sería pedirle a la
# bitácora que deje de ser una bitácora.
TABLAS = [
    ('PredictIT_Negocio', 'Equipo'),
    ('PredictIT_Negocio', 'Incidencia'),
    ('PredictIT_Negocio', 'Mantenimiento'),
    ('PredictIT_Negocio', 'AlertaPredictiva'),
    ('PredictIT_Negocio', 'EvaluacionRiesgo'),
    ('PredictIT_Negocio', 'RecomendacionAsignacion'),
    ('PredictIT_Negocio', 'ClasificacionIncidencia'),
    ('PredictIT_Negocio', 'Organizacion'),
    ('PredictIT_Negocio', 'ReglaAlerta'),
    ('PredictIT_Servicio', 'Usuario'),
    ('PredictIT_Servicio', 'Rol'),
    ('PredictIT_Servicio', 'Respaldo'),
    ('PredictIT_Servicio', 'Traduccion'),
]


def contar():
    """{'base.tabla': filas} en una sola consulta."""
    partes = ["SELECT '%s.%s=' + CAST(COUNT(*) AS varchar) FROM %s.dbo.%s"
              % (b, t, b, t) for b, t in TABLAS]
    consulta = 'SET NOCOUNT ON; ' + ' UNION ALL '.join(partes) + ';'

    r = subprocess.run(
        ['docker', 'exec', CONTENEDOR, SQLCMD, '-S', 'localhost', '-U', 'sa',
         '-P', PASSWORD, '-C', '-h', '-1', '-W', '-Q', consulta],
        capture_output=True, text=True, encoding='utf-8', errors='replace')

    if r.returncode != 0:
        raise SystemExit('sqlcmd falló:\n%s' % (r.stderr or r.stdout))

    salida = {}
    for linea in r.stdout.split('\n'):
        linea = linea.strip()
        if '=' in linea and '.' in linea:
            clave, valor = linea.rsplit('=', 1)
            if valor.isdigit():
                salida[clave] = int(valor)
    return salida


def main():
    print('Contando antes…')
    antes = contar()
    if not antes:
        raise SystemExit('No pude leer los conteos. ¿Está levantado el contenedor?')

    print('Corriendo la suite…')
    r = subprocess.run([DOTNET, 'test', os.path.join(RAIZ, 'backend', 'PredictIT.Tests'),
                        '--nologo', '-v', 'q'],
                       capture_output=True, text=True, encoding='utf-8',
                       errors='replace', cwd=RAIZ)

    resumen = [l for l in (r.stdout or '').split('\n') if 'Correctas' in l or 'Erróneas' in l]
    if resumen:
        print('  %s' % resumen[-1].strip()[:100])

    print('Contando después…')
    despues = contar()

    diferencias = [(k, antes[k], despues.get(k, 0))
                   for k in sorted(antes) if antes[k] != despues.get(k)]

    if not diferencias:
        print('\nSin residuo: las %d tablas quedaron con las mismas filas.' % len(antes))
        return 0

    print('\nLa suite dejó residuo:')
    for clave, a, d in diferencias:
        print('  %-42s %6d -> %6d  (%+d)' % (clave, a, d, d - a))
    return 1


if __name__ == '__main__':
    sys.exit(main())
