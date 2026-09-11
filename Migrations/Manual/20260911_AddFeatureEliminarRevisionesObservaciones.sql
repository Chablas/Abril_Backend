-- Migración manual (pgAdmin) — feature para eliminar observaciones de Revisiones
-- (botón de eliminar en la lista, pedido para uso muy restringido). A propósito
-- NO se otorga a ningún rol acá: el admin decide quién lo tiene desde
-- Configuración > Roles y Permisos (ej. asignarlo solo al rol de Almendra Huaripoma).

INSERT INTO feature (feature_key, module_id)
SELECT 'arquitectura-comercial.revisiones.eliminar', module_id
FROM feature
WHERE feature_key = 'arquitectura-comercial.revisiones'
  AND NOT EXISTS (SELECT 1 FROM feature WHERE feature_key = 'arquitectura-comercial.revisiones.eliminar');
