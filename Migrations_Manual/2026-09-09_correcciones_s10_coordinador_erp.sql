-- ============================================================================
-- Gestion de Salidas: bandeja "Correcciones S10" y rol COORDINADOR ERP
-- ============================================================================
-- Cierra el hueco del paso del medio de la subsanacion (seccion 10.5 del
-- requerimiento funcional). Hasta ahora, cuando la jefatura devolvia el
-- reembolso con una observacion, el unico camino era que el propio colaborador
-- volviera a adjuntar el Consolidado del S10 -- pero la correccion casi siempre
-- hay que hacerla DENTRO del S10, donde el colaborador no tiene permiso.
--
-- Ahora tiene dos caminos, y el segundo es el que faltaba:
--
--   jefatura OBSERVA
--        |
--        +--(a) el colaborador arregla el S10 el mismo --> recarga el consolidado
--        |
--        +--(b) el colaborador SOLICITA la correccion al Coordinador ERP
--                   |
--                   v
--             Pendiente de correccion S10   (la pelota esta en el ERP)
--                   |  el ERP corrige en el S10 y marca el check
--                   v
--             Pendiente de recarga S10      (la pelota vuelve al colaborador)
--                   |  recarga el consolidado
--                   v
--             el reembolso vuelve a Pendiente y la correccion se cierra
--
-- Abril One NO toca el S10: registra el pedido, el motivo y la confirmacion.
--
-- Ademas renombra el estado 3 del reembolso de "Rechazado" a "Observado": el id
-- NO cambia (lo usa EstadosSalida.Reembolso), solo el nombre, porque el flujo
-- nunca fue un rechazo -- siempre fue una observacion que se subsana.
--
-- Idempotente: se puede correr mas de una vez.
-- ============================================================================

-- El archivo esta en UTF-8 y trae texto acentuado. En Windows psql arranca con el
-- client_encoding del locale (WIN1252) y esos bytes se rechazan, asi que se fija aca:
-- el script queda correcto sin depender de como se invoque.
SET client_encoding TO 'UTF8';

BEGIN;

-- ── 1. El estado 3 del reembolso pasa a llamarse "Observado" ────────────────
-- Solo el nombre del catalogo. El backend compara por id, asi que no hay data
-- que migrar: las filas que estaban en 3 siguen en 3.

UPDATE ga_estado_reembolso
   SET descripcion = 'Observado'
 WHERE id = 3 AND descripcion <> 'Observado';

COMMENT ON TABLE ga_estado_reembolso IS
  'Estados del reembolso de una salida ya rendida con Consolidado del S10 adjunto: Pendiente -> Firmado -> Proceder con el reembolso -> Pagado, u Observado (con el comentario de la jefatura) hasta que el trabajador subsane.';

-- ── 2. Rol COORDINADOR ERP ──────────────────────────────────────────────────
-- Id fijo por diseno: lo usan Shared/Constants/Roles.cs (backend) y
-- core/constants/roles.ts (frontend), que son espejo uno del otro.
--
-- OJO: si el 84 ya estuviera ocupado en produccion, este INSERT falla en vez de
-- correrse a otro id. Eso es a proposito -- un id distinto al de las constantes
-- dejaria el rol sin efecto y sin ningun error visible.

INSERT INTO role (role_id, role_description, created_user_id, active, state)
SELECT 84, 'COORDINADOR ERP', 1, true, true
 WHERE NOT EXISTS (SELECT 1 FROM role WHERE role_id = 84);

-- La secuencia queda por encima de los ids sembrados a mano, o el proximo rol
-- creado desde el CRUD chocaria con este.
SELECT setval('role_role_id_seq', GREATEST((SELECT MAX(role_id) FROM role), 1));

-- ── 3. Feature de la pantalla y su permiso ──────────────────────────────────
-- module_id 10 = Gestion Administrativa (el mismo de las otras cinco pantallas).

INSERT INTO feature (feature_key, module_id)
SELECT 'gestion-administrativa.correcciones-s10', 10
 WHERE NOT EXISTS (
   SELECT 1 FROM feature WHERE feature_key = 'gestion-administrativa.correcciones-s10');

