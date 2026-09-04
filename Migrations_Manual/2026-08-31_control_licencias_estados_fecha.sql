-- ============================================================================
-- Control de Licencias — Estados de fecha (Dashboard "organizador visual")
-- Ejecutar manualmente en pgAdmin. No usar `dotnet ef migrations`.
--
-- En el Excel de referencia del comité, cada fecha (Inscripción/Inicio/Fin/
-- Renovación) puede no ser una fecha real: a veces dice "No se cuenta",
-- "Pendiente", "Indeterminado" o "No registrada". Se agrega una columna de
-- estado por cada fecha para guardar ese texto cuando no hay fecha real
-- (mutuamente excluyente con la fecha: si hay estado, la fecha queda NULL).
-- ============================================================================

BEGIN;

ALTER TABLE vecino_licencia_control
    ADD COLUMN fecha_inscripcion_estado  TEXT NULL,
    ADD COLUMN fecha_inicio_estado       TEXT NULL,
    ADD COLUMN fecha_vencimiento_estado  TEXT NULL,
    ADD COLUMN fecha_renovacion_estado   TEXT NULL;

COMMENT ON COLUMN vecino_licencia_control.fecha_inscripcion_estado IS 'Cuando no hay fecha real: NoSeCuenta / Pendiente / Indeterminado / NoRegistrada. NULL si hay fecha o si está en blanco.';
COMMENT ON COLUMN vecino_licencia_control.fecha_inicio_estado IS 'Idem fecha_inscripcion_estado, para fecha_inicio.';
COMMENT ON COLUMN vecino_licencia_control.fecha_vencimiento_estado IS 'Idem fecha_inscripcion_estado, para fecha_vencimiento (Fecha Fin).';
COMMENT ON COLUMN vecino_licencia_control.fecha_renovacion_estado IS 'Idem fecha_inscripcion_estado, para fecha_renovacion.';

COMMIT;
