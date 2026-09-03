-- Migración manual (pgAdmin) — features nuevas del módulo de Costos de Arquitectura Comercial.
-- A propósito NO se otorga a ningún rol acá: el admin asigna quién tiene acceso
-- desde Configuración > Roles y Permisos.

INSERT INTO feature (feature_key, module_id)
SELECT 'arquitectura-comercial.costos', module_id
FROM feature
WHERE feature_key = 'arquitectura-comercial.observaciones'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'arquitectura-comercial.costos');

INSERT INTO feature (feature_key, module_id)
SELECT 'arquitectura-comercial.costos.configurar', module_id
FROM feature
WHERE feature_key = 'arquitectura-comercial.observaciones'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'arquitectura-comercial.costos.configurar');
