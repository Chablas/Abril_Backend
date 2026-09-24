-- ============================================================================
-- Módulo SSOMA — Cumplimiento: agrega columna "categoria" y reemplaza el
-- catálogo actual por el checklist detallado de Prevencionista (agrupado por
-- categoría, para el acordeón mobile). Ejecutar en pgAdmin. Idempotente salvo
-- el TRUNCATE, que es intencional — ver nota abajo.
--
-- IMPORTANTE — respaldo antes de borrar:
-- El catálogo de Coordinador SSOMA (rol_responsable IN ('coordinador_ssoma','ambos'))
-- que existía antes de este script queda abajo, comentado, para volver a
-- sembrarlo en una sesión futura ya reorganizado por categoría igual que este:
--
--   Diaria    | ambos              | Charla de 5 minutos / inducción de inicio de jornada
--   Diaria    | coordinador_ssoma  | Registro de incidentes/accidentes del día (si los hubiera)
--   Semanal   | coordinador_ssoma  | Reunión de comité de SST / coordinación semanal SSOMA
--   Semanal   | coordinador_ssoma  | Revisión de indicadores proactivos y reactivos de la semana
--   Semanal   | ambos              | Simulacro o capacitación semanal programada
--   Mensual   | coordinador_ssoma  | Feedback y retroalimentación a los PDRs
--   Mensual   | coordinador_ssoma  | Informe mensual de gestión SSOMA a Gerencia
--   Mensual   | coordinador_ssoma  | Auditoría interna / autoevaluación del sistema de gestión
--   Mensual   | coordinador_ssoma  | Verificación de vigencia de pólizas, certificados y SCTR del personal
--   Mensual   | coordinador_ssoma  | Revisión y actualización del plan de emergencia
--   Mensual   | coordinador_ssoma  | Evaluación al Coordinador, Contratistas, Residente y Jefe SSOMA
--
-- ============================================================================
BEGIN;

-- ── Columna nueva ─────────────────────────────────────────────────────────
ALTER TABLE ss_cumplimiento_actividad ADD COLUMN IF NOT EXISTS categoria VARCHAR(150) NULL;

-- ── Borra TODO el catálogo actual (Prevencionista + Coordinador + Ambos) ───
-- ON DELETE CASCADE en ss_cumplimiento_registro se lleva también el histórico
-- de marcado ligado a estas actividades — asumido a propósito por pedido
-- explícito del usuario (el módulo es reciente, sin histórico relevante aún).
TRUNCATE TABLE ss_cumplimiento_actividad RESTART IDENTITY CASCADE;

-- ── Nuevo catálogo: solo Prevencionista, agrupado por categoría ────────────
INSERT INTO ss_cumplimiento_actividad
    (nombre, descripcion, categoria, rol_responsable, frecuencia, orden, activo, created_at, updated_at)
