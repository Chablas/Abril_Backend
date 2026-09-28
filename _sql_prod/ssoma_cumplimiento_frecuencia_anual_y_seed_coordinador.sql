-- ============================================================================
-- Módulo SSOMA — Cumplimiento: agrega la frecuencia "anual" (columna frecuencia
-- ya es VARCHAR(20) sin CHECK, no requiere ALTER) y siembra el catálogo
-- detallado de Coordinador SSOMA, agrupado por categoría (mismo patrón que el
-- reseed de Prevencionista). Ejecutar en pgAdmin. Idempotente: cada INSERT
-- valida por nombre antes de insertar, no borra nada existente.
-- ============================================================================
BEGIN;

INSERT INTO ss_cumplimiento_actividad
    (nombre, descripcion, categoria, rol_responsable, frecuencia, orden, activo, created_at, updated_at)
SELECT v.nombre, v.descripcion, v.categoria, 'coordinador_ssoma', v.frecuencia, v.orden, true, NOW(), NOW()
FROM (VALUES
  -- ── Diaria: Revisión y liberación de trabajos ──
  ('Revisión de entregables (7:30 am, 2:00 pm y 4:30 pm)', NULL, 'Revisión y liberación de trabajos', 'diaria', 1),
  ('Liberación de trabajos críticos', NULL, 'Revisión y liberación de trabajos', 'diaria', 2),
  ('Cumplimiento de actividades planificadas en el PASST', NULL, 'Revisión y liberación de trabajos', 'diaria', 3),

  -- ── Diaria: Recorridos y verificación en campo ──
  ('Recorrido diario a toda la obra', NULL, 'Recorridos y verificación en campo', 'diaria', 1),
  ('Verificación de equipos y máquinas', NULL, 'Recorridos y verificación en campo', 'diaria', 2),
  ('Verificación de andamios colgantes y sillas colgantes', NULL, 'Recorridos y verificación en campo', 'diaria', 3),
  ('Verificación diaria de las protecciones colectivas de vecinos', NULL, 'Recorridos y verificación en campo', 'diaria', 4),

  -- ── Diaria: Gestión de contratistas ──
  ('Entrevistas a supervisores de contratistas', NULL, 'Gestión de contratistas', 'diaria', 1),
  ('Firma de herramientas de gestión de contratistas', NULL, 'Gestión de contratistas', 'diaria', 2),
  ('Coordinación de actividades de contratistas, protecciones colectivas y trabajos críticos', NULL, 'Gestión de contratistas', 'diaria', 3),

  -- ── Diaria: Reuniones diarias ──
  ('Reunión de planificación diaria con el área de producción', NULL, 'Reuniones diarias', 'diaria', 1),
  ('Participación en la reunión Kanban (si aplica)', NULL, 'Reuniones diarias', 'diaria', 2),

  -- ── Diaria: Gestión de EPP y vigías ──
  ('Firma de vales de EPP (4:00–5:00 pm o 6:30–7:10 am)', NULL, 'Gestión de EPP y vigías', 'diaria', 1),
  ('Programación de actividades de vigías, monitores y encapsuladores', NULL, 'Gestión de EPP y vigías', 'diaria', 2),

  -- ── Diaria: Registro y reporte ──
  ('Recopilación de registro de charlas', NULL, 'Registro y reporte', 'diaria', 1),
  ('Reporte de incidentes o accidentes (de ocurrir)', NULL, 'Registro y reporte', 'diaria', 2),
  ('Coordinación de trabajos relacionados con seguridad en vecinos', NULL, 'Registro y reporte', 'diaria', 3),

  -- ── Semanal: Gestión documentaria ──
  ('Actualización de IPERC y PETS', NULL, 'Gestión documentaria', 'semanal', 1),
  ('Revisión y actualización del plano de riesgos y evacuación de la obra', NULL, 'Gestión documentaria', 'semanal', 2),

  -- ── Semanal: Indicadores y horas hombre ──
  ('Verificación de horas hombre', NULL, 'Indicadores y horas hombre', 'semanal', 1),
  ('Gestión de indicadores proactivos y reactivos', NULL, 'Indicadores y horas hombre', 'semanal', 2),
  ('Revisión y aprobación de horas hombre cargadas a SSOMA enviadas por Administración', NULL, 'Indicadores y horas hombre', 'semanal', 3),

  -- ── Semanal: Reuniones semanales ──
  ('Reunión semanal con staff de actividades semanales', NULL, 'Reuniones semanales', 'semanal', 1),
  ('Reunión semanal con equipo de SSOMA', NULL, 'Reuniones semanales', 'semanal', 2),
  ('Reunión de SSOMA con jefe corporativo', NULL, 'Reuniones semanales', 'semanal', 3),

  -- ── Semanal: Gestión administrativa semanal ──
  ('Revisión y firma de hoja de ruta', NULL, 'Gestión administrativa semanal', 'semanal', 1),
  ('Pedido de materiales y EPP semanal', NULL, 'Gestión administrativa semanal', 'semanal', 2),
  ('Revisión de materiales SSOMA cargados a la partida', NULL, 'Gestión administrativa semanal', 'semanal', 3),
  ('Elaboración de reporte a gerencia en comité de obra', NULL, 'Gestión administrativa semanal', 'semanal', 4),

  -- ── Semanal: Seguimiento PASST e inducciones ──
  ('Seguimiento y cumplimiento del PASST', NULL, 'Seguimiento PASST e inducciones', 'semanal', 1),
  ('Cumplimiento del rol de inducciones', NULL, 'Seguimiento PASST e inducciones', 'semanal', 2),

  -- ── Mensual: Auditorías y evaluaciones ──
  ('Cumplimiento de la inspección cruzada', NULL, 'Auditorías y evaluaciones', 'mensual', 1),
  ('Cumplimiento de evaluaciones de contratistas', NULL, 'Auditorías y evaluaciones', 'mensual', 2),

  -- ── Mensual: Investigación y aprendizaje ──
  ('Cierre de investigación de accidentes o incidentes', NULL, 'Investigación y aprendizaje', 'mensual', 1),
  ('Carga de lecciones aprendidas', NULL, 'Investigación y aprendizaje', 'mensual', 2),

  -- ── Mensual: Gestión documentaria mensual ──
  ('Actualización del panel informativo', NULL, 'Gestión documentaria mensual', 'mensual', 1),
  ('Cronograma de charlas', NULL, 'Gestión documentaria mensual', 'mensual', 2),
  ('Certificados de eliminación', NULL, 'Gestión documentaria mensual', 'mensual', 3),
  ('Certificados de servicios higiénicos', NULL, 'Gestión documentaria mensual', 'mensual', 4),

  -- ── Mensual: Reuniones y difusión ──
  ('Reunión mensual con equipo SSOMA de residentes', NULL, 'Reuniones y difusión', 'mensual', 1),
  ('Paradas de seguridad por accidentes propios o de otras empresas', NULL, 'Reuniones y difusión', 'mensual', 2),

  -- ── Anual ──
  ('Registro de declaración de residuos sólidos', NULL, 'Gestión anual', 'anual', 1),
  ('Informe médico a DIGESA', NULL, 'Gestión anual', 'anual', 2)
) AS v(nombre, descripcion, categoria, frecuencia, orden)
WHERE NOT EXISTS (SELECT 1 FROM ss_cumplimiento_actividad WHERE nombre = v.nombre);

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────
SELECT frecuencia, categoria, orden, nombre
FROM ss_cumplimiento_actividad
WHERE rol_responsable = 'coordinador_ssoma'
ORDER BY
  CASE frecuencia WHEN 'diaria' THEN 1 WHEN 'semanal' THEN 2 WHEN 'mensual' THEN 3 WHEN 'anual' THEN 4 END,
  categoria, orden;
