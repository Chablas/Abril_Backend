using System.Text;
using System.Text.RegularExpressions;
using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;

namespace Abril_Backend.Features.SsomaModule.PetsFeature.Application.Services;

// Lee un PETS ya existente en Word y separa su "PROCEDIMIENTO DE TRABAJO" en pasos
// candidatos — detectando también subtítulos, letras (a, b, c...) y guiones (-) por
// el estilo del párrafo (Heading 2, 3...) y el patrón del texto, para reconstruir la
// misma jerarquía que se edita a mano en la plataforma. No guarda nada todavía: solo
// arma la vista previa — el usuario revisa/edita/confirma antes de que se cree un
// solo paso real, porque los documentos reales son inconsistentes (estilos mal
// aplicados, numeración tipeada a mano) y para un documento de seguridad no es
// aceptable que el sistema adivine mal en silencio.
public class PetsImportService : IPetsImportService
{
    private const string MarcadorInicio = "PROCEDIMIENTO DE TRABAJO";

    private readonly IPetsService _petsService;

    public PetsImportService(IPetsService petsService)
    {
        _petsService = petsService;
    }

    // Registro interno de trabajo — "Nivel" (1 = Heading 1, 2 = Heading 2...) solo
    // se necesita mientras se arma el árbol; no viaja al DTO público.
    private record ParrafoAnotado(int Indice, int? ParentIndice, string Tipo, int Nivel, string Texto, string? ImagenBase64);

    public PetsImportPreviewDto PreviewDesdeDocx(Stream docxStream)
    {
        using var wordDoc = WordprocessingDocument.Open(docxStream, false);
        var mainPart = wordDoc.MainDocumentPart;
        if (mainPart?.Document.Body == null)
            return new PetsImportPreviewDto { SeccionEncontrada = false };

        var paragraphs = mainPart.Document.Body.Elements<Paragraph>().ToList();
        var styleNames = LoadStyleNames(mainPart);

        var todos = AnotarParrafos(paragraphs, styleNames, mainPart);
        var todosDto = todos.Select(ToPublicDto).ToList();

        // Marcador de inicio: se busca sobre el TEXTO (independiente del tipo ya
        // clasificado) — el título puede o no estar en un estilo "heading" real.
        var marcador = -1;
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (EsTituloDeSeccion(GetParagraphText(paragraphs[i]), MarcadorInicio))
            {
                marcador = i;
                break;
            }
        }

        if (marcador == -1)
            return new PetsImportPreviewDto { SeccionEncontrada = false, TodosLosParrafos = todosDto };

        // Fin de la sección: el siguiente subtítulo de nivel 1 (un "Heading 1" real,
        // como "CONTROLES ESPECIFICOS") después del marcador. Los Heading 2+ de en
        // medio son subtítulos DENTRO del procedimiento, no el final de la sección.
        var finIndice = todos.FirstOrDefault(p => p.Indice > marcador && p.Tipo == "subtitulo" && p.Nivel <= 1)?.Indice
            ?? int.MaxValue;

        var pasos = todos
            .Where(p => p.Indice > marcador && p.Indice < finIndice)
            .Select(ToPublicDto)
            .ToList();

