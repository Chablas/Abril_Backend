-- SOLO LECTURA. Enumera TODOS los trabajadores Staff/Oficina Central cuyo
-- ítem Vida Ley (ss_hab_trabajador) sigue apuntando a uno de los archivos de
-- "lote" ya confirmados como mal cargados (el mismo documento SCTR subido
-- como Vida Ley para todos los trabajadores de una empresa en un solo
-- batch). Es la lista completa antes de decidir cuáles ya se corrigieron y
-- cuáles faltan — sin depender de heurística de nombre, que ya demostró
-- fallar (caso Comeca Loja).
--
-- Actualiza la lista de archivos_lote si en la revisión visual aparecen más
-- (uno por empresa, el que se repite para todos sus trabajadores el mismo
-- día/mes).

SELECT
    per.document_identity_code AS dni,
    per.full_name              AS nombre,
    c.contributor_name         AS empresa,
    CASE w.obra_oficina_staff_id WHEN 1 THEN 'Obra' WHEN 2 THEN 'Staff' WHEN 3 THEN 'Oficina Central' WHEN 4 THEN 'Personal Externo' END AS obra_oficina,
    h.id                        AS hab_trabajador_id,
    h.archivo_url               AS archivo_actual
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person per ON per.person_id = w.person_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
JOIN ss_item_trabajador it ON it.id = h.item_id AND it.nombre ILIKE '%Vida%' AND it.es_sctr_vidaley = true
WHERE w.obra_oficina_staff_id IN (2, 3)
  AND h.archivo_url IN (
    'habilitacion/sctr/20260901_AQUITANIA.pdf',
    'habilitacion/sctr/20260901_BAHIA_DE_ORO0.pdf',
    'habilitacion/sctr/20260901_CONTANCIA_RENOVACION_2026.202.pdf',
    'habilitacion/sctr/20260901_Renovacion_2026.2027.pdf',
    'habilitacion/sctr/20260805_CONSTANCIA._2026.2027.pdf',
    'habilitacion/sctr/20260710_constancia_julio_2026.pdf',
    'habilitacion/sctr/20260831_Contancia_13944983_inclusion_31.08.pdf',
    'habilitacion/sctr/20260804_136735_(1)_seshat_2026.pdf'
  )
ORDER BY empresa, nombre;
