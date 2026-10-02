-- Reemplaza los criterios de SSOMA, Producción, Residencia y Calidad en la evaluación de
-- contratistas por los del CSV oficial (CriterioEvalContratista.csv). Mismas filas y orden,
-- salvo dos diferencias de cantidad:
--   * SSOMA: la base tenía 6 criterios y el CSV trae 5 -> el 6.º se desactiva (no se borra:
--     ev_evaluacion_contratista_detalle lo referencia con FK).
--   * Calidad: la base tenía 5 y el CSV trae 6 -> se inserta el 6.º.
-- Es seguro reemplazar texto porque el detalle de cada evaluación guarda su propia copia
-- del criterio. area_nombre y puesto_evaluador no se tocan (siguen coincidiendo con
-- ResolverArea). El CSV llama "Residente" a lo que la base llama "Residencia".
-- Se corrigieron erratas obvias del CSV (posse, rportes, manteniento, espacio final).

-- 1) Verificar antes (esperado: SSOMA 6, Producción 5, Residencia 5, Calidad 5; todas activas)
SELECT area_nombre, COUNT(*) AS activos, MIN(orden) AS min_orden, MAX(orden) AS max_orden
FROM ev_contratista_plantilla
WHERE activo AND area_nombre IN ('SSOMA', 'Producción', 'Residencia', 'Calidad')
GROUP BY area_nombre ORDER BY area_nombre;

BEGIN;

-- ── SSOMA ────────────────────────────────────────────────────────────────
UPDATE ev_contratista_plantilla SET criterio = 'La empresa cumple con todas las normativas estandares y/o procedimientos alineados a la politica SSOMA.'
WHERE area_nombre = 'SSOMA' AND activo AND orden = 1;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa mantiene un historial bajo de incidentes y accidentes en los trabajos realizados, manteniendo registros y reportes según lo establecido en la Política SSOMA'
WHERE area_nombre = 'SSOMA' AND activo AND orden = 2;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa posee un plan de seguridad específico para el proyecto que cumpla con los requisitos de la política SSOMA'
WHERE area_nombre = 'SSOMA' AND activo AND orden = 3;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa proporciona formación y capacitación regular a su personal en temas de seguridad, conforme a los contenidos y frecuencias exigidos por la norma G.050'
WHERE area_nombre = 'SSOMA' AND activo AND orden = 4;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa asegura la disponibilidad y uso adecuado de equipos de protección personal y otros dispositivos de seguridad.'
WHERE area_nombre = 'SSOMA' AND activo AND orden = 5;
UPDATE ev_contratista_plantilla SET activo = FALSE
WHERE area_nombre = 'SSOMA' AND activo AND orden = 6;

-- ── Producción ───────────────────────────────────────────────────────────
UPDATE ev_contratista_plantilla SET criterio = 'Cumplimiento de los trenes de trabajo'
WHERE area_nombre = 'Producción' AND activo AND orden = 1;
UPDATE ev_contratista_plantilla SET criterio = 'Compromiso con la obra y la meta trazada'
WHERE area_nombre = 'Producción' AND activo AND orden = 2;
UPDATE ev_contratista_plantilla SET criterio = 'Se programa con anticipacion y prevee sus recursos.'
WHERE area_nombre = 'Producción' AND activo AND orden = 3;
UPDATE ev_contratista_plantilla SET criterio = 'Disponibilidad de personal'
WHERE area_nombre = 'Producción' AND activo AND orden = 4;
UPDATE ev_contratista_plantilla SET criterio = 'Existe supervisión calificada de campo por parte de la empresa'
WHERE area_nombre = 'Producción' AND activo AND orden = 5;

-- ── Residencia (en el CSV: "Residente") ─────────────────────────────────
UPDATE ev_contratista_plantilla SET criterio = 'La empresa mantiene una comunicación y coordinación efectiva con el equipo de trabajo y otros subcontratistas'
WHERE area_nombre = 'Residencia' AND activo AND orden = 1;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa gestiona y entrega los documentos necesarios para el proyecto y con puntualidad.'
WHERE area_nombre = 'Residencia' AND activo AND orden = 2;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa tiene la capacidad para identificar y resolver problemas en el sitio de manera eficiente'
WHERE area_nombre = 'Residencia' AND activo AND orden = 3;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa realiza una supervisión y control de calidad alta y muestra efectividad de trabajo diario en el sitio'
WHERE area_nombre = 'Residencia' AND activo AND orden = 4;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa colabora con la obra y su desarrollo'
WHERE area_nombre = 'Residencia' AND activo AND orden = 5;

-- ── Calidad ──────────────────────────────────────────────────────────────
UPDATE ev_contratista_plantilla SET criterio = 'La empresa cumple con la calidad requerida en las liberaciones.'
WHERE area_nombre = 'Calidad' AND activo AND orden = 1;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa dispone de personal para el levantamiento de observaciones y salidas no conforme en el tiempo acordado.'
WHERE area_nombre = 'Calidad' AND activo AND orden = 2;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa da soluciones, aportes y previene situaciones relacionadas a su especialidad.'
WHERE area_nombre = 'Calidad' AND activo AND orden = 3;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa cumple con las entregas de los planos asbuilt.'
WHERE area_nombre = 'Calidad' AND activo AND orden = 4;
UPDATE ev_contratista_plantilla SET criterio = 'La empresa detecta fallas en los materiales previo a la instalación y gestiona eficientemente el reemplazo de estos.'
WHERE area_nombre = 'Calidad' AND activo AND orden = 5;

INSERT INTO ev_contratista_plantilla (area_nombre, puesto_evaluador, criterio, orden, activo)
SELECT 'Calidad', 'Responsable de Calidad',
       'La empresa cumple con el envío de procedimientos, manuales, fichas tecnicas y documentación de calidad cuando se solicita.', 6, TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM ev_contratista_plantilla WHERE area_nombre = 'Calidad' AND activo AND orden = 6
);

-- 2) Verificar antes de confirmar (esperado: SSOMA 5, Producción 5, Residencia 5, Calidad 6)
SELECT area_nombre, orden, criterio
FROM ev_contratista_plantilla
WHERE activo AND area_nombre IN ('SSOMA', 'Producción', 'Residencia', 'Calidad')
ORDER BY area_nombre, orden;

COMMIT;
