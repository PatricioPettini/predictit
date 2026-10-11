# -*- coding: utf-8 -*-
"""Mide la cobertura de pruebas y la publica como línea de base (H-37).

    python scripts/cobertura.py            # mide y compara con la línea de base
    python scripts/cobertura.py --guardar  # fija la línea de base actual

Corre la suite con coverlet, lee el informe Cobertura y escribe
`docs/tecnico/COBERTURA.md` con el número por proyecto.

La línea de base sirve para una sola cosa, y es la que importa: que la
cobertura no baje sin que alguien lo note. Un porcentaje suelto no dice nada
—hay código que no vale la pena cubrir y código que con 100 % sigue estando
mal— pero una caída de diez puntos entre dos commits sí dice algo.

No hay un objetivo de porcentaje declarado a propósito. Poner uno lleva a
escribir pruebas para el número en lugar de para el riesgo.
"""
from __future__ import print_function

import datetime as dt
import glob
import io
import json
import os
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOTNET = r'C:\Program Files\dotnet\dotnet.exe'

PRUEBAS = os.path.join(RAIZ, 'backend', 'PredictIT.Tests')
RESULTADOS = os.path.join(PRUEBAS, 'TestResults')
LINEA_BASE = os.path.join(RAIZ, 'docs', 'tecnico', '_cobertura-base.json')
INFORME = os.path.join(RAIZ, 'docs', 'tecnico', 'COBERTURA.md')

# Cuánto puede bajar antes de que se considere una caída y no ruido. Dos
# puntos: agregar un archivo mueve el número sin que nadie haya roto nada.
TOLERANCIA = 2.0


def medir():
    """Corre la suite con cobertura y devuelve {proyecto: porcentaje}."""
    if os.path.isdir(RESULTADOS):
        shutil.rmtree(RESULTADOS, ignore_errors=True)

    print('Corriendo la suite con cobertura…')
    r = subprocess.run(
        [DOTNET, 'test', PRUEBAS, '--nologo', '-v', 'q',
         '--collect:XPlat Code Coverage'],
        capture_output=True, text=True, encoding='utf-8', errors='replace', cwd=RAIZ)

    informes = glob.glob(os.path.join(RESULTADOS, '**', 'coverage.cobertura.xml'),
                         recursive=True)
    if not informes:
        raise SystemExit('No se generó el informe de cobertura:\n%s'
                         % (r.stdout or r.stderr)[-1500:])

    arbol = ET.parse(informes[0])
    raiz = arbol.getroot()

    # Por paquete, que en Cobertura es el ensamblado.
    por_proyecto = {}
    for paquete in raiz.findall('.//package'):
        nombre = paquete.get('name') or '?'
        # El propio proyecto de pruebas no se mide: cubrir las pruebas con
        # pruebas no dice nada de la calidad del sistema.
        if nombre.endswith('.Tests'):
            continue
        cubiertas = validas = 0
        for linea in paquete.findall('.//line'):
            validas += 1
            if int(linea.get('hits', '0')) > 0:
                cubiertas += 1
        if validas:
            por_proyecto[nombre] = round(100.0 * cubiertas / validas, 1)

    total_c = sum(1 for p in raiz.findall('.//package')
                  if not (p.get('name') or '').endswith('.Tests'))
    if not por_proyecto:
        raise SystemExit('El informe no trae ningún paquete medible.')

    lineas_c = lineas_v = 0
    for paquete in raiz.findall('.//package'):
        if (paquete.get('name') or '').endswith('.Tests'):
            continue
        for linea in paquete.findall('.//line'):
            lineas_v += 1
            if int(linea.get('hits', '0')) > 0:
                lineas_c += 1

    por_proyecto['_total'] = round(100.0 * lineas_c / lineas_v, 1)
    por_proyecto['_lineas'] = lineas_v
    por_proyecto['_proyectos'] = total_c

    shutil.rmtree(RESULTADOS, ignore_errors=True)
    return por_proyecto


