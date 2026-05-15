using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogAndScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ss_alertas_emo_worker_emos_emo_id",
                table: "ss_alertas_emo");

            migrationBuilder.DropForeignKey(
                name: "fk_ss_alertas_emo_workers_worker_id",
                table: "ss_alertas_emo");

            migrationBuilder.DropColumn(
                name: "apellido_nombre",
                table: "workers");

            migrationBuilder.DropColumn(
                name: "dni",
                table: "workers");

            migrationBuilder.DropColumn(
                name: "ruc",
                table: "workers");

            migrationBuilder.DropColumn(
                name: "contract_id",
                table: "project_sub_contractor");

            migrationBuilder.AddColumn<int>(
                name: "contributor_id",
                table: "workers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "person_id",
                table: "workers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instructivos_folder_id",
                table: "work_item_category",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instructivos_folder_name",
                table: "work_item_category",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "instructivos_sync_status",
                table: "work_item_category",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "instructivos_synced_at",
                table: "work_item_category",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "check_in_hora",
                table: "ss_programacion_emos",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_notificacion",
                table: "ss_programacion_emos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_rechazo",
                table: "ss_programacion_emos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origen",
                table: "ss_programacion_emos",
                type: "text",
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "ss_clinicas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "worker_id",
                table: "ss_alertas_emo",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "emo_id",
                table: "ss_alertas_emo",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "contract_modality_id",
                table: "project_sub_contractor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "guarantee_fund_days",
                table: "project_sub_contractor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "guarantee_fund_percentage",
                table: "project_sub_contractor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "project_sub_contractor_instructivo_id",
                table: "project_sub_contractor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "project_sub_contractor_non_conforming_output_id",
                table: "project_sub_contractor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "project_sub_contractor_tolerance_chart_id",
                table: "project_sub_contractor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "abbreviation",
                table: "project",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "partida_id",
                table: "phase_stage_sub_stage_sub_specialty",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "catalog_item_id",
                table: "lesson",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sub_area_id",
                table: "lesson",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "contractor_person_type_id",
                table: "contractor_email",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "activation_token",
                table: "contractor",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "activation_token_expiry",
                table: "contractor",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_file_url",
                table: "contractor",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "categoria_id",
                table: "ac_actividades",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "especialidad_id",
                table: "ac_actividades",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cat_jefatura",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cat_jefatura", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "catalog_type",
                columns: table => new
                {
                    catalog_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    catalog_type_name = table.Column<string>(type: "text", nullable: false),
                    catalog_type_code = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_type", x => x.catalog_type_id);
                });

            migrationBuilder.CreateTable(
                name: "contract_modality",
                columns: table => new
                {
                    contract_modality_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contract_modality_description = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    created_datetime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_datetime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract_modality", x => x.contract_modality_id);
                });

            migrationBuilder.CreateTable(
                name: "contractor_person_type",
                columns: table => new
                {
                    contractor_person_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    description = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contractor_person_type", x => x.contractor_person_type_id);
                });

            migrationBuilder.CreateTable(
                name: "contractor_user",
                columns: table => new
                {
                    contractor_user_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contractor_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    created_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contractor_user", x => x.contractor_user_id);
                    table.ForeignKey(
                        name: "fk_contractor_user_contractor_contractor_id",
                        column: x => x.contractor_id,
                        principalTable: "contractor",
                        principalColumn: "contractor_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_contractor_user_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ga_hora_opcion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    hora = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    etiqueta = table.Column<string>(type: "text", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ga_hora_opcion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ga_lugar",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: true),
                    project_id = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ga_lugar", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ga_motivo_salida",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ga_motivo_salida", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ga_solicitud_salida",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    worker_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_salida = table.Column<DateOnly>(type: "date", nullable: false),
                    hora_salida = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_retorno = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    motivo_id = table.Column<int>(type: "integer", nullable: false),
                    lugar_origen_id = table.Column<int>(type: "integer", nullable: true),
                    lugar_origen_libre = table.Column<string>(type: "text", nullable: true),
                    lugar_destino_id = table.Column<int>(type: "integer", nullable: true),
                    lugar_destino_libre = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<string>(type: "text", nullable: false),
                    registrado_por_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ga_solicitud_salida", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "partida",
                columns: table => new
                {
                    partida_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    partida_description = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partida", x => x.partida_id);
                });

            migrationBuilder.CreateTable(
                name: "project_sub_contractor_instructivo",
                columns: table => new
                {
                    project_sub_contractor_instructivo_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    original_file_name = table.Column<string>(type: "text", nullable: true),
                    sharepoint_item_id = table.Column<string>(type: "text", nullable: true),
                    project_sub_contractor_file_status_id = table.Column<int>(type: "integer", nullable: true),
                    observation = table.Column<string>(type: "text", nullable: true),
                    created_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_sub_contractor_instructivo", x => x.project_sub_contractor_instructivo_id);
                    table.ForeignKey(
                        name: "fk_project_sub_contractor_instructivo_project_sub_contractor_f",
                        column: x => x.project_sub_contractor_file_status_id,
                        principalTable: "project_sub_contractor_file_status",
                        principalColumn: "project_sub_contractor_file_status_id");
                });

            migrationBuilder.CreateTable(
                name: "project_sub_contractor_non_conforming_output",
                columns: table => new
                {
                    project_sub_contractor_non_conforming_output_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    original_file_name = table.Column<string>(type: "text", nullable: true),
                    sharepoint_item_id = table.Column<string>(type: "text", nullable: true),
                    project_sub_contractor_file_status_id = table.Column<int>(type: "integer", nullable: true),
                    observation = table.Column<string>(type: "text", nullable: true),
                    created_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_sub_contractor_non_conforming_output", x => x.project_sub_contractor_non_conforming_output_id);
                    table.ForeignKey(
                        name: "fk_project_sub_contractor_non_conforming_output_project_sub_co",
                        column: x => x.project_sub_contractor_file_status_id,
                        principalTable: "project_sub_contractor_file_status",
                        principalColumn: "project_sub_contractor_file_status_id");
                });

            migrationBuilder.CreateTable(
                name: "project_sub_contractor_tolerance_chart",
                columns: table => new
                {
                    project_sub_contractor_tolerance_chart_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    original_file_name = table.Column<string>(type: "text", nullable: true),
                    sharepoint_item_id = table.Column<string>(type: "text", nullable: true),
                    project_sub_contractor_file_status_id = table.Column<int>(type: "integer", nullable: true),
                    observation = table.Column<string>(type: "text", nullable: true),
                    created_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_sub_contractor_tolerance_chart", x => x.project_sub_contractor_tolerance_chart_id);
                    table.ForeignKey(
                        name: "fk_project_sub_contractor_tolerance_chart_project_sub_contract",
                        column: x => x.project_sub_contractor_file_status_id,
                        principalTable: "project_sub_contractor_file_status",
                        principalColumn: "project_sub_contractor_file_status_id");
                });

            migrationBuilder.CreateTable(
                name: "psss_scope",
                columns: table => new
                {
                    psss_scope_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    phase_stage_sub_stage_sub_specialty_id = table.Column<int>(type: "integer", nullable: false),
                    area_id = table.Column<int>(type: "integer", nullable: true),
                    sub_area_id = table.Column<int>(type: "integer", nullable: true),
                    state = table.Column<bool>(type: "boolean", nullable: false)
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
                    template_name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: true),
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
                    psss_template_id = table.Column<int>(type: "integer", nullable: false),
                    phase_stage_sub_stage_sub_specialty_id = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_psss_template_detail", x => x.psss_template_detail_id);
                });

            migrationBuilder.CreateTable(
                name: "ss_clinica_emails",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clinica_id = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ss_clinica_emails", x => x.id);
                    table.ForeignKey(
                        name: "fk_ss_clinica_emails_ss_clinicas_clinica_id",
                        column: x => x.clinica_id,
                        principalTable: "ss_clinicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ss_clinica_reset_token",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clinica_id = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    expira_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usado = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ss_clinica_reset_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_ss_clinica_reset_token_ss_clinicas_clinica_id",
                        column: x => x.clinica_id,
                        principalTable: "ss_clinicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sub_area",
                columns: table => new
                {
                    sub_area_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    sub_area_description = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sub_area", x => x.sub_area_id);
                    table.ForeignKey(
                        name: "fk_sub_area_area_area_id",
                        column: x => x.area_id,
                        principalTable: "area",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_item_category_clause",
                columns: table => new
                {
                    work_item_category_clause_id = table.Column<int>(type: "integer", nullable: false)
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
                    table.PrimaryKey("pk_work_item_category_clause", x => x.work_item_category_clause_id);
                    table.ForeignKey(
                        name: "fk_work_item_category_clause_work_item_category_work_item_cate",
                        column: x => x.work_item_category_id,
                        principalTable: "work_item_category",
                        principalColumn: "work_item_category_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_item",
                columns: table => new
                {
                    catalog_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    catalog_type_id = table.Column<int>(type: "integer", nullable: false),
                    catalog_item_parent_id = table.Column<int>(type: "integer", nullable: true),
                    catalog_item_description = table.Column<string>(type: "text", nullable: false),
                    catalog_item_code = table.Column<string>(type: "text", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_item", x => x.catalog_item_id);
                    table.ForeignKey(
                        name: "fk_catalog_item_catalog_item_catalog_item_parent_id",
                        column: x => x.catalog_item_parent_id,
                        principalTable: "catalog_item",
                        principalColumn: "catalog_item_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_catalog_item_catalog_type_catalog_type_id",
                        column: x => x.catalog_type_id,
                        principalTable: "catalog_type",
                        principalColumn: "catalog_type_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "area_subarea",
                columns: table => new
                {
                    area_subarea_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    sub_area_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_area_subarea", x => x.area_subarea_id);
                    table.ForeignKey(
                        name: "fk_area_subarea_area_area_id",
                        column: x => x.area_id,
                        principalTable: "area",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_area_subarea_sub_area_sub_area_id",
                        column: x => x.sub_area_id,
                        principalTable: "sub_area",
                        principalColumn: "sub_area_id");
                });

            migrationBuilder.CreateTable(
                name: "scope_item",
                columns: table => new
                {
                    scope_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_subarea_id = table.Column<int>(type: "integer", nullable: false),
                    catalog_item_id = table.Column<int>(type: "integer", nullable: false),
                    scope_item_parent_id = table.Column<int>(type: "integer", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scope_item", x => x.scope_item_id);
                    table.ForeignKey(
                        name: "fk_scope_item_area_subarea_area_subarea_id",
                        column: x => x.area_subarea_id,
                        principalTable: "area_subarea",
                        principalColumn: "area_subarea_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scope_item_catalog_item_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_item",
                        principalColumn: "catalog_item_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scope_item_scope_item_scope_item_parent_id",
                        column: x => x.scope_item_parent_id,
                        principalTable: "scope_item",
                        principalColumn: "scope_item_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scope_template",
                columns: table => new
                {
                    scope_template_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_subarea_id = table.Column<int>(type: "integer", nullable: false),
                    template_name = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scope_template", x => x.scope_template_id);
                    table.ForeignKey(
                        name: "fk_scope_template_area_subarea_area_subarea_id",
                        column: x => x.area_subarea_id,
                        principalTable: "area_subarea",
                        principalColumn: "area_subarea_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scope_template_item",
                columns: table => new
                {
                    scope_template_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    scope_template_id = table.Column<int>(type: "integer", nullable: false),
                    scope_item_id = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scope_template_item", x => x.scope_template_item_id);
                    table.ForeignKey(
                        name: "fk_scope_template_item_scope_item_scope_item_id",
                        column: x => x.scope_item_id,
                        principalTable: "scope_item",
                        principalColumn: "scope_item_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scope_template_item_scope_template_scope_template_id",
                        column: x => x.scope_template_id,
                        principalTable: "scope_template",
                        principalColumn: "scope_template_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workers_contributor_id",
                table: "workers",
                column: "contributor_id");

            migrationBuilder.CreateIndex(
                name: "ix_workers_person_id",
                table: "workers",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_sub_contractor_project_sub_contractor_instructivo_id",
                table: "project_sub_contractor",
                column: "project_sub_contractor_instructivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_sub_contractor_project_sub_contractor_non_conformin",
                table: "project_sub_contractor",
                column: "project_sub_contractor_non_conforming_output_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_sub_contractor_project_sub_contractor_tolerance_cha",
                table: "project_sub_contractor",
                column: "project_sub_contractor_tolerance_chart_id");

            migrationBuilder.CreateIndex(
                name: "ix_contractor_email_contractor_person_type_id",
                table: "contractor_email",
                column: "contractor_person_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_area_subarea_area_id_sub_area_id",
                table: "area_subarea",
                columns: new[] { "area_id", "sub_area_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_area_subarea_sub_area_id",
                table: "area_subarea",
                column: "sub_area_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_item_catalog_item_parent_id",
                table: "catalog_item",
                column: "catalog_item_parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_item_catalog_type_id",
                table: "catalog_item",
                column: "catalog_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_contractor_user_contractor_id",
                table: "contractor_user",
                column: "contractor_id");

            migrationBuilder.CreateIndex(
                name: "ix_contractor_user_user_id",
                table: "contractor_user",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_sub_contractor_instructivo_project_sub_contractor_f",
                table: "project_sub_contractor_instructivo",
                column: "project_sub_contractor_file_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_sub_contractor_non_conforming_output_project_sub_co",
                table: "project_sub_contractor_non_conforming_output",
                column: "project_sub_contractor_file_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_sub_contractor_tolerance_chart_project_sub_contract",
                table: "project_sub_contractor_tolerance_chart",
                column: "project_sub_contractor_file_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_item_area_subarea_id",
                table: "scope_item",
                column: "area_subarea_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_item_catalog_item_id",
                table: "scope_item",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_item_scope_item_parent_id",
                table: "scope_item",
                column: "scope_item_parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_template_area_subarea_id",
                table: "scope_template",
                column: "area_subarea_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_template_item_scope_item_id",
                table: "scope_template_item",
                column: "scope_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_template_item_scope_template_id",
                table: "scope_template_item",
                column: "scope_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_ss_clinica_emails_clinica_id",
                table: "ss_clinica_emails",
                column: "clinica_id");

            migrationBuilder.CreateIndex(
                name: "ix_ss_clinica_reset_token_clinica_id",
                table: "ss_clinica_reset_token",
                column: "clinica_id");

            migrationBuilder.CreateIndex(
                name: "ix_sub_area_area_id",
                table: "sub_area",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_category_clause_work_item_category_id",
                table: "work_item_category_clause",
                column: "work_item_category_id");

            migrationBuilder.AddForeignKey(
                name: "fk_contractor_email_contractor_person_type_contractor_person_t",
                table: "contractor_email",
                column: "contractor_person_type_id",
                principalTable: "contractor_person_type",
                principalColumn: "contractor_person_type_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_sub_contractor_project_sub_contractor_instructivo_p",
                table: "project_sub_contractor",
                column: "project_sub_contractor_instructivo_id",
                principalTable: "project_sub_contractor_instructivo",
                principalColumn: "project_sub_contractor_instructivo_id");

            migrationBuilder.AddForeignKey(
                name: "fk_project_sub_contractor_project_sub_contractor_non_conformin",
                table: "project_sub_contractor",
                column: "project_sub_contractor_non_conforming_output_id",
                principalTable: "project_sub_contractor_non_conforming_output",
                principalColumn: "project_sub_contractor_non_conforming_output_id");

            migrationBuilder.AddForeignKey(
                name: "fk_project_sub_contractor_project_sub_contractor_tolerance_cha",
                table: "project_sub_contractor",
                column: "project_sub_contractor_tolerance_chart_id",
                principalTable: "project_sub_contractor_tolerance_chart",
                principalColumn: "project_sub_contractor_tolerance_chart_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ss_alertas_emo_worker_emos_emo_id",
                table: "ss_alertas_emo",
                column: "emo_id",
                principalTable: "worker_emos",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_ss_alertas_emo_workers_worker_id",
                table: "ss_alertas_emo",
                column: "worker_id",
                principalTable: "workers",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_workers_contributor_contributor_id",
                table: "workers",
                column: "contributor_id",
                principalTable: "contributor",
                principalColumn: "contributor_id");

            migrationBuilder.AddForeignKey(
                name: "fk_workers_person_person_id",
                table: "workers",
                column: "person_id",
                principalTable: "person",
                principalColumn: "person_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_contractor_email_contractor_person_type_contractor_person_t",
                table: "contractor_email");

            migrationBuilder.DropForeignKey(
                name: "fk_project_sub_contractor_project_sub_contractor_instructivo_p",
                table: "project_sub_contractor");

            migrationBuilder.DropForeignKey(
                name: "fk_project_sub_contractor_project_sub_contractor_non_conformin",
                table: "project_sub_contractor");

            migrationBuilder.DropForeignKey(
                name: "fk_project_sub_contractor_project_sub_contractor_tolerance_cha",
                table: "project_sub_contractor");

            migrationBuilder.DropForeignKey(
                name: "fk_ss_alertas_emo_worker_emos_emo_id",
                table: "ss_alertas_emo");

            migrationBuilder.DropForeignKey(
                name: "fk_ss_alertas_emo_workers_worker_id",
                table: "ss_alertas_emo");

            migrationBuilder.DropForeignKey(
                name: "fk_workers_contributor_contributor_id",
                table: "workers");

            migrationBuilder.DropForeignKey(
                name: "fk_workers_person_person_id",
                table: "workers");

            migrationBuilder.DropTable(
                name: "cat_jefatura");

            migrationBuilder.DropTable(
                name: "contract_modality");

            migrationBuilder.DropTable(
                name: "contractor_person_type");

            migrationBuilder.DropTable(
                name: "contractor_user");

            migrationBuilder.DropTable(
                name: "ga_hora_opcion");

            migrationBuilder.DropTable(
                name: "ga_lugar");

            migrationBuilder.DropTable(
                name: "ga_motivo_salida");

            migrationBuilder.DropTable(
                name: "ga_solicitud_salida");

            migrationBuilder.DropTable(
                name: "partida");

            migrationBuilder.DropTable(
                name: "project_sub_contractor_instructivo");

            migrationBuilder.DropTable(
                name: "project_sub_contractor_non_conforming_output");

            migrationBuilder.DropTable(
                name: "project_sub_contractor_tolerance_chart");

            migrationBuilder.DropTable(
                name: "psss_scope");

            migrationBuilder.DropTable(
                name: "psss_template");

            migrationBuilder.DropTable(
                name: "psss_template_detail");

            migrationBuilder.DropTable(
                name: "scope_template_item");

            migrationBuilder.DropTable(
                name: "ss_clinica_emails");

            migrationBuilder.DropTable(
                name: "ss_clinica_reset_token");

            migrationBuilder.DropTable(
                name: "work_item_category_clause");

            migrationBuilder.DropTable(
                name: "scope_item");

            migrationBuilder.DropTable(
                name: "scope_template");

            migrationBuilder.DropTable(
                name: "catalog_item");

            migrationBuilder.DropTable(
                name: "area_subarea");

            migrationBuilder.DropTable(
                name: "catalog_type");

            migrationBuilder.DropTable(
                name: "sub_area");

            migrationBuilder.DropIndex(
                name: "ix_workers_contributor_id",
                table: "workers");

            migrationBuilder.DropIndex(
                name: "ix_workers_person_id",
                table: "workers");

            migrationBuilder.DropIndex(
                name: "ix_project_sub_contractor_project_sub_contractor_instructivo_id",
                table: "project_sub_contractor");

            migrationBuilder.DropIndex(
                name: "ix_project_sub_contractor_project_sub_contractor_non_conformin",
                table: "project_sub_contractor");

            migrationBuilder.DropIndex(
                name: "ix_project_sub_contractor_project_sub_contractor_tolerance_cha",
                table: "project_sub_contractor");

            migrationBuilder.DropIndex(
                name: "ix_contractor_email_contractor_person_type_id",
                table: "contractor_email");

            migrationBuilder.DropColumn(
                name: "contributor_id",
                table: "workers");

            migrationBuilder.DropColumn(
                name: "person_id",
                table: "workers");

            migrationBuilder.DropColumn(
                name: "instructivos_folder_id",
                table: "work_item_category");

            migrationBuilder.DropColumn(
                name: "instructivos_folder_name",
                table: "work_item_category");

            migrationBuilder.DropColumn(
                name: "instructivos_sync_status",
                table: "work_item_category");

            migrationBuilder.DropColumn(
                name: "instructivos_synced_at",
                table: "work_item_category");

            migrationBuilder.DropColumn(
                name: "check_in_hora",
                table: "ss_programacion_emos");

            migrationBuilder.DropColumn(
                name: "fecha_notificacion",
                table: "ss_programacion_emos");

            migrationBuilder.DropColumn(
                name: "motivo_rechazo",
                table: "ss_programacion_emos");

            migrationBuilder.DropColumn(
                name: "origen",
                table: "ss_programacion_emos");

            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "ss_clinicas");

            migrationBuilder.DropColumn(
                name: "contract_modality_id",
                table: "project_sub_contractor");

            migrationBuilder.DropColumn(
                name: "guarantee_fund_days",
                table: "project_sub_contractor");

            migrationBuilder.DropColumn(
                name: "guarantee_fund_percentage",
                table: "project_sub_contractor");

            migrationBuilder.DropColumn(
                name: "project_sub_contractor_instructivo_id",
                table: "project_sub_contractor");

            migrationBuilder.DropColumn(
                name: "project_sub_contractor_non_conforming_output_id",
                table: "project_sub_contractor");

            migrationBuilder.DropColumn(
                name: "project_sub_contractor_tolerance_chart_id",
                table: "project_sub_contractor");

            migrationBuilder.DropColumn(
                name: "abbreviation",
                table: "project");

            migrationBuilder.DropColumn(
                name: "partida_id",
                table: "phase_stage_sub_stage_sub_specialty");

            migrationBuilder.DropColumn(
                name: "catalog_item_id",
                table: "lesson");

            migrationBuilder.DropColumn(
                name: "sub_area_id",
                table: "lesson");

            migrationBuilder.DropColumn(
                name: "contractor_person_type_id",
                table: "contractor_email");

            migrationBuilder.DropColumn(
                name: "activation_token",
                table: "contractor");

            migrationBuilder.DropColumn(
                name: "activation_token_expiry",
                table: "contractor");

            migrationBuilder.DropColumn(
                name: "logo_file_url",
                table: "contractor");

            migrationBuilder.DropColumn(
                name: "categoria_id",
                table: "ac_actividades");

            migrationBuilder.DropColumn(
                name: "especialidad_id",
                table: "ac_actividades");

            migrationBuilder.AddColumn<string>(
                name: "apellido_nombre",
                table: "workers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dni",
                table: "workers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ruc",
                table: "workers",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "worker_id",
                table: "ss_alertas_emo",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "emo_id",
                table: "ss_alertas_emo",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "contract_id",
                table: "project_sub_contractor",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "fk_ss_alertas_emo_worker_emos_emo_id",
                table: "ss_alertas_emo",
                column: "emo_id",
                principalTable: "worker_emos",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ss_alertas_emo_workers_worker_id",
                table: "ss_alertas_emo",
                column: "worker_id",
                principalTable: "workers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
