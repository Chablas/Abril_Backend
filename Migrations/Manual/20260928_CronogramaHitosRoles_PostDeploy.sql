-- ============================================================================
-- Mejora Continua · Cronograma de Hitos — Roles que administran el cronograma
-- PASO 2 de 2 · SOLO DESPUÉS DE DESPLEGAR EL BACKEND Y EL FRONTEND
-- Fecha: 2026-09-28
--
-- Quita el Cronograma de Hitos a ADMINISTRADOR DE UDP (role_id 2) y a ADMINISTRADOR DE
-- RESIDENTES (role_id 4). Con el código viejo el 4 era el único que eliminaba versiones y
-- editaba o agregaba hitos guardados; con el nuevo eso lo hace la feature de administrar que
-- el paso 1 les dio a COORDINADOR DE PROYECTOS, JEFE DE PROYECTOS y GERENTE INMOBILIARIO.
-- Quien se quede sin acceso por esto es a propósito.
--
-- ADMINISTRADOR DE RESIDENTES NO se da de baja: lo siguen usando otras funcionalidades
-- (Control de respuesta de reportes, Cuaderno de obra, IVTs, Seguimiento de residentes, SSOMA,
-- etc.). Este paso solo le quita esta.
--
-- Requiere el paso 1 (20260928_CronogramaHitosRoles.sql): aborta si falta. Idempotente. Los
-- usuarios lo ven al volver a iniciar sesión (allowed_features se arma en el login).
-- ============================================================================

BEGIN;

DO $$
BEGIN
    -- Los IDs 2 y 4 son los de Roles.cs / roles.ts: se comprueba que sigan siendo esos roles.
    IF NOT EXISTS (SELECT 1 FROM role WHERE role_id = 2 AND role_description = 'ADMINISTRADOR DE UDP') THEN
        RAISE EXCEPTION 'El role_id 2 no es ADMINISTRADOR DE UDP. Abortado.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM role WHERE role_id = 4 AND role_description = 'ADMINISTRADOR DE RESIDENTES') THEN
        RAISE EXCEPTION 'El role_id 4 no es ADMINISTRADOR DE RESIDENTES. Abortado.';
    END IF;

    -- Sin el paso 1 nadie podría administrar el cronograma después de esto.
    IF NOT EXISTS (
        SELECT 1
        FROM role r
        JOIN role_feature rf ON rf.role_id = r.role_id
        JOIN feature f ON f.feature_id = rf.feature_id
        WHERE r.role_description = 'COORDINADOR DE PROYECTOS'
          AND r.state
          AND f.feature_key = 'mejora-continua.milestone-schedule.administrar') THEN
        RAISE EXCEPTION 'Falta el paso 1: COORDINADOR DE PROYECTOS no tiene mejora-continua.milestone-schedule.administrar. Abortado.';
    END IF;
END $$;

DELETE FROM role_feature rf
USING feature f
WHERE rf.feature_id = f.feature_id
  AND rf.role_id IN (2, 4)
  AND f.feature_key IN ('mejora-continua.milestone-schedule',
                        'mejora-continua.milestone-schedule.editar',
                        'mejora-continua.milestone-schedule.administrar');

COMMIT;

-- ── Verificación ────────────────────────────────────────────────────────────
-- a) Ni el 2 ni el 4 aparecen en ninguna feature del cronograma (debe devolver 0 filas):
-- SELECT rf.role_id, f.feature_key
-- FROM role_feature rf
-- JOIN feature f ON f.feature_id = rf.feature_id
-- WHERE rf.role_id IN (2, 4)
--   AND f.feature_key LIKE 'mejora-continua.milestone-schedule%';
--
-- b) Lo que le queda a ADMINISTRADOR DE RESIDENTES (por eso no se da de baja):
-- SELECT f.feature_key
-- FROM role_feature rf
-- JOIN feature f ON f.feature_id = rf.feature_id
-- WHERE rf.role_id = 4
-- ORDER BY 1;
