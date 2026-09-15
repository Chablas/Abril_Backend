-- SOLO LECTURA. Estado actual (en vivo) de hab_trabajador_id 212283, y su
-- historial de versiones, para ver si algo/alguien lo tocó después de
-- nuestra corrección (que dejó vigencia=2027-01-31, updated_at ~23:46 del
-- 2026-09-07).

SELECT id, worker_id, item_id, estado, vigencia, archivo_url, updated_at
FROM ss_hab_trabajador
WHERE id = 212283;

SELECT id, hab_trabajador_id, version, archivo_url, estado_al_subir,
       estado_anterior, subido_por_user_id, aprobado_por_user_id,
       motivo_rechazo, created_at
FROM ss_hab_documento_version
WHERE hab_trabajador_id = 212283
ORDER BY version DESC;
