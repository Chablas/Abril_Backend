"""
MIGRACIÓN MASIVA — ABRIL
Requiere: pip install psycopg2-binary openpyxl pandas
"""

import psycopg2
import psycopg2.extras
import pandas as pd
from datetime import datetime, timezone
import sys
import traceback

import os

DB = {
    "host":     "abril-alvarezvillegaschristian-cb43.b.aivencloud.com",
    "port":     27688,
    "dbname":   "defaultdb",
    "user":     "avnadmin",
    "password": os.environ["AIVEN_DB_PASSWORD"],
    "sslmode":  "require",
}

EXCEL_ENT_EMPRESA  = "ENTREGABLESempresaESTANDARIZADO.xlsx"
EXCEL_TRABAJADORES = "trabajadores_limpios_2.xlsx"
EXCEL_ENT_TRAB     = "entregables_trabajadores_limpios.xlsx"

BATCH_SIZE = 500
NOW = datetime.now(timezone.utc)

def log(msg):
    print(f"[{datetime.now().strftime('%H:%M:%S')}] {msg}", flush=True)

def limpia(val):
    if val is None: return None
    if isinstance(val, float) and val != val: return None
    s = str(val).strip()
    return s if s else None

def limpia_fecha(val):
    if val is None: return None
    if isinstance(val, float) and val != val: return None
    s = str(val).strip()
    if not s or s in ('nan', 'NaT', 'None'): return None
    try: return pd.to_datetime(s).date()
    except: return None

def limpia_bool(val):
    if val is None: return False
    if isinstance(val, bool): return val
    return str(val).strip().lower() in ('true', '1', 'sí', 'si', 'yes')

def limpia_int(val, default=None):
    try:
        v = float(str(val).replace(',', '.'))
        if v != v: return default
        return int(v)
    except: return default

