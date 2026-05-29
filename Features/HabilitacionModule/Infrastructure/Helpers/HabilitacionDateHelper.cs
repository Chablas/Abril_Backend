namespace Abril_Backend.Features.Habilitacion.Infrastructure.Helpers
{
    public static class HabilitacionDateHelper
    {
        public static DateTime? AsUtc(DateTime? value)
        {
            if (!value.HasValue) return null;
            var v = value.Value;
            return v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc);
        }

        public static DateTime? ResolverVigencia(bool requiereVigencia, string estado, DateTime? dtoVigencia)
        {
            var esSintetico = !requiereVigencia
                && (string.Equals(estado, "Aprobado", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estado, "Enviado", StringComparison.OrdinalIgnoreCase));
            if (esSintetico)
                return DateTime.SpecifyKind(new DateOnly(2040, 12, 31).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            return AsUtc(dtoVigencia);
        }
    }
}
