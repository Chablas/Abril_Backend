-- Diagnóstico: trabajadores cuyo Certificado de Aptitud (item 4) quedó en "Falta" en
-- ss_hab_trabajador aunque su último EMO activo ya está Apto/Vigente y sin interconsulta
-- pendiente (bug: InterconsultaRepository.UpdateResultado marcaba InterconsultaResuelta=true
-- pero nunca volvía a correr EmoRepository.SincronizarEntregableEmoAsync).
SELECT h.id AS hab_id, h.worker_id, p.document_identity_code AS dni, p.full_name AS nombre,
       h.estado AS estado_hab, e.id AS emo_id, e.aptitud, e.estado AS estado_emo,
       e.requiere_interconsulta, e.interconsulta_resuelta,
       COALESCE(e.fecha_vencimiento_calculada, e.fecha_vencimiento) AS vigencia_emo
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
JOIN ss_item_trabajador it ON it.id = h.item_id
JOIN LATERAL (
    SELECT *
    FROM worker_emos we
    WHERE we.worker_id = h.worker_id AND we.activo
    ORDER BY we.fecha_emo DESC
    LIMIT 1
) e ON true
WHERE it.id = 4
  AND h.estado = 'Falta'
  AND e.aptitud IN ('Apto', 'Apto con Restricciones')
  AND NOT (e.requiere_interconsulta = true AND e.interconsulta_resuelta = false)
  AND NOT EXISTS (
      SELECT 1 FROM ss_interconsultas si
      WHERE si.emo_id = e.id AND si.estado = 'Pendiente'
  )
  -- CertAptitud tampoco se aprueba directo si hubo un cambio de empresa/puesto/riesgo
  -- posterior al EMO sin convalidar todavía (ver EmoRepository.SincronizarEntregableEmoAsync) —
  -- esos casos quedan fuera de este diagnóstico a propósito: revisar aparte si aparecen.
ORDER BY h.updated_at DESC;
