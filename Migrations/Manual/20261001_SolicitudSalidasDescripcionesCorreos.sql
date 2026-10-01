-- ============================================================================
-- Solicitud de Salidas → Configuración: descripciones de una línea
-- ============================================================================
-- Las descripciones de sus cinco correos y recordatorios explicaban el flujo («Es informativo:
-- lleva el detalle de la solicitud pero no los botones…»). Quedan en una línea: a quién y cuándo.
-- Ahora, además, debajo del nombre de cada uno se ve su asunto, que sale del código (no de esta
-- tabla).
--
-- Solo cambia texto de ga_correo_evento.descripcion. CUANDO SEA: el código de hoy y el nuevo leen la
-- misma columna. Re-ejecutable. Aplicar en dev, demo y prod.
-- ============================================================================

-- El archivo está en UTF-8 y trae texto acentuado: psql en Windows arranca en WIN1252.
SET client_encoding TO 'UTF8';

BEGIN;

UPDATE ga_correo_evento e
SET descripcion = v.descripcion,
    updated_at  = now()
FROM (VALUES
    ('REVISOR',
     'Al revisor que aprueba o rechaza la solicitud, con los adjuntos.'),
    ('REVISOR_JEFE_AREA',
     'Al jefe del área, cuando quien aprueba es el residente de la obra. Solo informa.'),
    ('CONFIRMACION',
     'Al solicitante: su solicitud está en revisión.'),
    ('RECORDATORIO_RENDICION_APERTURA',
     'El primer día hábil del mes, a quien tiene salidas del mes anterior sin rendir.'),
    ('RECORDATORIO_RENDICION_CIERRE',
     'El último día del plazo, a quien todavía tiene salidas del mes anterior sin rendir.')
) AS v (codigo, descripcion)
WHERE e.codigo = v.codigo
  AND e.state
  AND e.descripcion IS DISTINCT FROM v.descripcion;

COMMIT;

-- Verificación: las cinco con su texto nuevo.
SELECT e.codigo, e.descripcion
FROM ga_correo_evento e
WHERE e.state
  AND e.codigo IN ('REVISOR', 'REVISOR_JEFE_AREA', 'CONFIRMACION',
                   'RECORDATORIO_RENDICION_APERTURA', 'RECORDATORIO_RENDICION_CIERRE')
ORDER BY e.grupo_id, e.orden;
