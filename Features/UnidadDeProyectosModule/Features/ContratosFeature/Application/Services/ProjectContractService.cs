using System.Globalization;
using Humanizer;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Helpers;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Services
{
    public class ProjectContractService : IProjectContractService
    {
        private readonly IProjectContractRepository _repository;
        private readonly IProjectContractStorage _storage;
        private readonly IEmailService _emailService;

        /// <summary>Correo de Unidad de Proyectos para la notificación del paso 8 — acordado con
        /// el usuario en sesión (reemplaza al de Staff de Obra que usa Adjudicaciones). Si más
        /// adelante esto necesita ser configurable por entorno, mover a appsettings.</summary>
        private const string CorreoUnidadDeProyectos = "unidadproyectosnm@abril.pe";

        public ProjectContractService(
            IProjectContractRepository repository, IProjectContractStorage storage, IEmailService emailService)
        {
            _repository = repository;
            _storage = storage;
            _emailService = emailService;
        }

        public Task<List<ProjectContractDTO>> GetAllByProjectIdAsync(int projectId)
            => _repository.GetAllByProjectIdAsync(projectId);

        public async Task<ProjectContractDTO> GetByIdAsync(int projectContractId)
            => await _repository.GetByIdAsync(projectContractId)
               ?? throw new AbrilException("Contrato no encontrado.", 404);

        public Task<int> CreateAsync(ProjectContractCreateDTO dto, int userId)
            => _repository.CreateAsync(dto, userId);

        public Task EditAsync(int projectContractId, ProjectContractEditDTO dto, int userId)
            => _repository.EditAsync(projectContractId, dto, userId);

        public Task<ProjectContractMilestoneDTO> AddMilestoneAsync(
            int projectContractId, ProjectContractMilestoneCreateDTO dto, int userId)
            => _repository.AddMilestoneAsync(projectContractId, dto, userId);

        public Task DeleteMilestoneAsync(int projectContractMilestoneId, int userId)
            => _repository.DeleteMilestoneAsync(projectContractMilestoneId, userId);

        // ── Generación del documento (paso 3) ────────────────────────────────

        /// <summary>Especialidades que usan la plantilla propia en vez de la genérica — por
        /// contenido de WorkSpecialtyDescription, no por id (puede diferir entre entornos, D3).
        /// TODO: confirmar contra el catálogo real de WorkSpecialty que este match por texto
        /// cubre exactamente "Arquitectura" y "Estructuras" y ningún falso positivo.</summary>
        private static bool EsEspecialidadArquitecturaOEstructuras(string? workSpecialtyDescription)
        {
            var d = (workSpecialtyDescription ?? "").ToUpperInvariant();
            return d.Contains("ARQUITECTURA") || d.Contains("ESTRUCTURA");
        }

        private static void ValidateGenerationData(ProjectContractGenerationDataDTO data)
        {
            var missing = new List<string>();

            void Req(string? value, string label)
            {
                if (string.IsNullOrWhiteSpace(value)) missing.Add(label);
            }

            Req(data.ServiceDescription, "Tipo de servicio");
            Req(data.ProjectRazonSocial, "Razón social del proyecto (Contributor del proyecto)");
            Req(data.ProjectRuc, "RUC del proyecto");
            Req(data.ProjectNombre, "Nombre del proyecto");
            Req(data.ContratistaRazonSocial, "Razón social del contratista");
            Req(data.ContratistaRuc, "RUC del contratista");
            if (!data.SigningDate.HasValue) missing.Add("Fecha de firma del contrato");
            if (!data.ContractNumber.HasValue) missing.Add("Número de contrato");
            if (data.Milestones.Count == 0) missing.Add("Hitos de pago (al menos uno)");

            if (missing.Count > 0)
                throw new AbrilException(
                    "No se puede generar el contrato. Complete primero los siguientes datos: " +
                    $"{string.Join(", ", missing)}.", 400);
        }

        public async Task<(byte[] Bytes, string FileName)> GenerateContractAsync(int projectContractId)
        {
            var data = await _repository.GetGenerationDataAsync(projectContractId);
            ValidateGenerationData(data);

            var templateFileName = EsEspecialidadArquitecturaOEstructuras(data.WorkSpecialtyDescription)
                ? "plantilla_arquitectura_estructuras_con_placeholders.docx"
                : "plantilla_generica_con_placeholders.docx";

            var templatePath = Path.Combine(
                AppContext.BaseDirectory,
                "Features", "UnidadDeProyectosModule", "Features", "ContratosFeature",
                "Templates", templateFileName);

            if (!File.Exists(templatePath))
                throw new AbrilException(
                    $"No se encontró la plantilla '{templateFileName}' en el servidor. " +
                    "Contacte al administrador del sistema.");

            var esCulture = new CultureInfo("es");
            var abreviaturaProyecto = !string.IsNullOrWhiteSpace(data.ProyectoAbreviatura)
                ? data.ProyectoAbreviatura!
                : (data.ProjectNombre.Length >= 3
                    ? data.ProjectNombre[..3].ToUpperInvariant()
                    : data.ProjectNombre.ToUpperInvariant());

            var year = data.SigningDate!.Value.Year;
            var numContrato = $"{data.ContractNumber!.Value:D3}{abreviaturaProyecto}-{year}";

            var moneda = data.CurrencyCode == "USD" ? "dólares" : "soles";
            var entero = (long)Math.Truncate(data.Amount);
            var centavos = (int)Math.Round((data.Amount - entero) * 100);
            var palabras = entero.ToWords(esCulture);
            palabras = char.ToUpper(palabras[0]) + palabras[1..];
            var montoEnPalabras = $"{palabras} con {centavos:D2}/100 {moneda}";

            // Fecha de firma en el formato usado por el contrato ("06 de noviembre del 2025").
            var fecha = data.SigningDate.Value;
            var fechaFirma =
                $"{fecha.Day:D2} de {esCulture.DateTimeFormat.GetMonthName(fecha.Month)} del {fecha.Year}";

            var hitoGarantia = data.Milestones[^1];

            var replacements = new Dictionary<string, string>
            {
                { "{{NUM_CONTRATO}}", numContrato },
                { "{{TIPO_SERVICIO}}", data.ServiceDescription ?? "" },
                { "{{PROYECTO_RAZON_SOCIAL}}", data.ProjectRazonSocial },
                { "{{PROYECTO_RUC}}", data.ProjectRuc },
                { "{{PROYECTO_NOMBRE}}", data.ProjectNombre },
                { "{{PROYECTO_UBICACION_OBRA}}", data.ProjectUbicacionObra ?? "" },
                { "{{PROYECTO_DISTRITO}}", data.ProjectDistrito ?? "" },
                { "{{CONTRATISTA_RAZON_SOCIAL}}", data.ContratistaRazonSocial },
                { "{{CONTRATISTA_RUC}}", data.ContratistaRuc },
                { "{{CONTRATISTA_UBICACION}}", data.ContratistaUbicacion ?? "" },
                { "{{CONTRATISTA_DISTRITO}}", data.ContratistaDistrito ?? "" },
                { "{{CONTRATISTA_REPRESENTANTE_NOMBRE}}", data.ContratistaRepresentanteNombre ?? data.ContratistaRazonSocial },
                { "{{CONTRATISTA_REPRESENTANTE_DNI}}", data.ContratistaRepresentanteDni ?? "" },
                { "{{MONTO}}", $"{(data.CurrencyCode == "USD" ? "US$" : "S/")}. {data.Amount:N2}" },
                { "{{MONTO_EN_PALABRAS}}", montoEnPalabras },
                { "{{HITO_GARANTIA_PORCENTAJE}}", hitoGarantia.Percentage.ToString("0.##") },
                { "{{FECHA_FIRMA_DEL_CONTRATO}}", fechaFirma },
                // Se pega a mano por contrato (decisión ya acordada) — nunca vacío en la plantilla:
                // si no se cargó, se avisa antes de llegar acá (missing no lo valida a propósito,
                // es el único campo verdaderamente opcional del merge).
                { "{{DETALLE_SERVICIOS}}", data.DetalleServicios ?? "" },
            };

            var hitosDePago = data.Milestones
                .Select(m => $"{m.Percentage:0.##}% {m.Description}")
                .ToList();

            byte[] docBytes;
            using (var templateStream = File.OpenRead(templatePath))
                docBytes = WordTemplateHelper.FillTemplate(
                    templateStream,
                    replacements,
                    multiParagraphReplacements: new Dictionary<string, List<string>>
                    {
                        { "{{HITOS_DE_PAGO}}", hitosDePago }
                    });

            var contratAbrev = new string(
                data.ContratistaRazonSocial
                    .Where(char.IsLetterOrDigit)
                    .Take(4)
                    .ToArray()
            ).ToUpperInvariant();

            var fileName = $"CONTRATO N°{numContrato}-{contratAbrev}.docx";

            // Subir a SharePoint (Configuración → Carpeta de Contratos) y persistir la referencia.
            // Best-effort: si el proyecto todavía no tiene la carpeta configurada (422 — ningún
            // proyecto la tiene configurada todavía, recién se está armando este módulo), no se
            // bloquea la generación — el .docx recién armado igual se devuelve para descargar.
            // Cualquier otro error (ej. especialidad faltante, 400) sí se propaga: ese es un dato
            // del contrato mal cargado, no una carpeta pendiente de configurar.
            try
            {
                using var ms = new MemoryStream(docBytes);
                const string docxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                var spResult = await _storage.UploadContractAsync(data, fileName, ms, docxMime);

                await _repository.SetContractDocumentAsync(
                    projectContractId, spResult.WebUrl!, spResult.FileName ?? fileName, spResult.ItemId);
            }
            catch (AbrilException ex) when (ex.StatusCode == 422)
            {
                // Carpeta de Contratos no configurada para este proyecto — se ignora a propósito.
            }

            return (docBytes, fileName);
        }

        // ── Pasos 4-9 ─────────────────────────────────────────────────────────

        public async Task AdvanceToStep4Async(int projectContractId, bool skipNotification, int userId)
        {
            if (!skipNotification)
            {
                var contrato = await GetByIdAsync(projectContractId);
                if (string.IsNullOrWhiteSpace(contrato.ContractorEmail))
                    throw new AbrilException(
                        "El contrato no tiene un correo de contratista registrado. " +
                        "Complételo en el paso 2 antes de enviarlo, o marque la opción de omitir envío.", 400);

                var (bytes, fileName) = await GenerateContractAsync(projectContractId);

                await _emailService.SendAsync(
                    to: new List<string> { contrato.ContractorEmail },
                    subject: $"Contrato de servicios — {contrato.ServiceDescription}",
                    body: "Estimado(a),<br><br>" +
                          "Adjuntamos el contrato de locación de servicios correspondiente para su revisión y firma.<br><br>" +
                          "Saludos cordiales.",
                    isHtml: true,
                    attachments: new List<EmailAttachment>
                    {
                        new EmailAttachment
                        {
                            FileName = fileName,
                            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            Content = bytes
                        }
                    });
            }

            await _repository.SetStep4SentAsync(projectContractId, skipNotification, userId);
        }

        public Task RegisterStep5ArrivalAsync(int projectContractId, ProjectContractStep5ArrivalDTO dto, int userId)
            => _repository.SetStep5ArrivalAsync(projectContractId, dto, userId);

        public Task UpdateStep6SignaturesAsync(int projectContractId, ProjectContractStep6SignaturesDTO dto, int userId)
            => _repository.SetStep6SignaturesAsync(projectContractId, dto, userId);

        public async Task NotifyStep8Async(int projectContractId, int userId)
        {
            var contrato = await GetByIdAsync(projectContractId);

            await _emailService.SendAsync(
                to: new List<string> { CorreoUnidadDeProyectos },
                subject: $"Contrato firmado — {contrato.ServiceDescription} ({contrato.ContractorName})",
                body: $"El contrato con <b>{contrato.ContractorName}</b> ({contrato.ServiceDescription}) " +
                      "ya fue firmado por todas las partes. Se les notifica para que estén al tanto.",
                isHtml: true);

            await _repository.SetStep8NotifiedAsync(projectContractId, userId);
        }

        public Task CloseStep9Async(int projectContractId, int userId)
            => _repository.SetStep9ClosedAsync(projectContractId, userId);
    }
}
