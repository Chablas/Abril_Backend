-- ============================================================================
-- 2026-09-02 · Área de destino de los 9 puestos de oficina que no la tenían
--
-- Pantalla: /habilitacion/gestion/trabajadores (modal Nuevo / Editar trabajador)
--
-- ── Por qué ─────────────────────────────────────────────────────────────────
-- El modal de trabajadores ya NO deja elegir el área: en Staff, Oficina Central
-- y Personal Externo la muestra de solo lectura y la deriva del puesto, de su
-- `area_destino_scope_id` (el área a la que ENTRA quien lo ejerce, no la que
-- puede PEDIRLO — ver 2026-08-25_puesto_area_solicitante_y_destino.sql).
--
-- Con eso, un puesto de oficina sin área de destino deja el campo Área vacío y
-- bloquea el alta (Área es obligatoria al crear Staff / Oficina Central). Son 9
-- puestos, con 10 fichas vivas entre todos. El padrón de GTH («Grupo Ocupacional
-- - Abril.xlsx») simplemente no los alcanzó a mapear; no hay ninguna decisión
-- que tomar, porque el área que se les asigna es la que YA tienen las fichas que
-- hoy los ejercen, y todas coinciden (SUBGERENTE DE FINANZAS tiene 2 fichas y
-- las dos están en Finanzas).
--
-- El área solicitante NO se toca: quién puede pedir el puesto es otra decisión
-- y no se deduce de esto. Los ~190 puestos de obra tampoco se tocan — Obra no
-- gestiona área en el modal y sigue funcionando igual que antes.
--
-- Los ids van explícitos (leídos de PRODUCCIÓN, no de dev) porque el nombre del
-- área no identifica un nodo: «Producción» existe en dos ramas del árbol, y de
-- las dos la que usan las fichas es la 76.
--
-- Esto es DATA, no esquema: lo mismo se puede hacer a mano desde
-- Gestión GTH → Configuración → Categorías y Puestos. El script es para no
-- repetir 9 veces lo mismo.
--
-- Re-corrible: solo llena el destino de los puestos que lo tienen en NULL.
-- ============================================================================

BEGIN;

-- ── Guarda: cada id tiene que ser lo que este script cree que es ───────────
-- Si un puesto se eliminó/renombró o un nodo del árbol se movió, aborta sin
-- tocar nada en vez de dejar la mitad hecha o mandar gente al área equivocada.
DO $$
DECLARE
    v_malos text;
BEGIN
    SELECT string_agg(format('puesto %s (esperado %L)', x.puesto_id, x.nombre), '; ' ORDER BY x.puesto_id)
    INTO v_malos
    FROM (VALUES
        (7,   'ADMINISTRACIÓN'),
        (44,  'ASISTENTE DE CAMPO'),
        (112, 'COORDINADOR DE POST-VENTA'),
        (122, 'DISEÑADOR GRÁFICO'),
        (125, 'DISEÑADOR GRAFICO SR'),
        (220, 'OFICINA TÉCNICA'),
        (275, 'PRACTICANTE DE GTH'),
        (276, 'PRACTICANTE LEGAL'),
        (296, 'SUBGERENTE DE FINANZAS')
    ) AS x(puesto_id, nombre)
    WHERE NOT EXISTS (
        SELECT 1 FROM puesto p
        WHERE p.puesto_id = x.puesto_id
          AND p.state
          AND upper(btrim(p.nombre)) = upper(x.nombre)
    );

    IF v_malos IS NOT NULL THEN
        RAISE EXCEPTION 'El padrón de puestos cambió: %. Revisar antes de correr.', v_malos;
    END IF;

    SELECT string_agg(format('area_scope %s (esperado %L)', x.area_scope_id, x.nombre), '; ' ORDER BY x.area_scope_id)
    INTO v_malos
    FROM (VALUES
        (46, 'Costos y Presupuestos'),
        (52, 'Post Venta'),
        (56, 'Contabilidad'),
        (57, 'Finanzas'),
        (58, 'Gestión del Talento Humano'),
        (59, 'Legal'),
        (75, 'Marketing'),
        (76, 'Producción')
    ) AS x(area_scope_id, nombre)
    WHERE NOT EXISTS (
        SELECT 1
        FROM area_scope s
        JOIN area_item i ON i.area_item_id = s.area_item_id
        WHERE s.area_scope_id = x.area_scope_id
          AND s.state AND i.state
          AND upper(btrim(i.area_item_name)) = upper(x.nombre)
    );

    IF v_malos IS NOT NULL THEN
        RAISE EXCEPTION 'El árbol de áreas cambió: %. Asignar el destino a mano.', v_malos;
    END IF;
END $$;

-- ── Asignación del área de destino ─────────────────────────────────────────
UPDATE puesto p
SET area_destino_scope_id = m.area_scope_id,
    updated_date_time     = now()
FROM (VALUES
    (7,   56),  -- ADMINISTRACIÓN            → Contabilidad
    (44,  76),  -- ASISTENTE DE CAMPO        → Producción
    (112, 52),  -- COORDINADOR DE POST-VENTA → Post Venta
    (122, 75),  -- DISEÑADOR GRÁFICO         → Marketing
    (125, 75),  -- DISEÑADOR GRAFICO SR      → Marketing
    (220, 46),  -- OFICINA TÉCNICA           → Costos y Presupuestos
    (275, 58),  -- PRACTICANTE DE GTH        → Gestión del Talento Humano
    (276, 59),  -- PRACTICANTE LEGAL         → Legal
    (296, 57)   -- SUBGERENTE DE FINANZAS    → Finanzas
) AS m(puesto_id, area_scope_id)
WHERE p.puesto_id = m.puesto_id
  AND p.state
  AND p.area_destino_scope_id IS NULL;

COMMIT;

-- ============================================================================
-- Verificación: NINGUNA ficha viva de Staff / Oficina Central / Personal
-- Externo debe quedar con un puesto sin área de destino (0 filas).
--
-- SELECT pu.puesto_id, pu.nombre AS puesto, count(*) AS fichas
-- FROM workers w
-- JOIN workers_obra_oficina_staff s ON s.workers_obra_oficina_staff_id = w.obra_oficina_staff_id
-- JOIN puesto pu ON pu.puesto_id = w.puesto_id
-- WHERE w.state
--   AND s.name IN ('Staff', 'Oficina Central', 'Personal Externo')
--   AND pu.area_destino_scope_id IS NULL
-- GROUP BY 1, 2
-- ORDER BY 3 DESC, 2;
-- ============================================================================
