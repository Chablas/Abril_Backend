-- ============================================================================
-- 2026-08-31 · Correo "Resultado de EMO" en la Configuración de EMOs
--
-- Agrega una quinta sección a /ssoma/salud-ocupacional/emos/configuracion:
-- el correo que avisa el resultado del examen cuando la clínica lo registra
-- desde Clínica → Agenda (y cuando lo registra SSOMA desde EMOs).
--
-- Sale SOLO con un veredicto cerrado: Apto, Apto con Restricciones o No Apto.
-- "Observado" no envía nada — esa aptitud significa que falta la interconsulta
-- y todavía no hay resultado que comunicar. Ese corte vive en el código
-- (EmoResultadoNotificacionService), no acá: no es algo que se configure.
--
-- Además agrega un destinatario dinámico nuevo:
--   JEFE_SOLICITANTE = el jefe del trabajador mientras ya esté en Abril, y el
--   SOLICITANTE de la vacante cuando la ficha todavía es de pre-ingreso
--   (workers.workers_estado_id in (4,5) — ver WorkersEstadoIds.PreIngreso). A un
--   postulante que viene de Solicitud de Personal el resultado de su EMO le
--   importa a quien pidió la vacante, no al revisor de un área a la que todavía
--   no entró.
--   Es un destinatario aparte de JEFE a propósito: los otros 4 correos de EMO no
--   tienen por qué cambiar de destinatario por esto.
--
-- Destinatarios que quedan ACTIVOS por defecto en los 3 perfiles:
--   MEDICINA_OCUPACIONAL (el médico), GTH y JEFE_SOLICITANTE.
-- El resto de las celdas nacen apagadas y se prenden desde la pantalla.
--
-- Orden respecto del deploy: da igual. Si esto corre antes, la sección nueva
-- aparece en la pantalla y todavía no la dispara nadie; si corre después, el
-- envío no encuentra reglas, deja un warning en el log y no manda nada. En
-- ningún caso rompe algo que ya funcionaba.
--
-- Idempotente: se puede correr más de una vez sin duplicar nada.
-- ============================================================================

BEGIN;

-- ── 1) El correo nuevo (evento) ─────────────────────────────────────────────
INSERT INTO ss_emo_correo_evento (codigo, nombre, descripcion, orden)
SELECT v.codigo, v.nombre, v.descripcion, v.orden
FROM (VALUES
    ('RESULTADO', 'Resultado del EMO',
     'Sale cuando se registra el resultado del examen. Solo con un veredicto cerrado: Apto, Apto con Restricciones o No Apto — un EMO Observado no avisa a nadie porque todavía falta la interconsulta.', 5)
) AS v(codigo, nombre, descripcion, orden)
WHERE NOT EXISTS (
    SELECT 1 FROM ss_emo_correo_evento e WHERE e.state AND upper(e.codigo) = v.codigo
);

-- ── 2) El destinatario nuevo ────────────────────────────────────────────────
-- Dinámico (codigo NOT NULL + email NULL): su correo se resuelve al enviar, en
-- EmoDestinatariosResolver. Va detrás de JEFE en el orden de la pantalla.
INSERT INTO ss_emo_correo_destinatario
    (tipo_id, codigo, email, nombre, descripcion, editable, orden, active, state)
SELECT t.id, v.codigo, NULL, v.nombre, v.descripcion, false, v.orden, true, true
FROM (VALUES
    ('JEFE_SOLICITANTE', 'Jefe del trabajador o solicitante de la vacante',
     'Se resuelve al enviar: el jefe del trabajador si ya está en Abril; si la ficha todavía es de pre-ingreso, quien pidió la vacante en Solicitud de Personal.', 2)
) AS v(codigo, nombre, descripcion, orden)
CROSS JOIN LATERAL (
    SELECT id FROM ss_emo_correo_tipo WHERE state AND upper(codigo) = 'PRINCIPAL' LIMIT 1
) t
WHERE NOT EXISTS (
    SELECT 1 FROM ss_emo_correo_destinatario d WHERE d.state AND upper(d.codigo) = v.codigo
);

