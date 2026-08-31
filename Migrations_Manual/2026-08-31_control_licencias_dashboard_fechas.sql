-- ============================================================================
-- Control de Licencias — Dashboard gerencial (fechas ampliadas + mes activo)
-- Ejecutar manualmente en pgAdmin. No usar `dotnet ef migrations`.
--
-- Agrega a vecino_licencia_control los campos que pide el "Organizador visual"
-- de comité: Fecha Inscripción, Fecha Inicio y Fecha Renovación (Fecha Fin ya
-- existe como fecha_vencimiento) + un flag manual "Mes Activo" (SI/NO, no se
-- calcula: lo marca quien administra la licencia).
-- ============================================================================

BEGIN;

ALTER TABLE vecino_licencia_control
    ADD COLUMN fecha_inscripcion DATE NULL,
    ADD COLUMN fecha_inicio      DATE NULL,
    ADD COLUMN fecha_renovacion  DATE NULL,
    ADD COLUMN mes_activo        BOOLEAN NOT NULL DEFAULT TRUE;

COMMENT ON COLUMN vecino_licencia_control.fecha_inscripcion IS 'Fecha en que se inscribió/tramitó el documento (dato informativo del dashboard).';
COMMENT ON COLUMN vecino_licencia_control.fecha_inicio IS 'Fecha de inicio de vigencia del documento.';
COMMENT ON COLUMN vecino_licencia_control.fecha_renovacion IS 'Fecha de renovación, si el documento es renovable (distinto de fecha_vencimiento).';
COMMENT ON COLUMN vecino_licencia_control.mes_activo IS 'SI/NO manual: si el documento está vigente/activo este mes, para el dashboard gerencial.';

COMMIT;
