using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class PlaneamientoBimTorreRediseno : Migration
    {
        /// <summary>
        /// Solo Planeamiento BIM (Configuración Inicial). El scaffold automático de esta
        /// migración traía además un volumen enorme de drift acumulado de otros módulos
        /// (workers, gth_aprobacion_gg, reunion_tema, ss_equipo, incluso un DropTable de
        /// vecino_licencia) — esas tablas ya tienen esas columnas aplicadas en la BD real
        /// pero el historial de EF nunca las registró (probablemente aplicadas a mano, ver
        /// Migrations_Manual/). Ese contenido se descartó a propósito de este Up()/Down():
        /// no se toca acá, se resuelve aparte el día que se decida encarar ese drift.
        ///
        /// Acá solo van 2 renames reales (RenameTable/RenameColumn, NO drop+create — se
        /// verificó a mano) y las 2 columnas nuevas de conteo de sectores. bim_zona_sector,
        /// bim_registro_diario.sector_id y bim_bloqueo.zona_sector_id NO se tocan: siguen
        /// apuntando a bim_zona_sector tal cual, que ya existe y no se renombra ni se borra.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "bim_proyecto_zona",
                newName: "bim_proyecto_torre");

            migrationBuilder.RenameTable(
                name: "bim_zona_nivel",
                newName: "bim_torre_nivel");

            migrationBuilder.RenameColumn(
                name: "zona_id",
                table: "bim_torre_nivel",
                newName: "torre_id");

            migrationBuilder.RenameIndex(
                name: "ix_bim_proyecto_zona_project_id",
                table: "bim_proyecto_torre",
                newName: "ix_bim_proyecto_torre_project_id");

            migrationBuilder.RenameIndex(
                name: "ix_bim_zona_nivel_zona_id",
                table: "bim_torre_nivel",
                newName: "ix_bim_torre_nivel_torre_id");

            migrationBuilder.AddColumn<int>(
                name: "cantidad_sectores_subestructura",
                table: "bim_proyecto_torre",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cantidad_sectores_superestructura",
                table: "bim_proyecto_torre",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cantidad_sectores_superestructura",
                table: "bim_proyecto_torre");

            migrationBuilder.DropColumn(
                name: "cantidad_sectores_subestructura",
                table: "bim_proyecto_torre");

            migrationBuilder.RenameIndex(
                name: "ix_bim_torre_nivel_torre_id",
                table: "bim_torre_nivel",
                newName: "ix_bim_zona_nivel_zona_id");

            migrationBuilder.RenameIndex(
                name: "ix_bim_proyecto_torre_project_id",
                table: "bim_proyecto_torre",
                newName: "ix_bim_proyecto_zona_project_id");

            migrationBuilder.RenameColumn(
                name: "torre_id",
                table: "bim_torre_nivel",
                newName: "zona_id");

            migrationBuilder.RenameTable(
                name: "bim_torre_nivel",
                newName: "bim_zona_nivel");

            migrationBuilder.RenameTable(
                name: "bim_proyecto_torre",
                newName: "bim_proyecto_zona");
        }
    }
}
