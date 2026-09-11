using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.SsomaModule.PetsFeature.Application.Services;

// Vuelca el estado ACTUAL del PETS a un PDF, en el mismo orden que las pestañas de
// la pantalla de detalle — para que quien lo va completando pueda ver "cómo va
// quedando" sin abrir cada sección por separado. No reemplaza el documento oficial
// en SharePoint ni intenta replicar su formato exacto (esa plantilla vive en Word);
// esto es una vista de trabajo.
public static class PetsPdfService
{
    private static readonly HttpClient _http = new();

    private static readonly (string Tipo, string Etiqueta)[] TiposEpp =
        [("basico", "Básico"), ("especifico", "Específico según la tarea"), ("emergencia", "Emergencia")];

    private static readonly (string Tipo, string Etiqueta)[] TiposRecurso =
        [("equipo", "Equipos"), ("herramienta", "Herramientas"), ("material", "Materiales")];

    // QuestPDF arma el layout de forma SÍNCRONA (el árbol de Column/Item de abajo), así
    // que las imágenes de cada paso se descargan ANTES de crear el Document — mismo
    // patrón que InspeccionPdfService. ImagenUrl ya es una URL pública (local o Azure
    // Blob), así que basta un GET plano; si una falla (borrada, red caída) se omite esa
    // imagen puntual en vez de tumbar el PDF entero.
    private static async Task<byte[]?> DescargarImagenAsync(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        try { return await _http.GetByteArrayAsync(url); }
        catch { return null; }
    }

    public static async Task<byte[]> GenerarPdfAsync(PetDetalleDto pet)
    {
        var imagenesPorUrl = new Dictionary<string, byte[]>();
        foreach (var url in pet.Pasos.Concat(pet.Responsabilidades)
                     .SelectMany(p => p.Imagenes.Select(i => i.Url))
                     .Where(u => !string.IsNullOrEmpty(u))
                     .Distinct())
        {
            var bytes = await DescargarImagenAsync(url);
            if (bytes != null) imagenesPorUrl[url!] = bytes;
        }

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                page.Header().PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).Column(col =>
                {
                    col.Item().Text(pet.Nombre).Bold().FontSize(14);
                    if (!string.IsNullOrWhiteSpace(pet.Codigo))
                        col.Item().Text($"Código: {pet.Codigo}").FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    void Titulo(string texto) =>
                        col.Item().PaddingTop(14).PaddingBottom(6).Background(Colors.Grey.Lighten3).Padding(4).Text(texto).Bold().FontSize(11);

                    // "4 +" para que el nivel 0 empiece exactamente donde empieza la letra del
                    // título (el título tiene Padding(4) por el fondo gris) — antes el contenido
                    // arrancaba pegado al margen y el título 4px más adentro, desalineados.
                    void Parrafo(string texto, int nivel = 0) =>
                        col.Item().PaddingLeft(4 + nivel * 14).PaddingBottom(5).Text(texto ?? string.Empty).FontSize(9);

                    // Para Definiciones: "Término: descripción" -> el término en negrita. Si la
                    // línea no trae ":" se muestra igual, sin romper el resto del texto.
                    void ParrafoConTerminoEnNegrita(string texto, int nivel = 0)
                    {
                        var separador = texto.IndexOf(':');
                        if (separador <= 0) { Parrafo(texto, nivel); return; }

                        col.Item().PaddingLeft(4 + nivel * 14).PaddingBottom(5).Text(t =>
                        {
                            t.Span(texto[..(separador + 1)]).Bold().FontSize(9);
                            t.Span(texto[(separador + 1)..]).FontSize(9);
                        });
                    }

                    void TextoLibre(string? texto, bool negritaAntesDeDosPuntos = false)
                    {
                        if (string.IsNullOrWhiteSpace(texto)) { Parrafo("(Sin contenido)"); return; }
                        foreach (var linea in texto.Split('\n'))
                        {
                            if (string.IsNullOrWhiteSpace(linea)) continue;
                            if (negritaAntesDeDosPuntos) ParrafoConTerminoEnNegrita(linea);
                            else Parrafo(linea);
                        }
                    }

                    // "numeroSeccion" (ej. "6" para Responsabilidades, "8" para Procedimiento) es
                    // el número real de la sección en ESTE pdf — antes cada árbol arrancaba su
                    // numeración en "1" sin importar qué sección lo llamaba, así que "Residente de
                    // obra" salía como "1." en vez de "6.1".
                    void Arbol(List<PetPasoDto> pasos, string numeroSeccion)
                    {
                        if (pasos.Count == 0) { Parrafo("(Sin contenido)"); return; }
                        // "?? 0" en vez de agrupar por el int? crudo: Dictionary<int?, T> lanza
                        // ArgumentNullException al insertar la clave null (los pasos raíz, sin
                        // padre) aunque el tipo la permita — 0 nunca es un Id real (autoincremental
                        // desde 1), así que sirve de centinela seguro para "sin padre".
                        var hijosPorPadre = pasos.GroupBy(p => p.ParentId ?? 0).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Orden).ToList());

                        void Render(int parentId, int nivel, string prefijoSubtitulo)
                        {
                            if (!hijosPorPadre.TryGetValue(parentId, out var hijos)) return;
                            var nSubtitulo = 0;
                            var nLetra = 0;
                            foreach (var h in hijos)
                            {
                                var numero = string.Empty;
                                if (h.Tipo == "subtitulo")
                                {
                                    nSubtitulo++;
                                    numero = $"{prefijoSubtitulo}.{nSubtitulo}";
                                }
                                else if (h.Tipo == "letra")
                                {
                                    nLetra++;
                                    numero = LetraDesdeIndice(nLetra - 1);
                                }

                                var prefijo = h.Tipo switch
                                {
                                    "subtitulo" => $"{numero}. ",
                                    "letra" => $"{numero}. ",
                                    "guion" => "- ",
                                    _ => string.Empty
                                };
                                // "medio_ambiente" (única categoría por ahora — ver Categoria en
                                // SsomaPetPaso) se resalta en verde con un marcador simple; agregar
                                // otra categoría más adelante es un caso nuevo en este switch, no
                                // una migración.
                                var esMedioAmbiente = h.Categoria == "medio_ambiente";
                                var colorTexto = esMedioAmbiente ? Colors.Green.Darken2 : Colors.Black;
                                var textoConMarca = esMedioAmbiente ? $"🌿 {prefijo}{h.Descripcion}" : $"{prefijo}{h.Descripcion}";

                                if (h.Tipo == "subtitulo")
                                    col.Item().PaddingLeft(4 + nivel * 14).PaddingBottom(5).Text(textoConMarca).Bold().FontSize(9).FontColor(colorTexto);
                                else if (esMedioAmbiente)
                                    col.Item().PaddingLeft(4 + nivel * 14).PaddingBottom(5).Text(textoConMarca).FontSize(9).FontColor(colorTexto);
                                else
                                    Parrafo($"{prefijo}{h.Descripcion}", nivel);

                                // Varias imágenes por paso, una debajo de otra (un párrafo del Word
                                // puede traer 2-3 fotos juntas — antes solo se guardaba la primera).
                                foreach (var img in h.Imagenes)
                                {
                                    if (imagenesPorUrl.TryGetValue(img.Url, out var imagenBytes))
                                        col.Item().PaddingLeft(4 + (nivel + 1) * 14).PaddingBottom(6).MaxWidth(260).Image(imagenBytes).FitWidth();
                                }

                                Render(h.Id, nivel + 1, h.Tipo == "subtitulo" ? numero : prefijoSubtitulo);
                            }
                        }
                        Render(0, 0, numeroSeccion);
                    }

