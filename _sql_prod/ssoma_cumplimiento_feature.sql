-- ============================================================================
-- Módulo SSOMA — Cumplimiento mínimo del Coordinador SSOMA y el Prevencionista
-- Guía de actividades diarias/semanales/mensuales, con seguimiento por proyecto.
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

-- ── Feature + permisos ───────────────────────────────────────────────────────
INSERT INTO feature (feature_key, module_id)
SELECT 'ssoma.gestion.cumplimiento', m.module_id
FROM module m
WHERE m.module_name = 'SSOMA'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'ssoma.gestion.cumplimiento');

INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE f.feature_key = 'ssoma.gestion.cumplimiento'
  AND r.role_description IN ('USUARIO DE ABRIL', 'ADMINISTRADOR DEL SISTEMA')
  AND NOT EXISTS (
      SELECT 1 FROM role_feature rf WHERE rf.role_id = r.role_id AND rf.feature_id = f.feature_id
  );

-- ── Catálogo de actividades ──────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ss_cumplimiento_actividad (
    id              SERIAL PRIMARY KEY,
    nombre          VARCHAR(250) NOT NULL,
    descripcion     TEXT NULL,
    rol_responsable VARCHAR(30) NOT NULL DEFAULT 'ambos',
    frecuencia      VARCHAR(20) NOT NULL DEFAULT 'diaria',
    orden           INT NOT NULL DEFAULT 0,
    activo          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NULL
);

-- ── Registro de cumplimiento por proyecto y periodo ──────────────────────────
CREATE TABLE IF NOT EXISTS ss_cumplimiento_registro (
    id                  SERIAL PRIMARY KEY,
    actividad_id        INT NOT NULL REFERENCES ss_cumplimiento_actividad(id) ON DELETE CASCADE,
    proyecto_id         INT NOT NULL REFERENCES project(project_id),
    periodo             DATE NOT NULL,
    cumplido            BOOLEAN NOT NULL DEFAULT FALSE,
    fecha_cumplimiento  TIMESTAMPTZ NULL,
    cumplido_por_id     INT NULL,
    observacion         TEXT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NULL,
    UNIQUE (actividad_id, proyecto_id, periodo)
);

CREATE INDEX IF NOT EXISTS ix_ss_cumplimiento_registro_proyecto_id ON ss_cumplimiento_registro(proyecto_id);

-- ── Seed del catálogo mínimo (ajustar/ampliar según el estándar real de Abril) ─
INSERT INTO ss_cumplimiento_actividad (nombre, descripcion, rol_responsable, frecuencia, orden, activo, created_at, updated_at)
SELECT v.nombre, v.descripcion, v.rol, v.frecuencia, v.orden, true, NOW(), NOW()
FROM (VALUES
  -- Diarias
  ('Charla de 5 minutos / inducción de inicio de jornada', 'Verificar registro de asistencia firmado', 'ambos', 'diaria', 1),
  ('Revisión de ATS/PETS de las actividades del día', 'Validar que cada frente cuente con su análisis de trabajo seguro', 'prevencionista', 'diaria', 2),
  ('Recorrido de campo (inspección visual de condiciones y actos subestándar)', NULL, 'prevencionista', 'diaria', 3),
  ('Verificación de EPP y protecciones colectivas en los frentes activos', NULL, 'prevencionista', 'diaria', 4),
  ('Registro de incidentes/accidentes del día (si los hubiera)', NULL, 'coordinador_ssoma', 'diaria', 5),
  -- Semanales
  ('Inspección planeada de seguridad (OPT/checklist de partida)', 'Al menos una inspección formal documentada en la semana', 'prevencionista', 'semanal', 1),
  ('Reunión de comité de SST / coordinación semanal SSOMA', NULL, 'coordinador_ssoma', 'semanal', 2),
  ('Revisión de indicadores proactivos y reactivos de la semana', NULL, 'coordinador_ssoma', 'semanal', 3),
  ('Simulacro o capacitación semanal programada', 'Según cronograma de capacitaciones del proyecto', 'ambos', 'semanal', 4),
  -- Mensuales
  ('Informe mensual de gestión SSOMA a Gerencia', NULL, 'coordinador_ssoma', 'mensual', 1),
  ('Auditoría interna / autoevaluación del sistema de gestión', NULL, 'coordinador_ssoma', 'mensual', 2),
  ('Actualización de matriz IPERC del proyecto', NULL, 'prevencionista', 'mensual', 3),
  ('Verificación de vigencia de pólizas, certificados y SCTR del personal', NULL, 'coordinador_ssoma', 'mensual', 4),
  ('Revisión y actualización del plan de emergencia', NULL, 'coordinador_ssoma', 'mensual', 5)
) AS v(nombre, descripcion, rol, frecuencia, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_cumplimiento_actividad WHERE nombre = v.nombre);

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT frecuencia, rol_responsable, orden, nombre FROM ss_cumplimiento_actividad ORDER BY frecuencia, orden;
