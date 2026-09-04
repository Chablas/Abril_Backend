-- ============================================================================
-- 2026-09-02 · El nombre del puesto se repite si cambia la CATEGORÍA
--
-- Pantalla: /gestion-gth/configuracion/categorias-puestos (pestaña Puestos,
--           modal Nuevo / Editar puesto)
--
-- ── Por qué ─────────────────────────────────────────────────────────────────
-- `ux_puesto_nombre_area_solicitante_vivo` sólo mira (nombre, área que puede
-- pedirlo), así que dos puestos con el mismo nombre en la misma área choca
-- aunque sean de categorías distintas — y esa repetición es legítima: MODELADOR
-- BIM existe como INGENIERO y como ARQUITECTO, son dos cargos distintos que
-- Unidad de Proyectos pide por separado. Guardar el segundo daba «Ya existe un
-- puesto con ese nombre en esa área.» sin que hubiera nada duplicado.
--
-- La regla pasa a ser (nombre, categoría, área que puede pedirlo): el nombre se
-- puede repetir si cambia la categoría O si cambia el área, y se bloquea sólo
-- cuando coinciden las tres cosas — que ahí sí es la misma fila dos veces.
--
-- La CATEGORÍA entra porque es lo que de verdad distingue dos cargos con el
-- mismo nombre (es de donde sale la categoría de cada ficha:
-- `workers.puesto_id → puesto.categoria_id`).
--
-- El ÁREA se queda porque es lo que filtra el desplegable de Solicitud de
-- Personal (`ReclutamientoRepository.QueryPuestosDelArea`): un cargo que piden
-- dos áreas necesita una fila por área, y hoy hay 3 pares así, vivos y con
-- fichas, desde el corte del 2026-08-25 (ARQUITECTO DE PROYECTOS en
-- Arquitectura y en Unidad de Proyectos, ASISTENTE ADMINISTRATIVO en
-- Administración y en Gerencia General, CHOFER en Logística y en Gerencia
-- General). Sacar el área de la regla los volvería ilegales y obligaría a
-- fusionarlos, moviendo fichas de área.
--
-- El área de DESTINO sigue fuera: dos puestos distintos pueden mandar a la
-- misma área (INGENIERO y ASISTENTE DE PRODUCCIÓN van los dos a Producción).
--
-- `NULLS NOT DISTINCT` (PG 15+) se mantiene por el bolsón «Sin área»: los ~190
-- puestos de obra no tienen área solicitante y sin eso cada NULL contaría como
-- distinto y entrarían diez ALMACENERO sueltos de la misma categoría.
--
-- ── Riesgo ──────────────────────────────────────────────────────────────────
-- Ninguno sobre la data: el índice nuevo sólo RELAJA al viejo (agrega una
-- columna a la llave), así que toda fila que hoy pasa el viejo pasa el nuevo.
-- Por eso no hay nada que limpiar antes.
--
-- Se crea el nuevo ANTES de bajar el viejo: en ningún momento la tabla queda
-- sin índice único.
--
-- Re-corrible: `IF NOT EXISTS` / `IF EXISTS` en las dos sentencias.
-- ============================================================================

BEGIN;

-- ── Guarda: que no haya ya un trío repetido ────────────────────────────────
-- Con el índice viejo puesto esto es imposible, pero si el script se corre en
-- una base donde ese índice no estaba (dev desalineada), mejor abortar con un
-- mensaje legible que con un 23505 que no dice qué filas son.
DO $$
DECLARE
    v_malos text;
BEGIN
    SELECT string_agg(
               format('%L / categoría %s / área %s → puestos %s',
                      d.nombre, d.categoria_id,
                      coalesce(d.area_solicitante_scope_id::text, 'SIN ÁREA'),
                      d.ids),
               '; ' ORDER BY d.nombre)
    INTO v_malos
    FROM (
        SELECT nombre, categoria_id, area_solicitante_scope_id,
               array_agg(puesto_id ORDER BY puesto_id)::text AS ids
        FROM puesto
        WHERE state
        GROUP BY nombre, categoria_id, area_solicitante_scope_id
        HAVING count(*) > 1
    ) AS d;

    IF v_malos IS NOT NULL THEN
        RAISE EXCEPTION 'Hay puestos vivos repetidos en (nombre, categoría, área): %. Resolverlos antes de crear el índice.', v_malos;
    END IF;
END $$;

-- ── Índice nuevo ───────────────────────────────────────────────────────────
CREATE UNIQUE INDEX IF NOT EXISTS ux_puesto_nombre_categoria_area_solicitante_vivo
    ON puesto (nombre, categoria_id, area_solicitante_scope_id) NULLS NOT DISTINCT
 WHERE state;

-- ── Baja del viejo ─────────────────────────────────────────────────────────
-- Ya no aporta nada: el nuevo cubre todo lo que éste prohibía menos justo el
-- caso que había que permitir.
DROP INDEX IF EXISTS ux_puesto_nombre_area_solicitante_vivo;

COMMIT;

-- ============================================================================
-- Verificación: sólo debe quedar el índice único nuevo (1 fila, y ninguna con
-- el nombre viejo).
--
-- SELECT indexname, indexdef
-- FROM pg_indexes
-- WHERE schemaname = 'public' AND tablename = 'puesto' AND indexdef ILIKE '%UNIQUE%'
-- ORDER BY indexname;
-- ============================================================================
