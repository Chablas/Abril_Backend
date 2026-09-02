namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Shared
{
    /// <summary>
    /// Cómo se le llama en los correos de EMO a la persona examinada: <b>trabajador</b> si ya
    /// trabaja en Abril, <b>postulante</b> si su ficha todavía es de pre-ingreso (el EMO de Ingreso
    /// que GTH programa desde Reclutamiento).
    ///
    /// Está en un solo lugar porque los cinco correos de EMO lo repiten —la etiqueta de la tarjeta,
    /// la bajada y el aviso de cada uno— y se pidió expresamente que el término sea el mismo en
    /// todos. Con la palabra escrita a mano en cada plantilla, arreglar uno y olvidar otro deja un
    /// correo llamando "trabajador" a alguien que todavía no lo es, que es justo el bug que esto
    /// evita.
    ///
    /// Qué audiencia le toca a cada ficha lo decide
    /// <c>EmoCorreoEventoCodigo.ParaFicha</c> (pre-ingreso, no el tipo de EMO); acá solo se
    /// resuelve la palabra.
    /// </summary>
    public static class EmoExaminadoTexto
    {
        /// <summary>Minúscula, para el medio de una oración ("...del trabajador Juan Pérez").</summary>
        public static string Minuscula(bool esPostulante) => esPostulante ? "postulante" : "trabajador";

        /// <summary>Capitalizada, para la etiqueta de una fila de la tarjeta o el inicio de una oración.</summary>
        public static string Capitalizada(bool esPostulante) => esPostulante ? "Postulante" : "Trabajador";

        /// <summary>Plural en minúscula, para los correos que resumen a varias personas.</summary>
        public static string Plural(bool esPostulante) => esPostulante ? "postulantes" : "trabajadores";

        /// <summary>
        /// Con artículo determinado, para los avisos ("El postulante debe presentarse...").
        /// </summary>
        public static string ConArticulo(bool esPostulante) =>
            esPostulante ? "El postulante" : "El trabajador";
    }
}
