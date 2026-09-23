-- Agrega el entregable "Certificado de operador" para trabajadores cuyo puesto
-- pertenece a la categoria Operador (CategoriaIds.Operador = 21).
-- Aplica a todos (Abril y contratista) sin restriccion obra/oficina.
--
-- No requiere migracion EF: ss_item_trabajador es tabla de configuracion,
-- leida en runtime por HabTrabajadorRepository.InicializarEntregablesAsync.
--
-- Ejecutar manualmente en pgAdmin.

INSERT INTO ss_item_trabajador (
    id,
    nombre,
    aplica_a,
    aplica_categoria,
    aplica_obra_oficina,
    excluye_obra_oficina,
    excluye_categoria_contratista,
    responsable,
    requiere_vigencia,
    es_sctr_vidaley,
    orden,
    activo
)
SELECT
    (SELECT COALESCE(MAX(id), 0) + 1 FROM ss_item_trabajador),
    'Certificado de operador',
    'TODOS',
    (SELECT UPPER(nombre) FROM categoria WHERE categoria_id = 21),
    NULL,
    NULL,
    NULL,
    'SSOMA',
    true,
    false,
    (SELECT COALESCE(MAX(orden), 0) + 1 FROM ss_item_trabajador),
    true
WHERE NOT EXISTS (
    SELECT 1 FROM ss_item_trabajador WHERE UPPER(nombre) = 'CERTIFICADO DE OPERADOR'
);

-- Verificacion rapida despues de correr el INSERT:
-- SELECT * FROM ss_item_trabajador WHERE nombre = 'Certificado de operador';
