-- ═══════════════════════════════════════════════════════════════════════════════
-- Correos de EMO: una versión para trabajadores y otra para postulantes
--
-- Cada correo de EMO que puede hablarle a las dos audiencias pasa a existir dos
-- veces en ss_emo_correo_evento: la versión del trabajador (el código de siempre)
-- y la del postulante (sufijo _POSTULANTE). Así cada una es su propia sección en
-- /ssoma/salud-ocupacional/emos/configuracion, con sus propios destinatarios, y
-- el texto de cada correo llama a la persona por lo que es.
--
-- PROGRAMACION_AUTOMATICA no tiene versión de postulante y no puede tenerla: el
-- cron programa por vencimiento de un EMO anterior y exige vinculación vigente,
-- así que solo alcanza a trabajadores que ya están adentro.
--
-- Idempotente: se puede correr dos veces sin duplicar nada.
-- Todo en una transacción: si algo falla, la matriz no queda a medias.
-- ═══════════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. Los correos que ya existían ahora dicen a qué audiencia le hablan ──────
UPDATE ss_emo_correo_evento SET
    nombre = 'Programación automática · Trabajador',
    descripcion = 'Resumen que sale del cron diario cuando el sistema programa EMOs por vencimiento. Solo existe para trabajadores: el cron programa por vencimiento de un EMO anterior, que un postulante no tiene. La clínica todavía tiene que aceptar o rechazar.',
    orden = 1,
    updated_at = now()
WHERE codigo = 'PROGRAMACION_AUTOMATICA' AND state;

UPDATE ss_emo_correo_evento SET
    nombre = 'Programación manual · Trabajador',
    descripcion = 'Sale al programar a mano el EMO de un trabajador de Abril desde EMOs o Programaciones. La clínica todavía tiene que aceptar o rechazar.',
    orden = 2,
    updated_at = now()
WHERE codigo = 'PROGRAMACION_MANUAL' AND state;

UPDATE ss_emo_correo_evento SET
    nombre = 'Aceptada por la clínica · Trabajador',
    descripcion = 'Confirmación de la cita de un trabajador: fecha, hora, clínica y dirección. Lo dispara la clínica desde su agenda.',
    orden = 4,
    updated_at = now()
WHERE codigo = 'ACEPTADA' AND state;

UPDATE ss_emo_correo_evento SET
    nombre = 'Rechazada por la clínica · Trabajador',
    descripcion = 'Aviso de que la clínica rechazó la cita de un trabajador, con el motivo. Hay que coordinar una nueva fecha.',
    orden = 6,
    updated_at = now()
WHERE codigo = 'RECHAZADA' AND state;

UPDATE ss_emo_correo_evento SET
    nombre = 'Resultado del EMO · Trabajador',
    descripcion = 'Sale cuando se registra el resultado del examen de un trabajador. Solo con un veredicto cerrado: Apto, Apto con Restricciones o No Apto — un EMO Observado no avisa a nadie porque todavía falta la interconsulta.',
    orden = 8,
    updated_at = now()
WHERE codigo = 'RESULTADO' AND state;

-- ── 2. Las versiones del postulante ──────────────────────────────────────────
-- Del EMO de Ingreso que GTH le programa al finalista aprobado desde Reclutamiento,
-- cuando su ficha todavía es de pre-ingreso.
INSERT INTO ss_emo_correo_evento (codigo, nombre, descripcion, orden, active, state, created_at, updated_at)
SELECT v.codigo, v.nombre, v.descripcion, v.orden, true, true, now(), now()
FROM (VALUES
    ('PROGRAMACION_MANUAL_POSTULANTE',
     'Programación manual · Postulante',
     'Sale al programarle el EMO de Ingreso al finalista aprobado de un requerimiento, cuando su ficha todavía es de pre-ingreso. La clínica todavía tiene que aceptar o rechazar.',
     3),
    ('ACEPTADA_POSTULANTE',
     'Aceptada por la clínica · Postulante',
     'Confirmación de la cita de un postulante: fecha, hora, clínica y dirección. Lo dispara la clínica desde su agenda.',
     5),
    ('RECHAZADA_POSTULANTE',
     'Rechazada por la clínica · Postulante',
     'Aviso de que la clínica rechazó la cita de un postulante, con el motivo. Hay que coordinar una nueva fecha.',
     7),
    ('RESULTADO_POSTULANTE',
     'Resultado del EMO · Postulante',
     'Sale cuando se registra el resultado del EMO de Ingreso de un postulante. Solo con un veredicto cerrado: Apto, Apto con Restricciones o No Apto — un EMO Observado no avisa a nadie porque todavía falta la interconsulta.',
     9)
) AS v(codigo, nombre, descripcion, orden)
WHERE NOT EXISTS (
    SELECT 1 FROM ss_emo_correo_evento e
     WHERE upper(e.codigo) = upper(v.codigo) AND e.state
);