SELECT v.nombre, v.descripcion, v.categoria, 'prevencionista', v.frecuencia, v.orden, true, NOW(), NOW()
FROM (VALUES
  -- ── Diaria: Protecciones colectivas y encapsulados ──
  ('Encapsulado específico implementado (escaleras, ductos, ascensor, fachada acordonados)', NULL, 'Protecciones colectivas y encapsulados', 'diaria', 1),
  ('Encapsulado perimetral Cefomaq implementado', NULL, 'Protecciones colectivas y encapsulados', 'diaria', 2),
  ('Plataformas en escuadra con encapsulado perimetral', NULL, 'Protecciones colectivas y encapsulados', 'diaria', 3),
  ('Malla anticaída — anillo de objetos / anillo fijo implementada (a partir de piso 3)', NULL, 'Protecciones colectivas y encapsulados', 'diaria', 4),
  ('Malla anticaída rotativa implementada (a partir de piso 6)', NULL, 'Protecciones colectivas y encapsulados', 'diaria', 5),
  ('Malla anticaída de personas implementada', NULL, 'Protecciones colectivas y encapsulados', 'diaria', 6),
  ('Liberación de protecciones colectivas en prelosa antes de iniciar trabajos', NULL, 'Protecciones colectivas y encapsulados', 'diaria', 7),
  ('Encapsulado de ductos de ascensor asegurado', NULL, 'Protecciones colectivas y encapsulados', 'diaria', 8),

  -- ── Diaria: Andamios y accesos ──
  ('Andamios liberados', NULL, 'Andamios y accesos', 'diaria', 1),
  ('Andamios de más de 3 cuerpos arriostrados según modulación', NULL, 'Andamios y accesos', 'diaria', 2),
  ('Bolillos de andamio tipo escuadra con tarjetas de advertencia', NULL, 'Andamios y accesos', 'diaria', 3),
  ('Liberación de rutas para colocación de prelosas', NULL, 'Andamios y accesos', 'diaria', 4),
  ('Liberación de escaleras de acceso a prelosas (no lineales)', NULL, 'Andamios y accesos', 'diaria', 5),
  ('Liberación de tranqueras para elevadores y plataforma de descarga', NULL, 'Andamios y accesos', 'diaria', 6),
  ('Liberación de plataforma de vaciado en verticales', NULL, 'Andamios y accesos', 'diaria', 7),

  -- ── Diaria: Izaje y estructuras ──
  ('Inspección de aparejos y equipos de izaje', NULL, 'Izaje y estructuras', 'diaria', 1),
  ('Placas de encofrado/modulación cumplen estándar al momento de izarlas', NULL, 'Izaje y estructuras', 'diaria', 2),
  ('Revisión del apilamiento de encofrado', NULL, 'Izaje y estructuras', 'diaria', 3),
  ('Punto de anclaje adecuado para personas que suben a volquetes/camiones', NULL, 'Izaje y estructuras', 'diaria', 4),
  ('Balde basculante cumple estándar y en buenas condiciones', NULL, 'Izaje y estructuras', 'diaria', 5),

  -- ── Diaria: Trabajos en altura y protección ──
  ('Sistema de protección contra caídas que usan los trabajadores es el idóneo', NULL, 'Trabajos en altura y protección', 'diaria', 1),
  ('Plataforma de ascensor con rieles metálicas (mínimo) si se usa como plataforma de trabajo', NULL, 'Trabajos en altura y protección', 'diaria', 2),
  ('Protección contra caída de objetos: fenólicos y listones de madera cada 3 niveles', NULL, 'Trabajos en altura y protección', 'diaria', 3),
  ('Aseguramiento de instalación de marcelinos según plano', NULL, 'Trabajos en altura y protección', 'diaria', 4),

  -- ── Diaria: Seguridad general y operativa ──
  ('Vigías con vestimenta limpia y adecuada, siempre en su puesto, cumpliendo funciones', NULL, 'Seguridad general y operativa', 'diaria', 1),
  ('Rutas de evacuación liberadas', NULL, 'Seguridad general y operativa', 'diaria', 2),
  ('Puntos de hidratación en piso 1 y último piso (mínimo)', NULL, 'Seguridad general y operativa', 'diaria', 3),
  ('Bloqueador solar disponible en piso 1 y en el último nivel', NULL, 'Seguridad general y operativa', 'diaria', 4),
  ('Inspección de estación de emergencia', NULL, 'Seguridad general y operativa', 'diaria', 5),
  ('Inspección de tableros eléctricos', NULL, 'Seguridad general y operativa', 'diaria', 6),
  ('Alarmas de chutes operativas y chutes en buen estado', NULL, 'Seguridad general y operativa', 'diaria', 7),
  ('Recursos para instalación de rodapiés, tapas y barandas FRP', NULL, 'Seguridad general y operativa', 'diaria', 8),
  ('Seguimiento de bloqueo de zonas restringidas (patios, etc.)', NULL, 'Seguridad general y operativa', 'diaria', 9),

  -- ── Semanal ──
  ('Pedido de EPC', NULL, 'Gestión semanal', 'semanal', 1),
  ('Orden y limpieza (liberación)', NULL, 'Gestión semanal', 'semanal', 2),
  ('Levantamiento de RACs', NULL, 'Gestión semanal', 'semanal', 3),

  -- ── Mensual ──
  ('Cumplimiento de indicadores proactivos', NULL, 'Gestión mensual', 'mensual', 1)
) AS v(nombre, descripcion, categoria, frecuencia, orden);

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────
SELECT frecuencia, categoria, orden, nombre FROM ss_cumplimiento_actividad ORDER BY frecuencia, categoria, orden;
