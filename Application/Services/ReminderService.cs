using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Graph.Interfaces;
using System.Globalization;

namespace Abril_Backend.Application.Services
{
    public class ReminderService : IReminderService
    {
        private readonly IMilestoneScheduleRepository _milestoneScheduleRepository;
        private readonly IEmailService _emailService;
        private readonly IMilestoneScheduleHistoryRepository _milestoneScheduleHistoryRepository;
        private readonly ILessonReminderRepository _lessonReminderRepository;
        private readonly IEmailGroupResolver _emailGroupResolver;
        private readonly string _frontendUrl;
        private readonly List<string> _systemAdminsEmails = new List<string>
        {
            "alvarezvillegaschristian@outlook.com",
            "calvarez@abril.pe"
        };
        private readonly List<string> _supervisorsEmails = new List<string> {
            "hmamani@abril.pe",
            //"coriundo@abril.pe",
            //"alvarezvillegaschristian@outlook.com",
            //"calvarez@abril.pe"
        };
        private readonly List<string> _residentsEmails = new List<string>
        {
            "alvarezvillegaschristian@outlook.com",
            "calvarez@abril.pe"/*,
            "arquitecturacomercialnm@abril.pe",
            "comercialnm@abril.pe",
            "sistemas@abril.pe",
            "Marketingnm@abril.pe",
            "jefesgerenciadeproyectos@abril.pe",
            "maximo_op@abril.pe",
            "camelia_op@abril.pe",
            "kauriop@abril.pe",
            "cedro33_op@abril.pe",
            "granmanzano_op@abril.pe"*/
        };

        // ── Aviso mensual de publicación de lecciones (1er día del mes) ─────────
        // Buzón visible como destinatario/remitente del aviso masivo; también
        // recibe cada lote como copia de auditoría. Cambiar por el correo
        // institucional (ej. comunicaciones@abril.pe) cuando se defina.
        private readonly List<string> _publicationAnnouncementTo = new List<string>
        {
            "calvarez@abril.pe"
        };
        // Tamaño de lote para el BCC: evita topes del proveedor (Outlook/365 ~500
        // destinatarios por mensaje) y reduce el riesgo de marcas de spam.
        private const int PublicationBatchSize = 90;

        public ReminderService(
            IEmailService emailService,
            IMilestoneScheduleRepository milestoneScheduleRepository,
            IMilestoneScheduleHistoryRepository milestoneScheduleHistoryRepository,
            ILessonReminderRepository lessonReminderRepository,
            IEmailGroupResolver emailGroupResolver,
            IConfiguration configuration
        )
        {
            _emailService = emailService;
            _milestoneScheduleRepository = milestoneScheduleRepository;
            _milestoneScheduleHistoryRepository = milestoneScheduleHistoryRepository;
            _lessonReminderRepository = lessonReminderRepository;
            _emailGroupResolver = emailGroupResolver;
            _frontendUrl = configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
        }

        /// <summary>
        /// Envía un correo desglosando previamente los grupos de correo (mail-enabled) en
        /// los correos de sus miembros, tanto en To como en Cc/Bcc. Si un destinatario no es
        /// grupo, se conserva tal cual. Esto evita que un grupo como AppAbrilTest@abril.pe no
        /// llegue a sus miembros cuando el proveedor (PowerAutomate) no sabe entregar a grupos.
        /// </summary>
        private async Task SendEmailExpandingGroupsAsync(
            List<string> to,
            string subject,
            string body,
            bool isHtml,
            List<string>? cc = null,
            List<string>? bcc = null)
        {
            var expandedTo = await ExpandRecipientsAsync(to) ?? to;
            var expandedCc = await ExpandRecipientsAsync(cc);
            var expandedBcc = await ExpandRecipientsAsync(bcc);

            await _emailService.SendAsync(
                to: expandedTo,
                subject: subject,
                body: body,
                isHtml: isHtml,
                cc: expandedCc,
                bcc: expandedBcc);
        }

