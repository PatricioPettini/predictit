# ADR 0015 — Métricas en formato Prometheus, sin stack de observabilidad propio

**Estado:** Aprobado
**Fecha:** 2026-09-15
**Origen:** la pregunta de si el sistema podía tener observabilidad con Grafana
o Prometheus. Al buscar el motivo apareció uno concreto: el RNF-05 no tenía
forma de verificarse

## Contexto

El documento compromete diez requisitos no funcionales. Cuatro son medibles y
tres tienen su medición:

| Requisito | Cómo se verifica hoy |
|---|---|
| RNF-04 · percentil 95 por debajo de 2 s | `scripts/medir-carga.py`, medición puntual |
| RNF-09 · sin fugas de memoria bajo carga | perfilado de memoria del ADR 0008 |
| RNF-10 · degradación controlada de la IA | CP-14 y CP-15, automatizados |
| **RNF-05 · disponibilidad del 95 % del horario laboral** | **nada** |

El RNF-05 es el único requisito del trabajo que se afirma y no se puede
comprobar. El sistema tiene `/api/salud` y los contenedores tienen healthcheck,
pero nadie registra cuánto estuvo arriba: el 95 % es una promesa sin medición.

Hay además un activo construido que no se ve desde afuera. El ADR 0009
incorporó circuit breaker y bulkhead, y el sistema tiene rate limiter, timeout
configurable y reintento en la bitácora. Todo eso **funciona y no deja rastro
consultable**: si el disyuntor se abrió tres veces esta semana, no hay dónde
verlo. Lo único visible es el estado actual del proveedor, en una pantalla.

## Decisión

**El sistema expone sus métricas en formato Prometheus. No incorpora Prometheus
ni Grafana.**

La aplicación expone, la infraestructura recolecta. Es el reparto habitual y
acá además es el que corresponde al alcance: el despliegue en servidor está
declarado fuera de alcance para esta entrega, así que montar un stack de
recolección sería instrumentar un sistema que corre en una notebook.

### Qué se expone

Tres grupos, en `GET /api/metricas`:

1. **Lo que da la biblioteca**: duración y conteo de peticiones HTTP por ruta,
   método y código de respuesta, y los contadores del runtime de .NET —memoria
   administrada, colecciones de basura por generación, hilos del pool—. Cubre
   RNF-04 y RNF-09 de forma continua y no puntual.

2. **El tiempo en línea** (`predictit_tiempo_en_linea_segundos`), que es lo que
   hace verificable el RNF-05. Un contador de uptime más el registro externo del
   recolector permiten calcular la disponibilidad real del período.

3. **Los dos mecanismos del ADR 0009 que se pueden observar sin instrumentar
   el código de negocio**, que es lo que convierte esta decisión en algo más que
   plomería:
   - `predictit_disyuntores_abiertos`, cuántos circuitos no están cerrados en
     este momento. Es un gauge y no un contador de aperturas porque el disyuntor
     no avisa cuando cambia de estado: se lo consulta. Con recolección cada
     quince segundos, las transiciones de la serie dan igual las aperturas y el
     tiempo que estuvo abierta cada una;
   - `predictit_peticiones_limitadas_total`, lo que rechazó el limitador. Éste
     sí es un contador, porque el limitador tiene un punto donde avisar.

   **Lo que no se instrumenta, y por qué.** Cuántas consultas resolvió el
   proveedor real y cuántas cayeron al respaldo ya está contado, en la bitácora,
   con un código de evento por caso —`ASIGNACION_IA`, `ASIGNACION_HEURISTICA`,
   `ASIGNACION_RESPALDO`—. Duplicarlo como métrica sería un segundo contador que
   puede contradecir al registro de auditoría, que es la fuente. La pantalla de
   Integración de IA ya lo muestra desde ahí.

### Se usa una biblioteca para el formato y no se escribe a mano

`prometheus-net.AspNetCore`. Es deliberadamente lo contrario de lo que se
decidió con el disyuntor —que se escribió a mano «para que el patrón esté a la
vista»— y la diferencia es cuál de las dos cosas evalúa la materia. El circuit
breaker es un patrón de diseño y tiene que leerse en el código; un exportador de
métricas es plomería de formato, y la parte no trivial —la agregación de
histogramas para calcular percentiles— se hace mal con facilidad.

### El endpoint no es público

Las métricas dicen qué rutas existen, con qué frecuencia se usan y cuánto
tardan. Es material de reconocimiento para quien esté buscando por dónde entrar.

El endpoint se habilita sólo si hay un token configurado
(`Metricas:Token`) y exige ese token en la cabecera `X-Metricas-Token`. **Sin
token configurado, el endpoint no existe**: es seguro por omisión, y quien quiera
recolectar tiene que decidirlo explícitamente.

No se usa el JWT del sistema porque un recolector no es una persona: no tiene
sesión, no rota credenciales y pedirle que se autentique como usuario obligaría
a crear un usuario de servicio con permisos, que es peor.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| **Prometheus y Grafana en el `docker-compose`** | Dos contenedores más sobre los 2,3 GB que ya pesa el clon limpio, que según H-53 tiene que arrancar en la notebook de la defensa. Y un tablero de Grafana es un archivo JSON generado que hay que versionar y mantener al día, con el mismo problema que tenía el documento antes de automatizarlo |
| **Como perfil opcional del compose** (`--profile observabilidad`) | Es la opción intermedia razonable y queda como trabajo posterior. No se descarta por mala sino por alcance: primero el sistema tiene que ser observable, y recién ahí tiene sentido decidir con qué mirarlo |
| **Escribir el exportador a mano** | El formato de texto de Prometheus es trivial, pero la agregación de histogramas para percentiles no. Reimplementarla para ahorrar una dependencia madura es exactamente el tipo de decisión que el ADR 0004 rechazó en el otro sentido |
| **Dejarlo fuera de alcance y declararlo** | Es honesto y era una opción real. Se descartó porque el RNF-05 quedaría como el único requisito sin forma de comprobarse, y eso en la defensa es una pregunta de un renglón sin buena respuesta |

## Consecuencias

**A favor.** El RNF-05 pasa a ser verificable. Los patrones de resiliencia del
ADR 0009 dejan de ser sólo código probado y pasan a tener registro de cuándo
actuaron. Y la aplicación queda lista para cualquier stack de observabilidad sin
tocarla.

**En contra, y hay que decirlo.** Se suma una dependencia —`prometheus-net`, con
lo que implica para el control de componentes vulnerables del ADR 0008, que
ahora la incluye—. Y las métricas se pierden al reiniciar el contenedor: sin
recolector que las guarde, el tiempo en línea vuelve a cero. Eso es inherente a
la decisión de no incorporar el stack, no un descuido.

**Lo que esta decisión no resuelve.** Sin recolector no hay alertas ni
histórico. El sistema puede decir «el disyuntor está abierto ahora» pero no
«se abrió ocho veces el martes», salvo que alguien esté recolectando. Está
declarado acá para que no se lea como una promesa de más.
