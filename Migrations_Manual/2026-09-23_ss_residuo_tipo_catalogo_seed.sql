-- ============================================================================
-- Catálogo inicial de tipos de residuo (RCD) + factor de conversión m3->t.
--
-- El factor es SOLO un valor por defecto para cuando el registro de viaje
-- llega en m3 y no se conoce el peso real: ver ss_residuo_viaje.cantidad_ton,
-- que es opcional y se respeta tal cual si el usuario la ingresa directamente
-- (ticket de pesaje ya en toneladas) — el factor nunca sobrescribe un valor
-- manual, solo se usa quando cantidad_ton viene en null.
--
-- Idempotente (se puede re-correr sin duplicar).
-- ============================================================================
BEGIN;

INSERT INTO ss_residuo_tipo (codigo_sigersol, nombre, es_peligroso, activo)
SELECT v.codigo_sigersol, v.nombre, v.es_peligroso, true
FROM (VALUES
  ('RCD-01', 'Desmonte / Excedente de excavación', false),
  ('RCD-02', 'Demolición', false),
  ('RCD-03', 'Residuos de construcción (mezcla general)', false),
  ('RCD-04', 'Madera', false),
  ('RCD-05', 'Metal / chatarra', false),
  ('RCD-06', 'Concreto / escombro', false),
  ('RCD-07', 'Papel y cartón', false),
  ('RCD-08', 'Plástico', false),
  ('RCD-09', 'Residuos peligrosos', true)
) AS v(codigo_sigersol, nombre, es_peligroso)
WHERE NOT EXISTS (SELECT 1 FROM ss_residuo_tipo WHERE codigo_sigersol = v.codigo_sigersol);

INSERT INTO ss_residuo_tipo_factor (residuo_tipo_id, factor_m3_a_ton, vigencia_desde)
SELECT t.id, v.factor, CURRENT_DATE
FROM ss_residuo_tipo t
JOIN (VALUES
  ('RCD-01', 1.8),
  ('RCD-02', 2.3),
  ('RCD-03', 1.8),
  ('RCD-04', 0.3),
  ('RCD-05', 1.0),
  ('RCD-06', 2.2),
  ('RCD-07', 0.15),
  ('RCD-08', 0.1),
  ('RCD-09', 0.5)
) AS v(codigo_sigersol, factor) ON v.codigo_sigersol = t.codigo_sigersol
WHERE NOT EXISTS (
  SELECT 1 FROM ss_residuo_tipo_factor f
  WHERE f.residuo_tipo_id = t.id AND f.vigencia_desde = CURRENT_DATE
);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT t.codigo_sigersol, t.nombre, t.es_peligroso, f.factor_m3_a_ton, f.vigencia_desde
-- FROM ss_residuo_tipo t
-- LEFT JOIN ss_residuo_tipo_factor f ON f.residuo_tipo_id = t.id
-- ORDER BY t.codigo_sigersol;
