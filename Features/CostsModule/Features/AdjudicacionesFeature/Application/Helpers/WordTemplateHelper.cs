using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Helpers;

/// <summary>
/// Rellena un template Word (.docx) reemplazando placeholders del tipo {{CLAVE}}.
/// Trabaja directamente sobre el XML interno del .docx (ZIP) y fusiona únicamente
/// los nodos &lt;w:t&gt; que forman el placeholder, preservando el formato del resto.
/// Procesa: cuerpo del documento, encabezados y pies de página.
/// </summary>
public static class WordTemplateHelper
{
    /// <param name="multiParagraphReplacements">
    /// Reemplazos que expanden un único párrafo &lt;w:p&gt; que contiene la clave
    /// en N párrafos (uno por elemento de la lista). Si la lista está vacía el párrafo
    /// se elimina. Útil para marcadores como <c>{{CLÁUSULAS}}</c>.
    /// </param>
    public static byte[] FillTemplate(
        Stream templateStream,
        Dictionary<string, string> replacements,
        Dictionary<string, List<string>>? multiParagraphReplacements = null)
    {
        var ms = new MemoryStream();
        templateStream.CopyTo(ms);

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            // Recopilar todas las partes que pueden contener texto con placeholders:
            // cuerpo principal + todos los encabezados y pies de página numerados.
            var entryNames = zip.Entries
                .Select(e => e.FullName)
                .Where(n =>
                    n == "word/document.xml" ||
                    Regex.IsMatch(n, @"^word/header\d+\.xml$", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(n, @"^word/footer\d+\.xml$", RegexOptions.IgnoreCase))
                .ToList();

            foreach (var name in entryNames)
                ProcessZipEntry(zip, name, replacements, multiParagraphReplacements);
        }

        ms.Position = 0;
        return ms.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void ProcessZipEntry(
        ZipArchive zip,
        string entryName,
        Dictionary<string, string> replacements,
        Dictionary<string, List<string>>? multiParagraphReplacements)
    {
        var entry = zip.GetEntry(entryName);
        if (entry is null) return;

        string xml;
        using (var stream = entry.Open())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
            xml = reader.ReadToEnd();

        // Eliminar elementos que fragmentan runs sin aportar texto visible
        xml = Regex.Replace(xml, @"<w:proofErr\b[^>]*/?>",           "");
        xml = Regex.Replace(xml, @"<w:bookmarkStart\b[^>]*/?>",      "");
        xml = Regex.Replace(xml, @"<w:bookmarkEnd\b[^>]*/?>",        "");
        xml = Regex.Replace(xml, @"<w:rPrChange\b.*?</w:rPrChange>", "", RegexOptions.Singleline);

        // Reemplazos simples: un valor por placeholder (dentro del mismo párrafo)
        xml = Regex.Replace(
            xml,
            @"<w:p[\s>].*?</w:p>",
            m => ReplaceInParagraphXml(m.Value, replacements),
            RegexOptions.Singleline);

        // Reemplazos multi-párrafo: un placeholder → N párrafos (uno por valor)
        if (multiParagraphReplacements is { Count: > 0 })
        {
            foreach (var (placeholder, values) in multiParagraphReplacements)
                xml = ReplaceWithMultipleParagraphs(xml, placeholder, values);
        }

        entry.Delete();
        var newEntry = zip.CreateEntry(entryName);
        using var writer = new StreamWriter(newEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(xml);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Reemplazo multi-párrafo
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Localiza el párrafo &lt;w:p&gt; que contiene <paramref name="placeholder"/> y lo
    /// sustituye por tantos párrafos como elementos tenga <paramref name="values"/>.
    /// Si <paramref name="values"/> está vacío, el párrafo se elimina.
    /// Los saltos de línea (\n) dentro de cada valor se convierten en &lt;w:br/&gt;.
    /// </summary>
    private static string ReplaceWithMultipleParagraphs(string xml, string placeholder, List<string> values)
    {
        return Regex.Replace(
            xml,
            @"<w:p[\s>].*?</w:p>",
            m =>
            {
                var paraXml = m.Value;

                // Comprobar si este párrafo contiene el placeholder en su texto visible
                if (!ExtractPlainText(paraXml).Contains(placeholder, StringComparison.Ordinal))
                    return paraXml;

                // Sin valores → eliminar el párrafo del documento
                if (values.Count == 0) return "";

                // Extraer propiedades del párrafo (<w:pPr>) para clonarlas
                var pPrMatch = Regex.Match(paraXml, @"<w:pPr>.*?</w:pPr>", RegexOptions.Singleline);
                var pPr = pPrMatch.Success ? pPrMatch.Value : "";

                // Extraer <w:rPr> del primer <w:r> real del párrafo.
                // IMPORTANTE: <w:pPr> también puede contener un <w:rPr> (del marcador de párrafo ¶)
                // que aparece antes que los runs de texto, por lo que no se puede usar un
                // Regex.Match simple sobre todo el XML del párrafo.
                var rPr = "";
                var firstRunMatch = Regex.Match(paraXml, @"<w:r\b[^>]*>(.*?)</w:r>", RegexOptions.Singleline);
                if (firstRunMatch.Success)
                {
                    var rPrInRun = Regex.Match(firstRunMatch.Groups[1].Value, @"<w:rPr>.*?</w:rPr>", RegexOptions.Singleline);
                    rPr = rPrInRun.Success ? rPrInRun.Value : "";
                }

                // Párrafo separador: mismas propiedades que el original PERO sin <w:numPr>
                // para que el contador de la lista no avance y no aparezca un número vacío.
                var separatorPPr = Regex.Replace(pPr, @"<w:numPr>.*?</w:numPr>", "", RegexOptions.Singleline);

                var sb = new StringBuilder();
                for (int vi = 0; vi < values.Count; vi++)
                {
                    sb.Append(BuildParagraphWithText(pPr, rPr, values[vi]));
                    // Línea en blanco entre cláusulas (sin numeración)
                    if (vi < values.Count - 1)
                        sb.Append($"<w:p>{separatorPPr}</w:p>");
                }

                return sb.ToString();
            },
            RegexOptions.Singleline);
    }

    /// <summary>
    /// Emite los runs de una sola línea de texto, dividiendo por tabulaciones (\t).
    /// Cada \t produce un run con &lt;w:tab/&gt; seguido del siguiente segmento de texto.
    /// </summary>
    private static void AppendLineRuns(StringBuilder sb, string rPr, string line)
    {
        var segments = line.Split('\t');
        bool firstSeg = true;
        foreach (var seg in segments)
        {
            if (!firstSeg)
            {
                // Run de tabulación
                sb.Append("<w:r>");
                if (!string.IsNullOrEmpty(rPr)) sb.Append(rPr);
                sb.Append("<w:tab/>");
                sb.Append("</w:r>");
            }
            firstSeg = false;

            // Emitir el run de texto (incluso si está vacío, para no perder la posición)
            var spaceAttr = (seg.StartsWith(' ') || seg.EndsWith(' '))
                ? " xml:space=\"preserve\""
                : "";
            sb.Append("<w:r>");
            if (!string.IsNullOrEmpty(rPr)) sb.Append(rPr);
            sb.Append($"<w:t{spaceAttr}>{XmlEscape(seg)}</w:t>");
            sb.Append("</w:r>");
        }
    }

    private static string BuildParagraphWithText(string pPr, string rPr, string text)
    {
        // Normalizar saltos de línea (CRLF / CR → LF)
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');

        var sb = new StringBuilder();
        sb.Append("<w:p>");
        if (!string.IsNullOrEmpty(pPr)) sb.Append(pPr);

        bool firstLine = true;
        foreach (var rawLine in lines)
        {
            if (!firstLine)
            {
                // Run de salto de línea suave (Shift+Enter en Word)
                sb.Append("<w:r>");
                if (!string.IsNullOrEmpty(rPr)) sb.Append(rPr);
                sb.Append("<w:br/>");
                sb.Append("</w:r>");
            }
            firstLine = false;

            AppendLineRuns(sb, rPr, rawLine);
        }

        sb.Append("</w:p>");
        return sb.ToString();
    }

    /// <summary>
    /// Extrae el texto plano visible de un párrafo Word (concatena el contenido de
    /// todos los nodos &lt;w:t&gt;), decodificando entidades XML.
    /// </summary>
    private static string ExtractPlainText(string paraXml)
    {
        var sb = new StringBuilder();
        foreach (Match m in Regex.Matches(paraXml, @"<w:t[^>]*>([^<]*)</w:t>"))
            sb.Append(XmlUnescape(m.Groups[1].Value));
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static string ReplaceInParagraphXml(string paraXml, Dictionary<string, string> replacements)
    {
        // Ubicar todos los <w:t> con su posición exacta dentro del XML del párrafo
        var matches = Regex.Matches(paraXml, @"<w:t(\s[^>]*)?>([^<]*)</w:t>").ToList();
        if (matches.Count == 0) return paraXml;

        // Texto decodificado de cada nodo
        var nodeTexts = matches.Select(m => XmlUnescape(m.Groups[2].Value)).ToArray();
        var combined  = string.Concat(nodeTexts);

        if (!replacements.Keys.Any(combined.Contains)) return paraXml;

        // Calcular offset de inicio de cada nodo dentro de `combined`
        var offsets = new int[nodeTexts.Length];
        for (int i = 1; i < nodeTexts.Length; i++)
            offsets[i] = offsets[i - 1] + nodeTexts[i - 1].Length;

        // Copiar los textos para poder modificarlos
        var newTexts = nodeTexts.ToArray();

        foreach (var (placeholder, value) in replacements)
        {
            int searchFrom = 0;

            while (true)
            {
                // Recalcular combined y offsets en cada pasada (los nodos pueden haber cambiado)
                var cur    = string.Concat(newTexts);
                var curOff = new int[newTexts.Length];
                for (int i = 1; i < newTexts.Length; i++)
                    curOff[i] = curOff[i - 1] + newTexts[i - 1].Length;

                int phStart = cur.IndexOf(placeholder, searchFrom, StringComparison.Ordinal);
                if (phStart < 0) break;
                int phEnd = phStart + placeholder.Length;

                // Encontrar primer y último nodo que forman el placeholder
                int firstNode = -1, lastNode = -1;
                for (int i = 0; i < newTexts.Length; i++)
                {
                    int nStart = curOff[i];
                    int nEnd   = nStart + newTexts[i].Length;
                    if (nEnd > phStart && nStart < phEnd)
                    {
                        if (firstNode < 0) firstNode = i;
                        lastNode = i;
                    }
                }
                if (firstNode < 0) break;

                // Texto antes y después del placeholder dentro de los nodos límite
                var before = cur[curOff[firstNode]..phStart];
                var after  = cur[phEnd..(curOff[lastNode] + newTexts[lastNode].Length)];

                // Fusionar: primer nodo recibe before + value + after; el resto queda vacío
                newTexts[firstNode] = before + value + after;
                for (int i = firstNode + 1; i <= lastNode; i++)
                    newTexts[i] = "";

                // Continuar búsqueda tras el valor insertado (maneja múltiples ocurrencias)
                searchFrom = curOff[firstNode] + before.Length + value.Length;
            }
        }

        // Reconstruir el XML del párrafo con los nuevos textos
        int idx = 0;
        return Regex.Replace(paraXml, @"<w:t(\s[^>]*)?>([^<]*)</w:t>", m =>
        {
            if (idx >= newTexts.Length) return m.Value;
            var attrs   = m.Groups[1].Value;
            var newText = newTexts[idx++];

            // Sin cambio → devolver original intacto
            if (newText == XmlUnescape(m.Groups[2].Value)) return m.Value;

            // Asegurar xml:space="preserve" si el texto tiene espacios al inicio/fin
            if (!attrs.Contains("xml:space") && (newText.StartsWith(' ') || newText.EndsWith(' ')))
                attrs = " xml:space=\"preserve\"" + attrs;

            return $"<w:t{attrs}>{XmlEscape(newText)}</w:t>";
        });
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Escapa únicamente los caracteres que DEBEN escaparse en contenido de texto XML:
    /// &amp; → &amp;amp;  |  &lt; → &amp;lt;  |  &gt; → &amp;gt;
    /// Las comillas dobles (") NO se escapan en contenido de texto (solo son obligatorias
    /// dentro de valores de atributos). Escaparlas en texto produce &amp;quot; que Word
    /// puede re-fragmentar los nodos &lt;w:t&gt; al volver a leer el archivo.
    /// </summary>
    private static string XmlEscape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>
    /// Convierte referencias de entidad XML y referencias numéricas de carácter
    /// a sus caracteres correspondientes.
    /// </summary>
    private static string XmlUnescape(string s)
    {
        // Entidades nombradas
        s = s.Replace("&quot;", "\"")
             .Replace("&apos;", "'")
             .Replace("&lt;",   "<")
             .Replace("&gt;",   ">")
             .Replace("&amp;",  "&");   // &amp; siempre al final para no doble-desescapar

        // Referencias numéricas decimales comunes (Word las usa a veces)
        s = Regex.Replace(s, @"&#(\d+);", m =>
            char.ConvertFromUtf32(int.Parse(m.Groups[1].Value)));

        // Referencias numéricas hexadecimales
        s = Regex.Replace(s, @"&#x([0-9A-Fa-f]+);", m =>
            char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16)));

        return s;
    }
}
