-- ============================================================================
-- Gestión Administrativa · Salidas — Copia firmada del Consolidado del S10
-- Fecha: 2026-09-07
--
-- Aprobar el reembolso pasó a ser TAMBIÉN la firma del revisor: al aprobar se
-- estampa su firma en todas las hojas de los DOS documentos de la planilla —la
-- planilla de rendición y el Consolidado del S10— y la salida queda directamente
-- en "Firmado", que es lo que Tesorería ve como pagable. El paso "Firmar" que
-- existía aparte desapareció.
--
-- ga_rendicion ya tenía sus columnas pdf_firmado_*; este script le da las mismas
-- al consolidado, que hasta hoy solo guardaba el PDF original.
--
-- Todas nullable: los consolidados que ya están cargados no tienen copia firmada
-- y los que se suban antes de aprobar tampoco.
--
-- Idempotente: se puede correr varias veces. Aplicar en dev y prod.
-- ============================================================================

BEGIN;

ALTER TABLE ga_consolidado_s10
    ADD COLUMN IF NOT EXISTS pdf_firmado_url      text,
    ADD COLUMN IF NOT EXISTS pdf_firmado_item_id  text,
    ADD COLUMN IF NOT EXISTS pdf_firmado_filename text,
    ADD COLUMN IF NOT EXISTS firmado_por_id       integer,
    ADD COLUMN IF NOT EXISTS firmado_at           timestamp with time zone;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'ga_consolidado_s10'::regclass
          AND conname  = 'fk_ga_consolidado_s10_firmado_por'
    ) THEN
        ALTER TABLE ga_consolidado_s10
            ADD CONSTRAINT fk_ga_consolidado_s10_firmado_por
            FOREIGN KEY (firmado_por_id) REFERENCES app_user(user_id);
    END IF;
END $$;

COMMENT ON COLUMN ga_consolidado_s10.pdf_firmado_url IS
    'Copia del consolidado con la firma del revisor estampada en todas sus hojas. Se genera al '
    'aprobar el reembolso. NULL mientras no se haya aprobado.';

COMMIT;

-- ============================================================================
-- SOLO SI HACE FALTA — salidas que quedaron en "Aprobado" con el flujo viejo
--
-- Antes de este cambio, aprobar el reembolso dejaba la salida en "Aprobado" y la
-- firma era un paso posterior. Ese paso ya no existe, así que las salidas que
-- quedaron a medias (Aprobado, id 2) no tienen cómo llegar a "Firmado" y
-- Tesorería no las va a ver.
--
-- Primero MIRAR cuántas son:
--
--   SELECT s.id, s.codigo, s.rendicion_id, r.pdf_firmado_url
--   FROM ga_solicitud_salida s
--   JOIN ga_rendicion r ON r.id = s.rendicion_id
--   WHERE s.estado_reembolso_id = 2;
--
-- Y recién decidir con esa lista a la vista:
--
--   • Si su planilla NO tiene pdf_firmado_url (el caso normal: se aprobó pero
--     nunca se llegó a firmar), lo correcto es devolverlas a "Pendiente" para
--     que el revisor las vuelva a aprobar y así se firmen los dos documentos:
--
--       UPDATE ga_solicitud_salida
--       SET    estado_reembolso_id = 1,
--              reembolso_decidido_por_id = NULL,
--              reembolso_decidido_at = NULL,
--              updated_at = now()
--       WHERE  estado_reembolso_id = 2;
--
--   • Si su planilla YA tiene pdf_firmado_url, quedaron entre la aprobación y
--     la firma: ahí conviene pasarlas a "Firmado" (id 4) a mano en vez de
--     rehacer la firma. Esas hay que mirarlas una por una.
--
-- Esto NO se corre con el resto del script: es data, depende de lo que muestre
-- la consulta y es una decisión del negocio. En dev ya se aplicó el primer caso
-- (era una sola salida, SOL-2026-0349, con la planilla sin firmar).
-- ============================================================================
