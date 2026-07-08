-- ============================================================================
-- Cronograma de Actividades — Preferencia de última pestaña por usuario/proyecto
-- Ejecutar en PRODUCCIÓN (Aiven). Idempotente.
-- ============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS user_cronograma_preference (
  user_id integer NOT NULL,
  project_id integer NOT NULL,
  tipo_cronograma varchar(30) NOT NULL,
  updated_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id, project_id)
);

COMMIT;
