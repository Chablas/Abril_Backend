using Abril_Backend.Features.ConvivirModule.Shared.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Shared.Services
{
    /// <summary>
    /// Traduce el Cronograma de Hitos al avance que ve el propietario. Mismo criterio que la
    /// intranet (milestone-schedule, <c>getEstado</c>): un hito está cumplido cuando tiene fecha
    /// real, o sea cuando lo culminaron. Las fechas planificadas son solo la estimación.
    /// </summary>
    public static class AvanceObra
    {
        public static (ConvivirAvanceDto Avance, List<ConvivirHitoDto> Hitos) Calcular(ConvivirContextoDto contexto)
        {
            // Los hitos para propietarios que la versión vigente no tiene (p. ej. «Inicio de
            // demolición» en un terreno sin demolición) no se muestran: no son de esa obra.
            var hitos = contexto.Hitos
                .Where(h => h.EnCronograma)
                .Select(h => new ConvivirHitoDto
                {
                    Orden = h.Orden,
                    Descripcion = h.Descripcion,
                    // El inicio del hito; si es de una sola fecha, esa. La intranet guarda los de una
                    // fecha con inicio = fin, salvo «Inicio de obra», que va solo en el inicio.
                    FechaEstimada = AFecha(h.PlannedStartDate ?? h.PlannedEndDate),
                    FechaReal = AFecha(h.FechaRealFin),
                    Estado = h.FechaRealFin != null ? ConvivirHitoEstado.Cumplido : ConvivirHitoEstado.Pendiente,
                })
                .ToList();

            var actual = hitos.LastOrDefault(h => h.Estado == ConvivirHitoEstado.Cumplido);
            var proximo = hitos.FirstOrDefault(h =>
                h.Estado != ConvivirHitoEstado.Cumplido && (actual == null || h.Orden > actual.Orden));

            if (proximo != null)
                proximo.Estado = ConvivirHitoEstado.Proximo;

            // La entrega es el último hito para propietarios (Edificio concluido): su fin, real si
            // ya se cumplió.
            var ultimo = contexto.Hitos.OrderBy(h => h.Orden).LastOrDefault();
            var entrega = ultimo is { EnCronograma: true }
                ? AFecha(ultimo.FechaRealFin ?? ultimo.PlannedEndDate ?? ultimo.PlannedStartDate)
                : null;

            var avance = new ConvivirAvanceDto
            {
                TieneCronograma = contexto.TieneCronograma,
                HitoActual = actual,
                ProximoHito = proximo,
                EntregaEstimada = entrega ?? AFecha(contexto.FinObraProyecto),
            };

            return (avance, hitos);
        }

        private static DateOnly? AFecha(DateTime? fecha) =>
            fecha == null ? null : DateOnly.FromDateTime(fecha.Value);
    }
}
