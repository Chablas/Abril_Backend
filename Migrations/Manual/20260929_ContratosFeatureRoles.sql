-- ============================================================================
-- Unidad de Proyectos · Contratos — features y roles
-- Fecha: 2026-09-29
--
-- Contratos queda con 2 features, atadas al módulo "Proyectos" (module_id, se resuelve por
-- module_name para no depender de que sea 6 en todos los entornos):
--   · unidad-de-proyectos.contratos          → VER: listado, detalle, hitos de pago, descargar
--     el contrato generado. Se crea pero NO se asigna a ningún rol todavía (a propósito).
--   · unidad-de-proyectos.contratos.editar   → Crear/editar contratos e hitos, generar el
--     documento, avanzar los pasos 4-9, configurar la carpeta de SharePoint del proyecto.
--
-- Roles con Editar: COORDINADOR DE PROYECTOS, JEFE DE PROYECTOS, GERENTE INMOBILIARIO (los
-- mismos 3 que administran Cronograma de Hitos) + USUARIO DE UDP (cubre a quienes hoy tienen el
-- puesto "Ingeniero de Proyecto" o "Arquitecto de Proyectos", que comparten ese rol de sistema).
--
-- Idempotente. Aplicar en el único entorno real (D1).
-- ============================================================================

BEGIN;

-- Guardas: si falta algo de lo que se asume, no se toca nada.
DO $$
DECLARE
    faltan text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM module WHERE module_name = 'Proyectos') THEN
        RAISE EXCEPTION 'No existe el módulo "Proyectos". Abortado.';
    END IF;

    SELECT string_agg(n.role_description, ', ')
    INTO faltan
    FROM (VALUES ('COORDINADOR DE PROYECTOS'), ('JEFE DE PROYECTOS'),
                 ('GERENTE INMOBILIARIO'), ('USUARIO DE UDP')) AS n(role_description)
    WHERE NOT EXISTS (SELECT 1 FROM role r WHERE r.role_description = n.role_description AND r.state);

    IF faltan IS NOT NULL THEN
        RAISE EXCEPTION 'Falta el rol vigente: %. Abortado.', faltan;
    END IF;
END $$;

-- 1) Features, atadas al módulo "Proyectos".
INSERT INTO feature (feature_key, module_id)
SELECT k.feature_key, m.module_id
FROM (VALUES ('unidad-de-proyectos.contratos'),
             ('unidad-de-proyectos.contratos.editar')) AS k(feature_key)
CROSS JOIN module m
WHERE m.module_name = 'Proyectos'
ON CONFLICT (feature_key) DO NOTHING;

-- 2) Editar: los 4 roles acordados.
INSERT INTO role_feature (role_id, feature_id)
SELECT r.role_id, f.feature_id
FROM role r
CROSS JOIN feature f
WHERE r.role_description IN ('COORDINADOR DE PROYECTOS', 'JEFE DE PROYECTOS',
                              'GERENTE INMOBILIARIO', 'USUARIO DE UDP')
  AND r.state
  AND f.feature_key = 'unidad-de-proyectos.contratos.editar'
ON CONFLICT (role_id, feature_id) DO NOTHING;

-- NOTA: unidad-de-proyectos.contratos (Ver) queda creada sin asignar a ningún rol — a propósito,
-- según lo acordado. Asignarla más adelante con un INSERT INTO role_feature igual al de arriba.

COMMIT;

-- ── Verificación ────────────────────────────────────────────────────────────
-- SELECT f.feature_key, r.role_id, r.role_description
-- FROM feature f
-- JOIN role_feature rf ON rf.feature_id = f.feature_id
-- JOIN role r ON r.role_id = rf.role_id
-- WHERE f.feature_key LIKE 'unidad-de-proyectos.contratos%'
-- ORDER BY 1, 2;
