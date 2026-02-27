using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Infrastructure.Repositories
{

    public class LessonRepository
    {

        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IFileStorageService _fileStorageService;

        public LessonRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory, IFileStorageService fileStorageService)
        {
            _context = contexto;
            _factory = factory;
            _fileStorageService = fileStorageService;
        }

        public async Task<List<LessonListDTO>> GetAll()
        {
            var registros = await (from lesson in _context.Lesson
                                   join project in _context.Project on lesson.ProjectId equals project.ProjectId
                                   join area in _context.Area on lesson.AreaId equals area.AreaId
                                   join psss in _context.PhaseStageSubStageSubSpecialty on lesson.PhaseStageSubStageSubSpecialtyId equals psss.PhaseStageSubStageSubSpecialtyId
                                   join phase in _context.Phase on psss.PhaseId equals phase.PhaseId
                                   join stage in _context.Stage on psss.StageId equals stage.StageId
                                   join substage in _context.SubStage on psss.SubStageId equals substage.SubStageId
                                   join state in _context.State on lesson.StateId equals state.StateId
                                   where lesson.State == true
                                   select new LessonListDTO
                                   {
                                       LessonId = lesson.LessonId,
                                       LessonCode = lesson.LessonCode,
                                       Period = lesson.Period,
                                       ProblemDescription = lesson.ProblemDescription,
                                       ReasonDescription = lesson.ReasonDescription,
                                       LessonDescription = lesson.LessonDescription,
                                       ImpactDescription = lesson.ImpactDescription,
                                       ProjectId = lesson.ProjectId,
                                       ProjectDescription = project.ProjectDescription,
                                       AreaId = lesson.AreaId,
                                       AreaDescription = area.AreaDescription,
                                       PhaseStageSubStageSubSpecialtyId = lesson.PhaseStageSubStageSubSpecialtyId,
                                       PhaseId = psss.PhaseId,
                                       PhaseDescription = phase.PhaseDescription,
                                       StageId = psss.StageId,
                                       StageDescription = stage.StageDescription,
                                       SubStageId = psss.SubStageId,
                                       SubStageDescription = substage.SubStageDescription,
                                       StateId = lesson.StateId,
                                       StateDescription = state.StateDescription,
                                       CreatedDateTime = lesson.CreatedDateTime,
                                       CreatedUserId = lesson.CreatedUserId,
                                       UpdatedDateTime = lesson.UpdatedDateTime,
                                       UpdatedUserId = lesson.UpdatedUserId,
                                       Active = lesson.Active,
                                       Images = new List<LessonImageDTO>()
                                   }).ToListAsync();

            var imagenes = await (from img in _context.LessonImages
                                  join imagetype in _context.ImageType on img.ImageTypeId equals imagetype.ImageTypeId
                                  where img.State == true
                                  select new LessonImageDTO
                                  {
                                      LessonImageId = img.LessonImageId,
                                      ImageUrl = img.ImageUrl,
                                      LessonId = img.LessonId,
                                      ImageTypeId = img.ImageTypeId,
                                      ImageTypeDescription = imagetype.ImageTypeDescription
                                  }).ToListAsync();

            var imagesByLesson = imagenes.GroupBy(i => i.LessonId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var lesson in registros)
            {
                if (imagesByLesson.TryGetValue(lesson.LessonId, out var imgs))
                {
                    lesson.Images = imgs;
                }
            }
            return registros;
        }

        public async Task<LessonDetailDTO?> GetById(int id)
        {
            var registro = await (
                from lesson in _context.Lesson

                join project in _context.Project
                    on lesson.ProjectId equals project.ProjectId

                join area in _context.Area
                    on lesson.AreaId equals area.AreaId

                join psss in _context.PhaseStageSubStageSubSpecialty
                    on lesson.PhaseStageSubStageSubSpecialtyId equals psss.PhaseStageSubStageSubSpecialtyId

                join phase in _context.Phase
                    on psss.PhaseId equals phase.PhaseId

                join stage in _context.Stage
                    on psss.StageId equals stage.StageId

                join substage in _context.SubStage
                    on psss.SubStageId equals substage.SubStageId into ss
                from substage in ss.DefaultIfEmpty()

                join subspecialty in _context.SubSpecialty
                    on psss.SubSpecialtyId equals subspecialty.SubSpecialtyId into sp
                from subspecialty in sp.DefaultIfEmpty()

                join user in _context.User
                    on lesson.CreatedUserId equals user.UserId into us
                from user in us.DefaultIfEmpty()

                join person in _context.Person
                    on user.PersonId equals person.PersonId into pe
                from person in pe.DefaultIfEmpty()

                join state in _context.State
                    on lesson.StateId equals state.StateId

                where lesson.State == true && lesson.LessonId == id

                select new LessonDetailDTO
                {
                    LessonId = lesson.LessonId,
                    LessonCode = lesson.LessonCode,
                    Period = lesson.Period,
                    ProblemDescription = lesson.ProblemDescription,
                    ReasonDescription = lesson.ReasonDescription,
                    LessonDescription = lesson.LessonDescription,
                    ImpactDescription = lesson.ImpactDescription,

                    ProjectId = lesson.ProjectId,
                    ProjectDescription = project.ProjectDescription,

                    AreaId = lesson.AreaId,
                    AreaDescription = area.AreaDescription,

                    PhaseStageSubStageSubSpecialtyId = lesson.PhaseStageSubStageSubSpecialtyId,

                    PhaseId = psss.PhaseId,
                    PhaseDescription = phase.PhaseDescription,

                    StageId = psss.StageId,
                    StageDescription = stage.StageDescription,

                    SubStageId = psss.SubStageId,
                    SubStageDescription = substage != null
                        ? substage.SubStageDescription
                        : null,

                    SubSpecialtyId = psss.SubSpecialtyId,
                    SubSpecialtyDescription = subspecialty != null
                        ? subspecialty.SubSpecialtyDescription
                        : null,

                    StateId = lesson.StateId,
                    StateDescription = state.StateDescription,

                    CreatedDateTime = lesson.CreatedDateTime,
                    CreatedUserId = lesson.CreatedUserId,
                    CreatedUserFullName = person != null ? person.FullName : null,
                    UpdatedDateTime = lesson.UpdatedDateTime,
                    UpdatedUserId = lesson.UpdatedUserId,
                    Active = lesson.Active
                }
            ).FirstOrDefaultAsync();

            if (registro == null)
                return null;

            // 🔹 Imágenes (correcto como lo tienes)
            registro.Images = await (
                from img in _context.LessonImages
                join imagetype in _context.ImageType
                    on img.ImageTypeId equals imagetype.ImageTypeId
                where img.LessonId == id && img.State == true
                select new LessonImageDTO
                {
                    LessonImageId = img.LessonImageId,
                    ImageUrl = img.ImageUrl,
                    LessonId = img.LessonId,
                    ImageTypeId = img.ImageTypeId,
                    ImageTypeDescription = imagetype.ImageTypeDescription
                }
            ).ToListAsync();

            return registro;
        }

        public async Task<List<LessonListDTO>> GetLessonsFilter(
            string? period,
            int? stateId,
            int? projectId,
            int? areaId,
            int? phaseId,
            int? stageId,
            int? layerId,
            int? subStageId,
            int? subSpecialtyId
        )
        {
            var query = _context.Lesson
                .Where(x => x.Active)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(period))
                query = query.Where(x => x.Period == period);

            if (stateId.HasValue)
                query = query.Where(x => x.StateId == stateId.Value);

            if (projectId.HasValue)
                query = query.Where(x => x.ProjectId == projectId.Value);

            if (areaId.HasValue)
                query = query.Where(x => x.AreaId == areaId.Value);

            var result =
                from lesson in query
                join project in _context.Project on lesson.ProjectId equals project.ProjectId into pj
                from project in pj.DefaultIfEmpty()

                join area in _context.Area on lesson.AreaId equals area.AreaId

                join psss in _context.PhaseStageSubStageSubSpecialty
                    on lesson.PhaseStageSubStageSubSpecialtyId equals psss.PhaseStageSubStageSubSpecialtyId into ps
                from psss in ps.DefaultIfEmpty()

                join phase in _context.Phase on psss.PhaseId equals phase.PhaseId into ph
                from phase in ph.DefaultIfEmpty()

                join stage in _context.Stage on psss.StageId equals stage.StageId into st
                from stage in st.DefaultIfEmpty()

                join layer in _context.Layer on psss.LayerId equals layer.LayerId into ly
                from layer in ly.DefaultIfEmpty()

                join substage in _context.SubStage on psss.SubStageId equals substage.SubStageId into ss
                from substage in ss.DefaultIfEmpty()

                join subspecialty in _context.SubSpecialty
                    on psss.SubSpecialtyId equals subspecialty.SubSpecialtyId into sp
                from subspecialty in sp.DefaultIfEmpty()

                join user in _context.User
                    on lesson.CreatedUserId equals user.UserId into us
                from user in us.DefaultIfEmpty()

                join person in _context.Person
                    on user.PersonId equals person.PersonId into pe
                from person in pe.DefaultIfEmpty()

                join state in _context.State on lesson.StateId equals state.StateId
                select new
                {
                    lesson,
                    project,
                    area,
                    psss,
                    phase,
                    stage,
                    layer,
                    substage,
                    subspecialty,
                    state,
                    person
                };

            if (phaseId.HasValue)
                result = result.Where(x => x.psss != null && x.psss.PhaseId == phaseId.Value);

            if (stageId.HasValue)
                result = result.Where(x => x.psss != null && x.psss.StageId == stageId.Value);

            if (layerId.HasValue)
                result = result.Where(x => x.psss != null && x.psss.LayerId == layerId.Value);

            if (subStageId.HasValue)
                result = result.Where(x => x.psss != null && x.psss.SubStageId == subStageId.Value);

            if (subSpecialtyId.HasValue)
                result = result.Where(x => x.psss != null && x.psss.SubSpecialtyId == subSpecialtyId.Value);

            var registros = await result
                .OrderByDescending(x => x.lesson.CreatedDateTime)
                .Select(x => new LessonListDTO
                {
                    LessonId = x.lesson.LessonId,
                    LessonCode = x.lesson.LessonCode,
                    Period = x.lesson.Period,
                    ProblemDescription = x.lesson.ProblemDescription,
                    ReasonDescription = x.lesson.ReasonDescription,
                    LessonDescription = x.lesson.LessonDescription,
                    ImpactDescription = x.lesson.ImpactDescription,

                    ProjectId = x.lesson.ProjectId,
                    ProjectDescription = x.project != null ? x.project.ProjectDescription : null,

                    AreaId = x.lesson.AreaId,
                    AreaDescription = x.area.AreaDescription,

                    PhaseStageSubStageSubSpecialtyId = x.lesson.PhaseStageSubStageSubSpecialtyId,

                    PhaseId = x.psss != null ? (int?)x.psss.PhaseId : null,
                    PhaseDescription = x.phase != null ? x.phase.PhaseDescription : null,

                    StageId = x.psss != null ? (int?)x.psss.StageId : null,
                    StageDescription = x.stage != null ? x.stage.StageDescription : null,

                    LayerId = x.psss != null ? (int?)x.psss.LayerId : null,
                    LayerDescription = x.layer != null ? x.layer.LayerDescription : null,

                    SubStageId = x.psss != null ? (int?)x.psss.SubStageId : null,
                    SubStageDescription = x.substage != null ? x.substage.SubStageDescription : null,

                    SubSpecialtyId = x.psss != null ? (int?)x.psss.SubSpecialtyId : null,
                    SubSpecialtyDescription = x.subspecialty != null ? x.subspecialty.SubSpecialtyDescription : null,

                    StateId = x.lesson.StateId,
                    StateDescription = x.state.StateDescription,

                    CreatedDateTime = x.lesson.CreatedDateTime,
                    CreatedUserId = x.lesson.CreatedUserId,
                    CreatedUserFullName = x.person != null ? x.person.FullName : null,
                    UpdatedDateTime = x.lesson.UpdatedDateTime,
                    UpdatedUserId = x.lesson.UpdatedUserId,
                    Active = x.lesson.Active,
                    Images = new List<LessonImageDTO>()
                })
                .ToListAsync();

            var lessonIds = registros.Select(x => x.LessonId).ToList();

            var imagenes = await (
                from img in _context.LessonImages
                join imagetype in _context.ImageType on img.ImageTypeId equals imagetype.ImageTypeId
                where img.State == true && lessonIds.Contains(img.LessonId)
                select new LessonImageDTO
                {
                    LessonImageId = img.LessonImageId,
                    ImageUrl = img.ImageUrl,
                    LessonId = img.LessonId,
                    ImageTypeId = img.ImageTypeId,
                    ImageTypeDescription = imagetype.ImageTypeDescription
                }
            ).ToListAsync();

            var imagesByLesson = imagenes
                .GroupBy(i => i.LessonId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var lesson in registros)
            {
                if (imagesByLesson.TryGetValue(lesson.LessonId, out var imgs))
                    lesson.Images = imgs;
            }

            return registros;
        }

        public async Task<List<LessonPeriodDTO>> GetAllPeriodsFactory()
        {
            using var ctx = _factory.CreateDbContext();

            var registros = ctx.Lesson
                .Where(l => l.State)
                .Select(l => new LessonPeriodDTO
                {
                    PeriodDate = l.PeriodDate
                })
                .Distinct()
                .OrderByDescending(l => l.PeriodDate);

            return await registros.ToListAsync();
        }

        public async Task<PagedResult<LessonListDTO>> GetLessonsFilterPaged(
            DateTime? periodDate,
            int? stateId,
            int? projectId,
            int? areaId,
            int? phaseId,
            int? stageId,
            int? layerId,
            int? subStageId,
            int? subSpecialtyId,
            int? userId,
            int page,
            int pageSize
        )
        {
            var query = _context.Lesson
                .Where(x => x.Active)
                .AsQueryable();

            if (periodDate.HasValue) {
                periodDate = DateTime.SpecifyKind(periodDate.Value, DateTimeKind.Utc);
                query = query.Where(x => x.PeriodDate == periodDate);
            }

            if (stateId.HasValue)
                query = query.Where(x => x.StateId == stateId.Value);

            if (projectId.HasValue)
                query = query.Where(x => x.ProjectId == projectId.Value);

            if (areaId.HasValue)
                query = query.Where(x => x.AreaId == areaId.Value);

            if (userId.HasValue)
                query = query.Where(x => x.CreatedUserId == userId.Value);

            var result =
                from lesson in query
                join project in _context.Project on lesson.ProjectId equals project.ProjectId into pj
                from project in pj.DefaultIfEmpty()

                join area in _context.Area on lesson.AreaId equals area.AreaId
                join psss in _context.PhaseStageSubStageSubSpecialty
                    on lesson.PhaseStageSubStageSubSpecialtyId equals psss.PhaseStageSubStageSubSpecialtyId into ps
                from psss in ps.DefaultIfEmpty()

                join phase in _context.Phase on psss.PhaseId equals phase.PhaseId into ph
                from phase in ph.DefaultIfEmpty()

                join stage in _context.Stage on psss.StageId equals stage.StageId into st
                from stage in st.DefaultIfEmpty()

                join layer in _context.Layer on psss.LayerId equals layer.LayerId into ly
                from layer in ly.DefaultIfEmpty()

                join substage in _context.SubStage on psss.SubStageId equals substage.SubStageId into ss
                from substage in ss.DefaultIfEmpty()

                join subspecialty in _context.SubSpecialty
                    on psss.SubSpecialtyId equals subspecialty.SubSpecialtyId into sp
                from subspecialty in sp.DefaultIfEmpty()

                join user in _context.User
                    on lesson.CreatedUserId equals user.UserId into us
                from user in us.DefaultIfEmpty()

                join person in _context.Person
                    on user.PersonId equals person.PersonId into pe
                from person in pe.DefaultIfEmpty()

                join state in _context.State on lesson.StateId equals state.StateId
                select new
                {
                    lesson,
                    project,
                    area,
                    psss,
                    phase,
                    stage,
                    layer,
                    substage,
                    subspecialty,
                    state,
                    person
                };

            // Filtros secundarios
            if (phaseId.HasValue)
                result = result.Where(x => x.psss != null && x.psss.PhaseId == phaseId.Value);

            if (stageId.HasValue)
                result = result.Where(x => x.psss != null && x.psss.StageId == stageId.Value);

            if (layerId.HasValue)
                result = result.Where(x => x.psss != null && x.psss.LayerId == layerId.Value);

            if (subStageId.HasValue)
                result = result.Where(x => x.psss != null && x.psss.SubStageId == subStageId.Value);

            if (subSpecialtyId.HasValue)
                result = result.Where(x => x.psss != null && x.psss.SubSpecialtyId == subSpecialtyId.Value);

            var totalRecords = await result.CountAsync();

            var registros = await result
                .OrderByDescending(x => x.lesson.CreatedDateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new LessonListDTO
                {
                    LessonId = x.lesson.LessonId,
                    LessonCode = x.lesson.LessonCode,
                    Period = x.lesson.Period,
                    ProblemDescription = x.lesson.ProblemDescription,
                    ReasonDescription = x.lesson.ReasonDescription,
                    LessonDescription = x.lesson.LessonDescription,
                    ImpactDescription = x.lesson.ImpactDescription,

                    ProjectId = x.lesson.ProjectId,
                    ProjectDescription = x.project != null ? x.project.ProjectDescription : null,

                    AreaId = x.lesson.AreaId,
                    AreaDescription = x.area.AreaDescription,

                    PhaseStageSubStageSubSpecialtyId = x.lesson.PhaseStageSubStageSubSpecialtyId,

                    PhaseId = x.psss != null ? (int?)x.psss.PhaseId : null,
                    PhaseDescription = x.phase != null ? x.phase.PhaseDescription : null,

                    StageId = x.psss != null ? (int?)x.psss.StageId : null,
                    StageDescription = x.stage != null ? x.stage.StageDescription : null,

                    LayerId = x.psss != null ? (int?)x.psss.LayerId : null,
                    LayerDescription = x.layer != null ? x.layer.LayerDescription : null,

                    SubStageId = x.psss != null ? (int?)x.psss.SubStageId : null,
                    SubStageDescription = x.substage != null ? x.substage.SubStageDescription : null,

                    SubSpecialtyId = x.psss != null ? (int?)x.psss.SubSpecialtyId : null,
                    SubSpecialtyDescription = x.subspecialty != null ? x.subspecialty.SubSpecialtyDescription : null,

                    StateId = x.lesson.StateId,
                    StateDescription = x.state.StateDescription,

                    CreatedDateTime = x.lesson.CreatedDateTime,
                    CreatedUserId = x.lesson.CreatedUserId,
                    CreatedUserFullName = x.person != null ? x.person.FullName : null,
                    UpdatedDateTime = x.lesson.UpdatedDateTime,
                    UpdatedUserId = x.lesson.UpdatedUserId,
                    Active = x.lesson.Active,
                    Images = new List<LessonImageDTO>()
                })
                .ToListAsync();

            // 🔹 imágenes SOLO de los lessons paginados
            var lessonIds = registros.Select(x => x.LessonId).ToList();

            var imagenes = await (
                from img in _context.LessonImages
                join imagetype in _context.ImageType on img.ImageTypeId equals imagetype.ImageTypeId
                where img.State == true && lessonIds.Contains(img.LessonId)
                select new LessonImageDTO
                {
                    LessonImageId = img.LessonImageId,
                    ImageUrl = img.ImageUrl,
                    LessonId = img.LessonId,
                    ImageTypeId = img.ImageTypeId,
                    ImageTypeDescription = imagetype.ImageTypeDescription
                }
            ).ToListAsync();

            var imagesByLesson = imagenes
                .GroupBy(i => i.LessonId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var lesson in registros)
            {
                if (imagesByLesson.TryGetValue(lesson.LessonId, out var imgs))
                    lesson.Images = imgs;
            }

            return new PagedResult<LessonListDTO>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = registros
            };
        }

        public async Task<object> GetPaged(int page)
        {
            const int pageSize = 10;

            var query = from lesson in _context.Lesson
                        join project in _context.Project on lesson.ProjectId equals project.ProjectId
                        join area in _context.Area on lesson.AreaId equals area.AreaId
                        join psss in _context.PhaseStageSubStageSubSpecialty on lesson.PhaseStageSubStageSubSpecialtyId equals psss.PhaseStageSubStageSubSpecialtyId
                        join phase in _context.Phase on psss.PhaseId equals phase.PhaseId
                        join stage in _context.Stage on psss.StageId equals stage.StageId
                        join substage in _context.SubStage on psss.SubStageId equals substage.SubStageId
                        join state in _context.State on lesson.StateId equals state.StateId
                        where lesson.State == true
                        orderby lesson.LessonId descending
                        select new LessonListDTO
                        {
                            LessonId = lesson.LessonId,
                            LessonCode = lesson.LessonCode,
                            Period = lesson.Period,
                            ProblemDescription = lesson.ProblemDescription,
                            ReasonDescription = lesson.ReasonDescription,
                            LessonDescription = lesson.LessonDescription,
                            ImpactDescription = lesson.ImpactDescription,
                            ProjectId = lesson.ProjectId,
                            ProjectDescription = project.ProjectDescription,
                            AreaId = lesson.AreaId,
                            AreaDescription = area.AreaDescription,
                            PhaseStageSubStageSubSpecialtyId = lesson.PhaseStageSubStageSubSpecialtyId,
                            PhaseId = psss.PhaseId,
                            PhaseDescription = phase.PhaseDescription,
                            StageId = psss.StageId,
                            StageDescription = stage.StageDescription,
                            SubStageId = psss.SubStageId,
                            SubStageDescription = substage.SubStageDescription,
                            StateId = lesson.StateId,
                            StateDescription = state.StateDescription,
                            CreatedDateTime = lesson.CreatedDateTime,
                            CreatedUserId = lesson.CreatedUserId,
                            UpdatedDateTime = lesson.UpdatedDateTime,
                            UpdatedUserId = lesson.UpdatedUserId,
                            Active = lesson.Active
                        };

            var totalRecords = await query.CountAsync();

            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new
            {
                page,
                pageSize,
                totalRecords,
                totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                data
            };
        }

        /*
        public bool Update(UpdateLessonDTO dto) {
            var lesson = _context.Lesson.FirstOrDefault(x => x.LessonId == dto.LessonId && x.Active);

            if (lesson == null)
                return false;

            lesson.LessonCode = dto.LessonCode;
            lesson.ProblemDescription = dto.ProblemDescription;
            lesson.ReasonDescription = dto.ReasonDescription;
            lesson.LessonDescription = dto.LessonDescription;
            lesson.ImpactDescription = dto.ImpactDescription;
            lesson.ProjectId = dto.ProjectId;
            lesson.PhaseStageSubStageSubSpecialtyId = dto.PhaseStageSubStageSubSpecialtyId;
            var utcMinus5 = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            lesson.UpdatedDateTime = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                utcMinus5
            );

            if (dto.ProblemImages != null && dto.ProblemImages.Any())
            {
                foreach (var image in dto.ProblemImages)
                {
                }
            }

            if (dto.LessonImages != null && dto.LessonImages.Any())
            {
                foreach (var image in dto.LessonImages)
                {
                }
            }

            _context.SaveChanges();

            return true;
        }
        */
        public async Task<object> Create(LessonCreateDTO dto, int userId)
        {
            int? stageId = dto.StageId > 0 ? dto.StageId : null;
            int? layerId = dto.LayerId > 0 ? dto.LayerId : null;
            int? subStageId = dto.SubStageId > 0 ? dto.SubStageId : null;
            int? subSpecialtyId = dto.SubSpecialtyId > 0 ? dto.SubSpecialtyId : null;

            int? psssId = null;

            if (dto.PhaseId > 0)
            {
                var query = _context.PhaseStageSubStageSubSpecialty
                    .Where(x =>
                        x.Active && x.State &&
                        x.PhaseId == dto.PhaseId
                    );

                if (stageId.HasValue)
                    query = query.Where(x => x.StageId == stageId);
                else
                    query = query.Where(x => x.StageId == null);

                if (layerId.HasValue)
                    query = query.Where(x => x.LayerId == layerId);
                else
                    query = query.Where(x => x.LayerId == null);

                if (subStageId.HasValue)
                    query = query.Where(x => x.SubStageId == subStageId);
                else
                    query = query.Where(x => x.SubStageId == null);

                if (subSpecialtyId.HasValue)
                    query = query.Where(x => x.SubSpecialtyId == subSpecialtyId);
                else
                    query = query.Where(x => x.SubSpecialtyId == null);

                psssId = await query
                    .Select(x => (int?)x.PhaseStageSubStageSubSpecialtyId)
                    .FirstOrDefaultAsync();

                if (psssId == null)
                    return null;
            }

            var lesson = new Lesson
            {
                Period = DateTime.UtcNow.ToString("MM-yyyy"),
                PeriodDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                ProblemDescription = dto.ProblemDescription,
                ReasonDescription = dto.ReasonDescription,
                LessonDescription = dto.LessonDescription,
                ImpactDescription = dto.ImpactDescription,

                ProjectId = dto.ProjectId,
                AreaId = dto.AreaId,
                PhaseStageSubStageSubSpecialtyId = psssId,
                StateId = 2,

                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                UpdatedDateTime = null,
                Active = true,
                State = true
            };

            _context.Lesson.Add(lesson);
            await _context.SaveChangesAsync();

            // Guardar imágenes
            if (dto.OpportunityImages?.Any() == true)
            {
                await SaveImages(dto.OpportunityImages, lesson.LessonId, 1); // OPORTUNIDAD
            }

            if (dto.ImprovementImages?.Any() == true)
            {
                await SaveImages(dto.ImprovementImages, lesson.LessonId, 2); // MEJORA
            }

            return lesson.LessonId;
        }

        public async Task<bool> DeleteSoftAsync(int lessonId, int userId)
        {
            var lesson = await _context.Lesson
                .FirstOrDefaultAsync(u => u.LessonId == lessonId && u.State == true);

            if (lesson == null)
                return false;

            lesson.State = false;
            lesson.Active = false;
            lesson.UpdatedDateTime = DateTime.UtcNow;
            lesson.UpdatedUserId = userId;

            await _context.LessonImages
                .Where(x => x.LessonId == lessonId && x.State == true)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.State, false)
                    .SetProperty(x => x.Active, false)
                    .SetProperty(x => x.UpdatedDateTime, DateTime.UtcNow)
                    .SetProperty(x => x.UpdatedUserId, userId)
                );

            await _context.SaveChangesAsync();

            return true;
        }

        private async Task SaveImages(IEnumerable<IFormFile> files, int lessonId, int imageTypeId)
        {
            foreach (var file in files)
            {
                if (file.Length == 0)
                    continue;

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

                string fileUrl;

                using (var stream = file.OpenReadStream())
                {
                    fileUrl = await _fileStorageService.UploadFileAsync(stream, fileName);
                }

                var entity = new LessonImages
                {
                    ImageUrl = fileUrl,
                    LessonId = lessonId,
                    ImageTypeId = imageTypeId,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = 1,
                    UpdatedDateTime = null,
                    Active = true,
                    State = true
                };

                _context.LessonImages.Add(entity);
            }

            await _context.SaveChangesAsync();
        }
    }
}