-- Solo el COORDINADOR ERP entra a la bandeja. El backend ademas lo exige contra
-- el token ([Authorize(Roles = Roles.CoordinadorErp)]), asi que agregar aca otro
-- rol le mostraria la pestana pero no le devolveria datos.
INSERT INTO role_feature (role_id, feature_id)
SELECT 84, f.feature_id
  FROM feature f
 WHERE f.feature_key = 'gestion-administrativa.correcciones-s10'
   AND NOT EXISTS (
     SELECT 1 FROM role_feature rf
      WHERE rf.role_id = 84 AND rf.feature_id = f.feature_id);

-- ── 4. Catalogo de estados de la correccion ─────────────────────────────────
-- Los dos estados que el requerimiento lista en 6.1. Describen QUIEN tiene que
-- actuar, no que paso: es lo que la pantalla muestra tal cual.
-- Ids fijos por diseno: los usa EstadosSalida.CorreccionS10 en el backend.

CREATE TABLE IF NOT EXISTS ga_estado_correccion_s10 (
    id          integer PRIMARY KEY,
    descripcion varchar(60) NOT NULL,
    orden       integer NOT NULL DEFAULT 0,
    activo      boolean NOT NULL DEFAULT true
);

COMMENT ON TABLE ga_estado_correccion_s10 IS
  'Estados de una solicitud de correccion del Consolidado del S10 al Coordinador ERP: Solicitada (pendiente de correccion) -> Atendida (pendiente de recarga por el colaborador).';

INSERT INTO ga_estado_correccion_s10 (id, descripcion, orden) VALUES
    (1, 'Pendiente de corrección S10', 1),
    (2, 'Pendiente de recarga S10',    2)
ON CONFLICT (id) DO UPDATE SET descripcion = EXCLUDED.descripcion, orden = EXCLUDED.orden;

-- ── 5. La solicitud de correccion ───────────────────────────────────────────
-- La unidad es la PLANILLA y no la salida, igual que el Consolidado del S10: el
-- documento que hay que corregir es uno solo y cubre todas las salidas que la
-- planilla agrupa.

CREATE TABLE IF NOT EXISTS ga_correccion_s10 (
    id                  serial PRIMARY KEY,
    rendicion_id        integer      NOT NULL,
    consolidado_s10_id  integer      NULL,
    motivo              varchar(2000) NOT NULL,
    motivo_jefatura     varchar(2000) NULL,
    numero_guia         varchar(60)  NULL,
    estado_id           integer      NOT NULL DEFAULT 1,
    solicitada_por_id   integer      NOT NULL,
    solicitada_at       timestamptz  NOT NULL DEFAULT now(),
    atendida_por_id     integer      NULL,
    atendida_at         timestamptz  NULL,
    comentario_atencion varchar(2000) NULL,
    guia_anulada        boolean      NOT NULL DEFAULT false,
    state               boolean      NOT NULL DEFAULT true,
    created_date_time   timestamptz  NOT NULL DEFAULT now(),
    updated_date_time   timestamptz  NULL
);

COMMENT ON TABLE ga_correccion_s10 IS
  'Solicitud de correccion del Consolidado del S10 al Coordinador ERP (seccion 10.5). Abril One no toca el S10: solo registra el pedido, el motivo y la confirmacion de que se atendio.';
COMMENT ON COLUMN ga_correccion_s10.motivo IS
  'El campo "MOTIVO *" del requerimiento (RG-21): lo que el colaborador le pide al ERP. Obligatorio.';
COMMENT ON COLUMN ga_correccion_s10.motivo_jefatura IS
  'Copia de la observacion con la que la jefatura devolvio el reembolso, tomada al solicitar. Se guarda porque la de la salida se sobrescribe si la jefatura vuelve a observar.';
COMMENT ON COLUMN ga_correccion_s10.numero_guia IS
  'Guia del consolidado observado, copiada al solicitar. Es el dato con el que el ERP ubica el registro en el S10.';
