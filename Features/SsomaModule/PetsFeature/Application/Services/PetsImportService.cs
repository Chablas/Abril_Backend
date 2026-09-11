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
    // Títulos de sección conocidos, en el orden en que normalmente aparecen en la
    // plantilla — el orden real en el documento no importa, se detectan por texto.
    // SeccionArbol != null -> Procedimiento/Responsabilidades (van a ssoma_pet_paso).
    // SeccionTexto != null -> narrativas (van a ssoma_pet_seccion_texto, texto plano).
    // Ambos null -> el título sirve solo de LÍMITE (Marco Legal, Gestión de personal,
    // Anexos): son catálogo/archivos, no párrafos, así que su contenido no se
    // importa automáticamente todavía — pero reconocer el título evita que su
    // contenido se cuele dentro de la sección anterior.
    private static readonly (string Marcador, string? SeccionTexto, string? SeccionArbol)[] Marcadores =
    [
        ("INTRODUCCION", "introduccion", null),
        ("ALCANCE", "alcance", null),
        ("OBJETIVO", "objetivo", null),
        ("OBJETIVOS", "objetivo", null),
        ("MARCO LEGAL", null, null),
        ("DEFINICIONES", "definiciones", null),
        ("RESPONSABILIDADES", null, "responsabilidades"),
        ("GESTION DE PERSONAL", null, null),
        ("PROCEDIMIENTO", null, "procedimiento"),
        ("RESTRICCIONES", "restricciones", null),
        ("ANEXOS", null, null),
    ];

    private readonly IPetsService _petsService;

    public PetsImportService(IPetsService petsService)
    {
        _petsService = petsService;
    }

    // Registro interno de trabajo — "Nivel" (1 = Heading 1, 2 = Heading 2...) solo
    // se necesita mientras se arma el árbol; no viaja al DTO público.
    private record ParrafoAnotado(int Indice, int? ParentIndice, string Tipo, int Nivel, string Texto, List<string> Imagenes);

    // Un límite de sección, conocido (marcador de la plantilla) o desconocido
    // (cualquier otro encabezado real del documento) — unificados para poder
    // ordenarlos juntos y cortar tramos sin importar de qué tipo sea cada uno.
    private record LimiteSeccion(int Indice, bool Desconocido, string? SeccionTexto, string? SeccionArbol, string? Titulo);

    public PetsImportPreviewDto PreviewDesdeDocx(Stream docxStream)
    {
        using var wordDoc = WordprocessingDocument.Open(docxStream, false);
        var mainPart = wordDoc.MainDocumentPart;
        if (mainPart?.Document.Body == null)
            return new PetsImportPreviewDto { SeccionEncontrada = false };

        // Descendants (no Elements): "Elements" solo trae hijos DIRECTOS del body, así que
        // cualquier párrafo dentro de una tabla (ej. "6.4 Recursos", que en los PETS reales
        // viene armado como tabla de 3 columnas Equipos/Herramientas/Materiales) quedaba
        // completamente invisible para el importador — ni siquiera aparecía en "no
        // reconocidas", desaparecía en silencio. Descendants sí baja dentro de las celdas.
        //
        // "!p.Ancestors<Paragraph>().Any()" filtra los párrafos DENTRO de otro párrafo — un
        // <w:p> real (de body o celda) nunca anida otro <w:p>, pero un cuadro de texto o
        // leyenda de imagen sí lo hace (w:txbxContent). Sin este filtro, un documento con
        // muchas fotos con leyenda multiplicaba el conteo de "párrafos" varias veces, y
        // como casi todo el resto del método es O(n) o O(n²) sobre esa lista, la importación
        // pasaba de segundos a minutos en documentos así.
        var paragraphs = mainPart.Document.Body.Descendants<Paragraph>()
            .Where(p => !p.Ancestors<Paragraph>().Any())
            .ToList();
        var styleNames = LoadStyleNames(mainPart);

        // Texto y estilo de cada párrafo, UNA sola vez — antes se recalculaban (recorriendo
        // Descendants<Text>() de nuevo) hasta 3 veces por párrafo, entre esta búsqueda, la de
        // encabezados desconocidos y AnotarParrafos. En un documento grande eso triplicaba
        // trabajo que ya de por sí es O(n).
        var textosParrafos = new string[paragraphs.Count];
        var stylesParrafos = new string?[paragraphs.Count];
        for (var i = 0; i < paragraphs.Count; i++)
        {
            textosParrafos[i] = GetParagraphText(paragraphs[i]);
            var styleId = paragraphs[i].ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            stylesParrafos[i] = (styleId != null && styleNames.TryGetValue(styleId, out var nombre)) ? nombre : styleId;
        }

        // Cada marcador conocido se busca sobre el TEXTO (independiente del tipo ya
        // clasificado) — el título puede o no estar en un estilo "heading" real.
        // Solo se toma la PRIMERA aparición de cada uno.
        var limitesConocidos = new List<(int Indice, string? SeccionTexto, string? SeccionArbol)>();
        var indicesConocidos = new HashSet<int>();
        foreach (var m in Marcadores)
        {
            for (var i = 0; i < paragraphs.Count; i++)
            {
                if (indicesConocidos.Contains(i)) continue;
                if (EsTituloDeSeccion(textosParrafos[i], stylesParrafos[i], m.Marcador))
                {
                    limitesConocidos.Add((i, m.SeccionTexto, m.SeccionArbol));
                    indicesConocidos.Add(i);
                    break;
                }
            }
        }

        // Cualquier OTRO párrafo con estilo de encabezado real (Heading/Título) que no
        // haya calzado con ningún marcador conocido: antes era invisible y su
        // contenido se colaba dentro de la sección anterior sin avisar (ej. un
        // documento real que llama "EQUIPOS DE PROTECCIÓN PERSONAL" a lo que la
        // plantilla espera como "EPP" bleedeaba dentro de Procedimiento o
        // Restricciones). Ahora también corta el tramo, pero como sección aparte
        // ("no reconocida") — nunca se asigna sola, el usuario decide a qué pestaña
        // enviarla.
        // Marcador conocido más cercano hacia atrás de un índice dado — para decidir si un
        // subtítulo (nivel > 1) que no calzó con ningún marcador está colgando de una
        // sección con destino real (Responsabilidades, Procedimiento...), en cuyo caso NO
        // debe cortarla, o de una sección "solo delimitadora" (Marco Legal, Gestión de
        // personal, Anexos), en cuyo caso sí debe seguir ofreciéndose para triaje manual.
        (int Indice, string? SeccionTexto, string? SeccionArbol)? MarcadorAnterior(int indice)
        {
            (int Indice, string? SeccionTexto, string? SeccionArbol)? mejor = null;
            foreach (var l in limitesConocidos)
                if (l.Indice < indice && (mejor is null || l.Indice > mejor.Value.Indice))
                    mejor = l;
            return mejor;
        }

        var limitesDesconocidos = new List<(int Indice, string Titulo)>();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (indicesConocidos.Contains(i)) continue;
            var texto = textosParrafos[i].Trim();
            if (string.IsNullOrWhiteSpace(texto)) continue;
            var styleName = stylesParrafos[i];
            if (!EsEncabezadoGenerico(texto, styleName)) continue;

            if (NivelEncabezado(styleName) > 1)
            {
                var anterior = MarcadorAnterior(i);
                if (anterior is { SeccionArbol: not null } or { SeccionTexto: not null })
                    continue; // subtítulo anidado dentro de una sección con destino real: es contenido de esa sección, no un corte propio.
            }

            limitesDesconocidos.Add((i, LimpiarNumeroInicial(texto)));
        }

        // Todo título que sea límite de sección (conocido o no) se marca ANTES de anotar
        // los párrafos, para que AnotarParrafos nunca lo use como "padre" del contenido
        // que sigue — ver el comentario dentro de esa función.
        var limitesIndices = new HashSet<int>(indicesConocidos);
        foreach (var l in limitesDesconocidos)
            limitesIndices.Add(l.Indice);

        var todos = AnotarParrafos(paragraphs, textosParrafos, stylesParrafos, mainPart, limitesIndices);
        var todosDto = todos.Select(ToPublicDto).ToList();

        if (limitesConocidos.Count == 0 && limitesDesconocidos.Count == 0)
            return new PetsImportPreviewDto { SeccionEncontrada = false, TodosLosParrafos = todosDto };

        var seccionesArbol = new Dictionary<string, List<ImportPasoPreviewDto>>();
        var seccionesTexto = new Dictionary<string, string>();
        var seccionesNoReconocidas = new Dictionary<string, List<ImportPasoPreviewDto>>();

        // Límites de ambos tipos, juntos y en orden — así un encabezado desconocido
        // corta correctamente el tramo de la sección conocida anterior (y viceversa),
        // sin importar en qué orden aparezcan en el documento real.
        var limites = new List<LimiteSeccion>();
        foreach (var l in limitesConocidos)
            limites.Add(new LimiteSeccion(l.Indice, false, l.SeccionTexto, l.SeccionArbol, null));
        foreach (var l in limitesDesconocidos)
            limites.Add(new LimiteSeccion(l.Indice, true, null, null, l.Titulo));
        limites = limites.OrderBy(l => l.Indice).ToList();

        for (var i = 0; i < limites.Count; i++)
        {
            var actual = limites[i];
            var finIndice = i + 1 < limites.Count ? limites[i + 1].Indice : int.MaxValue;

            var enTramo = todos.Where(p => p.Indice > actual.Indice && p.Indice < finIndice).ToList();
            if (enTramo.Count == 0) continue;

            if (actual.Desconocido)
            {
                var clave = actual.Titulo!;
                var sufijo = 2;
                while (seccionesNoReconocidas.ContainsKey(clave))
                    clave = $"{actual.Titulo} ({sufijo++})";
                seccionesNoReconocidas[clave] = ExpandirParaTriaje(enTramo);
            }
            else if (actual.SeccionArbol != null)
            {
                var pasos = enTramo.Select(ToPublicDto).ToList();
                // Las imágenes solo tienen sentido como evidencia de un paso de Procedimiento;
                // en Responsabilidades (roles y funciones, puro texto) una imagen colada suele
                // ser ruido del diseño del Word original (logos, firmas de la carátula), no
                // contenido real de esa fila.
                if (actual.SeccionArbol != "procedimiento")
                    foreach (var p in pasos) { p.ImagenBase64 = null; p.ImagenesBase64 = []; }
                seccionesArbol[actual.SeccionArbol] = pasos;
            }
            else if (actual.SeccionTexto != null)
            {
                var texto = string.Join("\n\n", enTramo.Where(p => !string.IsNullOrWhiteSpace(p.Texto)).Select(p => p.Texto));
                if (!string.IsNullOrWhiteSpace(texto)) seccionesTexto[actual.SeccionTexto] = texto;
            }
            else
            {
                // Marcador conocido "solo delimitador" (Marco Legal, Gestión de personal,
                // Anexos): no tiene una pestaña de destino automática porque son catálogo
                // (normas, EPP, recursos), no texto/árbol libre. En vez de descartar su
                // contenido en silencio, se ofrece igual que un encabezado no reconocido para
                // que el usuario triangule cada ítem a mano (norma → Marco Legal, EPP básico →
                // EPP, etc.) usando el mismo selector "Enviar a..." que ya existe.
                var tituloConocido = LimpiarNumeroInicial(GetParagraphText(paragraphs[actual.Indice]));
                var clave = tituloConocido;
                var sufijo = 2;
                while (seccionesNoReconocidas.ContainsKey(clave))
                    clave = $"{tituloConocido} ({sufijo++})";
                seccionesNoReconocidas[clave] = ExpandirParaTriaje(enTramo);
            }
        }

        return new PetsImportPreviewDto
        {
            SeccionEncontrada = limitesConocidos.Count > 0,
            SeccionesArbol = seccionesArbol,
            SeccionesTexto = seccionesTexto,
            SeccionesNoReconocidas = seccionesNoReconocidas,
            TodosLosParrafos = todosDto
        };
    }

    private static ImportPasoPreviewDto ToPublicDto(ParrafoAnotado p) => new()
    {
        Indice = p.Indice,
        ParentIndice = p.ParentIndice,
        Tipo = p.Tipo,
        Texto = p.Texto,
        ImagenBase64 = p.Imagenes.FirstOrDefault(),
        ImagenesBase64 = p.Imagenes
    };

    // Solo para "no reconocidas" (triaje manual a catálogo): a diferencia de
    // SeccionesArbol, acá el Indice/ParentIndice ya NO se usa para resolver padres en
    // la base de datos (el usuario convierte cada fila en un ítem de catálogo suelto),
    // así que es seguro partir un párrafo con varias viñetas pegadas ("• Ítem A • Ítem
    // B • Ítem C", típico de una celda de tabla como Equipos/Herramientas/Materiales de
    // Recursos) en varias filas independientes. Si el documento en cambio ya trae un
    // ítem por línea (formato EPP, sin tabla) no hay nada que partir y se comporta
    // exactamente igual que antes — funciona para los dos formatos sin distinguirlos.
    private static List<ImportPasoPreviewDto> ExpandirParaTriaje(List<ParrafoAnotado> enTramo)
    {
        var resultado = new List<ImportPasoPreviewDto>();
        foreach (var p in enTramo)
        {
            var partes = p.Tipo == "guion" ? p.Texto.Split('•').Select(s => s.Trim()).Where(s => s.Length > 0).ToList() : null;
            if (partes != null && partes.Count > 1)
            {
                for (var k = 0; k < partes.Count; k++)
                {
                    resultado.Add(new ImportPasoPreviewDto
                    {
                        Indice = p.Indice * 1000 + k,
                        ParentIndice = p.ParentIndice,
                        Tipo = "guion",
                        Texto = partes[k],
                        ImagenBase64 = k == 0 ? p.Imagenes.FirstOrDefault() : null,
                        ImagenesBase64 = k == 0 ? p.Imagenes : []
                    });
                }
            }
            else
            {
                resultado.Add(ToPublicDto(p));
            }
        }
        return resultado;
    }

    // Recorre TODO el documento una sola vez, clasificando cada párrafo y
    // reconstruyendo su padre con una pila de subtítulos abiertos (igual que un
    // outline: un Heading 3 cuelga del Heading 2 más cercano hacia arriba, ese del
    // Heading 1 más cercano, etc.).
    private static List<ParrafoAnotado> AnotarParrafos(List<Paragraph> paragraphs, string[] textosParrafos, string?[] stylesParrafos, MainDocumentPart mainPart, HashSet<int> limitesIndices)
    {
        var resultado = new List<ParrafoAnotado>();
        var pilaSubtitulos = new List<(int Indice, int Nivel)>();

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var texto = textosParrafos[i].Trim();
            var imagenes = GetImagenesBase64(paragraphs[i], mainPart);
            if (string.IsNullOrWhiteSpace(texto) && imagenes.Count == 0) continue;

            var styleName = stylesParrafos[i];

            var (tipo, nivel) = ClasificarTipo(texto, styleName, EsParrafoTodoNegrita(paragraphs[i]));
            var textoGuardado = LimpiarPrefijoParaGuardar(texto, tipo);

            // Un título que ES el límite de una sección (conocida o no) nunca es "padre"
            // del contenido que sigue — es el encabezado de la sección misma, no un
            // subtítulo DENTRO de ella. Antes se apilaba igual que cualquier subtítulo,
            // así que el primer subtítulo real de Procedimiento/Responsabilidades (ej.
            // "7.1 Preparación previa") quedaba con ParentIndice apuntando al título de
            // la sección ("7. Procedimiento") — un índice que el corte de tramo excluye a
            // propósito y que por lo tanto NUNCA llega al backend. Como ese padre nunca
            // podía resolverse, AgregarPasosBulkAsync descartaba la sección ENTERA en
            // silencio (el preview se veía bien porque el frontend es tolerante con
            // padres no encontrados, pero nada quedaba guardado). Al llegar a un límite
            // se resetea la pila para que sus hijos directos arranquen como raíz.
            if (limitesIndices.Contains(i))
            {
                pilaSubtitulos.Clear();
                if (tipo == "subtitulo")
                {
                    resultado.Add(new ParrafoAnotado(i, null, tipo, nivel, textoGuardado, imagenes));
                    continue;
                }
            }

            if (tipo == "subtitulo")
            {
                while (pilaSubtitulos.Count > 0 && pilaSubtitulos[^1].Nivel >= nivel)
                    pilaSubtitulos.RemoveAt(pilaSubtitulos.Count - 1);

                var parentIndice = pilaSubtitulos.Count > 0 ? pilaSubtitulos[^1].Indice : (int?)null;
                resultado.Add(new ParrafoAnotado(i, parentIndice, tipo, nivel, textoGuardado, imagenes));
                pilaSubtitulos.Add((i, nivel));
            }
            else
            {
                var parentIndice = pilaSubtitulos.Count > 0 ? pilaSubtitulos[^1].Indice : (int?)null;
                resultado.Add(new ParrafoAnotado(i, parentIndice, tipo, 0, textoGuardado, imagenes));
            }
        }

        return resultado;
    }

    // "\s*" (no "\s") a propósito: documentos reales como "8.7.2.Colocación de acero..." no
    // dejan espacio entre el punto final de la numeración y el texto — exigir un espacio
    // ahí hacía que ese sub-subtítulo cayera como "paso" suelto en vez de anidarse bajo 8.7.
    private static readonly Regex NumeracionAnidada = new(@"^(\d+(?:\.\d+)+)\.?\s*", RegexOptions.Compiled);

    // Heurística de clasificación: estilo "Heading N" -> subtítulo de nivel N;
    // numeración anidada al inicio ("7.1", "7.1.2") -> subtítulo "informal" aunque el
    // estilo de Word sea "Normal" (muchos documentos reales no aplican Heading 2/3
    // aunque el contenido sí sea jerárquico, como vimos en el PETS de Tarrajeo);
    // negrita + TODO EN MAYÚSCULAS + corto + sin punto final -> subtítulo "informal"
    // de un tercer formato (ej. "RESIDENTE DE OBRA" en el PETS de Carpintería: ni
    // Heading ni numeración, solo negrita/mayúsculas como único indicio visual de
    // que es un cargo/rol, no una oración); "a. texto" -> letra; "- texto" / "• texto"
    // -> guión; cualquier otra cosa (Normal, Body Text, List Paragraph sin viñeta
    // detectable) -> paso simple.
    private static (string Tipo, int Nivel) ClasificarTipo(string texto, string? styleName, bool esNegritaCompleta)
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

        // "Sin punto final" descarta oraciones normales resaltadas en negrita por
        // énfasis (ej. una advertencia en Restricciones) — un rol/cargo real ("Jefe de
        // SSOMA", "Operario de Grúa") es una frase corta, nunca termina en ".", "!" ni "?".
        if (esNegritaCompleta
            && t.Length is > 0 and <= 40
            && !Regex.IsMatch(t, @"[.!?]\s*$")
            && t.Any(char.IsLetter)
            && t.Where(char.IsLetter).All(c => !char.IsLower(c)))
        {
            return ("subtitulo", 1);
        }

        return ("paso", 0);
    }

    // Todo el texto visible del párrafo está en negrita (ignora corridas vacías/solo
    // espacios) — algunos documentos reales no usan NINGÚN estilo de encabezado ni
    // numeración para marcar un subtítulo, solo negrita + mayúsculas.
    private static bool EsParrafoTodoNegrita(Paragraph p)
    {
        var runsConTexto = p.Descendants<Run>()
            .Where(r => r.Descendants<Text>().Any(t => !string.IsNullOrWhiteSpace(t.Text)))
            .ToList();
        if (runsConTexto.Count == 0) return false;
        // <w:b/> sin "val" = negrita ON; <w:b w:val="false"/> (o "0") = negrita OFF a
        // pesar de estar presente el elemento — hay que revisar Val, no solo existencia.
        return runsConTexto.All(r => r.RunProperties?.Bold != null
            && (r.RunProperties.Bold.Val == null || r.RunProperties.Bold.Val.Value));
    }

    // La plataforma ya numera solita cada subtítulo/letra al mostrarlos (`nodo.numero` en
    // la pantalla de detalle) — si el texto importado conserva su propio prefijo ("5.1",
    // "a.", "-") el número queda duplicado en pantalla. Se recorta acá el MISMO prefijo que
    // ClasificarTipo usó para reconocer el tipo, nunca un intento nuevo de adivinar.
    private static string LimpiarPrefijoParaGuardar(string texto, string tipo)
    {
        var t = texto.TrimStart();
        return tipo switch
        {
            "subtitulo" => NumeracionAnidada.IsMatch(t) ? NumeracionAnidada.Replace(t, "", 1).TrimStart() : t,
            "letra" => Regex.Replace(t, @"^[a-z]\.\s*", ""),
            "guion" => Regex.Replace(t, @"^[-•\*]\s*", ""),
            _ => t,
        };
    }

    // Para los TÍTULOS de sección que se muestran tal cual al usuario ("Marco Legal",
    // "Equipo de Protección Personal específico según actividad" en "no reconocidas") —
    // a diferencia de LimpiarPrefijoParaGuardar, que limpia el CONTENIDO de cada paso
    // según su tipo ya clasificado, esto limpia el numeral de portada del título mismo
    // ("4. Marco Legal" -> "Marco Legal", "6.2 Equipo..." -> "Equipo..."), sin importar
    // si el numeral es simple ("4.") o anidado ("6.2", "7.1.2").
    private static readonly Regex NumeroInicialSimple = new(@"^\d+[\.\)]?\s*-?\s*", RegexOptions.Compiled);

    private static string LimpiarNumeroInicial(string texto)
    {
        var t = texto.Trim();
        return NumeracionAnidada.IsMatch(t)
            ? NumeracionAnidada.Replace(t, "", 1).TrimStart()
            : NumeroInicialSimple.Replace(t, "", 1).TrimStart();
    }

    public async Task ConfirmarImportacionAsync(int petId, ConfirmarImportacionRequest request)
    {
        foreach (var (seccion, pasosOriginales) in request.SeccionesArbol)
        {
            var pasos = pasosOriginales.Where(p => !string.IsNullOrWhiteSpace(p.Texto)).ToList();
            if (pasos.Count == 0) continue;

            // Reimportando una versión corregida del mismo documento: se limpia la
            // sección vigente antes de insertar, para no duplicar todo lo que ya estaba.
            if (request.Reemplazar)
                await _petsService.DesactivarSeccionAsync(petId, seccion);

            // Inserta TODO el árbol de la sección de un tirón (un SaveChanges por nivel de
            // profundidad, no uno por paso) — antes, un PETS con 200+ pasos tardaba varios
            // minutos porque cada paso hacía su propio viaje a la base de datos y encima
            // releía todos sus hermanos existentes en cada llamada. Devuelve el mapa
            // "Indice del preview" -> id real, que se sigue necesitando para resolver a qué
            // paso le corresponde cada imagen.
            var idPorIndice = await _petsService.AgregarPasosBulkAsync(petId, seccion, pasos);

            // Las imágenes SÍ se suben en paralelo (son independientes entre sí, no hay
            // relación padre/hijo que respetar) — es la parte que de verdad demora al
            // hablar con storage externo, así que aquí es donde vale la pena paralelizar.
            // Un paso puede traer varias fotos (ImagenesBase64) — cada una se sube como
            // una imagen NUEVA del paso (SubirImagenPasoAsync ya no reemplaza, agrega).
            var imagenesAsubir = new List<(int PasoId, string Base64)>();
            foreach (var p in pasos)
            {
                if (!idPorIndice.TryGetValue(p.Indice, out var pasoId)) continue;
                var lista = p.ImagenesBase64.Count > 0
                    ? p.ImagenesBase64
                    : (p.ImagenBase64 != null ? new List<string> { p.ImagenBase64 } : new List<string>());
                foreach (var b64 in lista) imagenesAsubir.Add((pasoId, b64));
            }
            const int maxEnParalelo = 4;
            for (var i = 0; i < imagenesAsubir.Count; i += maxEnParalelo)
            {
                var lote = imagenesAsubir.Skip(i).Take(maxEnParalelo);
                await Task.WhenAll(lote.Select(async item =>
                {
                    var base64 = item.Base64.Contains(',') ? item.Base64.Split(',')[1] : item.Base64;
                    var bytes = Convert.FromBase64String(base64);
                    using var ms = new MemoryStream(bytes);
                    await _petsService.SubirImagenPasoAsync(petId, item.PasoId, ms, "importado.png");
                }));
            }
        }

        foreach (var (seccion, contenido) in request.SeccionesTexto)
        {
            if (string.IsNullOrWhiteSpace(contenido)) continue;
            await _petsService.UpsertSeccionTextoAsync(petId, seccion, contenido);
        }

        // Ítems de Marco Legal/EPP/Recursos triados a mano desde una sección no
        // reconocida — siempre personalizados de este PETS, nunca al catálogo
        // global de forma automática.
        //
        // Con "Reemplazar" marcado, se desactiva TODO lo que había en cada grupo tocado
        // ANTES de insertar — si no, el chequeo de "ya existe" en AgregarItemPersonalizadoAsync
        // solo evita el duplicado EXACTO; un ítem cuyo texto cambió en el Word quedaría
        // conviviendo con su versión vieja para siempre.
        if (request.Reemplazar)
        {
            foreach (var grupo in request.ItemsCatalogo.Select(i => i.Grupo).Distinct())
                await _petsService.DesactivarSeleccionesGrupoAsync(petId, grupo);
        }

        foreach (var item in request.ItemsCatalogo)
        {
            if (string.IsNullOrWhiteSpace(item.Descripcion)) continue;
            item.AgregarAlCatalogoGlobal = false;
            await _petsService.AgregarItemPersonalizadoAsync(petId, item);
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
    //
    // Los PETS reales no siempre usan el texto exacto de la plantilla (ej.
    // "Procedimiento (Paso a paso)" en vez de "Procedimiento de Trabajo"), así que
    // si no hay igualdad exacta se acepta una coincidencia flexible: el párrafo debe
    // tener estilo de encabezado (Heading/Título), ser corto (no un párrafo de
    // cuerpo) y EMPEZAR con el marcador seguido de un separador no alfanumérico
    // (espacio, paréntesis, coma...). Eso evita falsos positivos como "Procedimientos
    // internos..." (texto de cuerpo que empieza con la misma palabra en plural).
    private static bool EsTituloDeSeccion(string texto, string? styleName, string marcador)
    {
        var limpio = Regex.Replace(texto.Trim(), @"^\d+[\.\)]?\s*-?\s*", "").TrimEnd('.', ':', ' ');
        var limpioNorm = Normalizar(limpio);
        var marcadorNorm = Normalizar(marcador);

        if (limpioNorm == marcadorNorm) return true;

        // "tulo" (no "t[ií]tulo") a propósito: algunos documentos exportados desde
        // LibreOffice pierden la tilde del nombre de estilo ("Título1" -> "Ttulo1"),
        // así que exigir la í rompía la detección. "tulo" sigue siendo específico
        // (no aparece en otros nombres de estilo) y cubre Título/Titulo/Ttulo/Subtítulo.
        var esEncabezado = styleName != null && Regex.IsMatch(styleName, @"heading|tulo", RegexOptions.IgnoreCase);
        if (!esEncabezado || limpio.Length > 60 || !limpioNorm.StartsWith(marcadorNorm)) return false;

        var siguiente = limpioNorm.Length > marcadorNorm.Length ? limpioNorm[marcadorNorm.Length] : ' ';
        return !char.IsLetterOrDigit(siguiente);
    }

    // Nivel del encabezado a partir del dígito final del nombre de estilo (Heading1,
    // Heading2, Ttulo1, Título2...). Sin dígito -> nivel 1 (no hay forma de saber que es
    // un subnivel, se asume el nivel más alto, como ya hacía el resto del archivo).
    private static int NivelEncabezado(string? styleName)
    {
        if (string.IsNullOrEmpty(styleName) || !Regex.IsMatch(styleName, @"heading|tulo", RegexOptions.IgnoreCase))
            return 0;
        var m = Regex.Match(styleName, @"(\d+)\s*$");
        return m.Success ? int.Parse(m.Groups[1].Value) : 1;
    }

    // Heurística para "esto ES un título de sección, aunque no sepamos cuál todavía":
    // estilo de encabezado real (Heading/Título) y corto — no un párrafo de cuerpo
    // largo que por error haya quedado con ese estilo. Mismo umbral de longitud que
    // el heading "conocido" usa en EsTituloDeSeccion, un poco más laxo (100 en vez de
    // 60) porque acá no hay un marcador contra el cual comparar el prefijo.
    //
    // El nivel (Heading1 vs Heading2...) NO se filtra acá — se decide más arriba, en
    // PreviewDesdeDocx, según de qué sección known cuelga cada candidato: un subtítulo
    // (5.1, 7.1...) anidado dentro de una sección con destino real (Responsabilidades,
    // Procedimiento) no debe cortarla; pero ese mismo subtítulo colgando de una sección
    // "solo delimitadora" (Marco Legal, Gestión de personal) SÍ debe seguir apareciendo
    // como bloque "no reconocido" — es la única forma de triar a mano cosas como EPP o
    // Recursos, que son catálogo y no texto/árbol.
    private static bool EsEncabezadoGenerico(string texto, string? styleName)
    {
        if (styleName == null || !Regex.IsMatch(styleName, @"heading|tulo", RegexOptions.IgnoreCase)) return false;
        var limpio = Regex.Replace(texto.Trim(), @"^\d+[\.\)]?\s*-?\s*", "").TrimEnd('.', ':', ' ');
        return limpio.Length > 0 && limpio.Length <= 100;
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

    // TODAS las imágenes del párrafo — un párrafo del Word real puede traer 2-3 fotos
    // juntas (ej. dos vistas del mismo andamio); antes solo se tomaba la primera y el
    // resto se perdía en silencio. El orden es el de aparición en el XML del párrafo.
    private static List<string> GetImagenesBase64(Paragraph p, MainDocumentPart mainPart)
    {
        var resultado = new List<string>();
        foreach (var blip in p.Descendants<A.Blip>())
        {
            var relId = blip.Embed?.Value;
            if (relId == null) continue;

            var part = mainPart.GetPartById(relId);
            using var stream = part.GetStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();

            var contentType = part.ContentType.Contains("png") ? "image/png"
                : (part.ContentType.Contains("jpeg") || part.ContentType.Contains("jpg")) ? "image/jpeg"
                : "image/png";

            resultado.Add($"data:{contentType};base64,{Convert.ToBase64String(bytes)}");
        }
        return resultado;
    }
}