-- JEFE y JEFE_SOLICITANTE comparten el orden 2 (van juntos en la pantalla) y el
-- desempate lo hace el id, que es mayor en el nuevo. Se deja explícito para que
-- se lea como una decisión y no como un descuido.

-- ── 3) Las celdas de la matriz ──────────────────────────────────────────────
-- Misma forma que el seed original: una celda por cada combinación
-- evento × perfil × destinatario de catálogo que todavía no exista. Las que ya
-- existen NO se tocan — lo que el usuario haya configurado en la pantalla manda.
--
-- Esto crea dos grupos de filas nuevas:
--   • RESULTADO × 3 perfiles × todos los destinatarios de catálogo.
--   • JEFE_SOLICITANTE × los 4 correos viejos × 3 perfiles, todas apagadas
--     (ese destinatario es solo para el correo de resultado, pero la pantalla
--     necesita la celda para poder prenderlo si algún día se quisiera).
WITH activos(evento, perfil, dest) AS (
    SELECT 'RESULTADO', p.codigo, d.codigo
    FROM       (VALUES ('OFICINA_CENTRAL'), ('STAFF'), ('OBRA'))                  p(codigo)
    CROSS JOIN (VALUES ('MEDICINA_OCUPACIONAL'), ('GTH'), ('JEFE_SOLICITANTE'))   d(codigo)
)
INSERT INTO ss_emo_correo_regla (evento_id, perfil_id, destinatario_id, active, state)
SELECT e.id, p.id, d.id,
       EXISTS (SELECT 1 FROM activos a
               WHERE a.evento = upper(e.codigo)
                 AND a.perfil = upper(p.codigo)
                 AND a.dest   = upper(d.codigo)),
       true
FROM ss_emo_correo_evento e
CROSS JOIN ss_emo_correo_perfil p
CROSS JOIN ss_emo_correo_destinatario d
WHERE e.state AND p.state AND d.state AND d.codigo IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM ss_emo_correo_regla r
      WHERE r.state AND r.evento_id = e.id AND r.perfil_id = p.id AND r.destinatario_id = d.id
  );

-- Los correos ADICIONALES (codigo IS NULL) que alguien haya agregado a mano no
-- entran en el INSERT de arriba (igual que en el seed original), así que se les
-- crea su celda del correo nuevo aparte, apagada: si no, esas filas no
-- aparecerían en la sección "Resultado del EMO" de la pantalla.
INSERT INTO ss_emo_correo_regla (evento_id, perfil_id, destinatario_id, active, state)
SELECT e.id, p.id, d.id, false, true
FROM ss_emo_correo_evento e
CROSS JOIN ss_emo_correo_perfil p
CROSS JOIN ss_emo_correo_destinatario d
WHERE e.state AND upper(e.codigo) = 'RESULTADO'
  AND p.state
  AND d.state AND d.codigo IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM ss_emo_correo_regla r
      WHERE r.state AND r.evento_id = e.id AND r.perfil_id = p.id AND r.destinatario_id = d.id
  );

COMMIT;

-- ============================================================================
-- Verificación: la sección nueva con sus 3 destinatarios prendidos.
--
-- SELECT d.nombre AS destinatario,
--        max(CASE WHEN p.codigo = 'OFICINA_CENTRAL' THEN r.active::int END) AS of_central,
--        max(CASE WHEN p.codigo = 'STAFF'           THEN r.active::int END) AS staff,
--        max(CASE WHEN p.codigo = 'OBRA'            THEN r.active::int END) AS obra
-- FROM ss_emo_correo_regla r
-- JOIN ss_emo_correo_evento e       ON e.id = r.evento_id
-- JOIN ss_emo_correo_perfil p       ON p.id = r.perfil_id
-- JOIN ss_emo_correo_destinatario d ON d.id = r.destinatario_id
-- WHERE r.state AND upper(e.codigo) = 'RESULTADO'
-- GROUP BY d.orden, d.id, d.nombre
-- ORDER BY d.orden, d.id;
-- ============================================================================
