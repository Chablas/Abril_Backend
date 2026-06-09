using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRetiroAutomaticoLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "layer");

            migrationBuilder.DropTable(
                name: "partida");

            migrationBuilder.DropTable(
                name: "phase");

            migrationBuilder.DropTable(
                name: "phase_stage_sub_stage_sub_specialty");

            migrationBuilder.DropTable(
                name: "psss_scope");

            migrationBuilder.DropTable(
                name: "psss_template");

            migrationBuilder.DropTable(
                name: "psss_template_detail");

            migrationBuilder.DropTable(
                name: "stage");

            migrationBuilder.DropTable(
                name: "sub_specialty");

            migrationBuilder.DropTable(
                name: "sub_stage");

            migrationBuilder.DropColumn(
                name: "estado_aprobacion",
                table: "ga_solicitud_salida");

            migrationBuilder.DropColumn(
                name: "estado_rendicion",
                table: "ga_solicitud_salida");

            migrationBuilder.RenameColumn(
                name: "phase_stage_sub_stage_sub_specialty_id",
                table: "lesson",
                newName: "reviewed_by_user_id");

            migrationBuilder.AddColumn<int>(
                name: "area_scope_id",
                table: "workers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "es_mensual",
                table: "ss_item_empresa",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "motivo_rechazo",
                table: "ss_hab_empresa",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "enviado",
                table: "ss_hab_documento_version",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_envio",
                table: "ss_hab_documento_version",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "finish_protection_status_id",
                table: "project_sub_contractor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "non_conforming_output_status_id",
                table: "project_sub_contractor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tolerance_chart_status_id",
                table: "project_sub_contractor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "operativo",
                table: "project",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "include_as_independent",
                table: "lesson_area",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "include_descendants",
                table: "lesson_area",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "include_in_form",
                table: "lesson_area",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "approval_status",
                table: "lesson",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "rejection_comment",
                table: "lesson",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                table: "lesson",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "estado_aprobacion_id",
                table: "ga_solicitud_salida",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "estado_rendicion_id",
                table: "ga_solicitud_salida",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "numero_planilla",
                table: "ga_rendicion",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lesson_jefe_reminder",
                columns: table => new
                {
                    lesson_jefe_reminder_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    worker_id = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_jefe_reminder", x => x.lesson_jefe_reminder_id);
                });

            migrationBuilder.CreateTable(
                name: "ss_hab_documento_archivo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    version_id = table.Column<int>(type: "integer", nullable: false),
                    archivo_url = table.Column<string>(type: "text", nullable: false),
                    nombre_archivo = table.Column<string>(type: "text", nullable: true),
                    es_zip = table.Column<bool>(type: "boolean", nullable: false),
                    zip_contenido = table.Column<string>(type: "text", nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ss_hab_documento_archivo", x => x.id);
                    table.ForeignKey(
                        name: "fk_ss_hab_documento_archivo_ss_hab_documento_version_version_id",
                        column: x => x.version_id,
                        principalTable: "ss_hab_documento_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ss_retiro_automatico_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    worker_id = table.Column<int>(type: "integer", nullable: false),
                    empresa_id = table.Column<int>(type: "integer", nullable: true),
                    motivo = table.Column<string>(type: "text", nullable: true),
                    ejecutado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tipo_retiro = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dias_gracia = table.Column<int>(type: "integer", nullable: true),
                    entregables_vencidos = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ss_retiro_automatico_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ssoma_paso",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    proyecto_id = table.Column<int>(type: "integer", nullable: true),
                    plantilla_id = table.Column<int>(type: "integer", nullable: true),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    anio = table.Column<int>(type: "integer", nullable: false),
                    mes_inicio = table.Column<int>(type: "integer", nullable: false),
                    es_plantilla = table.Column<bool>(type: "boolean", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    aprobado_por = table.Column<int>(type: "integer", nullable: true),
                    aprobado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ssoma_paso", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ssoma_paso_auditoria",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    entidad = table.Column<string>(type: "text", nullable: false),
                    entidad_id = table.Column<int>(type: "integer", nullable: false),
                    paso_id = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    motivo = table.Column<string>(type: "text", nullable: true),
                    valor_anterior = table.Column<string>(type: "jsonb", nullable: true),
                    valor_nuevo = table.Column<string>(type: "jsonb", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ssoma_paso_auditoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ssoma_paso_categoria",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    ambito = table.Column<string>(type: "text", nullable: false),
                    icono = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ssoma_paso_categoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_item_category_anexo3_clause",
                columns: table => new
                {
                    work_item_category_anexo3_clause_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_item_category_id = table.Column<int>(type: "integer", nullable: false),
                    clause_text = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    created_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_item_category_anexo3_clause", x => x.work_item_category_anexo3_clause_id);
                    table.ForeignKey(
                        name: "fk_work_item_category_anexo3_clause_work_item_category_work_it",
                        column: x => x.work_item_category_id,
                        principalTable: "work_item_category",
                        principalColumn: "work_item_category_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_item_category_anexo4_clause",
                columns: table => new
                {
                    work_item_category_anexo4_clause_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_item_category_id = table.Column<int>(type: "integer", nullable: false),
                    clause_text = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    created_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_item_category_anexo4_clause", x => x.work_item_category_anexo4_clause_id);
                    table.ForeignKey(
                        name: "fk_work_item_category_anexo4_clause_work_item_category_work_it",
                        column: x => x.work_item_category_id,
                        principalTable: "work_item_category",
                        principalColumn: "work_item_category_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ssoma_paso_actividad",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    paso_id = table.Column<int>(type: "integer", nullable: false),
                    categoria_id = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    alcance = table.Column<string>(type: "text", nullable: true),
                    frecuencia = table.Column<string>(type: "text", nullable: false),
                    responsable_id = table.Column<int>(type: "integer", nullable: true),
                    responsable_texto = table.Column<string>(type: "text", nullable: true),
                    mes_inicio = table.Column<int>(type: "integer", nullable: false),
                    mes_fin = table.Column<int>(type: "integer", nullable: false),
                    cantidad_planificada = table.Column<int>(type: "integer", nullable: false),
                    horas = table.Column<decimal>(type: "numeric", nullable: true),
                    recursos = table.Column<string>(type: "text", nullable: true),
                    indicador = table.Column<string>(type: "text", nullable: false),
                    meta = table.Column<string>(type: "text", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<int>(type: "integer", nullable: true),
                    motivo_eliminacion = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ssoma_paso_actividad", x => x.id);
                    table.ForeignKey(
                        name: "fk_ssoma_paso_actividad_ssoma_paso_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "ssoma_paso_categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ssoma_paso_actividad_ssoma_paso_paso_id",
                        column: x => x.paso_id,
                        principalTable: "ssoma_paso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ssoma_paso_ejecucion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    actividad_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_programada = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_verificacion = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_ejecutada = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_reprogramada = table.Column<DateOnly>(type: "date", nullable: true),
                    motivo_reprogramacion = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<string>(type: "text", nullable: false),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    participantes_count = table.Column<int>(type: "integer", nullable: true),
                    evidencia_nombre = table.Column<string>(type: "text", nullable: true),
                    evidencia_url = table.Column<string>(type: "text", nullable: true),
                    evidencia_sp_id = table.Column<string>(type: "text", nullable: true),
                    registrado_por = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ssoma_paso_ejecucion", x => x.id);
                    table.ForeignKey(
                        name: "fk_ssoma_paso_ejecucion_ssoma_paso_actividad_actividad_id",
                        column: x => x.actividad_id,
                        principalTable: "ssoma_paso_actividad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ss_hab_documento_archivo_version_id",
                table: "ss_hab_documento_archivo",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ix_ssoma_paso_actividad_categoria_id",
                table: "ssoma_paso_actividad",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_ssoma_paso_actividad_paso_id",
                table: "ssoma_paso_actividad",
                column: "paso_id");

            migrationBuilder.CreateIndex(
                name: "ix_ssoma_paso_ejecucion_actividad_id",
                table: "ssoma_paso_ejecucion",
                column: "actividad_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_category_anexo3_clause_work_item_category_id",
                table: "work_item_category_anexo3_clause",
                column: "work_item_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_category_anexo4_clause_work_item_category_id",
                table: "work_item_category_anexo4_clause",
                column: "work_item_category_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lesson_jefe_reminder");

            migrationBuilder.DropTable(
                name: "ss_hab_documento_archivo");

            migrationBuilder.DropTable(
                name: "ss_retiro_automatico_log");

            migrationBuilder.DropTable(
                name: "ssoma_paso_auditoria");

            migrationBuilder.DropTable(
                name: "ssoma_paso_ejecucion");

            migrationBuilder.DropTable(
                name: "work_item_category_anexo3_clause");

            migrationBuilder.DropTable(
                name: "work_item_category_anexo4_clause");

            migrationBuilder.DropTable(
                name: "ssoma_paso_actividad");

            migrationBuilder.DropTable(
                name: "ssoma_paso_categoria");

            migrationBuilder.DropTable(
                name: "ssoma_paso");

            migrationBuilder.DropColumn(
                name: "area_scope_id",
                table: "workers");

            migrationBuilder.DropColumn(
                name: "es_mensual",
                table: "ss_item_empresa");

            migrationBuilder.DropColumn(
                name: "motivo_rechazo",
                table: "ss_hab_empresa");

            migrationBuilder.DropColumn(
                name: "enviado",
                table: "ss_hab_documento_version");

            migrationBuilder.DropColumn(
                name: "fecha_envio",
                table: "ss_hab_documento_version");

            migrationBuilder.DropColumn(
                name: "finish_protection_status_id",
                table: "project_sub_contractor");

            migrationBuilder.DropColumn(
                name: "non_conforming_output_status_id",
                table: "project_sub_contractor");

            migrationBuilder.DropColumn(
                name: "tolerance_chart_status_id",
                table: "project_sub_contractor");

            migrationBuilder.DropColumn(
                name: "operativo",
                table: "project");

            migrationBuilder.DropColumn(
                name: "include_as_independent",
                table: "lesson_area");

            migrationBuilder.DropColumn(
                name: "include_descendants",
                table: "lesson_area");

            migrationBuilder.DropColumn(
                name: "include_in_form",
                table: "lesson_area");

            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "lesson");

            migrationBuilder.DropColumn(
                name: "rejection_comment",
                table: "lesson");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "lesson");

            migrationBuilder.DropColumn(
                name: "estado_aprobacion_id",
                table: "ga_solicitud_salida");

            migrationBuilder.DropColumn(
                name: "estado_rendicion_id",
                table: "ga_solicitud_salida");

            migrationBuilder.DropColumn(
                name: "numero_planilla",
                table: "ga_rendicion");

            migrationBuilder.RenameColumn(
                name: "reviewed_by_user_id",
                table: "lesson",
                newName: "phase_stage_sub_stage_sub_specialty_id");

            migrationBuilder.AddColumn<string>(
                name: "estado_aprobacion",
                table: "ga_solicitud_salida",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "estado_rendicion",
                table: "ga_solicitud_salida",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "layer",
                columns: table => new
                {
                    layer_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    layer_description = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_layer", x => x.layer_id);
                });

            migrationBuilder.CreateTable(
                name: "partida",
                columns: table => new
                {
                    partida_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: true),
                    partida_description = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    updated_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partida", x => x.partida_id);
                });

            migrationBuilder.CreateTable(
                name: "phase",
                columns: table => new
                {
                    phase_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    phase_order = table.Column<int>(type: "integer", nullable: true),
                    phase_description = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phase", x => x.phase_id);
                });

            migrationBuilder.CreateTable(
                name: "phase_stage_sub_stage_sub_specialty",
                columns: table => new
                {
                    phase_stage_sub_stage_sub_specialty_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    layer_id = table.Column<int>(type: "integer", nullable: true),
                    partida_id = table.Column<int>(type: "integer", nullable: true),
                    phase_id = table.Column<int>(type: "integer", nullable: false),
                    stage_id = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    sub_specialty_id = table.Column<int>(type: "integer", nullable: true),
                    sub_stage_id = table.Column<int>(type: "integer", nullable: true),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phase_stage_sub_stage_sub_specialty", x => x.phase_stage_sub_stage_sub_specialty_id);
                });

            migrationBuilder.CreateTable(
                name: "psss_scope",
                columns: table => new
                {
                    psss_scope_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_id = table.Column<int>(type: "integer", nullable: true),
                    phase_stage_sub_stage_sub_specialty_id = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    sub_area_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_psss_scope", x => x.psss_scope_id);
                });

            migrationBuilder.CreateTable(
                name: "psss_template",
                columns: table => new
                {
                    psss_template_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    template_name = table.Column<string>(type: "text", nullable: false),
                    updated_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_psss_template", x => x.psss_template_id);
                });

            migrationBuilder.CreateTable(
                name: "psss_template_detail",
                columns: table => new
                {
                    psss_template_detail_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    phase_stage_sub_stage_sub_specialty_id = table.Column<int>(type: "integer", nullable: false),
                    psss_template_id = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_psss_template_detail", x => x.psss_template_detail_id);
                });

            migrationBuilder.CreateTable(
                name: "stage",
                columns: table => new
                {
                    stage_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    stage_description = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stage", x => x.stage_id);
                });

            migrationBuilder.CreateTable(
                name: "sub_specialty",
                columns: table => new
                {
                    sub_specialty_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    sub_specialty_description = table.Column<string>(type: "text", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sub_specialty", x => x.sub_specialty_id);
                });

            migrationBuilder.CreateTable(
                name: "sub_stage",
                columns: table => new
                {
                    sub_stage_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    sub_stage_description = table.Column<string>(type: "text", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sub_stage", x => x.sub_stage_id);
                });
        }
    }
}
