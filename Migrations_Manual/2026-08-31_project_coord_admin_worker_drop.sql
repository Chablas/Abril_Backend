-- ============================================================================
-- 2026-08-31 · DROP de project.email_coord_admin
--
-- Segunda mitad de 2026-08-31_project_coord_admin_worker.sql. Ese script creó
-- workers_coord_admin_id y migró los datos; este borra la columna vieja.
--
-- ⚠️  CORRER **SOLO DESPUÉS** DE QUE EL DEPLOY ESTÉ ARRIBA Y VERIFICADO.
--     Si se corre junto con el otro script, el backend viejo (que todavía
--     selecciona email_coord_admin) tumba producción con 42703.
--
-- Se borra en vez de conservarse como columna histórica porque el dato dejó de
-- capturarse: la regla de auditoría del proyecto protege filas, no columnas, y
-- el correo no se pierde — sigue estando en la ficha del trabajador al que
-- ahora apunta la FK.
--
-- Confirmado antes de escribir esto: ninguna consulta de Dapper ni SQL crudo
-- del backend nombra email_coord_admin (grep sobre todo el repo); las únicas
-- menciones que quedaban eran comentarios, ya actualizados.
--
-- Idempotente: se puede correr más de una vez.
-- ============================================================================

BEGIN;

-- ── Guarda: abortar si quedó algún correo sin migrar ────────────────────────
-- Sin esto, un proyecto cuyo email_coord_admin nunca cruzó con un trabajador
-- perdería el dato en silencio. Si esto revienta, correr la "Verificación 1"
-- del script anterior, asignar el coordinador a mano desde Configuración →
-- Proyectos, y recién ahí volver a intentar el DROP.
DO $$
DECLARE
    v_huerfanos integer;
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'project' AND column_name = 'email_coord_admin'
    ) THEN
        SELECT count(*) INTO v_huerfanos
        FROM project
        WHERE nullif(btrim(email_coord_admin), '') IS NOT NULL
          AND workers_coord_admin_id IS NULL;

        IF v_huerfanos > 0 THEN
            RAISE EXCEPTION
                'Abortado: % proyecto(s) tienen email_coord_admin cargado pero workers_coord_admin_id en NULL. Resolverlos antes de dropear la columna.',
                v_huerfanos;
        END IF;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'project' AND column_name = 'workers_coord_admin_id'
    ) THEN
        RAISE EXCEPTION
            'Abortado: falta project.workers_coord_admin_id. Correr primero 2026-08-31_project_coord_admin_worker.sql.';
    END IF;
END $$;

-- ── DROP ────────────────────────────────────────────────────────────────────
ALTER TABLE project
    DROP COLUMN IF EXISTS email_coord_admin;

COMMIT;

-- ============================================================================
-- Verificación — debe devolver 0 filas.
--
-- SELECT column_name
-- FROM information_schema.columns
-- WHERE table_name = 'project' AND column_name = 'email_coord_admin';
-- ============================================================================
