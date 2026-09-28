using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abril_Backend.Migrations
{
    /// <inheritdoc />
    public partial class PlaneamientoBimCargaDiariaSectorPlano : Migration
    {
        /// <summary>
        /// El scaffold automático salió vacío: EF nunca tuvo esta FK en su historial de
        /// migraciones (mismo patrón de drift que el resto de esta sesión — la FK existe
        /// en la BD real pero se aplicó fuera de EF). Se escribe a mano, aislada.
        ///
        /// bim_registro_diario.sector_id deja de ser FK a bim_zona_sector y pasa a ser un
        /// int plano (número de sector derivado 1..N). La columna en sí NO cambia de tipo
        /// ni de nombre, solo se elimina la constraint. bim_zona_sector sigue existiendo
        /// físicamente (no se toca su tabla ni sus datos).
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "bim_registro_diario_sector_id_fkey",
                table: "bim_registro_diario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "bim_registro_diario_sector_id_fkey",
                table: "bim_registro_diario",
                column: "sector_id",
                principalTable: "bim_zona_sector",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