        private async Task<List<string>?> ExpandRecipientsAsync(List<string>? emails)
        {
            if (emails == null || emails.Count == 0)
                return emails;

            var expanded = await _emailGroupResolver.ExpandAsync(emails);
            // ExpandAsync ya hace dedup y pass-through; si por algún motivo vuelve vacío,
            // conservamos la lista original para no perder destinatarios.
            return expanded.Count > 0 ? expanded : emails;
        }
        public async Task<bool> ExecuteReminders()
        {
            var today = DateTime.UtcNow.AddHours(-5);
            //var today = new DateTime(2026, 6, 29);

            // 1er día del mes (independiente de la ventana de fin de mes): aviso de
            // publicación de las lecciones aprendidas del MES ANTERIOR — el que acaba
            // de cerrar — dirigido a todos los trabajadores @abril con usuario.
            if (today.Day == 1)
            {
                Console.WriteLine("⏰ Día 1 del mes: aviso de publicación de lecciones aprendidas");
                await SendLessonsLearnedPublicationAsync(today);
                Console.WriteLine("📧 Aviso de publicación enviado correctamente");
            }

            // Ventana de los últimos 5 días hábiles del mes:
            //   • Días 1–3: recordatorio para subir lecciones.
            //   • Día 4: reporte de quién NO subió su lección (antes salía el 1er día del mes).
            //   • Días 4–5: ventana de revisión de la jefatura (Aprobar/Rechazar en la app;
            //     no hay correo automático adicional — esos correos los dispara la acción del jefe).
            var ordinal = LastFiveBusinessDayOrdinal(today);

            if (ordinal >= 1 && ordinal <= 3)
            {
                Console.WriteLine($"⏰ Día {ordinal}/5 hábil final: recordatorio para subir lecciones aprendidas");
                // Pasamos `today` (puede estar simulado) para que el período del filtro,
                // el periodLabel del correo y el canal de staff sean consistentes.
                await SendLessonsLearnedMonthlyRemindersAsync(today);
                Console.WriteLine("📧 Recordatorios enviados correctamente");
            }
            else if (ordinal == 4)
            {
                Console.WriteLine("⏰ Día 4/5 hábil final: reporte de pendientes + aviso de revisión a jefaturas");
                await NotifySupervisorsAboutPendingLessonsAsync(today);
                await SendJefesReviewWindowReminderAsync(today);
                Console.WriteLine("📧 Reporte y aviso de revisión enviados correctamente");
            }
            else if (ordinal == 5)
            {
                Console.WriteLine("⏰ Día 5/5 hábil final: ventana de revisión de jefatura (sin correo automático)");
            }
            else
            {
                Console.WriteLine("Hoy no corresponde enviar recordatorios de lecciones");
            }
            return false;
        }

        /// <summary>
        /// Ordinal de <paramref name="date"/> dentro de los últimos 5 días hábiles del
        /// mes: 1 = el más temprano de los 5, 5 = el último día hábil. 0 si la fecha no
        /// cae en esa ventana.
        /// </summary>
        private int LastFiveBusinessDayOrdinal(DateTime date)
        {
            var year = date.Year;
            var month = date.Month;
            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));

            // businessDays[0] = último hábil del mes; businessDays[4] = el más temprano de los 5.
            var businessDays = new List<DateTime>();
            for (var d = lastDay; d.Month == month; d = d.AddDays(-1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                    businessDays.Add(d);
                if (businessDays.Count == 5) break;
            }

            var idx = businessDays.FindIndex(d => d.Date == date.Date);
            if (idx < 0) return 0;
            return businessDays.Count - idx; // idx 0 (último hábil) → 5 ; idx 4 → 1
        }

