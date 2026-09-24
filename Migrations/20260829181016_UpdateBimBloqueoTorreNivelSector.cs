using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBimBloqueoTorreNivelSector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bim_bloqueo_bim_proyecto_torre_zona_id",
                table: "bim_bloqueo");

            migrationBuilder.DropForeignKey(
                name: "fk_bim_bloqueo_bim_torre_nivel_zona_nivel_id",
                table: "bim_bloqueo");

            migrationBuilder.DropForeignKey(
                name: "fk_bim_bloqueo_bim_zona_sector_zona_sector_id",
                table: "bim_bloqueo");

            migrationBuilder.RenameColumn(
                name: "zona_id",
                table: "bim_bloqueo",
                newName: "torre_id");

            migrationBuilder.RenameColumn(
                name: "zona_nivel_id",
                table: "bim_bloqueo",
                newName: "nivel_id");

            migrationBuilder.RenameColumn(
                name: "zona_sector_id",
                table: "bim_bloqueo",
                newName: "sector");

            migrationBuilder.RenameIndex(
                name: "ix_bim_bloqueo_zona_id",
                table: "bim_bloqueo",
                newName: "ix_bim_bloqueo_torre_id");

            migrationBuilder.RenameIndex(
                name: "ix_bim_bloqueo_zona_nivel_id",
                table: "bim_bloqueo",
                newName: "ix_bim_bloqueo_nivel_id");

            migrationBuilder.DropIndex(
                name: "ix_bim_bloqueo_zona_sector_id",
                table: "bim_bloqueo");

            migrationBuilder.AddForeignKey(
                name: "fk_bim_bloqueo_bim_proyecto_torre_torre_id",
                table: "bim_bloqueo",
                column: "torre_id",
                principalTable: "bim_proyecto_torre",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bim_bloqueo_bim_torre_nivel_nivel_id",
                table: "bim_bloqueo",
                column: "nivel_id",
                principalTable: "bim_torre_nivel",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bim_bloqueo_bim_proyecto_torre_torre_id",
                table: "bim_bloqueo");

            migrationBuilder.DropForeignKey(
                name: "fk_bim_bloqueo_bim_torre_nivel_nivel_id",
                table: "bim_bloqueo");

            migrationBuilder.RenameColumn(
                name: "torre_id",
                table: "bim_bloqueo",
                newName: "zona_id");

            migrationBuilder.RenameColumn(
                name: "nivel_id",
                table: "bim_bloqueo",
                newName: "zona_nivel_id");

            migrationBuilder.RenameColumn(
                name: "sector",
                table: "bim_bloqueo",
                newName: "zona_sector_id");

            migrationBuilder.RenameIndex(
                name: "ix_bim_bloqueo_torre_id",
                table: "bim_bloqueo",
                newName: "ix_bim_bloqueo_zona_id");

            migrationBuilder.RenameIndex(
                name: "ix_bim_bloqueo_nivel_id",
                table: "bim_bloqueo",
                newName: "ix_bim_bloqueo_zona_nivel_id");

            migrationBuilder.CreateIndex(
                name: "ix_bim_bloqueo_zona_sector_id",
                table: "bim_bloqueo",
                column: "zona_sector_id");

            migrationBuilder.AddForeignKey(
                name: "fk_bim_bloqueo_bim_proyecto_torre_zona_id",
                table: "bim_bloqueo",
                column: "zona_id",
                principalTable: "bim_proyecto_torre",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bim_bloqueo_bim_torre_nivel_zona_nivel_id",
                table: "bim_bloqueo",
                column: "zona_nivel_id",
                principalTable: "bim_torre_nivel",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_bim_bloqueo_bim_zona_sector_zona_sector_id",
                table: "bim_bloqueo",
                column: "zona_sector_id",
                principalTable: "bim_zona_sector",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
