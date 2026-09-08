-- SOLO LECTURA. Busca el historial completo de versiones subidas al ítem
-- "Vida ley" de los trabajadores de Aquitania Inmobiliaria S.A.C, para ver si
-- en algún momento se cargó el documento Vida Ley correcto (D.Leg. 688) antes
-- de que quedara sobrescrito por el archivo SCTR (Ley 26790) — confirmado a
-- mano por el usuario para COMECA LOJA JOSSELYN ADELITA.
--
-- ss_hab_documento_version guarda TODAS las versiones (no solo la vigente),
-- así que si el archivo correcto se subió y luego se reemplazó, debe
-- aparecer acá con un número de versión anterior al actual.

SELECT
    p.document_identity_code AS dni,
    p.full_name              AS nombre,
    h.id                     AS hab_trabajador_id,
    h.estado                 AS estado_actual,
    h.archivo_url            AS archivo_actual,
    v.version,
    v.archivo_url            AS archivo_version,
    v.estado_al_subir,
    v.created_at             AS fecha_subida,
    v.subido_por_user_id
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN worker_vinculaciones vinc ON vinc.worker_id = w.id AND vinc.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = vinc.empresa_id
JOIN ss_item_trabajador it ON it.id = h.item_id AND it.nombre ILIKE '%Vida%' AND it.es_sctr_vidaley = true
LEFT JOIN ss_hab_documento_version v ON v.hab_trabajador_id = h.id
WHERE c.contributor_name ILIKE '%Aquitania%'
ORDER BY nombre, v.version;
