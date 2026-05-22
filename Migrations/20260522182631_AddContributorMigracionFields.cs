using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddContributorMigracionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contract_origin_id",
                table: "project_sub_contractor");

            migrationBuilder.RenameColumn(
                name: "estado",
                table: "ga_solicitud_salida",
                newName: "estado_rendicion");

            migrationBuilder.RenameColumn(
                name: "indice",
                table: "ac_actividades",
                newName: "user_id2");

            migrationBuilder.AlterColumn<int>(
                name: "motivo_id",
                table: "ga_solicitud_salida",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "aprobador_email",
                table: "ga_solicitud_salida",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado_aprobacion",
                table: "ga_solicitud_salida",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_decision",
                table: "ga_solicitud_salida",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_libre",
                table: "ga_solicitud_salida",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_rechazo",
                table: "ga_solicitud_salida",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contributor_nombre_comercial",
                table: "contributor",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sp_password_temp",
                table: "contributor",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "orden",
                table: "ac_actividades",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "spi",
                table: "ac_actividades",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ac_avance_semanal",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    actividad_id = table.Column<int>(type: "integer", nullable: false),
                    semana = table.Column<DateOnly>(type: "date", nullable: false),
                    porcentaje_avance = table.Column<decimal>(type: "numeric", nullable: false),
                    spi = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ac_avance_semanal", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "costos_presupuestos_email",
                columns: table => new
                {
                    costos_presupuestos_email_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "text", nullable: false),
                    created_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_date_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_user_id = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_costos_presupuestos_email", x => x.costos_presupuestos_email_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ac_avance_semanal");

            migrationBuilder.DropTable(
                name: "costos_presupuestos_email");

            migrationBuilder.DropColumn(
                name: "aprobador_email",
                table: "ga_solicitud_salida");

            migrationBuilder.DropColumn(
                name: "estado_aprobacion",
                table: "ga_solicitud_salida");

            migrationBuilder.DropColumn(
                name: "fecha_decision",
                table: "ga_solicitud_salida");

            migrationBuilder.DropColumn(
                name: "motivo_libre",
                table: "ga_solicitud_salida");

            migrationBuilder.DropColumn(
                name: "motivo_rechazo",
                table: "ga_solicitud_salida");

            migrationBuilder.DropColumn(
                name: "contributor_nombre_comercial",
                table: "contributor");

            migrationBuilder.DropColumn(
                name: "sp_password_temp",
                table: "contributor");

            migrationBuilder.DropColumn(
                name: "orden",
                table: "ac_actividades");

            migrationBuilder.DropColumn(
                name: "spi",
                table: "ac_actividades");

            migrationBuilder.RenameColumn(
                name: "estado_rendicion",
                table: "ga_solicitud_salida",
                newName: "estado");

            migrationBuilder.RenameColumn(
                name: "user_id2",
                table: "ac_actividades",
                newName: "indice");

            migrationBuilder.AddColumn<int>(
                name: "contract_origin_id",
                table: "project_sub_contractor",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "motivo_id",
                table: "ga_solicitud_salida",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
