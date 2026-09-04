-- Adjudicaciones · paso 3: poder eliminar un archivo del expediente sin borrarlo de verdad.
--
-- Cada documento (contrato, hoja resumen, ficha técnica, anexos, …) vive en su propia tabla
-- y ya tiene `state`, pero NO sabe a qué adjudicación pertenece: el vínculo existe solo en
-- project_sub_contractor.<documento>_id. Al eliminar un documento esa FK se limpia (para que
-- la pantalla deje de mostrarlo) y la fila quedaría huérfana, sin rastro de dónde estuvo.
--
-- Por eso se agrega la vuelta project_sub_contractor_id en cada tabla de documento: la fila
-- eliminada (state = false) sigue siendo auditable y el archivo en OneDrive nunca se toca.
--
-- Idempotente: se puede correr más de una vez sin efecto.

DO $$
DECLARE
    t        text;
    doc_col  text;
    fk_name  text;
    ix_name  text;
    tablas   text[] := ARRAY[
        'project_sub_contractor_contract',
        'project_sub_contractor_summary_sheet',
        'project_sub_contractor_budget',
        'project_sub_contractor_schedule',
        'project_sub_contractor_attached_quotation',
        'project_sub_contractor_service_order',
        'project_sub_contractor_promissory_note',
        'project_sub_contractor_package',
        'project_sub_contractor_instructivo',
        'project_sub_contractor_non_conforming_output',
        'project_sub_contractor_tolerance_chart',
        'project_sub_contractor_ficha_tecnica',
        'project_sub_contractor_anexo'
    ];
BEGIN
    FOREACH t IN ARRAY tablas LOOP
        -- La FK que project_sub_contractor usa para apuntar al documento se llama igual que la PK.
        doc_col := t || '_id';
        fk_name := 'fk_' || t || '_psc';
        ix_name := 'ux_' || t || '_vigente';

        EXECUTE format(
            'ALTER TABLE %I ADD COLUMN IF NOT EXISTS project_sub_contractor_id integer', t);

        IF NOT EXISTS (
            SELECT 1 FROM pg_constraint WHERE conrelid = t::regclass AND conname = fk_name
        ) THEN
            EXECUTE format(
                'ALTER TABLE %I ADD CONSTRAINT %I FOREIGN KEY (project_sub_contractor_id) '
                || 'REFERENCES project_sub_contractor(project_sub_contractor_id)', t, fk_name);
        END IF;

        -- Backfill: la adjudicación que hoy apunta a cada documento. Las filas sueltas (que
        -- ninguna adjudicación referencia) quedan en NULL — no hay de dónde deducir el dueño.
        EXECUTE format(
            'UPDATE %I d SET project_sub_contractor_id = p.project_sub_contractor_id '
            || 'FROM project_sub_contractor p '
            || 'WHERE p.%I = d.%I AND d.project_sub_contractor_id IS NULL', t, doc_col, doc_col);

        -- Un solo documento vigente por adjudicación; los eliminados (state = false) se
        -- acumulan sin chocar.
        EXECUTE format(
            'CREATE UNIQUE INDEX IF NOT EXISTS %I ON %I (project_sub_contractor_id) WHERE state',
            ix_name, t);
    END LOOP;
END $$;

-- Verificación: cuántos documentos quedaron sin adjudicación (deberían ser filas sueltas
-- antiguas; si alguna está referenciada por una adjudicación, el backfill falló).
SELECT 'project_sub_contractor_contract'              AS tabla, count(*) AS sin_adjudicacion FROM project_sub_contractor_contract              WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_summary_sheet',        count(*) FROM project_sub_contractor_summary_sheet         WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_budget',               count(*) FROM project_sub_contractor_budget                WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_schedule',             count(*) FROM project_sub_contractor_schedule              WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_attached_quotation',   count(*) FROM project_sub_contractor_attached_quotation    WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_service_order',        count(*) FROM project_sub_contractor_service_order         WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_promissory_note',      count(*) FROM project_sub_contractor_promissory_note       WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_package',              count(*) FROM project_sub_contractor_package               WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_instructivo',          count(*) FROM project_sub_contractor_instructivo           WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_non_conforming_output',count(*) FROM project_sub_contractor_non_conforming_output WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_tolerance_chart',      count(*) FROM project_sub_contractor_tolerance_chart       WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_ficha_tecnica',        count(*) FROM project_sub_contractor_ficha_tecnica         WHERE project_sub_contractor_id IS NULL
UNION ALL SELECT 'project_sub_contractor_anexo',                count(*) FROM project_sub_contractor_anexo                 WHERE project_sub_contractor_id IS NULL;
