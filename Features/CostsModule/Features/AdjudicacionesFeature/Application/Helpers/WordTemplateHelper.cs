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
    public static byte[] FillTemplate(Stream templateStream, Dictionary<string, string> replacements)
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
                ProcessZipEntry(zip, name, replacements);
        }

        ms.Position = 0;
        return ms.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void ProcessZipEntry(ZipArchive zip, string entryName, Dictionary<string, string> replacements)
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

        // Procesar cada párrafo
        xml = Regex.Replace(
            xml,
            @"<w:p[\s>].*?</w:p>",
            m => ReplaceInParagraphXml(m.Value, replacements),
            RegexOptions.Singleline);

        entry.Delete();
        var newEntry = zip.CreateEntry(entryName);
        using var writer = new StreamWriter(newEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(xml);
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
