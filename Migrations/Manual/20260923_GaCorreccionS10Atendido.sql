-- ============================================================================
-- Gestión Administrativa — Correcciones S10: el estado 2 se llama «Atendido»
--
-- Qué cambia y por qué
--
-- 1) ga_estado_correccion_s10.id = 2 pasa de «Pendiente de recarga S10» a
--    «Atendido». Quien lo lee es el Coordinador ERP en su bandeja, y para él el
--    pedido ya terminó; la recarga que falta es del consolidador, y Consolidados
--    la sigue rotulando aparte («Por recargar»). El id NO cambia (lo usa
--    EstadosSalida.CorreccionS10.Atendida) y no hay data que migrar.
--
-- 2) La descripción del correo CORRECCION_S10_SOLICITADA (la que se ve en
--    Consolidados → Configuración → Correos) nombra el código del consolidado:
--    desde este cambio los dos correos de la corrección hablan del consolidado
--    (CONS-… y N.º de reembolso) y ya no de las rendiciones que cubre.
--
-- Este script NO toca el esquema: solo textos de catálogo.
--
-- ORDEN DE EJECUCIÓN
--   • Da igual antes o después de desplegar. El backend no lee el nombre del
--     catálogo (sale de las constantes de EstadosSalida) y la descripción del
--     correo es solo el texto de Configuración.
--
-- Re-ejecutable: los UPDATE dejan los mismos valores.
-- ============================================================================

-- El archivo está en UTF-8 y trae texto acentuado. En Windows psql arranca con
-- el client_encoding del locale (WIN1252) y esos bytes se rechazan, así que se
-- fija acá: el script queda correcto sin depender de cómo se invoque.
SET client_encoding TO 'UTF8';

BEGIN;

-- ── 0) Prerrequisitos ───────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM ga_estado_correccion_s10 WHERE id = 2)
    THEN
        RAISE EXCEPTION 'Falta el estado 2 en ga_estado_correccion_s10.';
    END IF;
END $$;

-- ── 1) El estado 2 se llama «Atendido» ──────────────────────────────────────
UPDATE ga_estado_correccion_s10
SET descripcion = 'Atendido'
WHERE id = 2;

COMMENT ON TABLE ga_estado_correccion_s10 IS
  'Estados de una solicitud de correccion del Consolidado del S10 al Coordinador ERP: Pendiente de correccion S10 -> Atendido (falta que el consolidador recargue el consolidado).';

-- ── 2) El correo al ERP nombra el consolidado ───────────────────────────────
UPDATE ga_correo_evento
SET descripcion = 'Lo dispara el consolidador desde Consolidados cuando un Consolidado del S10 quedó observado. Le avisa al Coordinador ERP que hay una corrección esperándolo, con el código del consolidado, el número de reembolso, la observación y el motivo del consolidador.',
    updated_at  = now()
WHERE codigo = 'CORRECCION_S10_SOLICITADA' AND state;

COMMIT;

-- Verificación
-- SELECT id, descripcion FROM ga_estado_correccion_s10 ORDER BY id;
-- SELECT codigo, descripcion FROM ga_correo_evento WHERE codigo LIKE 'CORRECCION_S10%' AND state;