-- ── 3. La matriz de cada versión nueva ───────────────────────────────────────
-- Se copia celda por celda de su versión de trabajador para no obligar a GTH a
-- reconfigurar de cero, con dos correcciones obligadas por el negocio:
--
--   • JEFE queda APAGADO: un postulante todavía no es trabajador de nadie, así que
--     ese destinatario no resuelve a ningún correo (ver EmoDestinatariosResolver).
--   • SOLICITANTE queda PRENDIDO si en la versión del trabajador estaba prendido
--     JEFE o SOLICITANTE: es su equivalente exacto — quien pidió la vacante y va a
--     ser su jefe. Así el aviso le sigue llegando a la misma persona que hoy.
--
-- El resto de destinatarios (clínica, medicina ocupacional, GTH, los correos
-- adicionales, etc.) mantiene el interruptor que ya tenía.
INSERT INTO ss_emo_correo_regla (evento_id, perfil_id, destinatario_id, active, state, created_at, updated_at)
SELECT
    nuevo.id,
    r.perfil_id,
    r.destinatario_id,
    CASE d.codigo
        WHEN 'JEFE'        THEN false
        WHEN 'SOLICITANTE' THEN (r.active OR coalesce(rj.active, false))
        ELSE r.active
    END,
    true, now(), now()
FROM ss_emo_correo_regla r
JOIN ss_emo_correo_destinatario d ON d.id = r.destinatario_id AND d.state
JOIN ss_emo_correo_evento base    ON base.id = r.evento_id AND base.state
JOIN ss_emo_correo_evento nuevo
     ON upper(nuevo.codigo) = upper(base.codigo) || '_POSTULANTE' AND nuevo.state
-- La celda de JEFE del mismo correo y perfil, para heredarla en SOLICITANTE.
LEFT JOIN ss_emo_correo_destinatario dj ON dj.codigo = 'JEFE' AND dj.state
LEFT JOIN ss_emo_correo_regla rj
     ON rj.evento_id = r.evento_id
    AND rj.perfil_id = r.perfil_id
    AND rj.destinatario_id = dj.id
    AND rj.state
WHERE r.state
  AND NOT EXISTS (
    SELECT 1 FROM ss_emo_correo_regla x
     WHERE x.evento_id = nuevo.id
       AND x.perfil_id = r.perfil_id
       AND x.destinatario_id = r.destinatario_id
       AND x.state
  );

-- ── 4. En la versión del trabajador, SOLICITANTE ya no tiene sentido ─────────
-- El resolver solo lo usa para fichas de pre-ingreso, así que en la sección del
-- trabajador nunca resolvía a nadie: dejarlo prendido solo confunde a quien lee
-- la matriz. Se apaga DESPUÉS del paso 3, que es el que lo hereda.
UPDATE ss_emo_correo_regla r SET active = false, updated_at = now()
FROM ss_emo_correo_destinatario d, ss_emo_correo_evento e
WHERE d.id = r.destinatario_id
  AND e.id = r.evento_id
  AND d.codigo = 'SOLICITANTE'
  AND e.codigo IN ('PROGRAMACION_AUTOMATICA', 'PROGRAMACION_MANUAL', 'ACEPTADA', 'RECHAZADA', 'RESULTADO')
  AND r.state
  AND r.active;

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT e.orden, e.codigo, e.nombre,
       count(*) FILTER (WHERE r.active) AS celdas_activas,
       count(*)                         AS celdas
  FROM ss_emo_correo_evento e
  LEFT JOIN ss_emo_correo_regla r ON r.evento_id = e.id AND r.state
 WHERE e.state AND e.active
 GROUP BY e.orden, e.codigo, e.nombre
 ORDER BY e.orden;
