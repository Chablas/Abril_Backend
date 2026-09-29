using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Shared.Services.RolesPorFuncion.Interfaces
{
    /// <summary>
    /// Los roles de Gestión Administrativa que salen de lo que la persona HACE, no de una pantalla:
    ///
    ///   • Por su puesto: JEFE (categoría JEFE), SUB GERENTE, GERENTE (categoría GERENTE), RESIDENTE
    ///     (categoría RESIDENTE), TESORERO (categoría TESORERO) y ADMINISTRADOR DE OBRA (administra
    ///     una obra activa). Se dan al crear la cuenta; después se manejan desde Seguridad, como
    ///     cualquier rol.
    ///   • Por lo asignado A MANO en Revisores de Áreas o en la ficha de un trabajador:
    ///       – CONSOLIDADOR, exacto: lo tiene quien figura como consolidador y no entra ya a Gestión
    ///         de Rendiciones y Consolidados por otro rol; a quien deja de figurar se le quita.
    ///       – quien figura como aprobador (de la salida, de la 1.ª revisión o del consolidado) y no
    ///         puede entrar a la bandeja donde actúa recibe el rol de su jefatura (JEFE si su puesto
    ///         no es de ninguna). Solo se agrega: quitarlo es de Seguridad.
    ///
    /// Todo es best-effort: si falla se registra y no corta lo que lo disparó.
    /// </summary>
    public interface IRolesPorFuncionService
    {
        /// <summary>
        /// Primer login: los roles del puesto de la persona y lo que le toque por lo asignado a mano.
        /// Devuelve los roles vivos del usuario después de asignar, para armar su token.
        /// </summary>
        Task<List<RoleSimpleDTO>> AsignarAlCrearCuentaAsync(int userId);

        /// <summary>
        /// Tras guardar actores (Revisores de Áreas, Delegación de Revisión o la ficha): recalcula
        /// CONSOLIDADOR y agrega la jefatura a los aprobadores que no entran a su bandeja. Avisa a los
        /// usuarios que cambiaron para que refresquen sus permisos sin volver a iniciar sesión.
        /// </summary>
        /// <param name="actorUserId">
        /// Quien guardó: queda como autor de las filas de user_role. Null si quien llama no lo conoce
        /// (la ficha del trabajador): cada fila queda a nombre de su propio usuario, como en el primer login.
        /// </param>
        Task SincronizarAsync(int? actorUserId = null);
    }
}