        /// <summary>
        /// Aviso mensual (1er día del mes) de que las lecciones aprendidas del mes
        /// anterior ya están publicadas. Va a todos los trabajadores cuyo correo
        /// corporativo @abril (worker.email_personal) tiene un usuario registrado.
        /// Los destinatarios viajan en BCC por lotes para no exponer sus correos.
        /// </summary>
        public async Task SendLessonsLearnedPublicationAsync(DateTime executionDate)
        {
            // Mes anterior: al día 1 el mes recién cerrado ya tiene sus lecciones
            // recolectadas (los recordatorios de subida corren a fin de mes).
            var target = executionDate.AddMonths(-1);
            var es = new CultureInfo("es-PE");
            var periodLabel = target.ToString("MMMM yyyy", es);        // "marzo 2026"
            var periodLabelTitle = es.TextInfo.ToTitleCase(periodLabel); // "Marzo 2026"

            var recipients = await _lessonReminderRepository.GetAbrilWorkerEmailsWithUserAsync();
            Console.WriteLine($"📊 [publicación lecciones] destinatarios @abril con usuario: {recipients.Count}");
            if (recipients.Count == 0)
            {
                Console.WriteLine("   • Sin destinatarios; no se envía el aviso de publicación.");
                return;
            }

            var platformUrl = $"{_frontendUrl}/auth/login";

            var subject = $"📘 Publicación de Lecciones Aprendidas — {periodLabelTitle}";

            var body = $@"
            <p>Estimados,</p>

            <p>
                Les informamos que las lecciones aprendidas correspondientes al mes de
                <strong>{periodLabel}</strong> ya se encuentran disponibles en la plataforma
                corporativa para su revisión y consulta.
            </p>

            <p>
                Este registro reúne las principales buenas prácticas, oportunidades de mejora
                y experiencias identificadas en los distintos proyectos, promoviendo la gestión
                del conocimiento y la mejora continua en la organización.
            </p>

            <p>
                Invitamos a todos los equipos a revisar el contenido publicado y considerar su
                aplicación en futuras etapas y proyectos, contribuyendo así a una ejecución más
                eficiente, preventiva y estandarizada.
            </p>

            <p>
                👉 <a href='{platformUrl}' target='_blank'>Acceder a la plataforma</a>
            </p>

            <p>
                Agradecemos el compromiso de cada área en la generación y difusión de
                conocimiento dentro de la organización.
            </p>

            <p style='font-size: 12px; color: #666;'>
                Este mensaje se envía automáticamente el primer día de cada mes.
            </p>
            ";

            // Envío masivo: trabajadores en BCC por lotes (no se exponen entre sí).
            // El To lleva el buzón institucional, que también queda como auditoría.
            var batches = 0;
            foreach (var batch in recipients.Chunk(PublicationBatchSize))
            {
                await _emailService.SendAsync(
                    to: _publicationAnnouncementTo,
                    subject: subject,
                    body: body,
                    isHtml: true,
                    bcc: batch.ToList()
                );
                batches++;
            }

            Console.WriteLine($"📧 Aviso de publicación enviado en {batches} lote(s) a {recipients.Count} destinatario(s).");
        }

        public async Task SendLessonsLearnedMonthlyRemindersAsync(DateTime executionDate)
        {
            var currentPeriod = executionDate.ToString("MM-yyyy");
            var periodLabel = executionDate.ToString("MMMM yyyy", new CultureInfo("es-PE"));
            var platformUrl = $"{_frontendUrl}/auth/login";

            // ─────────────────────────────────────────────────────────────────
            // CANAL 1 — user_project: usuarios asignados a proyectos que no han
            // subido lecciones este mes.
            // ─────────────────────────────────────────────────────────────────
            var pendingUserProjects = await _lessonReminderRepository.GetUsersWithoutLessonsThisMonth(currentPeriod);
            Console.WriteLine($"📊 [user_project] pendientes: {pendingUserProjects.Count}");

            // Staff emails activos por proyecto (project_staff_reminder.active=true).
            // En el canal 1 los usamos como CC; en el canal 2 son el origen mismo.
            var activeStaffEmails = await _lessonReminderRepository.GetActiveStaffEmailsAsync();
            var staffByProjectId = activeStaffEmails
                .GroupBy(s => s.ProjectId)
                .ToDictionary(g => g.Key, g => g.First().StaffEmail);

            // Para deduplicar entre canal 1 y canal 2: si ya mandé al user X
            // por el canal 1 (porque está en user_project), no le mando otra
            // vez por el canal 2 aunque sea miembro del staff_email.
            var emailedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in pendingUserProjects)
            {
                Console.WriteLine($"   • {item.UserFullName} <{item.Email}> · {item.Projects.Count} proyectos");
                var projectsHtml = string.Join("",
                    item.Projects.Select(p => $"<li>{p.ProjectDescription}</li>")
                );

                var body = $@"
                <p>Estimado(a) <strong>{item.UserFullName}</strong>,</p>

                <p>
                    Te recordamos que tienes pendiente el envío mensual de
                    <strong>lecciones aprendidas</strong> correspondiente a
                    <strong>{periodLabel}</strong> en los siguientes proyectos:
                </p>

                <ul>
                    {projectsHtml}
                </ul>

                <p>
                    Por favor ingresa a la plataforma y completa el envío:
                </p>

                <p>
                    👉 <a href='{platformUrl}' target='_blank'>
                        Acceder a la plataforma
                    </a>
                </p>

                <p style='font-size: 12px; color: #666;'>
                    Este recordatorio se envía de manera automática a quienes aún no han registrado
                    sus lecciones aprendidas del mes.
                </p>

                <p>Gracias por tu compromiso con la mejora continua.</p>
                ";

                // Recipientes: el usuario + staff_email activo de los proyectos donde tiene pendientes
                var to = new List<string> { item.Email };
                var staffCcSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in item.Projects)
                {
                    if (staffByProjectId.TryGetValue(p.ProjectId, out var staff)
                        && !string.IsNullOrWhiteSpace(staff))
                    {
                        staffCcSet.Add(staff);
                    }
                }

