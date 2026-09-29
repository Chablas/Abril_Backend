namespace Abril_Backend.Features.MiPerfilModule
{
    /// <summary>
    /// Mi Perfil: lo que cada usuario administra de sí mismo, desde su nombre arriba a la izquierda
    /// del sidebar. Hoy tiene una sola sección, Mi Firma.
    ///
    /// Todavía no registra nada: Mi Firma solo aporta su controller, y el servicio de la firma
    /// (<c>IFirmaPersonalService</c>) se registra globalmente en Program.cs porque también lo usan
    /// Contabilidad, Gestión GTH y Gestión Administrativa para estampar. Las próximas secciones
    /// registran acá lo que sea solo suyo.
    /// </summary>
    public static class MiPerfilModule
    {
        public static IServiceCollection AddMiPerfilModule(this IServiceCollection services)
        {
            return services;
        }
    }
}