def escribir_informe(actual, base):
    proyectos = sorted(k for k in actual if not k.startswith('_'))

    L = ['# Cobertura de pruebas', '',
         '> PredictIT · Trabajo Final de Ingeniería · Patricio Pettini',
         '',
         'Se genera corriendo la suite con coverlet. No se escribe a mano: un '
         'porcentaje escrito a mano sigue diciendo lo mismo el día que la cobertura '
         'se cae.',
         '',
         '## Medición',
         '',
         '| | |', '|---|---|',
         '| Fecha | %s |' % dt.datetime.now().strftime('%d/%m/%Y %H:%M'),
         '| Líneas medidas | %d |' % actual['_lineas'],
         '| **Cobertura total** | **%.1f %%** |' % actual['_total'],
         '']

    L += ['## Por proyecto', '', 'Tabla: Cobertura de líneas por proyecto',
          '', '| Proyecto | Cobertura | Contra la línea de base |', '|---|---|---|']

    for p in proyectos:
        previo = (base or {}).get(p)
        if previo is None:
            delta = 'nuevo'
        else:
            d = actual[p] - previo
            delta = '=' if abs(d) < 0.05 else '%+.1f' % d
        L.append('| %s | %.1f %% | %s |' % (p, actual[p], delta))

    # Un proyecto muy por debajo del resto es un dato que hay que decir, no
    # esconder detrás del promedio. El umbral es generico para que la
    # advertencia siga siendo cierta cuando los numeros cambien.
    flojos = [p for p in proyectos if actual[p] < 20.0]
    if flojos:
        L += ['',
              '## Dónde está el hueco',
              '',
              'Estos proyectos quedan por debajo del 20 %%, muy lejos del resto: '
              '**%s**.' % ', '.join('%s (%.1f %%)' % (p, actual[p]) for p in flojos),
              '',
              'El promedio los tapa, y por eso se listan aparte. En este proyecto el '
              'motivo es conocido: las pruebas de integración construyen los DAO '
              'directamente contra la base, así que ejercitan el acceso a datos y el '
              'dominio sin pasar por la capa de negocio.',
              '',
              'La primera tanda de pruebas propias de la capa de negocio ya está '
              '(H-71): la máquina de estados de la incidencia, el filtro que el '
              'negocio impone desde el contexto —y no el que manda la pantalla— y '
              'las validaciones del alta. Son 41 pruebas contra dobles escritos a '
              'mano, y subieron la capa de 2,3 % a 9,4 %.',
              '',
              'Lo que sigue sin cubrir es el grueso: el alta completa con triage y '
              'asignación, mantenimientos, reportes y administración. El número '
              'sigue siendo bajo y se dice así en lugar de redondearlo para arriba. '
              'No se resuelve subiendo el porcentaje: se resuelve eligiendo qué '
              'regla vale la pena atar con una prueba, que es otro trabajo.']

    L += ['',
          '## Para qué sirve este número',
          '',
          'Para una sola cosa: que la cobertura no baje sin que nadie lo note. '
          'La línea de base está en `docs/tecnico/_cobertura-base.json` y el script '
          'avisa cuando la medición actual queda más de %.0f puntos por debajo.'
          % TOLERANCIA,
          '',
          'No hay un objetivo de porcentaje declarado, y es deliberado. Hay código '
          'que no vale la pena cubrir y código que con el 100 % sigue estando mal. '
          'Poner un número como meta lleva a escribir pruebas para el número en lugar '
          'de para el riesgo, que es lo contrario de lo que se busca.',
          '',
          'Lo que sí está declarado es qué tiene que estar cubierto: los catorce casos '
          'de prueba del capítulo 10, y eso se verifica aparte en '
          '`CASOS-DE-PRUEBA.md`.',
          '']

    io.open(INFORME, 'w', encoding='utf-8', newline='\n').write('\n'.join(L))


def main():
    actual = medir()

    base = None
    if os.path.exists(LINEA_BASE):
        base = json.load(io.open(LINEA_BASE, encoding='utf-8'))

    escribir_informe(actual, base)

    print('\nCobertura total: %.1f %% sobre %d líneas'
          % (actual['_total'], actual['_lineas']))
    for p in sorted(k for k in actual if not k.startswith('_')):
        print('  %-28s %5.1f %%' % (p, actual[p]))

    if '--guardar' in sys.argv or base is None:
        io.open(LINEA_BASE, 'w', encoding='utf-8', newline='\n').write(
            json.dumps(actual, indent=2, ensure_ascii=False) + '\n')
        print('\nLínea de base %s.' % ('actualizada' if base else 'fijada'))
        return 0

    caida = base['_total'] - actual['_total']
    if caida > TOLERANCIA:
        print('\nLa cobertura bajó %.1f puntos contra la línea de base (%.1f %%).'
              % (caida, base['_total']))
        print('Si el cambio es el buscado: python scripts/cobertura.py --guardar')
        return 1

    print('\nSin caídas contra la línea de base (%.1f %%).' % base['_total'])
    return 0


if __name__ == '__main__':
    sys.exit(main())