                await SendEmailExpandingGroupsAsync(
                    to: to,
                    subject: "🔔 Abril App Recordatorio: envío mensual de lecciones aprendidas pendiente",
                    body: body,
                    isHtml: true,
                    cc: staffCcSet.Count > 0 ? staffCcSet.ToList() : null,
                    bcc: new List<string> {"calvarez@abril.pe"}
                );

                if (!string.IsNullOrWhiteSpace(item.Email))
                    emailedThisRun.Add(item.Email.Trim());
            }

            // ─────────────────────────────────────────────────────────────────
            // CANAL 2 — project_staff_reminder: para cada proyecto cuyo
            // staff_email está activo, expandir el grupo a sus miembros y
            // verificar individualmente que cada uno haya subido lección al
            // proyecto este mes. Los miembros que no aparecen en `user` se
            // consideran pendientes (no hay cómo verificar).
            // ─────────────────────────────────────────────────────────────────
            Console.WriteLine($"📊 [project_staff_reminder] proyectos activos: {activeStaffEmails.Count}");

            foreach (var staffProject in activeStaffEmails)
            {
                // Expandir el grupo a miembros individuales.
                var members = await _emailGroupResolver.ExpandAsync(
                    new List<string> { staffProject.StaffEmail }
                );

                // Si la expansión devuelve vacío o el correo no es grupo,
                // tratamos al propio staff_email como destinatario.
                if (members == null || members.Count == 0)
                    members = new List<string> { staffProject.StaffEmail };

                Console.WriteLine(
                    $"   • staff de {staffProject.ProjectDescription} ({staffProject.StaffEmail}) " +
                    $"→ expandido a {members.Count} miembro(s)"
                );

                // Filtrar a los que NO tienen lección registrada para el proyecto/período.
                var pendingMembers = await _lessonReminderRepository
                    .GetPendingMembersForProjectAsync(staffProject.ProjectId, currentPeriod, members);

                Console.WriteLine($"       pendientes: {pendingMembers.Count}");

                foreach (var m in pendingMembers)
                {
                    // Dedup: no mandar dos veces si ya recibió correo por el canal 1
                    if (emailedThisRun.Contains(m.Email)) continue;

                    var greeting = !string.IsNullOrWhiteSpace(m.FullName)
                        ? $"Estimado(a) <strong>{m.FullName}</strong>,"
                        : "Estimado(a),";

                    var body = $@"
                    <p>{greeting}</p>

                    <p>
                        Como miembro del staff del proyecto
                        <strong>{staffProject.ProjectDescription}</strong>, te recordamos que aún
                        no tienes registrada tu <strong>lección aprendida</strong> correspondiente
                        a <strong>{periodLabel}</strong>.
                    </p>

                    <p>
                        Por favor ingresa a la plataforma y completa el envío:
                    </p>

                    <p>
                        👉 <a href='{platformUrl}' target='_blank'>
                            Acceder a la plataforma
                        </a>
                    </p>

                    <p style='font-size: 12px; color: #666;'>
                        Este recordatorio se envía automáticamente a los miembros del grupo
                        <strong>{staffProject.StaffEmail}</strong>.
                    </p>

                    <p>Gracias por tu compromiso con la mejora continua.</p>
                    ";

                    // Enviar directo (ya está expandido — no volver a expandir).
                    await _emailService.SendAsync(
                        to: new List<string> { m.Email },
                        subject: $"🔔 Abril App Recordatorio: lección pendiente para {staffProject.ProjectDescription} — {periodLabel}",
                        body: body,
                        isHtml: true,
                        bcc: new List<string> { "calvarez@abril.pe" }
                    );

                    emailedThisRun.Add(m.Email);
                }
            }
        }

        public async Task NotifySupervisorsAboutPendingLessonsAsync(DateTime executionDate)
        {
            // Se mueve del 1er día del mes al día 4 de los últimos 5 hábiles → reporta el MES ACTUAL.
            var targetMonthDate = executionDate;
            var periodLabel = targetMonthDate.ToString("MMMM yyyy", new CultureInfo("es-PE"));

            var pendingUserProjects = await _lessonReminderRepository.GetUsersWithoutLessonsByPeriod(targetMonthDate);

            if (!pendingUserProjects.Any())
                return;

            var pendingEmails = pendingUserProjects
                .Select(u => u.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .ToList();

            var emailsTo = new List<string>();

            emailsTo.AddRange(_supervisorsEmails);
            emailsTo.AddRange(pendingEmails);

            var platformUrl = $"{_frontendUrl}/auth/login";

            var usersHtml = string.Join("",
                pendingUserProjects.Select(u => $@"
            <li>
                <strong>{u.UserFullName}</strong> ({u.Email})
                <ul>
                    {string.Join("", u.Projects.Select(p => $"<li>{p.ProjectDescription}</li>"))}
                </ul>
            </li>
        ")
            );

            var body = $@"
        <p>Estimados,</p>

        <p>
            Se detectaron usuarios que <strong>no registraron lecciones aprendidas</strong>
            correspondientes a <strong>{periodLabel}</strong>.
        </p>

        <p>Detalle:</p>

        <ul>
            {usersHtml}
        </ul>

        <p>
            👉 <a href='{platformUrl}' target='_blank'>
                Acceder a la plataforma
            </a>
        </p>

        <p style='font-size: 12px; color: #666;'>
            Este mensaje se envía automáticamente el 4.º día hábil final del mes.
        </p>
    ";

            await SendEmailExpandingGroupsAsync(
                to: emailsTo,
                //to: new List<string> {"alvarezvillegaschristian@outlook.com"},
                subject: $"📊 Reporte mensual: usuarios sin lecciones — {periodLabel}",
                body: body,
                isHtml: true,
                //bcc: _systemAdminsEmails
                bcc: new List<string> {"calvarez@abril.pe"}
            );
        }

        /// <summary>
        /// Aviso del 4.º día hábil a las jefaturas ACTIVAS en la sección
        /// "Jefaturas" (lesson_jefe_reminder.active=true): las lecciones del mes ya
        /// están listas y se abre la ventana de revisión (Aprobar/Rechazar). Es
        /// independiente del reporte de pendientes — sale aunque no haya pendientes.
        /// </summary>
        public async Task SendJefesReviewWindowReminderAsync(DateTime executionDate)
        {
            var periodLabel = executionDate.ToString("MMMM yyyy", new CultureInfo("es-PE"));

            var jefes = await _lessonReminderRepository.GetActiveJefesReviewStatusAsync();
            Console.WriteLine($"📊 [jefaturas] activas para aviso de revisión: {jefes.Count}");
            if (jefes.Count == 0)
            {
                Console.WriteLine("   • Sin jefaturas activas; no se envía el aviso de revisión.");
                return;
            }

            var platformUrl = $"{_frontendUrl}/auth/login";

            foreach (var jefe in jefes)
            {
                if (string.IsNullOrWhiteSpace(jefe.Email)) continue;

                var saludo = !string.IsNullOrWhiteSpace(jefe.FullName)
                    ? $"Estimado(a) <strong>{jefe.FullName}</strong>,"
                    : "Estimado(a),";

                var pieHtml = @"
                <p style='font-size: 12px; color: #666;'>
                    Este aviso se envía automáticamente el 4.º día hábil final del mes a las
                    jefaturas habilitadas en la configuración de recordatorios.
                </p>";

                string subject;
                string body;

                if (jefe.PendingCount > 0)
                {
                    var sustantivo = jefe.PendingCount == 1 ? "lección pendiente" : "lecciones pendientes";
                    subject = $"📋 Lecciones aprendidas listas para tu revisión — {periodLabel}";
                    body = $@"
                    <p>{saludo}</p>

                    <p>
                        Se ha abierto la <strong>ventana de revisión</strong> de Lecciones Aprendidas de
                        <strong>{periodLabel}</strong>. Tu equipo tiene <strong>{jefe.PendingCount}</strong>
                        {sustantivo} de tu revisión.
                    </p>

                    <p>
                        Ingresa a la plataforma para <strong>aprobar o rechazar</strong> durante la ventana
                        de revisión (los últimos 2 días hábiles del mes).
                    </p>

                    <p>
                        👉 <a href='{platformUrl}' target='_blank'>Acceder a la plataforma</a>
                    </p>
                    {pieHtml}";
                }
                else
                {
                    subject = $"📋 Ventana de revisión de Lecciones Aprendidas — {periodLabel} (sin pendientes)";
                    body = $@"
                    <p>{saludo}</p>

                    <p>
                        Se ha detectado que hoy inicia la <strong>ventana de revisión</strong> de Lecciones
                        Aprendidas de <strong>{periodLabel}</strong>, pero <strong>no tienes lecciones de tu
                        equipo pendientes de revisar</strong> en este momento.
                    </p>

                    <p>
                        No necesitas hacer nada. Si más adelante un integrante de tu equipo registra o
                        edita una lección, te aparecerá pendiente en la plataforma.
                    </p>

                    <p>
                        👉 <a href='{platformUrl}' target='_blank'>Acceder a la plataforma</a>
                    </p>
                    {pieHtml}";
                }

                await _emailService.SendAsync(
                    to: new List<string> { jefe.Email },
                    subject: subject,
                    body: body,
                    isHtml: true,
                    bcc: new List<string> { "calvarez@abril.pe" });
            }

            Console.WriteLine($"📧 Aviso de revisión enviado a {jefes.Count} jefatura(s).");
        }

        public async Task SendMilestoneScheduleMonthlyReminderAsync(DateTime executionDate)
        {
            var periodLabel = executionDate.ToString("MMMM yyyy", new CultureInfo("es-PE"));

            var changes = await _milestoneScheduleRepository.GetSchedulesWithChangesThisMonthAsync();

            if (!changes.Any())
                return;

            var platformUrl = $"{_frontendUrl}/auth/login";

            var projectsHtml = string.Join("",
                changes.Select(x =>
                {
                    var datesHtml = string.Join("<br/>",
                        x.ChangeDate
                            .OrderBy(d => d)
                            .Select(d => $"📅 {d:dd/MM/yyyy HH:mm}")
                    );

                    return $@"
            <li>
                <strong>{x.ProjectDescription}</strong><br/>
                👤 Usuario: {x.ChangedBy}<br/>
                {datesHtml}
            </li>
            ";
                })
            );

            var body = $@"
            <p>Estimados,</p>

            <p>
                Se detectaron cambios en el cronograma de los siguientes proyectos
                durante <strong>{periodLabel}</strong>.
            </p>

            <p>Detalle:</p>

            <ul>
                {projectsHtml}
            </ul>

            <p>
                👉 <a href='{platformUrl}' target='_blank'>
                    Acceder a la plataforma
                </a>
            </p>

            <p style='font-size: 12px; color: #666;'>
                Este mensaje se envía automáticamente el primer día laboral de cada mes.
            </p>
            ";

            await SendEmailExpandingGroupsAsync(
                to: _residentsEmails,
                subject: $"📊 Reporte mensual: cambios en cronogramas — {periodLabel}",
                body: body,
                isHtml: true,
                bcc: new List<string> {"calvarez@abril.pe"}
            );
        }

        public async Task SendMilestoneScheduleHistoryMonthlyRemindersAsync(DateTime executionDate)
        {
            var pendingUserProjects = await _milestoneScheduleHistoryRepository.GetUsersWithoutScheduleHistoryThisMonth();
            var periodLabel = executionDate.ToString("MMMM yyyy", new CultureInfo("es-PE"));
            //var periodLabel = "Enero 2026";
            var platformUrl = $"{_frontendUrl}/auth/login";
            foreach (var item in pendingUserProjects)
            {
                Console.WriteLine(item.Email);
                var projectsHtml = string.Join("",
                    item.Projects.Select(p => $"<li>{p.ProjectDescription}</li>")
                );

                var body = $@"
                <p>Estimado(a) <strong>{item.UserFullName}</strong>,</p>

                <p>
                    Te recordamos que tienes pendiente el envío mensual de
                    <strong>cronograma de hitos</strong> correspondiente a
                    <strong>{periodLabel}</strong> en los siguientes proyectos:
                </p>

                <ul>
                    {projectsHtml}
                </ul>

                <p>
                    Por favor ingresa a la plataforma y completa el envío:
                </p>

                <p>
                    👉 <a href='{platformUrl}' target='_blank'>
                        Acceder a la plataforma
                    </a>
                </p>

                <p style='font-size: 12px; color: #666;'>
                    Este recordatorio se envía de manera automática a quienes aún no han registrado si tuvieron cambios o no
                    en su cronograma de hitos durante este mes.
                </p>

                <p>Gracias por tu compromiso con la mejora continua.</p>
                ";

                await SendEmailExpandingGroupsAsync(
                    to: new List<string> { item.Email },
                    subject: "🔔 Abril App Recordatorio: envío mensual de cronograma de hitos pendiente",
                    body: body,
                    isHtml: true,
                    bcc: new List<string> {"calvarez@abril.pe"}
                );
            }
        }
    }
}