        return new PetsImportPreviewDto { SeccionEncontrada = true, Pasos = pasos, TodosLosParrafos = todosDto };
    }

    private static ImportPasoPreviewDto ToPublicDto(ParrafoAnotado p) => new()
    {
        Indice = p.Indice,
        ParentIndice = p.ParentIndice,
        Tipo = p.Tipo,
        Texto = p.Texto,
        ImagenBase64 = p.ImagenBase64
    };

    // Recorre TODO el documento una sola vez, clasificando cada párrafo y
    // reconstruyendo su padre con una pila de subtítulos abiertos (igual que un
    // outline: un Heading 3 cuelga del Heading 2 más cercano hacia arriba, ese del
    // Heading 1 más cercano, etc.).
    private static List<ParrafoAnotado> AnotarParrafos(List<Paragraph> paragraphs, Dictionary<string, string> styleNames, MainDocumentPart mainPart)
    {
        var resultado = new List<ParrafoAnotado>();
        var pilaSubtitulos = new List<(int Indice, int Nivel)>();

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var texto = GetParagraphText(paragraphs[i]).Trim();
            var imagen = GetPrimeraImagenBase64(paragraphs[i], mainPart);
            if (string.IsNullOrWhiteSpace(texto) && imagen == null) continue;

            var styleId = paragraphs[i].ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            var styleName = (styleId != null && styleNames.TryGetValue(styleId, out var nombre)) ? nombre : styleId;

            var (tipo, nivel) = ClasificarTipo(texto, styleName);

            if (tipo == "subtitulo")
            {
                while (pilaSubtitulos.Count > 0 && pilaSubtitulos[^1].Nivel >= nivel)
                    pilaSubtitulos.RemoveAt(pilaSubtitulos.Count - 1);

                var parentIndice = pilaSubtitulos.Count > 0 ? pilaSubtitulos[^1].Indice : (int?)null;
                resultado.Add(new ParrafoAnotado(i, parentIndice, tipo, nivel, texto, imagen));
                pilaSubtitulos.Add((i, nivel));
            }
            else
            {
                var parentIndice = pilaSubtitulos.Count > 0 ? pilaSubtitulos[^1].Indice : (int?)null;
                resultado.Add(new ParrafoAnotado(i, parentIndice, tipo, 0, texto, imagen));
            }
        }

        return resultado;
    }

    private static readonly Regex NumeracionAnidada = new(@"^(\d+(?:\.\d+)+)\.?\s", RegexOptions.Compiled);

    // Heurística de clasificación: estilo "Heading N" -> subtítulo de nivel N;
    // numeración anidada al inicio ("7.1", "7.1.2") -> subtítulo "informal" aunque el
    // estilo de Word sea "Normal" (muchos documentos reales no aplican Heading 2/3
    // aunque el contenido sí sea jerárquico, como vimos en el PETS de Tarrajeo);
    // "a. texto" -> letra; "- texto" / "• texto" -> guión; cualquier otra cosa
    // (Normal, Body Text, List Paragraph sin viñeta detectable) -> paso simple.
    private static (string Tipo, int Nivel) ClasificarTipo(string texto, string? styleName)
    {
        if (!string.IsNullOrEmpty(styleName))
        {
            var m = Regex.Match(styleName, @"heading\s*(\d+)", RegexOptions.IgnoreCase);
            if (m.Success) return ("subtitulo", int.Parse(m.Groups[1].Value));
        }

        var t = texto.TrimStart();

        var mNum = NumeracionAnidada.Match(t);
        if (mNum.Success)
        {
            var nivel = mNum.Groups[1].Value.Split('.').Length;
            return ("subtitulo", nivel);
        }

        if (Regex.IsMatch(t, @"^[a-z]\.\s")) return ("letra", 0);
        if (Regex.IsMatch(t, @"^[-•\*]\s")) return ("guion", 0);
        return ("paso", 0);
    }

    public async Task ConfirmarImportacionAsync(int petId, ConfirmarImportacionRequest request)
    {
        // Mapea el "Indice" del preview (posición original en el Word) al id REAL
        // que le asigna la base de datos al crearlo, para poder resolver ParentId
        // conforme se van creando — el padre siempre se crea antes que su hijo
        // porque el documento se recorre en el mismo orden en que aparece.
        var idPorIndice = new Dictionary<int, int>();

        foreach (var paso in request.Pasos)
        {
            if (string.IsNullOrWhiteSpace(paso.Texto)) continue;

            int? parentIdReal = null;
            if (paso.ParentIndice.HasValue && idPorIndice.TryGetValue(paso.ParentIndice.Value, out var pid))
                parentIdReal = pid;

            var pasoId = await _petsService.AgregarPasoAsync(petId, new CrearPetPasoRequest
            {
                Descripcion = paso.Texto,
                ParentId = parentIdReal,
                Tipo = paso.Tipo,
            });

            idPorIndice[paso.Indice] = pasoId;

            if (!string.IsNullOrEmpty(paso.ImagenBase64))
            {
                var base64 = paso.ImagenBase64.Contains(',') ? paso.ImagenBase64.Split(',')[1] : paso.ImagenBase64;
                var bytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);
                await _petsService.SubirImagenPasoAsync(petId, pasoId, ms, "importado.png");
            }
        }
    }

    private static string GetParagraphText(Paragraph p)
    {
        var sb = new StringBuilder();
        foreach (var text in p.Descendants<Text>())
            sb.Append(text.Text);
        return sb.ToString();
    }

    private static string Normalizar(string s)
    {
        return s.ToUpperInvariant()
            .Replace('Á', 'A').Replace('É', 'E').Replace('Í', 'I').Replace('Ó', 'O').Replace('Ú', 'U');
    }

    // El párrafo debe SER el título de la sección (ej. "7. PROCEDIMIENTO DE TRABAJO"),
    // no solo mencionarlo de pasada dentro de una oración más larga (ej. en las
    // instrucciones de la plantilla, que explican qué hace este marcador). Por eso
    // se exige igualdad después de quitar un posible número/punto inicial, en vez de
    // un simple "contains".
    private static bool EsTituloDeSeccion(string texto, string marcador)
    {
        var limpio = Regex.Replace(texto.Trim(), @"^\d+[\.\)]?\s*-?\s*", "").TrimEnd('.', ':', ' ');
        return Normalizar(limpio) == Normalizar(marcador);
    }

    private static Dictionary<string, string> LoadStyleNames(MainDocumentPart mainPart)
    {
        var dict = new Dictionary<string, string>();
        var styles = mainPart.StyleDefinitionsPart?.Styles;
        if (styles == null) return dict;

        foreach (var style in styles.Elements<Style>())
        {
            var id = style.StyleId?.Value;
            var name = style.StyleName?.Val?.Value;
            if (id != null && name != null) dict[id] = name;
        }
        return dict;
    }

    // Solo la primera imagen del párrafo — algunos párrafos del documento real traen
    // 2-3 imágenes juntas; para el piloto se importa la primera y el resto se agrega
    // a mano desde la pantalla de detalle, igual que cualquier otra imagen.
    private static string? GetPrimeraImagenBase64(Paragraph p, MainDocumentPart mainPart)
    {
        var blip = p.Descendants<A.Blip>().FirstOrDefault();
        var relId = blip?.Embed?.Value;
        if (relId == null) return null;

        var part = mainPart.GetPartById(relId);
        using var stream = part.GetStream();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        var contentType = part.ContentType.Contains("png") ? "image/png"
            : (part.ContentType.Contains("jpeg") || part.ContentType.Contains("jpg")) ? "image/jpeg"
            : "image/png";

        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
    }
}
