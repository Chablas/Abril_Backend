-- ═══════════════════════════════════════════════════════════════════════════════
-- Reclutamiento: referencia del lugar de la entrevista + estado "Finalizado"
--
-- 1) gth_lugar_entrevista.referencia: la línea que ayuda a ubicar el lugar. Es un
--    dato DEL lugar, igual que maps_url — una sala de venta o una obra tienen su
--    propia referencia, así que no puede ser una constante del backend. El correo
--    de invitación y la página de confirmación del postulante la muestran bajo la
--    dirección; null = ese lugar no la tiene cargada y no se muestra esa línea.
--
-- 2) La oficina principal pasa a decir el distrito ("…, Lince") y trae su
--    referencia. Lo pidió GTH: la dirección sola no alcanzaba para llegar.
--
-- 3) El último estado del requerimiento se llama "Finalizado" en pantalla. El
--    CÓDIGO sigue siendo CERRADO: es la clave estable con la que lo buscan
--    EstadoReclutamiento.Cerrado y OnboardingRepository. No renombrar el código.
--
-- Idempotente: se puede correr dos veces sin efecto adicional.
-- ═══════════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. La columna ────────────────────────────────────────────────────────────
ALTER TABLE gth_lugar_entrevista
    ADD COLUMN IF NOT EXISTS referencia text;

COMMENT ON COLUMN gth_lugar_entrevista.referencia IS
    'Referencia para ubicar el lugar (se muestra bajo la dirección en el correo de invitación y en la página de confirmación del postulante). Null si el lugar no la tiene cargada.';

-- ── 2. La oficina principal ──────────────────────────────────────────────────
UPDATE gth_lugar_entrevista
   SET nombre     = 'Calle Mama Ocllo 2647, Lince',
       referencia = 'A la altura de la cuadra 11 de la avenida 2 de Mayo',
       updated_date_time = now()
 WHERE nombre = 'Calle Mama Ocllo 2647'
   AND state;

-- Si el nombre ya se había corregido a mano, solo carga la referencia.
UPDATE gth_lugar_entrevista
   SET referencia = 'A la altura de la cuadra 11 de la avenida 2 de Mayo',
       updated_date_time = now()
 WHERE nombre = 'Calle Mama Ocllo 2647, Lince'
   AND referencia IS NULL
   AND state;

-- ── 3. Cerrado → Finalizado (solo el nombre visible) ─────────────────────────
UPDATE gth_estado_requerimiento
   SET nombre = 'Finalizado',
       updated_date_time = now()
 WHERE codigo = 'CERRADO'
   AND state
   AND nombre <> 'Finalizado';

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT gth_lugar_entrevista_id, nombre, referencia, maps_url
  FROM gth_lugar_entrevista
 WHERE state
 ORDER BY orden;

SELECT gth_estado_requerimiento_id, codigo, nombre
  FROM gth_estado_requerimiento
 WHERE codigo LIKE 'CERRADO%'
 ORDER BY codigo;
