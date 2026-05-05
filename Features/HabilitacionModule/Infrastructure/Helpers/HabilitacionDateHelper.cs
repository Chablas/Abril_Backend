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
            if (!string.Equals(estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
                return AsUtc(dtoVigencia);
            if (requiereVigencia)
                return AsUtc(dtoVigencia);
            return DateTime.SpecifyKind(new DateOnly(2040, 12, 31).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        }
    }
}