COMMENT ON COLUMN ga_correccion_s10.guia_anulada IS
  'True cuando el ERP anulo el registro en vez de corregirlo: hace falta una guia NUEVA y la anterior queda bloqueada al recargar el consolidado (HU-ERP-03 / CA-19).';
COMMENT ON COLUMN ga_correccion_s10.state IS
  'Soft delete. Pasa a false cuando el colaborador recarga el Consolidado del S10: la gestion termino y la planilla queda libre para pedir otra correccion. La fila se conserva para la auditoria (RF-TRZ-08/09).';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_ga_correccion_s10_rendicion') THEN
        ALTER TABLE ga_correccion_s10
            ADD CONSTRAINT fk_ga_correccion_s10_rendicion
            FOREIGN KEY (rendicion_id) REFERENCES ga_rendicion(id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_ga_correccion_s10_consolidado') THEN
        ALTER TABLE ga_correccion_s10
            ADD CONSTRAINT fk_ga_correccion_s10_consolidado
            FOREIGN KEY (consolidado_s10_id) REFERENCES ga_consolidado_s10(id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_ga_correccion_s10_estado') THEN
        ALTER TABLE ga_correccion_s10
            ADD CONSTRAINT fk_ga_correccion_s10_estado
            FOREIGN KEY (estado_id) REFERENCES ga_estado_correccion_s10(id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_ga_correccion_s10_solicitada_por') THEN
        ALTER TABLE ga_correccion_s10
            ADD CONSTRAINT fk_ga_correccion_s10_solicitada_por
            FOREIGN KEY (solicitada_por_id) REFERENCES app_user(user_id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_ga_correccion_s10_atendida_por') THEN
        ALTER TABLE ga_correccion_s10
            ADD CONSTRAINT fk_ga_correccion_s10_atendida_por
            FOREIGN KEY (atendida_por_id) REFERENCES app_user(user_id);
    END IF;

    -- El motivo es obligatorio de verdad: un NOT NULL con cadena vacia no serviria
    -- de nada al ERP, que es quien lo lee para saber que corregir (CA-17).
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_ga_correccion_s10_motivo') THEN
        ALTER TABLE ga_correccion_s10
            ADD CONSTRAINT chk_ga_correccion_s10_motivo CHECK (btrim(motivo) <> '');
    END IF;

    -- Atendida (2) exige quien y cuando; Solicitada (1) exige que no los tenga.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_ga_correccion_s10_atencion') THEN
        ALTER TABLE ga_correccion_s10
            ADD CONSTRAINT chk_ga_correccion_s10_atencion CHECK (
                (estado_id = 2 AND atendida_por_id IS NOT NULL AND atendida_at IS NOT NULL)
             OR (estado_id <> 2 AND atendida_por_id IS NULL AND atendida_at IS NULL)
            );
    END IF;
END $$;

-- Una correccion viva por planilla. Es un indice PARCIAL (solo state) para que
-- las cerradas se conserven: una planilla puede pasar por varias correcciones si
-- la jefatura vuelve a observar despues de la recarga.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_correccion_s10_rendicion_vigente
    ON ga_correccion_s10 (rendicion_id) WHERE state;

-- La bandeja del ERP lee por estado y ordena por antiguedad del pedido.
CREATE INDEX IF NOT EXISTS ix_ga_correccion_s10_bandeja
    ON ga_correccion_s10 (estado_id, solicitada_at) WHERE state;

-- ── 6. Los dos correos del paso ─────────────────────────────────────────────
-- Cada uno se administra desde la pantalla donde se ORIGINA: la solicitud sale
-- de Mis Rendiciones y el aviso de atencion, de la bandeja del ERP.

INSERT INTO ga_correo_pantalla (codigo, nombre, orden, active, state)
SELECT 'CORRECCIONES_S10', 'Correcciones S10', 6, true, true
 WHERE NOT EXISTS (SELECT 1 FROM ga_correo_pantalla WHERE codigo = 'CORRECCIONES_S10');

-- Al Coordinador ERP. Su destinatario principal se resuelve por ROL y no por el
-- organigrama del solicitante, asi que "permite_desactivar_principal" queda en
-- false: apagarlo dejaria la solicitud sin nadie que la vea.
INSERT INTO ga_correo_evento (
    codigo, nombre, descripcion, pantalla_id, orden, active, state,
    destinatario_principal_nombre, destinatario_principal_activo,
    permite_desactivar_envio, permite_desactivar_principal)
SELECT 'CORRECCION_S10_SOLICITADA',
       'Correccion del S10 solicitada - al Coordinador ERP',
       'Le avisa al Coordinador ERP que hay una correccion del Consolidado del S10 esperandolo, con la guia, la observacion de la jefatura y el motivo del colaborador.',
       (SELECT id FROM ga_correo_pantalla WHERE codigo = 'RENDICIONES'),
       14, true, true,
       'Coordinador ERP', true,
       false, false
 WHERE NOT EXISTS (SELECT 1 FROM ga_correo_evento WHERE lower(codigo) = lower('CORRECCION_S10_SOLICITADA'));

-- Al colaborador: el ERP ya corrigio y puede recargar el consolidado.
INSERT INTO ga_correo_evento (
    codigo, nombre, descripcion, pantalla_id, orden, active, state,
    destinatario_principal_nombre, destinatario_principal_activo,
    permite_desactivar_envio, permite_desactivar_principal)
SELECT 'CORRECCION_S10_ATENDIDA',
       'Correccion del S10 atendida - al solicitante',
       'Le avisa al colaborador que el Coordinador ERP ya hizo la correccion en el S10 y que puede recargar el Consolidado.',
       (SELECT id FROM ga_correo_pantalla WHERE codigo = 'CORRECCIONES_S10'),
       15, true, true,
       'Solicitante', true,
       true, true
 WHERE NOT EXISTS (SELECT 1 FROM ga_correo_evento WHERE lower(codigo) = lower('CORRECCION_S10_ATENDIDA'));

-- El correo del estado 3 cambia de nombre junto con el estado. El CODIGO no se
-- toca a proposito: es la clave del catalogo y renombrarla desconectaria la
-- configuracion de destinatarios que ya esta cargada.
UPDATE ga_correo_evento
   SET nombre = 'Reembolso observado - al solicitante'
 WHERE codigo = 'REEMBOLSO_RECHAZADO';

COMMIT;

-- ============================================================================
-- Verificacion (correr despues del COMMIT)
-- ============================================================================
-- SELECT * FROM ga_estado_reembolso ORDER BY id;                -- el 3 dice "Observado"
-- SELECT * FROM ga_estado_correccion_s10 ORDER BY id;           -- 2 filas
-- SELECT role_id, role_description FROM role WHERE role_id = 84;
-- SELECT f.feature_id, f.feature_key, rf.role_id
--   FROM feature f LEFT JOIN role_feature rf ON rf.feature_id = f.feature_id
--  WHERE f.feature_key = 'gestion-administrativa.correcciones-s10';
-- SELECT codigo, nombre, pantalla_id FROM ga_correo_evento
--  WHERE codigo LIKE 'CORRECCION_S10%' OR codigo = 'REEMBOLSO_RECHAZADO';
-- \d ga_correccion_s10
--
-- ── Falta un paso manual: asignarle el rol a alguien ────────────────────────
-- El rol existe pero nadie lo tiene todavia, asi que la bandeja no le aparece a
-- nadie y las solicitudes de correccion se cortarian con 409 ("no hay ningun
-- Coordinador ERP con correo"). Asignarlo desde el CRUD de usuarios, o:
--
--   INSERT INTO user_role (user_id, role_id, created_user_id, state, active)
--   SELECT p.user_id, 84, 1, true, true
--     FROM person p
--    WHERE p.user_id IS NOT NULL AND p.full_name ILIKE '%<nombre del coordinador>%'
--      AND NOT EXISTS (SELECT 1 FROM user_role ur
--                       WHERE ur.user_id = p.user_id AND ur.role_id = 84);
--
-- OJO (memoria del proyecto): el guardado de usuarios desde el CRUD borra los
-- roles previos y los reescribe, asi que conviene asignarlo desde la pantalla y
-- no por SQL si el usuario ya tiene otros roles.
-- ============================================================================
