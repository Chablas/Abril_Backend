using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddEvAsignacionSupervisor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "sub_area_id",
                table: "lesson",
                newName: "lesson_area_id");

            migrationBuilder.AddColumn<string>(
                name: "staff_email",
                table: "project",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "catalog_item_id",
                table: "lesson",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "area_type",
                columns: table => new
                {
                    area_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_type_name = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_area_type", x => x.area_type_id);
                });

            migrationBuilder.CreateTable(
                name: "catalog_type",
                columns: table => new
                {
                    catalog_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    catalog_type_name = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_type", x => x.catalog_type_id);
                });

            migrationBuilder.CreateTable(
                name: "ev_asignacion_supervisor",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    supervisor_worker_id = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ev_asignacion_supervisor", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ev_periodo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    anio = table.Column<int>(type: "integer", nullable: false),
                    fecha_apertura = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_cierre = table.Column<DateOnly>(type: "date", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ev_periodo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ev_plantilla",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_nombre = table.Column<string>(type: "text", nullable: false),
                    criterio = table.Column<string>(type: "text", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ev_plantilla", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ev_recordatorio_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    periodo_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    email_destino = table.Column<string>(type: "text", nullable: true),
                    cc_jefatura = table.Column<bool>(type: "boolean", nullable: false),
                    cc_gerencia = table.Column<bool>(type: "boolean", nullable: false),
                    enviado_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ev_recordatorio_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lesson_area",
                columns: table => new
                {
                    lesson_area_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_scope_id = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_area", x => x.lesson_area_id);
                });

            migrationBuilder.CreateTable(
                name: "project_staff_reminder",
                columns: table => new
                {
                    project_staff_reminder_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_staff_reminder", x => x.project_staff_reminder_id);
                });

            migrationBuilder.CreateTable(
                name: "scope_template",
                columns: table => new
                {
                    scope_template_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
                });

            migrationBuilder.CreateTable(
                name: "area_item",
                columns: table => new
                {
                    area_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_item_name = table.Column<string>(type: "text", nullable: false),
                    area_type_id = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_area_item", x => x.area_item_id);
                    table.ForeignKey(
                        name: "fk_area_item_area_type_area_type_id",
                        column: x => x.area_type_id,
                        principalTable: "area_type",
                        principalColumn: "area_type_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "catalog_item",
                columns: table => new
                {
                    catalog_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    catalog_type_id = table.Column<int>(type: "integer", nullable: false),
                    catalog_item_description = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_item", x => x.catalog_item_id);
                    table.ForeignKey(
                        name: "fk_catalog_item_catalog_type_catalog_type_id",
                        column: x => x.catalog_type_id,
                        principalTable: "catalog_type",
                        principalColumn: "catalog_type_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ev_evaluacion_residente",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    periodo_id = table.Column<int>(type: "integer", nullable: false),
                    evaluador_user_id = table.Column<int>(type: "integer", nullable: true),
                    evaluador_person_id = table.Column<int>(type: "integer", nullable: true),
                    evaluado_user_id = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<int>(type: "integer", nullable: true),
                    area_nombre = table.Column<string>(type: "text", nullable: false),
                    nota = table.Column<decimal>(type: "numeric", nullable: true),
                    comentario = table.Column<string>(type: "text", nullable: true),
                    no_aplica = table.Column<bool>(type: "boolean", nullable: false),
                    no_aplica_motivo = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ev_evaluacion_residente", x => x.id);
                    table.ForeignKey(
                        name: "fk_ev_evaluacion_residente_ev_periodo_periodo_id",
                        column: x => x.periodo_id,
                        principalTable: "ev_periodo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ev_evaluacion_residente_project_project_id",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "project_id");
                });

            migrationBuilder.CreateTable(
                name: "ev_no_aplica",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    periodo_id = table.Column<int>(type: "integer", nullable: false),
                    evaluado_user_id = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<int>(type: "integer", nullable: true),
                    motivo = table.Column<string>(type: "text", nullable: false),
                    registrado_por = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ev_no_aplica", x => x.id);
                    table.ForeignKey(
                        name: "fk_ev_no_aplica_ev_periodo_periodo_id",
                        column: x => x.periodo_id,
                        principalTable: "ev_periodo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "area_scope",
                columns: table => new
                {
                    area_scope_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_item_id = table.Column<int>(type: "integer", nullable: false),
                    area_scope_parent_id = table.Column<int>(type: "integer", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_area_scope", x => x.area_scope_id);
                    table.ForeignKey(
                        name: "fk_area_scope_area_item_area_item_id",
                        column: x => x.area_item_id,
                        principalTable: "area_item",
                        principalColumn: "area_item_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_area_scope_area_scope_area_scope_parent_id",
                        column: x => x.area_scope_parent_id,
                        principalTable: "area_scope",
                        principalColumn: "area_scope_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scope_item",
                columns: table => new
                {
                    scope_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lesson_area_id = table.Column<int>(type: "integer", nullable: false),
                    catalog_item_id = table.Column<int>(type: "integer", nullable: false),
                    scope_item_parent_id = table.Column<int>(type: "integer", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scope_item", x => x.scope_item_id);
                    table.ForeignKey(
                        name: "fk_scope_item_catalog_item_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_item",
                        principalColumn: "catalog_item_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scope_item_lesson_area_lesson_area_id",
                        column: x => x.lesson_area_id,
                        principalTable: "lesson_area",
                        principalColumn: "lesson_area_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scope_item_scope_item_scope_item_parent_id",
                        column: x => x.scope_item_parent_id,
                        principalTable: "scope_item",
                        principalColumn: "scope_item_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scope_template_item",
                columns: table => new
                {
                    scope_template_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    scope_template_id = table.Column<int>(type: "integer", nullable: false),
                    catalog_item_id = table.Column<int>(type: "integer", nullable: false),
                    scope_template_item_parent_id = table.Column<int>(type: "integer", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scope_template_item", x => x.scope_template_item_id);
                    table.ForeignKey(
                        name: "fk_scope_template_item_catalog_item_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_item",
                        principalColumn: "catalog_item_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scope_template_item_scope_template_item_scope_template_item",
                        column: x => x.scope_template_item_parent_id,
                        principalTable: "scope_template_item",
                        principalColumn: "scope_template_item_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_scope_template_item_scope_template_scope_template_id",
                        column: x => x.scope_template_id,
                        principalTable: "scope_template",
                        principalColumn: "scope_template_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ev_evaluacion_residente_detalle",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    evaluacion_id = table.Column<int>(type: "integer", nullable: false),
                    plantilla_id = table.Column<int>(type: "integer", nullable: true),
                    criterio = table.Column<string>(type: "text", nullable: false),
                    puntaje = table.Column<int>(type: "integer", nullable: true),
                    es_na = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ev_evaluacion_residente_detalle", x => x.id);
                    table.ForeignKey(
                        name: "fk_ev_evaluacion_residente_detalle_ev_evaluacion_residente_eva",
                        column: x => x.evaluacion_id,
                        principalTable: "ev_evaluacion_residente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ev_evaluacion_residente_detalle_ev_plantilla_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "ev_plantilla",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_area_item_area_type_id",
                table: "area_item",
                column: "area_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_area_scope_area_item_id",
                table: "area_scope",
                column: "area_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_area_scope_area_scope_parent_id",
                table: "area_scope",
                column: "area_scope_parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_item_catalog_type_id",
                table: "catalog_item",
                column: "catalog_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_ev_evaluacion_residente_periodo_id",
                table: "ev_evaluacion_residente",
                column: "periodo_id");

            migrationBuilder.CreateIndex(
                name: "ix_ev_evaluacion_residente_project_id",
                table: "ev_evaluacion_residente",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_ev_evaluacion_residente_detalle_evaluacion_id",
                table: "ev_evaluacion_residente_detalle",
                column: "evaluacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_ev_evaluacion_residente_detalle_plantilla_id",
                table: "ev_evaluacion_residente_detalle",
                column: "plantilla_id");

            migrationBuilder.CreateIndex(
                name: "ix_ev_no_aplica_periodo_id",
                table: "ev_no_aplica",
                column: "periodo_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_item_catalog_item_id",
                table: "scope_item",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_item_lesson_area_id",
                table: "scope_item",
                column: "lesson_area_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_item_scope_item_parent_id",
                table: "scope_item",
                column: "scope_item_parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_template_item_catalog_item_id",
                table: "scope_template_item",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_template_item_scope_template_id",
                table: "scope_template_item",
                column: "scope_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_scope_template_item_scope_template_item_parent_id",
                table: "scope_template_item",
                column: "scope_template_item_parent_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "area_scope");

            migrationBuilder.DropTable(
                name: "ev_asignacion_supervisor");

            migrationBuilder.DropTable(
                name: "ev_evaluacion_residente_detalle");

            migrationBuilder.DropTable(
                name: "ev_no_aplica");

            migrationBuilder.DropTable(
                name: "ev_recordatorio_log");

            migrationBuilder.DropTable(
                name: "project_staff_reminder");

            migrationBuilder.DropTable(
                name: "scope_item");

            migrationBuilder.DropTable(
                name: "scope_template_item");

            migrationBuilder.DropTable(
                name: "area_item");

            migrationBuilder.DropTable(
                name: "ev_evaluacion_residente");

            migrationBuilder.DropTable(
                name: "ev_plantilla");

            migrationBuilder.DropTable(
                name: "lesson_area");

            migrationBuilder.DropTable(
                name: "catalog_item");

            migrationBuilder.DropTable(
                name: "scope_template");

            migrationBuilder.DropTable(
                name: "area_type");

            migrationBuilder.DropTable(
                name: "ev_periodo");

            migrationBuilder.DropTable(
                name: "catalog_type");

            migrationBuilder.DropColumn(
                name: "staff_email",
                table: "project");

            migrationBuilder.DropColumn(
                name: "catalog_item_id",
                table: "lesson");

            migrationBuilder.RenameColumn(
                name: "lesson_area_id",
                table: "lesson",
                newName: "sub_area_id");
        }
    }
}
