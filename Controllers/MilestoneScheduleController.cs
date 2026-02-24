using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Abril_Backend.Controllers
{

    [ApiController]
    [Route("api/v1/[controller]")]
    public class MilestoneScheduleController : ControllerBase
    {
        MilestoneRepository _milestoneRepository;
        MilestoneScheduleRepository _repository;
        public MilestoneScheduleController(MilestoneScheduleRepository repository, MilestoneRepository milestoneRepository)
        {
            _repository = repository;
            _milestoneRepository = milestoneRepository;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllByMilestoneScheduleHistoryIdFactory([FromQuery] int milestoneScheduleHistoryId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _repository.GetAllByMilestoneScheduleHistoryIdFactory(milestoneScheduleHistoryId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        //[Authorize]
        [HttpGet("fake-data")]
        public async Task<IActionResult> BuildFakeSchedule(int milestoneScheduleHistoryId)
        {
            /*var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });*/
            var milestones = await _milestoneRepository.GetAllFactorySimple();

            var fakeScheduleConfig = new Dictionary<int, (DateTime start, DateTime? end)>
            {
                { 1,  (new DateTime(2025, 12, 22), null) },
                { 2,  (new DateTime(2025, 12, 22), new DateTime(2026, 4, 7)) },
                { 3,  (new DateTime(2026, 3, 28), new DateTime(2026, 5, 26)) },
                { 4,  (new DateTime(2026, 1, 30), null) },
                { 5,  (new DateTime(2026, 2, 20), null) },
                { 6,  (new DateTime(2026, 5, 21), new DateTime(2026, 7, 7)) },
                { 7,  (new DateTime(2026, 7, 2), null) },
                { 8,  (new DateTime(2026, 7, 7), new DateTime(2026, 11, 7)) },
                { 9,  (new DateTime(2026, 11, 10), null) },
                { 10, (new DateTime(2026, 6, 21), new DateTime(2026, 9, 3)) },
                { 11, (new DateTime(2026, 7, 21), new DateTime(2026, 10, 14)) },
                { 12, (new DateTime(2026, 8, 21), new DateTime(2026, 10, 31)) },
                { 13, (new DateTime(2026, 10, 7), new DateTime(2026, 12, 15)) },
                { 14, (new DateTime(2026, 9, 7), new DateTime(2027, 2, 6)) },
                { 15, (new DateTime(2026, 10, 7), new DateTime(2026, 10, 31)) },
                { 16, (new DateTime(2026, 10, 7), new DateTime(2027, 4, 12)) },
                { 17, (new DateTime(2026, 12, 7), new DateTime(2027, 1, 30)) },
                { 18, (new DateTime(2026, 10, 7), new DateTime(2027, 5, 3)) },
                { 19, (new DateTime(2026, 12, 28), new DateTime(2027, 3, 30)) },
                { 20, (new DateTime(2027, 4, 7), new DateTime(2027, 4, 30)) },
                { 21, (new DateTime(2027, 5, 3), null) }
            };

            int order = 1;

            var result = milestones
                .Where(m => fakeScheduleConfig.ContainsKey(m.MilestoneId))
                .Select(m =>
                {
                    var config = fakeScheduleConfig[m.MilestoneId];

                    return new MilestoneScheduleFakeDataDTO
                    {
                        MilestoneId = m.MilestoneId,
                        MilestoneDescription = m.MilestoneDescription,
                        PlannedStartDate = config.start,
                        PlannedEndDate = config.end,
                        Order = order++
                    };
                })
                .OrderBy(x => x.MilestoneId)
                .ToList();

            return Ok(result);
        }
        /*
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MilestoneScheduleCreateDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                if (string.IsNullOrWhiteSpace(dto.MilestoneScheduleDescription))
                    return BadRequest(new { message = "MilestoneScheduleDescription es obligatorio." });
                var result = await _repository.Create(dto, userId);
                return Ok(new { message = "MilestoneSchedule creada exitosamente" });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
        */
    }
}