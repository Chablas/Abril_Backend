-- ═══════════════════════════════════════════════════════════════════════════
-- "Otro motivo" pasa a ser una fila del catálogo de motivos
-- ═══════════════════════════════════════════════════════════════════════════
-- Hasta ahora el motivo escrito a mano por el trabajador vivía fuera de toda
-- configuración: el trayecto guardaba el texto en ga_solicitud_trayecto.motivo_libre
-- y dejaba motivo_id en NULL, así que esa vía no podía pedir adjunto, declararse
-- reembolsable ni describir una ausencia de día completo.
--
-- Ahora esa vía la configura una fila real del catálogo, marcada con
-- es_motivo_libre. No se ofrece en el desplegable (el front la filtra por esa
-- misma marca) pero se administra desde Configuración → Motivos como cualquier
-- otra. Los trayectos que la usan apuntan a ella con motivo_id y siguen guardando
-- lo escrito en motivo_libre: ese texto es el que se muestra en todas las
-- pantallas, nunca la descripción de la fila.
--
-- ⚠ PRE-DEPLOY. Sin esto el código nuevo se cae con 42703 (column
-- g0.es_motivo_libre does not exist) apenas se entra a Solicitud de Salidas.
-- Al revés no pasa nada: la columna y la fila conviven sin problema con el
-- código viejo, que simplemente no las nombra.
--
-- Los trayectos históricos SÍ se migran (paso 4): los que guardaban texto libre
-- con motivo_id NULL pasan a apuntar a la fila nueva, para que la vía quede
-- entera bajo una sola configuración y no queden dos clases de texto libre.
-- No cambia lo que se muestra ni lo que se rinde: el motivo se sigue leyendo del
-- texto (`m == null || m.es_motivo_libre → motivo_libre`) y ReembolsoTrayectoRule
-- filtra por `== true`, así que ni el NULL de antes ni el false de la fila nueva
-- entran en la rendición. Lo único que cambia es el pill del detalle: donde antes
-- se omitía el dato («no hay nada que afirmar»), ahora dice «no reembolsable».
--
-- Re-corrible: IF NOT EXISTS, INSERT condicionado y un UPDATE que ya no encuentra filas.

-- 1) La marca en el catálogo.
ALTER TABLE ga_motivo_salida
    ADD COLUMN IF NOT EXISTS es_motivo_libre boolean NOT NULL DEFAULT false;

-- 2) La vía "Otro motivo" es única: a lo más una fila puede tener la marca.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_motivo_salida_es_motivo_libre
    ON ga_motivo_salida (es_motivo_libre)
    WHERE es_motivo_libre;

-- 3) La fila que configura esa vía. Nace acá, no desde la pantalla de motivos.
--    activo = true a propósito: el front necesita recibirla para leer su
--    configuración (y es él quien la esconde del desplegable por es_motivo_libre).
--    requiere_motivo_adicional se queda en false siempre: el texto libre YA es el detalle.
INSERT INTO ga_motivo_salida (
    descripcion, activo, requiere_adjunto, es_hora_estimada,
    requiere_motivo_adicional, pide_horas_lugares, es_reembolsable,
    es_motivo_libre, created_at
)
SELECT 'Otro motivo', true, false, false, false, true, false, true, now()
WHERE NOT EXISTS (SELECT 1 FROM ga_motivo_salida WHERE es_motivo_libre);

-- 4) Los trayectos que ya venían con texto libre pasan a colgar de esa fila.
UPDATE ga_solicitud_trayecto t
   SET motivo_id = (SELECT id FROM ga_motivo_salida WHERE es_motivo_libre)
 WHERE t.motivo_id IS NULL
   AND coalesce(trim(t.motivo_libre), '') <> '';
