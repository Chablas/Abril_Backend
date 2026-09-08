-- ============================================================================
-- Gestión de Rendiciones + Reembolsos — dos funcionalidades nuevas de Gestión
-- Administrativa que salen de partir Gestión de Salidas en tres:
--
--   Gestión de Salidas      → aprobar / rechazar / cancelar la salida y RENDIR.
--   Gestión de Rendiciones  → de ahí en adelante: adjuntar el Consolidado del
--                             S10, aprobar o rechazar el reembolso y firmar la
--                             planilla. La unidad es la PLANILLA, no la salida.
--   Reembolsos              → la bandeja de Tesorería: marcar como pagado.
--
-- No hay cambios de esquema: las tres pantallas leen las mismas tablas
-- (ga_rendicion, ga_solicitud_salida, ga_consolidado_s10), que ya existen.
--
-- ⚠️ ORDEN: este script asume que ya corrió `_sql_prod/ga_rendiciones_feature.sql`
--    (Mis Rendiciones). No es obligatorio para que este funcione, pero sí para
--    que el flujo quede completo: ahí es donde el trabajador rinde.
--
-- Re-ejecutable: los ON CONFLICT y el DELETE dejan el script sin efecto si ya
-- se corrió. Los feature_id salen de la secuencia, no se fijan a mano.
-- ============================================================================

BEGIN;

-- ── Guarda: que el rol 83 sea de verdad TESORERO ────────────────────────────
-- El id se eligió mirando PROD, pero si algún día no coincide es mejor abortar
-- sin tocar nada que repartir permisos al rol equivocado.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM role
        WHERE role_id = 83 AND upper(role_description) = 'TESORERO'
    ) THEN
        RAISE EXCEPTION
            'El rol 83 no es TESORERO en esta base. Revisar los ids antes de correr el script.';
    END IF;
END $$;

-- ── 1) Las dos features nuevas ──────────────────────────────────────────────
-- El módulo sale de Gestión de Salidas y no de un id fijo: las tres pantallas
-- viven en el mismo (10 = Gestión Administrativa).
INSERT INTO feature (feature_key, module_id)
SELECT k.feature_key, f.module_id
FROM feature f
CROSS JOIN (VALUES
    ('gestion-administrativa.gestion-rendiciones'),
    ('gestion-administrativa.reembolsos')
) AS k(feature_key)
WHERE f.feature_key = 'gestion-administrativa.gestion-salidas'
ON CONFLICT (feature_key) DO NOTHING;

-- ── 2) Gestión de Rendiciones: la misma visibilidad que Gestión de Salidas ──
-- Se copian los roles que Gestión de Salidas tenga EN ESTA BASE en vez de
-- listarlos: dev y prod no coinciden (prod tiene además USUARIO DE GTH) y la
-- regla pedida es "la misma visibilidad", no "estos roles".
-- Menos TESORERO: su paso es el pago, que vive en Reembolsos.
INSERT INTO role_feature (role_id, feature_id)
SELECT rf.role_id, nueva.feature_id
FROM role_feature rf
JOIN feature vieja  ON vieja.feature_id = rf.feature_id
CROSS JOIN feature nueva
WHERE vieja.feature_key = 'gestion-administrativa.gestion-salidas'
  AND nueva.feature_key = 'gestion-administrativa.gestion-rendiciones'
  AND rf.role_id <> 83
ON CONFLICT DO NOTHING;

-- ── 3) Reembolsos: solo TESORERO ────────────────────────────────────────────
-- Tener el rol no alcanza: AuthRepository.GetAllowedFeaturesAsync solo concede
-- las features del rol 83 si además el puesto del trabajador es de categoría
-- TESORERO (46). Esa mitad no se toca acá, ya está en el código.
INSERT INTO role_feature (role_id, feature_id)
SELECT 83, f.feature_id
FROM feature f
WHERE f.feature_key = 'gestion-administrativa.reembolsos'
ON CONFLICT DO NOTHING;

-- ── 4) TESORERO sale de Gestión de Salidas ──────────────────────────────────
-- Tenía esa feature solo por el "modo Tesorería" que la pantalla ya no tiene.
-- Si se dejara, un tesorero pasaría a ver la pantalla de aprobación/rendición,
-- que nunca fue suya. Se revierte con el INSERT comentado al final.
DELETE FROM role_feature rf
USING feature f
WHERE rf.feature_id = f.feature_id
  AND f.feature_key = 'gestion-administrativa.gestion-salidas'
  AND rf.role_id = 83;

-- ── Guarda final: que las dos features hayan quedado con acceso ─────────────
DO $$
DECLARE
    v_gr integer;
    v_re integer;
BEGIN
    SELECT count(*) INTO v_gr
    FROM feature f
    JOIN role_feature rf ON rf.feature_id = f.feature_id
    WHERE f.feature_key = 'gestion-administrativa.gestion-rendiciones';

    SELECT count(*) INTO v_re
    FROM feature f
    JOIN role_feature rf ON rf.feature_id = f.feature_id
    WHERE f.feature_key = 'gestion-administrativa.reembolsos';

    IF v_gr = 0 OR v_re = 0 THEN
        RAISE EXCEPTION
            'Alguna feature quedó sin roles (gestion-rendiciones=%, reembolsos=%). Se aborta.',
            v_gr, v_re;
    END IF;
END $$;

COMMIT;

-- ============================================================================
-- Verificación
-- ============================================================================
-- SELECT f.feature_id, f.feature_key, rf.role_id, r.role_description
-- FROM feature f
-- LEFT JOIN role_feature rf ON rf.feature_id = f.feature_id
-- LEFT JOIN role r          ON r.role_id     = rf.role_id
-- WHERE f.feature_key IN ('gestion-administrativa.gestion-salidas',
--                         'gestion-administrativa.gestion-rendiciones',
--                         'gestion-administrativa.reembolsos')
-- ORDER BY f.feature_key, rf.role_id;

-- Para devolverle Gestión de Salidas a TESORERO (revierte el paso 4):
-- INSERT INTO role_feature (role_id, feature_id)
-- SELECT 83, feature_id FROM feature
-- WHERE feature_key = 'gestion-administrativa.gestion-salidas'
-- ON CONFLICT DO NOTHING;

-- Ojo: allowed_features se calcula en el LOGIN y viaja en localStorage. Quien
-- esté con la sesión abierta tiene que volver a iniciarla para ver los cambios.
