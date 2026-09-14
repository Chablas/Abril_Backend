-- ============================================================================
-- Gestión Administrativa · Salidas — Destinatario de correo por ROL
-- Fecha: 2026-09-14
--
-- Hasta hoy un destinatario configurado solo podía ser un trabajador concreto,
-- un área o un correo escrito a mano. El aviso «Reembolso firmado · a
-- Tesorería» no usaba ninguno de los tres: sus destinatarios salían
-- HARDCODEADOS del rol TESORERO, así que en la pantalla de Configuración
-- aparecía una sola fila «Sistema» y no se podía ni ver ni tocar quién más
-- entraba.
--
-- Se agrega un cuarto tipo, ROL: la fila apunta a un rol y al enviar se expande
-- a los correos corporativos de TODOS los que lo tengan ese día. Es el tipo
-- para los destinatarios que son «quien haga ese trabajo» (Tesorería, el
-- Coordinador ERP) y que no se pueden fijar a un nombre porque ese nombre
-- cambia sin que nadie se acuerde de venir a esta pantalla.
--
-- Con esto, «Aviso a Tesorería» queda con dos interruptores independientes:
--   • el TESORERO  → el destinatario principal (fila Sistema, rol TESORERO)
--   • el COORDINADOR ERP → una fila ROL normal, que se prende, se apaga, se
--     edita y se elimina como cualquier otro destinatario.
--
-- Idempotente: se puede correr varias veces. Aplicar en dev y prod.
-- ============================================================================

BEGIN;

-- ── 1. El tipo nuevo en el catálogo ─────────────────────────────────────────

INSERT INTO ga_correo_tipo_destinatario (codigo, nombre, orden, active, state)
SELECT 'ROL', 'Rol', 4, true, true
WHERE NOT EXISTS (SELECT 1 FROM ga_correo_tipo_destinatario WHERE codigo = 'ROL');

COMMENT ON TABLE ga_correo_tipo_destinatario IS
  'Tipos de destinatario de ga_correo_regla: TRABAJADOR (un worker), AREA (los miembros de un nodo area_scope), CORREO (una direccion escrita a mano) y ROL (todos los que hoy tengan ese rol; se resuelve por cargo, no por persona).';

-- ── 2. La columna en la regla ───────────────────────────────────────────────

ALTER TABLE ga_correo_regla
    ADD COLUMN IF NOT EXISTS role_id integer NULL;

COMMENT ON COLUMN ga_correo_regla.role_id IS
  'Rol (role.role_id) cuando el tipo es ROL. NULL en los otros tipos. Al enviar se expande a los email_corporativo de todos los que tengan ese rol activo.';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ga_correo_regla_role_id_fkey') THEN
        ALTER TABLE ga_correo_regla
            ADD CONSTRAINT ga_correo_regla_role_id_fkey
            FOREIGN KEY (role_id) REFERENCES role(role_id);
    END IF;
END $$;

-- El CHECK sigue exigiendo que la fila apunte a UNA sola cosa, ahora contando
-- también role_id: una regla con worker_id Y role_id seria ambigua al enviar.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_ga_correo_regla_target') THEN
        ALTER TABLE ga_correo_regla DROP CONSTRAINT chk_ga_correo_regla_target;
    END IF;

    ALTER TABLE ga_correo_regla
        ADD CONSTRAINT chk_ga_correo_regla_target
        CHECK (
            (CASE WHEN worker_id     IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN area_scope_id IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN correo        IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN role_id       IS NOT NULL THEN 1 ELSE 0 END) = 1
        );
END $$;

-- ── 3. El aviso a Tesorería: etiqueta del principal ─────────────────────────
-- El principal dejó de exigir la categoría Tesorero del puesto hace tiempo (hoy
-- basta el rol TESORERO, el mismo requisito que abre la bandeja de Reembolsos),
-- pero la etiqueta seguía diciendo lo viejo y hacía dudar de a quién le llega.

UPDATE ga_correo_evento
   SET destinatario_principal_nombre = 'Tesorería (rol TESORERO)',
       descripcion = 'Avisa a Tesorería que una planilla quedó firmada por la jefatura y su reembolso '
                     'entró a la bandeja de pago. El destinatario principal son los que tienen el rol '
                     'TESORERO; los demás se agregan acá como destinatarios (por rol, área, '
                     'trabajador o correo).',
       updated_at  = now()
 WHERE codigo = 'TESORERIA_REEMBOLSO';

-- ── 4. El Coordinador ERP como destinatario del aviso a Tesorería ───────────
-- Entra ACTIVO: hoy ya le llega (tiene el rol TESORERO asignado), y el punto
-- del cambio es que quede visible bajo su etiqueta correcta y con su propio
-- interruptor, no sacarlo por la puerta de atrás.

DO $$
DECLARE
    v_evento_id integer;
    v_tipo_id   integer;
    v_role_id   integer;
    v_orden     integer;
BEGIN
    SELECT id INTO v_evento_id FROM ga_correo_evento WHERE codigo = 'TESORERIA_REEMBOLSO' AND state;
    SELECT id INTO v_tipo_id   FROM ga_correo_tipo_destinatario WHERE codigo = 'ROL';

    -- Guarda: el id del rol se mira por su descripcion, no se asume el 84.
    SELECT role_id INTO v_role_id
      FROM role
     WHERE upper(btrim(role_description)) = 'COORDINADOR ERP' AND state;

    IF v_evento_id IS NULL THEN
        RAISE NOTICE 'No existe el correo TESORERIA_REEMBOLSO: no se siembra el destinatario.';
        RETURN;
    END IF;

    IF v_role_id IS NULL THEN
        RAISE NOTICE 'No existe el rol COORDINADOR ERP: no se siembra el destinatario.';
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1 FROM ga_correo_regla
        WHERE evento_id = v_evento_id AND role_id = v_role_id AND state
    ) THEN
        RETURN; -- ya sembrado
    END IF;

    SELECT COALESCE(MAX(orden), 0) + 1 INTO v_orden
      FROM ga_correo_regla WHERE evento_id = v_evento_id AND state;

    INSERT INTO ga_correo_regla
        (evento_id, tipo_id, role_id, incluir_descendientes, orden, active, state, created_at, updated_at)
    VALUES
        (v_evento_id, v_tipo_id, v_role_id, true, v_orden, true, true, now(), now());
END $$;

COMMIT;
