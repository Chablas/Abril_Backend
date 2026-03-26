using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;
using System.Globalization;

namespace Abril_Backend.Application.Services
{
    public class ReminderService : IReminderService
    {
        private readonly IUserProjectRepository _userProjectRepository;
        private readonly IMilestoneScheduleRepository _milestoneScheduleRepository;
        private readonly IEmailService _emailService;
        private readonly IMilestoneScheduleHistoryRepository _milestoneScheduleHistoryRepository;
        private readonly List<string> _systemAdminsEmails = new List<string>
        {
            "alvarezvillegaschristian@outlook.com",
            "calvarez@abril.pe"
        };
        private readonly List<string> _supervisorsEmails = new List<string> {
            //"hmamani@abril.pe",
            //"coriundo@abril.pe",
            "alvarezvillegaschristian@outlook.com",
            "calvarez@abril.pe"
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
            IMilestoneScheduleHistoryRepository milestoneScheduleHistoryRepository
        )
        {
            _userProjectRepository = userProjectRepository;
            _emailService = emailService;
            _milestoneScheduleRepository = milestoneScheduleRepository;
            _milestoneScheduleHistoryRepository = milestoneScheduleHistoryRepository;
        }
        public async Task<bool> ExecuteReminders()
        {
            var today = DateTime.UtcNow.AddHours(-5);
            //var today = new DateTime(2026, 3, 27);
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
                /*Console.WriteLine("⏰ Recordatorio mensual para subir lecciones aprendidas ejecutado");
                await SendLessonsLearnedMonthlyRemindersAsync(DateTime.UtcNow.AddHours(-5));
                Console.WriteLine("📧 Recordatorios enviados correctamente");*/

                // este deberia ejecutarse el ultimo dia laborable del mes
                /*Console.WriteLine("⏰ Recordatorio mensual de cronograma de hitos ejecutado");
                await SendMilestoneScheduleMonthlyReminderAsync(DateTime.UtcNow.AddHours(-5));
                Console.WriteLine("📧 Recordatorios enviados correctamente");*/

                Console.WriteLine("⏰ Recordatorio mensual para subir cronograma de hitos ejecutado");
                await SendMilestoneScheduleHistoryMonthlyRemindersAsync(DateTime.UtcNow.AddHours(-5));
                Console.WriteLine("📧 Recordatorios enviados correctamente");
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
            var pendingUserProjects = await _userProjectRepository.GetUsersWithoutLessonsThisMonth();
            var periodLabel = executionDate.ToString("MMMM yyyy", new CultureInfo("es-PE"));
            //var periodLabel = "Enero 2026";
            var platformUrl = "https://abril-frontend.onrender.com/auth/login";
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

                await _emailService.SendAsync(
                    to: new List<string> { item.Email },
                    subject: "🔔 Abril App Recordatorio: envío mensual de lecciones aprendidas pendiente",
                    body: body,
                    isHtml: true,
                    bcc: new List<string> {"calvarez@abril.pe"}
                );
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

            var platformUrl = "https://abril-frontend.onrender.com/auth/login";

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

            await _emailService.SendAsync(
                //to: emailsTo,
                to: new List<string> {"alvarezvillegaschristian@outlook.com"},
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

            var platformUrl = "https://abril-frontend.onrender.com/auth/login";

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

            await _emailService.SendAsync(
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
            var platformUrl = "https://abril-frontend.onrender.com/auth/login";
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

                await _emailService.SendAsync(
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