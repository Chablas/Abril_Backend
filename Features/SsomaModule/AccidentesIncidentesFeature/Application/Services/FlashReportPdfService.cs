using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Services;

public static class FlashReportPdfService
{
    private static readonly string[] NivelConsecuencia =
    [
        "", "1 - Sin daño", "2 - Lesión leve", "3 - Lesión con tiempo perdido",
        "4 - Lesión grave", "5 - Fatalidad simple", "6 - Fatalidad múltiple"
    ];

    public static byte[] Generar(FlashReportDetalleDto fr, byte[]? foto1, byte[]? foto2)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeContent(c, fr, foto1, foto2));
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("SSO-FO-035 | Flash Report | Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();

        void ComposeHeader(IContainer c)
        {
            c.Row(row =>
            {
                row.ConstantItem(80).Image(Placeholders.Image(80, 40));  // logo placeholder
                row.RelativeItem().Column(col =>
                {
                    col.Item().AlignCenter().Text("FLASH REPORT").Bold().FontSize(14);
                    col.Item().AlignCenter().Text(fr.Codigo).Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                });
                row.ConstantItem(100).Column(col =>
                {
                    col.Item().Text("Código: SSO-FO-035").FontSize(7);
                    col.Item().Text($"Versión: 01").FontSize(7);
                    col.Item().Text($"Fecha: {fr.Fecha:dd/MM/yyyy}").FontSize(7);
                });
            });
        }
    }

    private static void ComposeContent(IContainer c, FlashReportDetalleDto fr, byte[]? foto1, byte[]? foto2)
    {
        c.Column(col =>
        {
            col.Spacing(6);

            // Datos del proyecto
            col.Item().Element(e => SectionHeader(e, "DATOS DEL PROYECTO Y ETAPA"));
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); });
                Row(t, "Proyecto", fr.ProyectoNombre);
                Row(t, "Fecha / Hora", $"{fr.Fecha:dd/MM/yyyy} {(fr.Hora.HasValue ? fr.Hora.Value.ToString(@"hh\:mm") : "")}");
                Row(t, "Etapa", fr.EtapaProyectoNombre ?? "—");
                Row(t, "Partida", fr.PartidaNombre ?? "—");
                Row(t, "Lugar exacto", fr.LugarExacto);
                Row(t, "Tipo de evento", fr.TipoNombre);
            });

            // Empresa involucrada
            col.Item().Element(e => SectionHeader(e, "EMPRESA INVOLUCRADA"));
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); });
                Row(t, "Razón social", fr.EmpresaAbrilNombre ?? fr.ContributorNombre ?? "—");
                Row(t, "Jefe inmediato", fr.JefeInmediatoNombre ?? "—");
            });

            // Datos del trabajador
            col.Item().Element(e => SectionHeader(e, "DATOS DEL TRABAJADOR AFECTADO"));
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); });
                Row(t, "Nombre", fr.TrabajadorNombre ?? "—");
                Row(t, "Puesto", fr.PuestoTrabajo ?? "—");
                Row(t, "Edad", fr.Edad?.ToString() ?? "—");
                Row(t, "Años experiencia", fr.AniosExperiencia?.ToString() ?? "—");
                Row(t, "Celular", fr.CelularTrabajador ?? "—");
                Row(t, "Parte afectada", fr.ParteAfectadaNombre ?? "—");
            });

            // Daños y consecuencias
            col.Item().Element(e => SectionHeader(e, "DAÑOS Y CONSECUENCIAS"));
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); });
                Row(t, "Daño al proceso/equipo", fr.DanoProceso ?? "—");
                Row(t, "Consecuencia real (personal)", Nivel(fr.ConsecuenciaRealPersonal));
                Row(t, "Consecuencia potencial (personal)", Nivel(fr.ConsecuenciaPotencialPersonal));
            });

            // Descripción del evento
            col.Item().Element(e => SectionHeader(e, "DESCRIPCIÓN DEL EVENTO"));
            col.Item().Padding(4).Text(fr.Descripcion).FontSize(9);

            // Acciones inmediatas
            col.Item().Element(e => SectionHeader(e, "ACCIONES INMEDIATAS"));
            col.Item().Padding(4).Text(fr.AccionesInmediatas ?? "—").FontSize(9);

            // Descansos médicos
            if (fr.Descansos.Count > 0)
            {
                col.Item().Element(e => SectionHeader(e, "PERÍODOS DE DESCANSO MÉDICO"));
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.ConstantColumn(100);
                        cd.ConstantColumn(100);
                        cd.ConstantColumn(60);
                        cd.RelativeColumn();
                    });
                    t.Header(h =>
                    {
                        foreach (var label in new[] { "Inicio", "Fin", "Días", "Observación" })
                            h.Cell().Background(Colors.Blue.Darken3).Padding(3)
                             .Text(label).FontColor(Colors.White).Bold().FontSize(8);
                    });
                    foreach (var d in fr.Descansos)
                    {
                        t.Cell().BorderBottom(0.5f).Padding(3).Text(d.FechaInicio.ToString("dd/MM/yyyy"));
                        t.Cell().BorderBottom(0.5f).Padding(3).Text(d.FechaFin.ToString("dd/MM/yyyy"));
                        t.Cell().BorderBottom(0.5f).Padding(3).Text(d.DiasDescanso.ToString());
                        t.Cell().BorderBottom(0.5f).Padding(3).Text(d.Observacion ?? "");
                    }
                });
            }

            // Fotos
            if (foto1 != null || foto2 != null)
            {
                col.Item().Element(e => SectionHeader(e, "FOTOGRAFÍAS"));
                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    if (foto1 != null) row.RelativeItem().Image(foto1).FitArea();
                    if (foto2 != null) row.RelativeItem().Image(foto2).FitArea();
                    if (foto1 == null || foto2 == null) row.RelativeItem();
                });
            }

            // Elaborado por
            col.Item().Element(e => SectionHeader(e, "ELABORADO POR"));
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); });
                Row(t, "Nombre", fr.ElaboradoPorNombre ?? "—");
                Row(t, "Cargo", fr.ElaboradoPorCargo ?? "—");
                Row(t, "Email", fr.ElaboradoPorEmail ?? "—");
                Row(t, "Teléfono", fr.ElaboradoPorTelefono ?? "—");
            });
        });
    }

    private static void SectionHeader(IContainer c, string title)
    {
        c.Background(Colors.Blue.Darken3).Padding(4)
         .Text(title).Bold().FontColor(Colors.White).FontSize(9);
    }

    private static void Row(TableDescriptor t, string label, string value)
    {
        t.Cell().BorderBottom(0.5f).Padding(3).Text(label).Bold().FontSize(8);
        t.Cell().BorderBottom(0.5f).Padding(3).Text(value).FontSize(8);
    }

    private static string Nivel(int? n) =>
        n.HasValue && n.Value >= 1 && n.Value <= 6 ? NivelConsecuencia[n.Value] : "—";
}
