-- ============================================================================
-- 2026-08-31 · El coordinador administrativo del proyecto pasa de ser un correo
--              suelto a una referencia al trabajador.
--
-- Antes: project.email_coord_admin era texto libre. Se cargaba desde dos sitios
-- (Gestión de Responsables, con un picker que igual guardaba solo el correo, y
-- Configuración → Proyectos → Emails, con un <input type="email"> sin validar
-- contra workers). Si esa persona cambiaba de correo corporativo o se retiraba,
-- el proyecto se quedaba apuntando al string viejo y los correos de EMO salían
-- sin el administrador sin que nadie se enterara.
--
-- Ahora: project.workers_coord_admin_id → workers.id, y el correo se lee de
-- workers.email_corporativo al enviar. Mismo patrón que residente_workers_id
-- (ver 2026-08-07_project_residente_worker.sql).
--
-- ⚠️  ESTE SCRIPT VA **ANTES** DEL DEPLOY. Deja las dos columnas conviviendo:
--     el código viejo sigue leyendo email_coord_admin y no se rompe nada.
--     El DROP de la columna vieja va en un script aparte que se corre
--     **DESPUÉS** del deploy:
--     2026-08-31_project_coord_admin_worker_drop.sql
--
-- Idempotente: se puede correr más de una vez sin duplicar nada.
-- ============================================================================

BEGIN;

-- ── 1) Nueva columna ────────────────────────────────────────────────────────
ALTER TABLE project
    ADD COLUMN IF NOT EXISTS workers_coord_admin_id integer NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_project_workers_coord_admin'
    ) THEN
        ALTER TABLE project
            ADD CONSTRAINT fk_project_workers_coord_admin
            FOREIGN KEY (workers_coord_admin_id) REFERENCES workers (id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_project_workers_coord_admin
    ON project (workers_coord_admin_id) WHERE workers_coord_admin_id IS NOT NULL;

COMMENT ON COLUMN project.workers_coord_admin_id IS
    'Coordinador administrativo del proyecto (FK a workers). Su correo se lee de workers.email_corporativo al enviar; no se guarda copia del texto.';

-- ── 2) Migración de datos ───────────────────────────────────────────────────
-- Se cruza email_coord_admin contra workers.email_corporativo (sin distinguir
-- mayúsculas ni espacios). Solo se migra cuando el cruce es inequívoco: si un
-- correo devolviera más de un trabajador se deja en NULL para resolverlo a mano
-- desde Configuración → Proyectos.
--
-- No se filtra por project.active ni por project.state: se migra todo lo que
-- tenga un correo cargado, incluidos los proyectos inactivos o cerrados, para
-- no perder el dato si alguno se reactiva.
--
-- Verificado contra producción el 2026-08-31: 27 proyectos con correo cargado,
-- los 27 cruzan con exactamente un trabajador (0 ambiguos, 0 huérfanos).
UPDATE project pr
SET workers_coord_admin_id = m.worker_id,
    updated_date_time      = now()
FROM (
    SELECT pr2.project_id,
           min(w.id) AS worker_id,
           count(*)  AS coincidencias
    FROM project pr2
    JOIN workers w
      ON lower(btrim(w.email_corporativo)) = lower(btrim(pr2.email_coord_admin))
    WHERE nullif(btrim(pr2.email_coord_admin), '') IS NOT NULL
    GROUP BY pr2.project_id
    HAVING count(*) = 1
) m
WHERE pr.project_id = m.project_id
  AND pr.workers_coord_admin_id IS NULL;

-- ── 3) Comentario desactualizado de una migración anterior ──────────────────
-- 2026-08-31_control_licencias_visitas.sql dejó este COMMENT nombrando dos
-- columnas que ya no son la fuente del dato (email_residente se reemplazó por
-- residente_workers_id en agosto; email_coord_admin se reemplaza acá).
--
-- Condicional porque la tabla existe en producción pero todavía no en la base
-- de desarrollo, que va desfasada: sin el IF, este script no corre en dev.
DO $$
BEGIN
    IF to_regclass('public.vecino_licencia_control_visita') IS NOT NULL THEN
        COMMENT ON TABLE vecino_licencia_control_visita IS
            'Fechas de visita de la municipalidad registradas en el Anexo H (tipo_id 11) de una licencia. Recordatorio fijo 2 dias antes, enviado al residente (project.residente_workers_id) y al coordinador administrativo (project.workers_coord_admin_id), resolviendo el correo desde workers.email_corporativo.';
    END IF;
END $$;

COMMIT;

-- ============================================================================
-- Verificación 1 — proyectos cuyo email_coord_admin NO se pudo migrar.
-- Debe devolver 0 filas. Si devuelve alguna, hay que asignarles el coordinador
-- a mano desde Configuración → Proyectos ANTES de correr el script del DROP
-- (si no, ese correo se pierde).
--
-- SELECT pr.project_id,
--        pr.project_description AS proyecto,
--        pr.email_coord_admin,
--        pr.active,
--        (SELECT count(*) FROM workers w
--          WHERE lower(btrim(w.email_corporativo)) = lower(btrim(pr.email_coord_admin)))
--            AS trabajadores_que_coinciden
-- FROM project pr
-- WHERE nullif(btrim(pr.email_coord_admin), '') IS NOT NULL
--   AND pr.workers_coord_admin_id IS NULL
-- ORDER BY pr.project_description;
--
-- Verificación 2 — resultado de la migración (debe dar 27 filas en prod).
--
-- SELECT pr.project_description AS proyecto,
--        pr.email_coord_admin   AS correo_viejo,
--        p.full_name            AS coordinador_administrativo,
--        w.email_corporativo    AS correo_nuevo
-- FROM project pr
-- JOIN workers w ON w.id = pr.workers_coord_admin_id
-- LEFT JOIN person p ON p.person_id = w.person_id
-- ORDER BY pr.project_description;
-- ============================================================================
