using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Helpers;

/// <summary>
/// Rellena un template Word (.docx) reemplazando placeholders del tipo {{CLAVE}}.
/// Trabaja directamente sobre el XML interno del .docx (ZIP) y fusiona únicamente
/// los nodos &lt;w:t&gt; que forman el placeholder, preservando el formato del resto.
/// </summary>
public static class WordTemplateHelper
{
    public static byte[] FillTemplate(Stream templateStream, Dictionary<string, string> replacements)
    {
        var ms = new MemoryStream();
        templateStream.CopyTo(ms);

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
            ProcessZipEntry(zip, "word/document.xml", replacements);

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
        xml = Regex.Replace(xml, @"<w:proofErr\b[^>]*/?>",              "");
        xml = Regex.Replace(xml, @"<w:bookmarkStart\b[^>]*/?>",         "");
        xml = Regex.Replace(xml, @"<w:bookmarkEnd\b[^>]*/?>",           "");
        xml = Regex.Replace(xml, @"<w:rPrChange\b.*?</w:rPrChange>",    "", RegexOptions.Singleline);

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
            // Recalcular combined y offsets a partir del estado actual (puede haber cambiado)
            var cur     = string.Concat(newTexts);
            var curOff  = new int[newTexts.Length];
            for (int i = 1; i < newTexts.Length; i++)
                curOff[i] = curOff[i - 1] + newTexts[i - 1].Length;

            int searchFrom = 0;
            while (true)
            {
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

                // Texto antes del placeholder dentro del primer nodo
                var before = cur[curOff[firstNode]..phStart];
                // Texto después del placeholder dentro del último nodo
                var after  = cur[phEnd..( curOff[lastNode] + newTexts[lastNode].Length )];

                // Fusionar: primer nodo recibe before + value + after
                newTexts[firstNode] = before + value + after;
                for (int i = firstNode + 1; i <= lastNode; i++)
                    newTexts[i] = "";

                // Próxima búsqueda después del valor insertado
                searchFrom = curOff[firstNode] + before.Length + value.Length;
                break; // recalcular cur en la siguiente iteración del while
            }
        }

        // Reconstruir el XML del párrafo con los nuevos textos
        int idx = 0;
        return Regex.Replace(paraXml, @"<w:t(\s[^>]*)?>([^<]*)</w:t>", m =>
        {
            if (idx >= newTexts.Length) return m.Value;
            var attrs   = m.Groups[1].Value;
            var newText = newTexts[idx++];

            // Sin cambio → devolver original
            if (newText == XmlUnescape(m.Groups[2].Value)) return m.Value;

            // Asegurar xml:space="preserve" si el texto tiene espacios al inicio/fin
            if (!attrs.Contains("xml:space") && (newText.StartsWith(' ') || newText.EndsWith(' ')))
                attrs = " xml:space=\"preserve\"" + attrs;

            return $"<w:t{attrs}>{XmlEscape(newText)}</w:t>";
        });
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static string XmlEscape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string XmlUnescape(string s) =>
        s.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
         .Replace("&quot;", "\"").Replace("&apos;", "'");
}
