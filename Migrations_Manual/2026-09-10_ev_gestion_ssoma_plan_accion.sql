-- ============================================================================
-- Plan de acción de Coordinador SSOMA / Prevencionista sobre sus resultados de
-- la evaluación de Gestión SSOMA (ver ev_evaluacion_gestion_ssoma /
-- ev_evaluacion_gestion_ssoma_detalle).
--
-- A diferencia de ev_jefe_ssoma_plan_accion (un único puesto Jefe SSOMA), aquí
-- hay muchos dueños posibles — cada fila pertenece a quien la creó
-- (created_by_user_id) y toda consulta debe filtrar por ese usuario, nunca solo
-- por período, para que un coordinador/prevencionista no vea el plan de otro.
--
-- Idempotente: usa IF NOT EXISTS, se puede re-correr.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS ev_gestion_ssoma_plan_accion (
    id                  serial PRIMARY KEY,
    periodo_id          int NOT NULL REFERENCES ev_periodo(id),
    criterio            varchar(300) NOT NULL,
    accion              text NOT NULL,
    meta                text NOT NULL,
    fecha_limite        date,
    estado              varchar(20) NOT NULL DEFAULT 'Pendiente',
    created_by_user_id  int NOT NULL REFERENCES app_user(user_id),
    created_at          timestamp NOT NULL DEFAULT now(),
    updated_at          timestamp
);

CREATE INDEX IF NOT EXISTS ix_ev_gestion_ssoma_plan_accion_periodo_usuario
    ON ev_gestion_ssoma_plan_accion (periodo_id, created_by_user_id);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT table_name FROM information_schema.tables WHERE table_name = 'ev_gestion_ssoma_plan_accion';
-- Esperado: 1 fila.