def insertar_lotes(cur, sql, filas, desc):
    if not filas:
        log(f"  ⚠  {desc}: sin filas")
        return 0
    ok = 0
    errores = []
    for i in range(0, len(filas), BATCH_SIZE):
        lote = filas[i:i + BATCH_SIZE]
        cur.execute("SAVEPOINT sp_lote")
        try:
            psycopg2.extras.execute_values(cur, sql, lote, page_size=BATCH_SIZE)
            ok += len(lote)
        except Exception:
            cur.execute("ROLLBACK TO SAVEPOINT sp_lote")
            for fila in lote:
                cur.execute("SAVEPOINT sp_fila")
                try:
                    psycopg2.extras.execute_values(cur, sql, [fila])
                    ok += 1
                except Exception as e2:
                    cur.execute("ROLLBACK TO SAVEPOINT sp_fila")
                    errores.append(f"{str(fila)[:80]} → {e2}")
        if (i // BATCH_SIZE + 1) % 20 == 0:
            log(f"    ... {ok}/{len(filas)} {desc}")
    log(f"  ✓ {ok}/{len(filas)} {desc} | {len(errores)} errores")
    for e in errores[:10]: print(f"    ✗ {e}")
    return ok

def parsear_nombre(nombre_completo):
    if not nombre_completo:
        return None, None, None
    # Limpiar HTML y caracteres raros
    import re
    s = re.sub(r'<[^>]+>', ' ', nombre_completo)
    s = re.sub(r'\s+', ' ', s).strip()
    if ',' in s:
        partes = s.split(',', 1)
        aps = partes[0].strip().split()
        nombres = partes[1].strip()
        first_last  = aps[0][:50] if aps else None
        second_last = aps[1][:50] if len(aps) > 1 else None
        return nombres[:50], first_last, second_last
    else:
        tokens = s.split()
        if len(tokens) >= 4:
            return ' '.join(tokens[2:])[:50], tokens[0][:50], tokens[1][:50]
        elif len(tokens) == 3:
            return tokens[2][:50], tokens[0][:50], tokens[1][:50]
        elif len(tokens) == 2:
            return tokens[1][:50], tokens[0][:50], None
        else:
            return s[:50], s[:50], None

# ─────────────────────────────────────────────
# FASE 0 — BORRADO TOTAL
# ─────────────────────────────────────────────
def fase0(cur):
    log("FASE 0 — Borrado total (orden FK)")
    cur.execute("SELECT person_id FROM workers WHERE person_id IS NOT NULL")
    person_ids = [r[0] for r in cur.fetchall()]
    log(f"  person_ids a borrar: {len(person_ids)}")

    for tabla in ['ss_hab_documento_version','ss_hab_trabajador','ss_hab_worker_proyecto',
                  'worker_vinculaciones','ss_induccion','worker_emos','ss_programacion_emos',
                  'ss_sctr_vidaley_worker','ss_alertas_emo','ss_eval_supervisor',
                  'ss_hab_bloqueo_log','ss_interconsultas','ss_seguimientos_medicos',
                  'worker_eventos','ga_solicitud_salida']:
        cur.execute(f"DELETE FROM {tabla}")
        log(f"  Borrado {tabla}: {cur.rowcount} filas")

    cur.execute("DELETE FROM workers")
    log(f"  Borrado workers: {cur.rowcount} filas")

    if person_ids:
        cur.execute("DELETE FROM person WHERE person_id = ANY(%s) AND user_id IS NULL", (person_ids,))
        log(f"  Borrado person: {cur.rowcount} filas")

    cur.execute("DELETE FROM ss_hab_empresa")
    log(f"  Borrado ss_hab_empresa: {cur.rowcount} filas")
    log("  ✅ Borrado completo")

# ─────────────────────────────────────────────
# FASE 1 — ss_hab_empresa
# ─────────────────────────────────────────────
def fase1(cur, df):
    log("FASE 1 — ss_hab_empresa")
    filas = []
    for _, row in df.iterrows():
        e = limpia_int(row.get('contributor_id'))
        p = limpia_int(row.get('project_id_BD'))
        i = limpia_int(row.get('item_id'))
        if not e or not p or not i: continue
        filas.append((e, p, i, limpia(row.get('estado')) or 'Falta', limpia_fecha(row.get('vigencia')), NOW, NOW))
    log(f"  Válidas: {len(filas)}")
    log(f"  Insertando {len(filas)} entregables empresa de un golpe...")
    args = ','.join(cur.mogrify("(%s,%s,%s,%s,%s,%s,%s)", f).decode('utf-8') for f in filas)
    cur.execute(f"""
        INSERT INTO ss_hab_empresa (empresa_id, proyecto_id, item_id, estado, vigencia, created_at, updated_at)
        VALUES {args}
        ON CONFLICT (empresa_id, proyecto_id, item_id, mes, anio)
        DO UPDATE SET estado=EXCLUDED.estado, vigencia=EXCLUDED.vigencia, updated_at=EXCLUDED.updated_at
    """)
    log(f"  ✓ {cur.rowcount} entregables empresa insertados")

# ─────────────────────────────────────────────
# FASE 2 — person + workers + vinculaciones + proyectos
# ─────────────────────────────────────────────
def fase2(cur, df):
    log("FASE 2 — person + workers + worker_vinculaciones + ss_hab_worker_proyecto")

    cur.execute("SELECT contributor_ruc, contributor_id FROM contributor WHERE es_abril = true AND contributor_ruc IS NOT NULL")
    mapa_ruc = {r[0]: r[1] for r in cur.fetchall()}
    log(f"  Empresas Abril por RUC: {len(mapa_ruc)}")

    # ── PASO 1: Preparar datos ──
    filas_person = []   # (dni, first_names, first_last, second_last, full_name, email)
    filas_meta   = []   # datos worker por índice

    df_reset = df.reset_index(drop=True)
    for _, row in df_reset.iterrows():
        dni    = limpia(row.get('dni'))
        nombre = limpia(row.get('nombre_completo'))
        if not dni:
            filas_person.append(None)
            filas_meta.append(None)
            continue

        email      = limpia(row.get('email_personal'))
        f_ingreso  = limpia_fecha(row.get('fecha_ingreso'))
        f_nac      = limpia_fecha(row.get('fecha_nacimiento'))
        cont_casa  = limpia(row.get('contrata_casa'))
        contrib_id = limpia_int(row.get('contributor_id'))
        ruc        = limpia(row.get('ruc'))

        if cont_casa == 'Casa' and not contrib_id and ruc:
            contrib_id = mapa_ruc.get(ruc)

        first_names, first_last, second_last = parsear_nombre(nombre)

        filas_person.append((dni, first_names, first_last, second_last, nombre or '', NOW))
        filas_meta.append({
            'dni':        dni,
            'email':      email,
            'f_ingreso':  f_ingreso,
            'f_nac':      f_nac,
            'categoria':  limpia(row.get('categoria')),
            'ocupacion':  limpia(row.get('ocupacion')),
            'area':       limpia(row.get('area')),
            'subarea':    limpia(row.get('subarea')),
            'obra_of':    limpia(row.get('obra_oficina')),
            'cont_casa':  cont_casa,
            'cond_med':   limpia(row.get('condicion_medica')),
            'notas':      limpia(row.get('notas')),
            'puntos':     limpia_int(row.get('puntos_infraccion'), 0),
            'sctr':       limpia_bool(row.get('sctr')),
            'project_id': limpia_int(row.get('project_id_BD')),
            'contrib_id': contrib_id,
            'proy_h':     limpia(row.get('proyectos_habilitado')) or '',
            'id_sp':      limpia(row.get('id_trabajador_sp')),
        })

    # ── PASO 2: INSERT person — mogrify de un solo golpe ──
    filas_p_validas = [f for f in filas_person if f is not None]
    log(f"  Insertando {len(filas_p_validas)} persons de un golpe...")
    args_str = ','.join(
        cur.mogrify("(%s,%s,%s,%s,%s,%s)", f).decode('utf-8')
        for f in filas_p_validas
    )
    cur.execute(f"""
        INSERT INTO person (document_identity_code, first_names, first_last_name,
            second_last_name, full_name, created_date_time)
        VALUES {args_str}
        ON CONFLICT (document_identity_code) DO NOTHING
    """)
    log(f"  ✓ {cur.rowcount} persons insertados (los existentes se reusan)")

    # ── PASO 3: Recuperar person_id por DNI ──
    dnis = [f[0] for f in filas_p_validas]
    cur.execute("SELECT document_identity_code, person_id FROM person WHERE document_identity_code = ANY(%s)", (dnis,))
    mapa_dni_pid = {r[0]: r[1] for r in cur.fetchall()}
    log(f"  person_ids recuperados: {len(mapa_dni_pid)}")

    # ── PASO 4: INSERT workers con RETURNING ──
    filas_worker_validas = []
    indices_validos      = []
    for i, m in enumerate(filas_meta):
        if not m: continue
        pid = mapa_dni_pid.get(m['dni'])
        filas_worker_validas.append((
            m['email'], m['categoria'], m['ocupacion'], m['area'], m['subarea'],
            m['obra_of'], m['cont_casa'], 'ACTIVO', True, m['sctr'],
            m['cond_med'], m['notas'], m['puntos'],
            m['f_ingreso'], m['f_nac'],
            m['contrib_id'], pid, NOW, NOW
        ))
        indices_validos.append(i)

    log(f"  Insertando {len(filas_worker_validas)} workers...")
    worker_ids = []
    errores    = []

    for i in range(0, len(filas_worker_validas), BATCH_SIZE):
        lote = filas_worker_validas[i:i + BATCH_SIZE]
        cur.execute("SAVEPOINT sp_workers")
        try:
            args_str = ','.join(
                cur.mogrify("(%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)", f).decode('utf-8')
                for f in lote
            )
            cur.execute(f"""
                INSERT INTO workers (
                    email_personal, categoria, ocupacion, area, subarea,
                    obra_oficina, contrata_casa, estado, habilitado_obra, sctr,
                    condicion_medica, notas, puntos_infraccion,
                    fecha_ingreso, fecha_nacimiento,
                    contributor_id, person_id, created_at, updated_at
                ) VALUES {args_str} RETURNING id
            """)
            ids_lote = [r[0] for r in cur.fetchall()]
        except Exception:
            cur.execute("ROLLBACK TO SAVEPOINT sp_workers")
            ids_lote = []
            for j, fila in enumerate(lote):
                cur.execute("SAVEPOINT sp_wfila")
                try:
                    args_str = cur.mogrify("(%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)", fila).decode('utf-8')
                    cur.execute(f"""
                        INSERT INTO workers (
                            email_personal, categoria, ocupacion, area, subarea,
                            obra_oficina, contrata_casa, estado, habilitado_obra, sctr,
                            condicion_medica, notas, puntos_infraccion,
                            fecha_ingreso, fecha_nacimiento,
                            contributor_id, person_id, created_at, updated_at
                        ) VALUES {args_str} RETURNING id
                    """)
                    ids_lote.append(cur.fetchone()[0])
                except Exception as e2:
                    cur.execute("ROLLBACK TO SAVEPOINT sp_wfila")
                    ids_lote.append(None)
                    errores.append(f"Worker [{i+j}]: {e2}")

        worker_ids.extend(ids_lote)
        log(f"  ... {len(worker_ids)}/{len(filas_worker_validas)} workers")

    log(f"  ✓ {len([x for x in worker_ids if x])} workers | {len(errores)} errores")
    for e in errores[:10]: print(f"    ✗ {e}")

    # ── PASO 5: Vinculaciones y proyectos ──
    mapa_id_trab = {}
    filas_vinc   = []
    filas_proy   = []

    for seq, worker_id in enumerate(worker_ids):
        if not worker_id: continue
        m = filas_meta[indices_validos[seq]]
        if not m: continue

        if m['id_sp']: mapa_id_trab[str(m['id_sp'])] = worker_id
        fecha_base = m['f_ingreso'] or NOW.date()

        if m['project_id'] and m['f_ingreso']:
            filas_vinc.append((worker_id, m['project_id'], m['contrib_id'], m['f_ingreso'], m['cont_casa'], NOW, NOW))

        for p in str(m['proy_h']).split(','):
            p = p.strip()
            if p.lstrip('-').isdigit() and int(p) > 0:
                filas_proy.append((worker_id, int(p), m['contrib_id'], fecha_base, False, NOW, NOW))

    log(f"  Insertando {len(filas_vinc)} vinculaciones de un golpe...")
    if filas_vinc:
        args = ','.join(cur.mogrify("(%s,%s,%s,%s,%s,%s,%s)", f).decode('utf-8') for f in filas_vinc)
        cur.execute(f"INSERT INTO worker_vinculaciones (worker_id, proyecto_id, empresa_id, fecha_inicio, tipo_vinculacion, created_at, updated_at) VALUES {args}")
        log(f"  ✓ {cur.rowcount} vinculaciones insertadas")

    log(f"  Insertando {len(filas_proy)} worker_proyecto de un golpe...")
    if filas_proy:
        args = ','.join(cur.mogrify("(%s,%s,%s,%s,%s,%s,%s)", f).decode('utf-8') for f in filas_proy)
        cur.execute(f"INSERT INTO ss_hab_worker_proyecto (worker_id, proyecto_id, empresa_id, fecha_inicio, induccion_completada, created_at, updated_at) VALUES {args} ON CONFLICT DO NOTHING")
        log(f"  ✓ {cur.rowcount} worker_proyecto insertados")

    return mapa_id_trab

# ─────────────────────────────────────────────
# FASE 3 — ss_hab_trabajador
# ─────────────────────────────────────────────
def fase3(cur, df, mapa_id_trab):
    log("FASE 3 — ss_hab_trabajador")
    filas = []
    sin_worker = set()
    for _, row in df.iterrows():
        id_sp    = limpia(row.get('id_trabajador_sp'))
        item_id  = limpia_int(row.get('item_id'))
        if not id_sp or not item_id: continue
        worker_id = mapa_id_trab.get(str(id_sp))
        if not worker_id:
            sin_worker.add(id_sp); continue
        filas.append((worker_id, item_id, limpia(row.get('estado')) or 'Falta',
                      limpia_fecha(row.get('vigencia')), NOW, NOW))

    log(f"  Válidas: {len(filas)} | Sin worker: {len(sin_worker)}")
    if sin_worker: log(f"  ⚠  primeros 10: {sorted(sin_worker)[:10]}")
    log(f"  Insertando {len(filas)} entregables trabajador de un golpe...")
    args = ','.join(cur.mogrify("(%s,%s,%s,%s,%s,%s)", f).decode('utf-8') for f in filas)
    cur.execute(f"""
        INSERT INTO ss_hab_trabajador (worker_id, item_id, estado, vigencia, created_at, updated_at)
        VALUES {args}
        ON CONFLICT (worker_id, item_id)
        DO UPDATE SET estado=EXCLUDED.estado, vigencia=EXCLUDED.vigencia, updated_at=EXCLUDED.updated_at
    """)
    log(f"  ✓ {cur.rowcount} entregables trabajador insertados")

# ─────────────────────────────────────────────
# MAIN
# ─────────────────────────────────────────────
def main():
    log("=" * 60)
    log("INICIO MIGRACIÓN MASIVA — ABRIL")
    log("=" * 60)

    log("Cargando Excels...")
    try:
        df_emp  = pd.read_excel(EXCEL_ENT_EMPRESA)
        df_trab = pd.read_excel(EXCEL_TRABAJADORES)
        df_ent  = pd.read_excel(EXCEL_ENT_TRAB)
    except Exception as e:
        log(f"❌ Error: {e}"); sys.exit(1)

    log(f"  ent_empresa={len(df_emp)} | trabajadores={len(df_trab)} | ent_trab={len(df_ent)}")

    for df, cols, fname in [
        (df_emp,  ['contributor_id','project_id_BD','item_id','estado'],       EXCEL_ENT_EMPRESA),
        (df_trab, ['dni','id_trabajador_sp','contrata_casa','nombre_completo'], EXCEL_TRABAJADORES),
        (df_ent,  ['id_trabajador_sp','item_id','estado'],                      EXCEL_ENT_TRAB),
    ]:
        for col in cols:
            if col not in df.columns:
                log(f"❌ Columna '{col}' no encontrada en {fname}"); sys.exit(1)
    log("  ✓ Columnas validadas")

    log("Conectando a BD...")
    try:
        conn = psycopg2.connect(**DB)
    except Exception as e:
        log(f"❌ Conexión fallida: {e}"); sys.exit(1)
    log("  ✓ Conectado")

    conn.autocommit = False
    cur = conn.cursor()

    try:
        fase0(cur);  conn.commit(); log("✓ Commit fase 0\n")
        fase1(cur, df_emp);  conn.commit(); log("✓ Commit fase 1\n")
        mapa = fase2(cur, df_trab); conn.commit(); log("✓ Commit fase 2\n")
        fase3(cur, df_ent, mapa);   conn.commit(); log("✓ Commit fase 3\n")

        log("=" * 60)
        log("✅ MIGRACIÓN COMPLETA")
        log("=" * 60)

    except Exception as e:
        conn.rollback()
        log(f"❌ ERROR — ROLLBACK: {e}")
        traceback.print_exc()
        sys.exit(1)
    finally:
        cur.close()
        conn.close()

if __name__ == "__main__":
    main()