                    // Numera de forma continua a través de VARIOS grupos de catálogo bajo la
                    // MISMA sección (ej. EPP Básico=7.1, Específico=7.2, Emergencia=7.3, y si
                    // Recursos comparte sección: Equipos=7.4, Herramientas=7.5...) — "contador"
                    // se pasa por referencia para que el llamador siga la cuenta entre grupos.
                    void CatalogoPorTipo(List<PetItemSeleccionadoDto> items, (string Tipo, string Etiqueta)[] tipos, string numeroSeccion, ref int contador)
                    {
                        foreach (var (tipo, etiqueta) in tipos)
                        {
                            contador++;
                            Parrafo($"{numeroSeccion}.{contador} {etiqueta}:");
                            var delTipo = items.Where(i => i.Tipo == tipo).ToList();
                            if (delTipo.Count == 0) { Parrafo("(Sin ítems seleccionados)", 1); continue; }
                            foreach (var i in delTipo) Parrafo($"- {i.Descripcion}", 1);
                        }
                    }

                    Titulo("1. Introducción");
                    TextoLibre(pet.SeccionesTexto.GetValueOrDefault("introduccion"));

                    Titulo("2. Alcance");
                    TextoLibre(pet.SeccionesTexto.GetValueOrDefault("alcance"));

                    Titulo("3. Objetivo");
                    TextoLibre(pet.SeccionesTexto.GetValueOrDefault("objetivo"));

                    Titulo("4. Marco Legal");
                    if (pet.MarcoLegal.Count == 0) Parrafo("(Sin ítems seleccionados)");
                    foreach (var m in pet.MarcoLegal) Parrafo($"- {m.Descripcion}");

                    Titulo("5. Definiciones");
                    TextoLibre(pet.SeccionesTexto.GetValueOrDefault("definiciones"), negritaAntesDeDosPuntos: true);

                    Titulo("6. Responsabilidades");
                    Arbol(pet.Responsabilidades, "6");

                    Titulo("7. Gestión de personal");
                    var contadorGestionPersonal = 0;
                    CatalogoPorTipo(pet.Epp, TiposEpp, "7", ref contadorGestionPersonal);
                    CatalogoPorTipo(pet.Recursos, TiposRecurso, "7", ref contadorGestionPersonal);

                    Titulo("8. Procedimiento de trabajo");
                    Arbol(pet.Pasos, "8");

                    Titulo("9. Restricciones");
                    TextoLibre(pet.SeccionesTexto.GetValueOrDefault("restricciones"));

                    Titulo("10. Anexos");
                    if (pet.Anexos.Count == 0) Parrafo("(Sin anexos)");
                    foreach (var a in pet.Anexos) Parrafo($"- {a.Nombre}");

                    Titulo("Firmas");
                    foreach (var rol in new[] { "elaborado", "revisado", "aprobado" })
                    {
                        var f = pet.Firmas.GetValueOrDefault(rol);
                        var etiqueta = rol switch { "elaborado" => "Elaborado por", "revisado" => "Revisado por", _ => "Aprobado por" };
                        var fecha = f?.Fecha?.ToString("dd/MM/yyyy") ?? "-";
                        Parrafo($"{etiqueta}: {f?.Nombre ?? "-"} — {f?.Cargo ?? "-"} — {fecha}");
                    }
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Generado el ").FontSize(8).FontColor(Colors.Grey.Medium);
                    t.Span(DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm") + " UTC").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return doc.GeneratePdf();
    }

    // a, b, c, ..., z, aa, ab, ... — igual que la numeración de letras en la pantalla.
    private static string LetraDesdeIndice(int i)
    {
        var s = string.Empty;
        var n = i;
        do
        {
            s = (char)('a' + (n % 26)) + s;
            n = (n / 26) - 1;
        } while (n >= 0);
        return s;
    }
}
