-- ============================================================================
-- Control de Licencias — Visitas de Anexo H (municipalidad)
-- Ejecutar manualmente en pgAdmin. No usar `dotnet ef migrations`.
--
-- Diseño (mismo patrón que vecino_licencia_control_recordatorio):
--   - vecino_licencia_control_visita -> tabla hija de vecino_licencia_control
--       (1 fila por fecha de visita registrada en el Anexo H, tipo_id = 11).
--       fecha_recordatorio es fija: fecha_visita - 2 días (no configurable).
--
-- Destinatarios del recordatorio: NO se crea tabla nueva. Se usan las
-- columnas ya existentes en project:
--   - project.email_residente     -> Residente
--   - project.email_coord_admin   -> Administrador del proyecto
--
-- Auditoría: agregar esta tabla a TablasAuditar en
--   Shared/Interceptors/AuditoriaInterceptor.cs (cambio de código, no de SQL).
-- ============================================================================

BEGIN;

CREATE TABLE vecino_licencia_control_visita (
    vecino_licencia_control_visita_id SERIAL PRIMARY KEY,
    vecino_licencia_control_id        INT NOT NULL REFERENCES vecino_licencia_control(vecino_licencia_control_id),
    fecha_visita                       DATE NOT NULL,
    observacion                        TEXT NULL,
    fecha_recordatorio                 DATE NOT NULL,
    recordatorio_enviado_date_time     TIMESTAMPTZ NULL,
    created_date_time                  TIMESTAMPTZ NOT NULL,
    created_user_id                    INT NOT NULL,
    updated_date_time                  TIMESTAMPTZ NULL,
    updated_user_id                    INT NULL,
    active                              BOOLEAN NOT NULL DEFAULT TRUE,
    state                               BOOLEAN NOT NULL DEFAULT TRUE
);

COMMENT ON TABLE vecino_licencia_control_visita IS 'Fechas de visita de la municipalidad registradas en el Anexo H (tipo_id 11) de una licencia. Recordatorio fijo 2 días antes, enviado a project.email_residente y project.email_coord_admin.';

CREATE INDEX ix_vecino_licencia_control_visita_licencia
    ON vecino_licencia_control_visita (vecino_licencia_control_id);

COMMIT;
