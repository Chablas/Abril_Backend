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
        private readonly IUserProjectRepository _userProjectRepository;
        private readonly IMilestoneScheduleRepository _milestoneScheduleRepository;
        private readonly IEmailService _emailService;
        private readonly IMilestoneScheduleHistoryRepository _milestoneScheduleHistoryRepository;
        private readonly ILessonReminderRepository _lessonReminderRepository;
        private readonly IEmailGroupResolver _emailGroupResolver;
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
        public ReminderService(
            IUserProjectRepository userProjectRepository,
            IEmailService emailService,
            IMilestoneScheduleRepository milestoneScheduleRepository,
            IMilestoneScheduleHistoryRepository milestoneScheduleHistoryRepository,
            ILessonReminderRepository lessonReminderRepository,
            IEmailGroupResolver emailGroupResolver
        )
        {
            _userProjectRepository = userProjectRepository;
            _emailService = emailService;
            _milestoneScheduleRepository = milestoneScheduleRepository;
            _milestoneScheduleHistoryRepository = milestoneScheduleHistoryRepository;
            _lessonReminderRepository = lessonReminderRepository;
            _emailGroupResolver = emailGroupResolver;
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
            //var today = new DateTime(2026, 6, 26);
            if (IsInFirstDayOfMonth(today))
            {
                Console.WriteLine("⏰ Recordatorio mensual a supervisores ejecutado");
                await NotifySupervisorsAboutPendingLessonsAsync(today);
                Console.WriteLine("📧 Recordatorios enviados correctamente");
            }
            else
            {
                Console.WriteLine("Hoy no corresponde enviar recordatorios de notificación a supervisores");
            }

            if (IsInLastFiveBusinessDays(today))
            {
                Console.WriteLine("⏰ Recordatorio mensual para subir lecciones aprendidas ejecutado");
                // Pasamos `today` (puede estar simulado) para que el período del filtro,
                // el periodLabel del correo y el canal de staff sean consistentes.
                await SendLessonsLearnedMonthlyRemindersAsync(today);
                Console.WriteLine("📧 Recordatorios enviados correctamente");

                // este deberia ejecutarse el ultimo dia laborable del mes
                /*Console.WriteLine("⏰ Recordatorio mensual de cronograma de hitos ejecutado");
                await SendMilestoneScheduleMonthlyReminderAsync(DateTime.UtcNow.AddHours(-5));
                Console.WriteLine("📧 Recordatorios enviados correctamente");*/

                /*Console.WriteLine("⏰ Recordatorio mensual para subir cronograma de hitos ejecutado");
                await SendMilestoneScheduleHistoryMonthlyRemindersAsync(DateTime.UtcNow.AddHours(-5));
                Console.WriteLine("📧 Recordatorios enviados correctamente");*/
            }
            else
            {
                Console.WriteLine("Hoy no corresponde enviar recordatorios de lecciones ni cronogramas");
            }
            return false;
        }

        private bool IsInFirstDayOfMonth(DateTime date)
        {
            var firstDay = new DateTime(date.Year, date.Month, 1);

            if (firstDay.DayOfWeek == DayOfWeek.Saturday)
                firstDay = firstDay.AddDays(2);

            else if (firstDay.DayOfWeek == DayOfWeek.Sunday)
                firstDay = firstDay.AddDays(1);

            return date.Date == firstDay.Date;
        }

        private bool IsInLastFiveBusinessDays(DateTime date)
        {
            var year = date.Year;
            var month = date.Month;

            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));

            var businessDays = new List<DateTime>();

            for (var d = lastDay; d.Month == month; d = d.AddDays(-1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday &&
                    d.DayOfWeek != DayOfWeek.Sunday)
                {
                    businessDays.Add(d);
                }

                if (businessDays.Count == 5)
                    break;
            }

            return businessDays.Any(d => d.Date == date.Date);
        }

        public async Task SendLessonsLearnedMonthlyRemindersAsync(DateTime executionDate)
        {
            var currentPeriod = executionDate.ToString("MM-yyyy");
            var periodLabel = executionDate.ToString("MMMM yyyy", new CultureInfo("es-PE"));
            var platformUrl = "https://abril-frontend-m21l.onrender.com/auth/login";

            // ─────────────────────────────────────────────────────────────────
            // CANAL 1 — user_project: usuarios asignados a proyectos que no han
            // subido lecciones este mes.
            // ─────────────────────────────────────────────────────────────────
            var pendingUserProjects = await _userProjectRepository.GetUsersWithoutLessonsThisMonth(currentPeriod);
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
            var previousMonthDate = executionDate.AddMonths(-1);
            var periodLabel = previousMonthDate.ToString("MMMM yyyy", new CultureInfo("es-PE"));

            var pendingUserProjects = await _userProjectRepository.GetUsersWithoutLessonsByPeriod(previousMonthDate);

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

            var platformUrl = "https://abril-frontend-m21l.onrender.com/auth/login";

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
            Este mensaje se envía automáticamente el primer día laboral de cada mes.
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

        public async Task SendMilestoneScheduleMonthlyReminderAsync(DateTime executionDate)
        {
            var periodLabel = executionDate.ToString("MMMM yyyy", new CultureInfo("es-PE"));

            var changes = await _milestoneScheduleRepository.GetSchedulesWithChangesThisMonthAsync();

            if (!changes.Any())
                return;

            var platformUrl = "https://abril-frontend-m21l.onrender.com/auth/login";

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
            var platformUrl = "https://abril-frontend-m21l.onrender.com/auth/login";
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