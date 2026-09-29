-- Seed de datos para la vista "hitos de propietarios" (ver OwnerMilestone.cs / GetOwnerMilestonesByHistoryIdAsync).
-- Requiere que la tabla owner_milestone ya exista (migración EF AddOwnerMilestone /
-- Migrations/20260928194647_AddOwnerMilestone.cs).
--
-- created_user_id = 23 (vcolonio@abril.pe), quien corrió este script (D3: acá se hardcodea porque
-- es la persona que lo corre, no un feature_id que difiera entre entornos).

-- 1) Dos hitos nuevos en el catálogo interno (milestone), idempotente por descripción.
INSERT INTO milestone (milestone_description, es_obligatorio, es_puntual, active, state, created_date_time, created_user_id)
SELECT 'Inicio de demolición', false, true, true, true, now(), 23
WHERE NOT EXISTS (SELECT 1 FROM milestone WHERE milestone_description = 'Inicio de demolición');

INSERT INTO milestone (milestone_description, es_obligatorio, es_puntual, active, state, created_date_time, created_user_id)
SELECT 'Energización definitiva del edificio', false, true, true, true, now(), 23
WHERE NOT EXISTS (SELECT 1 FROM milestone WHERE milestone_description = 'Energización definitiva del edificio');

-- 2) Los 9 hitos de propietarios, cada uno apuntando (por SELECT, no por id hardcodeado — D3) al
--    hito interno del que toma PlannedStartDate/PlannedEndDate. Ver tabla de mapeo acordada en la
--    conversación de diseño.
INSERT INTO owner_milestone (description, milestone_id, owner_milestone_order, active, state, created_date_time, created_user_id)
SELECT 'Inicio de demolición', m.milestone_id, 1, true, true, now(), 23
FROM milestone m WHERE m.milestone_description = 'Inicio de demolición'
AND NOT EXISTS (SELECT 1 FROM owner_milestone WHERE description = 'Inicio de demolición');

INSERT INTO owner_milestone (description, milestone_id, owner_milestone_order, active, state, created_date_time, created_user_id)
SELECT 'Bendición e inicio de obra', m.milestone_id, 2, true, true, now(), 23
FROM milestone m WHERE m.milestone_description = 'Inicio de obra'
AND NOT EXISTS (SELECT 1 FROM owner_milestone WHERE description = 'Bendición e inicio de obra');

INSERT INTO owner_milestone (description, milestone_id, owner_milestone_order, active, state, created_date_time, created_user_id)
SELECT 'Inicio de casco estructural', m.milestone_id, 3, true, true, now(), 23
FROM milestone m WHERE m.milestone_description = 'Cimentaciones (perimetrales + zapatas) + Cisterna'
AND NOT EXISTS (SELECT 1 FROM owner_milestone WHERE description = 'Inicio de casco estructural');

INSERT INTO owner_milestone (description, milestone_id, owner_milestone_order, active, state, created_date_time, created_user_id)
SELECT 'Casco concluido', m.milestone_id, 4, true, true, now(), 23
FROM milestone m WHERE m.milestone_description = 'Fin Casco'
AND NOT EXISTS (SELECT 1 FROM owner_milestone WHERE description = 'Casco concluido');

INSERT INTO owner_milestone (description, milestone_id, owner_milestone_order, active, state, created_date_time, created_user_id)
SELECT 'Inicio de acabados', m.milestone_id, 5, true, true, now(), 23
FROM milestone m WHERE m.milestone_description = 'Acabados húmedos Torre'
AND NOT EXISTS (SELECT 1 FROM owner_milestone WHERE description = 'Inicio de acabados');

INSERT INTO owner_milestone (description, milestone_id, owner_milestone_order, active, state, created_date_time, created_user_id)
SELECT 'Energización definitiva del edificio', m.milestone_id, 6, true, true, now(), 23
FROM milestone m WHERE m.milestone_description = 'Energización definitiva del edificio'
AND NOT EXISTS (SELECT 1 FROM owner_milestone WHERE description = 'Energización definitiva del edificio');

INSERT INTO owner_milestone (description, milestone_id, owner_milestone_order, active, state, created_date_time, created_user_id)
SELECT 'Pruebas de instalaciones', m.milestone_id, 7, true, true, now(), 23
FROM milestone m WHERE m.milestone_description = 'Acabados secos - Torre'
AND NOT EXISTS (SELECT 1 FROM owner_milestone WHERE description = 'Pruebas de instalaciones');

INSERT INTO owner_milestone (description, milestone_id, owner_milestone_order, active, state, created_date_time, created_user_id)
SELECT 'Implementación de áreas comunes', m.milestone_id, 8, true, true, now(), 23
FROM milestone m WHERE m.milestone_description = 'Acabados áreas comunes'
AND NOT EXISTS (SELECT 1 FROM owner_milestone WHERE description = 'Implementación de áreas comunes');

INSERT INTO owner_milestone (description, milestone_id, owner_milestone_order, active, state, created_date_time, created_user_id)
SELECT 'Edificio concluido', m.milestone_id, 9, true, true, now(), 23
FROM milestone m WHERE m.milestone_description = 'Fin de Obra'
AND NOT EXISTS (SELECT 1 FROM owner_milestone WHERE description = 'Edificio concluido');

-- Verificación de solo lectura después de correr (D1):
-- SELECT * FROM milestone WHERE milestone_description IN ('Inicio de demolición', 'Energización definitiva del edificio');
-- SELECT om.owner_milestone_order, om.description, m.milestone_description AS hito_interno
-- FROM owner_milestone om JOIN milestone m ON m.milestone_id = om.milestone_id
-- ORDER BY om.owner_milestone_order;
