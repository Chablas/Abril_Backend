using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddFlashReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aprobado_en",
                table: "ss_charla");

            migrationBuilder.DropColumn(
                name: "motivo_rechazo",
                table: "ss_charla");

            migrationBuilder.RenameColumn(
                name: "aprobado_por_id",
                table: "ss_charla",
                newName: "proyecto_id");

            migrationBuilder.AddColumn<string>(
                name: "observaciones",
                table: "vecino_compromiso",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "es_capacitacion_individual",
                table: "ss_charla",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "vecino_compromiso_normativa",
                columns: table => new
                {
                    vecino_compromiso_normativa_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vecino_compromiso_id = table.Column<int>(type: "integer", nullable: false),
                    archivo_url = table.Column<string>(type: "text", nullable: false),
                    original_file_name = table.Column<string>(type: "text", nullable: true),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vecino_compromiso_normativa", x => x.vecino_compromiso_normativa_id);
                    table.ForeignKey(
                        name: "fk_vecino_compromiso_normativa_vecino_compromiso_vecino_compro",
                        column: x => x.vecino_compromiso_id,
                        principalTable: "vecino_compromiso",
                        principalColumn: "vecino_compromiso_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vecino_limpieza_tipo",
                columns: table => new
                {
                    vecino_limpieza_tipo_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vecino_limpieza_tipo", x => x.vecino_limpieza_tipo_id);
                });

            migrationBuilder.CreateTable(
                name: "vecino_limpieza",
                columns: table => new
                {
                    vecino_limpieza_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    vecino_limpieza_tipo_id = table.Column<int>(type: "integer", nullable: false),
                    vecino_id = table.Column<int>(type: "integer", nullable: true),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    atencion_archivo_url = table.Column<string>(type: "text", nullable: true),
                    atencion_original_file_name = table.Column<string>(type: "text", nullable: true),
                    atencion_vecino_compromiso_id = table.Column<int>(type: "integer", nullable: true),
                    created_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vecino_limpieza", x => x.vecino_limpieza_id);
                    table.ForeignKey(
                        name: "fk_vecino_limpieza_vecino_limpieza_tipo_vecino_limpieza_tipo_id",
                        column: x => x.vecino_limpieza_tipo_id,
                        principalTable: "vecino_limpieza_tipo",
                        principalColumn: "vecino_limpieza_tipo_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vecino_limpieza_vecino_vecino_id",
                        column: x => x.vecino_id,
                        principalTable: "vecino",
                        principalColumn: "vecino_id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_vecino_compromiso_normativa_vecino_compromiso_id",
                table: "vecino_compromiso_normativa",
                column: "vecino_compromiso_id");

            migrationBuilder.CreateIndex(
                name: "ix_vecino_limpieza_vecino_id",
                table: "vecino_limpieza",
                column: "vecino_id");

            migrationBuilder.CreateIndex(
                name: "ix_vecino_limpieza_vecino_limpieza_tipo_id",
                table: "vecino_limpieza",
                column: "vecino_limpieza_tipo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vecino_compromiso_normativa");

            migrationBuilder.DropTable(
                name: "vecino_limpieza");

            migrationBuilder.DropTable(
                name: "vecino_limpieza_tipo");

            migrationBuilder.DropColumn(
                name: "observaciones",
                table: "vecino_compromiso");

            migrationBuilder.DropColumn(
                name: "es_capacitacion_individual",
                table: "ss_charla");

            migrationBuilder.RenameColumn(
                name: "proyecto_id",
                table: "ss_charla",
                newName: "aprobado_por_id");

            migrationBuilder.AddColumn<DateTime>(
                name: "aprobado_en",
                table: "ss_charla",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_rechazo",
                table: "ss_charla",
                type: "text",
                nullable: true);
        }
    }
}
