using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Helpers;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Extensions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using System.Data;
using ProjectModel = Abril_Backend.Shared.Models.Project;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Infrastructure.Repositories
{
    public class LessonRepository : ILessonRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;

        public LessonRepository(
            IDbContextFactory<AppDbContext> factory,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver)
        {
            _factory = factory;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
        }

        public async Task<PagedResult<LessonListDTO>> GetLessonsFilterPaged(
            DateTimeOffset? periodDate,
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
            using var ctx = _factory.CreateDbContext();

            var query = ctx.Lesson
                .Where(x => x.Active)
                .AsQueryable();

            if (periodDate.HasValue)
                query = query.Where(x => x.PeriodDate == periodDate);

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
                join project in ctx.Project on lesson.ProjectId equals project.ProjectId into pj
                from project in pj.DefaultIfEmpty()

                join area in ctx.Area on lesson.AreaId equals area.AreaId into aj
                from area in aj.DefaultIfEmpty()

                join psss in ctx.PhaseStageSubStageSubSpecialty
                    on lesson.PhaseStageSubStageSubSpecialtyId equals psss.PhaseStageSubStageSubSpecialtyId into ps
                from psss in ps.DefaultIfEmpty()

                join phase in ctx.Phase on psss.PhaseId equals phase.PhaseId into ph
                from phase in ph.DefaultIfEmpty()

                join stage in ctx.Stage on psss.StageId equals stage.StageId into st
                from stage in st.DefaultIfEmpty()

                join layer in ctx.Layer on psss.LayerId equals layer.LayerId into ly
                from layer in ly.DefaultIfEmpty()

                join substage in ctx.SubStage on psss.SubStageId equals substage.SubStageId into ss
                from substage in ss.DefaultIfEmpty()

                join subspecialty in ctx.SubSpecialty
                    on psss.SubSpecialtyId equals subspecialty.SubSpecialtyId into sp
                from subspecialty in sp.DefaultIfEmpty()

                join partida in ctx.Partida
                    on psss.PartidaId equals partida.PartidaId into paj
                from partida in paj.DefaultIfEmpty()

                join user in ctx.User
                    on lesson.CreatedUserId equals user.UserId into us
                from user in us.DefaultIfEmpty()

                join person in ctx.Person
                    on user.UserId equals person.UserId into pe
                from person in pe.DefaultIfEmpty()

                join state in ctx.State on lesson.StateId equals state.StateId
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
                    partida,
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
                    AreaDescription = x.area != null ? x.area.AreaDescription : null,

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

                    PartidaId = x.psss != null ? (int?)x.psss.PartidaId : null,
                    PartidaDescription = x.partida != null ? x.partida.PartidaDescription : null,

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
                from img in ctx.LessonImages
                join imagetype in ctx.ImageType on img.ImageTypeId equals imagetype.ImageTypeId
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

        public async Task<LessonsPagedWithFiltersDTO> GetPagedWithFiltersAsync(LessonFilterDTO filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            int pageSize = filter.PageSize > 0 ? filter.PageSize : 10;
            int offset = (filter.Page - 1) * pageSize;

            using var ctx = _factory.CreateDbContext();
            var connection = ctx.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            string tLesson = ctx.Table<Lesson>();
            string cLessonId = ctx.Col<Lesson>(nameof(Lesson.LessonId));
            string cLessonCode = ctx.Col<Lesson>(nameof(Lesson.LessonCode));
            string cLessonPeriod = ctx.Col<Lesson>(nameof(Lesson.Period));
            string cLessonPeriodDate = ctx.Col<Lesson>(nameof(Lesson.PeriodDate));
            string cLessonProblemDesc = ctx.Col<Lesson>(nameof(Lesson.ProblemDescription));
            string cLessonReasonDesc = ctx.Col<Lesson>(nameof(Lesson.ReasonDescription));
            string cLessonDesc = ctx.Col<Lesson>(nameof(Lesson.LessonDescription));
            string cLessonImpactDesc = ctx.Col<Lesson>(nameof(Lesson.ImpactDescription));
            string cLessonProjectId = ctx.Col<Lesson>(nameof(Lesson.ProjectId));
            string cLessonAreaId = ctx.Col<Lesson>(nameof(Lesson.AreaId));
            string cLessonLessonAreaId = ctx.Col<Lesson>(nameof(Lesson.LessonAreaId));
            string cLessonCatalogItemId = ctx.Col<Lesson>(nameof(Lesson.CatalogItemId));
            string cLessonPsssId = ctx.Col<Lesson>(nameof(Lesson.PhaseStageSubStageSubSpecialtyId));
            string cLessonStateId = ctx.Col<Lesson>(nameof(Lesson.StateId));
            string cLessonCreatedDateTime = ctx.Col<Lesson>(nameof(Lesson.CreatedDateTime));
            string cLessonCreatedUserId = ctx.Col<Lesson>(nameof(Lesson.CreatedUserId));
            string cLessonUpdatedDateTime = ctx.Col<Lesson>(nameof(Lesson.UpdatedDateTime));
            string cLessonUpdatedUserId = ctx.Col<Lesson>(nameof(Lesson.UpdatedUserId));
            string cLessonActive = ctx.Col<Lesson>(nameof(Lesson.Active));
            string cLessonState = ctx.Col<Lesson>(nameof(Lesson.State));

            string tProject = ctx.Table<ProjectModel>();
            string cProjectId = ctx.Col<ProjectModel>(nameof(ProjectModel.ProjectId));
            string cProjectDesc = ctx.Col<ProjectModel>(nameof(ProjectModel.ProjectDescription));
            string cProjectActive = ctx.Col<ProjectModel>(nameof(ProjectModel.Active));
            string cProjectState = ctx.Col<ProjectModel>(nameof(ProjectModel.State));

            string tArea = ctx.Table<Area>();
            string cAreaId = ctx.Col<Area>(nameof(Area.AreaId));
            string cAreaDesc = ctx.Col<Area>(nameof(Area.AreaDescription));
            string cAreaState = ctx.Col<Area>(nameof(Area.State));

            string tPsss = ctx.Table<PhaseStageSubStageSubSpecialty>();
            string cPsssId = ctx.Col<PhaseStageSubStageSubSpecialty>(nameof(PhaseStageSubStageSubSpecialty.PhaseStageSubStageSubSpecialtyId));
            string cPsssPhaseId = ctx.Col<PhaseStageSubStageSubSpecialty>(nameof(PhaseStageSubStageSubSpecialty.PhaseId));
            string cPsssStageId = ctx.Col<PhaseStageSubStageSubSpecialty>(nameof(PhaseStageSubStageSubSpecialty.StageId));
            string cPsssLayerId = ctx.Col<PhaseStageSubStageSubSpecialty>(nameof(PhaseStageSubStageSubSpecialty.LayerId));
            string cPsssSubStageId = ctx.Col<PhaseStageSubStageSubSpecialty>(nameof(PhaseStageSubStageSubSpecialty.SubStageId));
            string cPsssSubSpecialtyId = ctx.Col<PhaseStageSubStageSubSpecialty>(nameof(PhaseStageSubStageSubSpecialty.SubSpecialtyId));
            string cPsssPartidaId = ctx.Col<PhaseStageSubStageSubSpecialty>(nameof(PhaseStageSubStageSubSpecialty.PartidaId));

            string tPartida = ctx.Table<Partida>();
            string cPartidaId = ctx.Col<Partida>(nameof(Partida.PartidaId));
            string cPartidaDesc = ctx.Col<Partida>(nameof(Partida.PartidaDescription));

            string tPhase = ctx.Table<Phase>();
            string cPhaseId = ctx.Col<Phase>(nameof(Phase.PhaseId));
            string cPhaseDesc = ctx.Col<Phase>(nameof(Phase.PhaseDescription));
            string cPhaseOrder = ctx.Col<Phase>(nameof(Phase.Order));
            string cPhaseState = ctx.Col<Phase>(nameof(Phase.State));

            string tStage = ctx.Table<Stage>();
            string cStageId = ctx.Col<Stage>(nameof(Stage.StageId));
            string cStageDesc = ctx.Col<Stage>(nameof(Stage.StageDescription));

            string tLayer = ctx.Table<Layer>();
            string cLayerId = ctx.Col<Layer>(nameof(Layer.LayerId));
            string cLayerDesc = ctx.Col<Layer>(nameof(Layer.LayerDescription));

            string tSubStage = ctx.Table<SubStage>();
            string cSubStageId = ctx.Col<SubStage>(nameof(SubStage.SubStageId));
            string cSubStageDesc = ctx.Col<SubStage>(nameof(SubStage.SubStageDescription));

            string tSubSpec = ctx.Table<SubSpecialty>();
            string cSubSpecId = ctx.Col<SubSpecialty>(nameof(SubSpecialty.SubSpecialtyId));
            string cSubSpecDesc = ctx.Col<SubSpecialty>(nameof(SubSpecialty.SubSpecialtyDescription));
            string cSubSpecState = ctx.Col<SubSpecialty>(nameof(SubSpecialty.State));

            string tStateLk = ctx.Table<State>();
            string cStateLkId = ctx.Col<State>(nameof(State.StateId));
            string cStateLkDesc = ctx.Col<State>(nameof(State.StateDescription));

            string tUser = ctx.Table<User>();
            string cUserId = ctx.Col<User>(nameof(User.UserId));
            string cUserActive = ctx.Col<User>(nameof(User.Active));

            string tPerson = ctx.Table<Person>();
            string cPersonUserId = ctx.Col<Person>(nameof(Person.UserId));
            string cPersonFullName = ctx.Col<Person>(nameof(Person.FullName));

            string tLessonImages = ctx.Table<LessonImages>();
            string cLiId = ctx.Col<LessonImages>(nameof(LessonImages.LessonImageId));
            string cLiUrl = ctx.Col<LessonImages>(nameof(LessonImages.ImageUrl));
            string cLiLessonId = ctx.Col<LessonImages>(nameof(LessonImages.LessonId));
            string cLiImageTypeId = ctx.Col<LessonImages>(nameof(LessonImages.ImageTypeId));
            string cLiState = ctx.Col<LessonImages>(nameof(LessonImages.State));

            string tImageType = ctx.Table<ImageType>();
            string cItId = ctx.Col<ImageType>(nameof(ImageType.ImageTypeId));
            string cItDesc = ctx.Col<ImageType>(nameof(ImageType.ImageTypeDescription));

            var parameters = new DynamicParameters();
            parameters.Add("@PageSize", pageSize);
            parameters.Add("@PageOffset", offset);

            var whereConditions = new List<string> { $"l.{cLessonActive} = TRUE" };

            if (filter.PeriodDate.HasValue)
            {
                whereConditions.Add($"l.{cLessonPeriodDate} = @PeriodDate");
                parameters.Add("@PeriodDate", filter.PeriodDate.Value);
            }

            if (filter.StateId.HasValue)
            {
                whereConditions.Add($"l.{cLessonStateId} = @StateId");
                parameters.Add("@StateId", filter.StateId.Value);
            }

            if (filter.ProjectId.HasValue)
            {
                whereConditions.Add($"l.{cLessonProjectId} = @ProjectId");
                parameters.Add("@ProjectId", filter.ProjectId.Value);
            }

            if (filter.AreaId.HasValue)
            {
                whereConditions.Add($"l.{cLessonAreaId} = @AreaId");
                parameters.Add("@AreaId", filter.AreaId.Value);
            }

            if (filter.UserId.HasValue)
            {
                whereConditions.Add($"l.{cLessonCreatedUserId} = @UserId");
                parameters.Add("@UserId", filter.UserId.Value);
            }

            if (filter.PhaseId.HasValue)
            {
                whereConditions.Add($"psss.{cPsssPhaseId} = @PhaseId");
                parameters.Add("@PhaseId", filter.PhaseId.Value);
            }

            if (filter.StageId.HasValue)
            {
                whereConditions.Add($"psss.{cPsssStageId} = @StageId");
                parameters.Add("@StageId", filter.StageId.Value);
            }

            if (filter.LayerId.HasValue)
            {
                whereConditions.Add($"psss.{cPsssLayerId} = @LayerId");
                parameters.Add("@LayerId", filter.LayerId.Value);
            }

            if (filter.SubStageId.HasValue)
            {
                whereConditions.Add($"psss.{cPsssSubStageId} = @SubStageId");
                parameters.Add("@SubStageId", filter.SubStageId.Value);
            }

            if (filter.SubSpecialtyId.HasValue)
            {
                whereConditions.Add($"psss.{cPsssSubSpecialtyId} = @SubSpecialtyId");
                parameters.Add("@SubSpecialtyId", filter.SubSpecialtyId.Value);
            }

            string whereClause = string.Join(" AND ", whereConditions);

            string baseFromJoins = $@"
FROM {tLesson} l
LEFT JOIN {tProject} p ON p.{cProjectId} = l.{cLessonProjectId}
LEFT JOIN {tArea} a ON a.{cAreaId} = l.{cLessonAreaId}
LEFT JOIN {tPsss} psss ON psss.{cPsssId} = l.{cLessonPsssId}
LEFT JOIN {tPhase} ph ON ph.{cPhaseId} = psss.{cPsssPhaseId}
LEFT JOIN {tStage} st ON st.{cStageId} = psss.{cPsssStageId}
LEFT JOIN {tLayer} ly ON ly.{cLayerId} = psss.{cPsssLayerId}
LEFT JOIN {tSubStage} ss ON ss.{cSubStageId} = psss.{cPsssSubStageId}
LEFT JOIN {tSubSpec} sp ON sp.{cSubSpecId} = psss.{cPsssSubSpecialtyId}
LEFT JOIN {tPartida} pa ON pa.{cPartidaId} = psss.{cPsssPartidaId}
LEFT JOIN {tUser} u ON u.{cUserId} = l.{cLessonCreatedUserId}
LEFT JOIN {tPerson} pe ON pe.{cPersonUserId} = u.{cUserId}
JOIN {tStateLk} s ON s.{cStateLkId} = l.{cLessonStateId}
WHERE {whereClause}";

            string sql = $@"
-- 1. Count
SELECT COUNT(*) AS ""Total"" {baseFromJoins};

-- 2. Paged data (alias PascalCase para que Dapper mapee a LessonListDTO)
SELECT l.{cLessonId} AS ""LessonId"",
       l.{cLessonCode} AS ""LessonCode"",
       l.{cLessonPeriod} AS ""Period"",
       l.{cLessonProblemDesc} AS ""ProblemDescription"",
       l.{cLessonReasonDesc} AS ""ReasonDescription"",
       l.{cLessonDesc} AS ""LessonDescription"",
       l.{cLessonImpactDesc} AS ""ImpactDescription"",
       l.{cLessonProjectId} AS ""ProjectId"",
       p.{cProjectDesc} AS ""ProjectDescription"",
       l.{cLessonAreaId} AS ""AreaId"",
       a.{cAreaDesc} AS ""AreaDescription"",
       l.{cLessonLessonAreaId} AS ""LessonAreaId"",
       l.{cLessonCatalogItemId} AS ""CatalogItemId"",
       l.{cLessonPsssId} AS ""PhaseStageSubStageSubSpecialtyId"",
       psss.{cPsssPhaseId} AS ""PhaseId"",
       ph.{cPhaseDesc} AS ""PhaseDescription"",
       psss.{cPsssStageId} AS ""StageId"",
       st.{cStageDesc} AS ""StageDescription"",
       psss.{cPsssLayerId} AS ""LayerId"",
       ly.{cLayerDesc} AS ""LayerDescription"",
       psss.{cPsssSubStageId} AS ""SubStageId"",
       ss.{cSubStageDesc} AS ""SubStageDescription"",
       psss.{cPsssSubSpecialtyId} AS ""SubSpecialtyId"",
       sp.{cSubSpecDesc} AS ""SubSpecialtyDescription"",
       psss.{cPsssPartidaId} AS ""PartidaId"",
       pa.{cPartidaDesc} AS ""PartidaDescription"",
       l.{cLessonStateId} AS ""StateId"",
       s.{cStateLkDesc} AS ""StateDescription"",
       l.{cLessonCreatedDateTime} AS ""CreatedDateTime"",
       l.{cLessonCreatedUserId} AS ""CreatedUserId"",
       pe.{cPersonFullName} AS ""CreatedUserFullName"",
       l.{cLessonUpdatedDateTime} AS ""UpdatedDateTime"",
       l.{cLessonUpdatedUserId} AS ""UpdatedUserId"",
       l.{cLessonActive} AS ""Active""
{baseFromJoins}
ORDER BY l.{cLessonCreatedDateTime} DESC
LIMIT @PageSize OFFSET @PageOffset;

-- 3. Lesson images SOLO para los lessons paginados (subquery duplicada con la misma paginacion)
SELECT li.{cLiId} AS ""LessonImageId"",
       li.{cLiUrl} AS ""ImageUrl"",
       li.{cLiLessonId} AS ""LessonId"",
       li.{cLiImageTypeId} AS ""ImageTypeId"",
       it.{cItDesc} AS ""ImageTypeDescription""
FROM {tLessonImages} li
JOIN {tImageType} it ON it.{cItId} = li.{cLiImageTypeId}
WHERE li.{cLiState} = TRUE
  AND li.{cLiLessonId} IN (
    SELECT l.{cLessonId} {baseFromJoins}
    ORDER BY l.{cLessonCreatedDateTime} DESC
    LIMIT @PageSize OFFSET @PageOffset
  );

-- 4. Filters: areas (state = true)
SELECT {cAreaId} AS ""AreaId"", {cAreaDesc} AS ""AreaDescription""
FROM {tArea} WHERE {cAreaState} = TRUE ORDER BY {cAreaDesc};

-- 5. Filters: projects (active = true AND state = true)
SELECT {cProjectId} AS ""ProjectId"", {cProjectDesc} AS ""ProjectDescription""
FROM {tProject} WHERE {cProjectActive} = TRUE AND {cProjectState} = TRUE ORDER BY {cProjectDesc};

-- 6. Filters: periods (distinct desde lesson)
SELECT DISTINCT {cLessonPeriodDate} AS ""PeriodDate""
FROM {tLesson} WHERE {cLessonState} = TRUE ORDER BY 1 DESC;

-- 7. Filters: phases (state = true, ordenado por order)
SELECT {cPhaseId} AS ""PhaseId"", {cPhaseDesc} AS ""PhaseDescription""
FROM {tPhase} WHERE {cPhaseState} = TRUE ORDER BY {cPhaseOrder};

-- 8. Filters: stages (sin filtro)
SELECT {cStageId} AS ""StageId"", {cStageDesc} AS ""StageDescription""
FROM {tStage} ORDER BY {cStageDesc};

-- 9. Filters: layers (sin filtro)
SELECT {cLayerId} AS ""LayerId"", {cLayerDesc} AS ""LayerDescription""
FROM {tLayer} ORDER BY {cLayerDesc};

-- 10. Filters: substages (sin filtro)
SELECT {cSubStageId} AS ""SubStageId"", {cSubStageDesc} AS ""SubStageDescription""
FROM {tSubStage} ORDER BY {cSubStageDesc};

-- 11. Filters: subspecialties (state = true)
SELECT {cSubSpecId} AS ""SubSpecialtyId"", {cSubSpecDesc} AS ""SubSpecialtyDescription""
FROM {tSubSpec} WHERE {cSubSpecState} = TRUE ORDER BY {cSubSpecDesc};

-- 12. Filters: users con join a person (active = true)
SELECT u.{cUserId} AS ""UserId"", pe.{cPersonFullName} AS ""FullName""
FROM {tUser} u
JOIN {tPerson} pe ON pe.{cPersonUserId} = u.{cUserId}
WHERE u.{cUserActive} = TRUE
ORDER BY pe.{cPersonFullName};
";

            using var multi = await connection.QueryMultipleAsync(sql, parameters);

            // 1. Count
            var countResult = await multi.ReadFirstOrDefaultAsync<dynamic>();
            int totalRecords = countResult == null ? 0 : Convert.ToInt32(countResult.Total);

            // 2. Paged data
            var lessons = (await multi.ReadAsync<LessonListDTO>()).ToList();

            // 3. Images
            var images = (await multi.ReadAsync<LessonImageDTO>()).ToList();

            // 4-12. Filters
            var areas = (await multi.ReadAsync<AreaSimpleDTO>()).ToList();
            var projects = (await multi.ReadAsync<ProjectSimpleDTO>()).ToList();
            var periods = (await multi.ReadAsync<LessonPeriodDTO>()).ToList();
            var phases = (await multi.ReadAsync<PhaseSimpleDTO>()).ToList();
            var stages = (await multi.ReadAsync<StageSimpleDTO>()).ToList();
            var layers = (await multi.ReadAsync<LayerSimpleDTO>()).ToList();
            var subStages = (await multi.ReadAsync<SubStageSimpleDTO>()).ToList();
            var subSpecialties = (await multi.ReadAsync<SubSpecialtySimpleDTO>()).ToList();
            var users = (await multi.ReadAsync<UserFilterDTO>()).ToList();

            var imagesByLesson = images.GroupBy(i => i.LessonId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var lesson in lessons)
            {
                lesson.Images = imagesByLesson.TryGetValue(lesson.LessonId, out var imgs)
                    ? imgs
                    : new List<LessonImageDTO>();
            }

            // Enriquecer con área (lesson_area → area_scope → area_item) y
            // clasificación (scope_item walk-up por catalog_type) del nuevo modelo.
            // Sobrescribe los campos legacy (AreaDescription, PhaseDescription, etc.).
            var enrichments = await LessonEnrichmentHelper.ComputeAsync(
                ctx,
                lessons.Select(l => (l.LessonId, l.LessonAreaId, l.CatalogItemId)).ToList()
            );
            foreach (var lesson in lessons)
            {
                if (!enrichments.TryGetValue(lesson.LessonId, out var e)) continue;
                if (e.AreaDescription != null)         lesson.AreaDescription = e.AreaDescription;
                if (e.PhaseDescription != null)        lesson.PhaseDescription = e.PhaseDescription;
                if (e.StageDescription != null)        lesson.StageDescription = e.StageDescription;
                if (e.LayerDescription != null)        lesson.LayerDescription = e.LayerDescription;
                if (e.SubStageDescription != null)     lesson.SubStageDescription = e.SubStageDescription;
                if (e.SubSpecialtyDescription != null) lesson.SubSpecialtyDescription = e.SubSpecialtyDescription;
                if (e.PartidaDescription != null)      lesson.PartidaDescription = e.PartidaDescription;
            }

            int totalPages = pageSize == 0 ? 0 : (totalRecords + pageSize - 1) / pageSize;

            return new LessonsPagedWithFiltersDTO
            {
                Paged = new PagedResult<LessonListDTO>
                {
                    Page = filter.Page,
                    PageSize = pageSize,
                    TotalRecords = totalRecords,
                    TotalPages = totalPages,
                    Data = lessons
                },
                Filters = new LessonFiltersFormDataDTO
                {
                    Areas = areas,
                    Projects = projects,
                    Periods = periods,
                    Phases = phases,
                    Stages = stages,
                    Layers = layers,
                    SubStages = subStages,
                    SubSpecialties = subSpecialties,
                    Users = users
                }
            };
        }

        public async Task<object?> CreateAsync(LessonCreateDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            int? psssId = null;
            int? catalogItemId = dto.CatalogItemId > 0 ? dto.CatalogItemId : null;

            // Flujo nuevo: CatalogItemId enviado directamente desde el árbol de scope
            if (catalogItemId.HasValue)
            {
                var itemExists = await ctx.CatalogItem.AnyAsync(c => c.CatalogItemId == catalogItemId.Value && c.Active);
                if (!itemExists)
                    return null;
            }
            // Flujo legacy: buscar PSSS por fase/etapa/etc (lecciones antiguas)
            else if (dto.PhaseId > 0)
            {
                int? stageId = dto.StageId > 0 ? dto.StageId : null;
                int? layerId = dto.LayerId > 0 ? dto.LayerId : null;
                int? subStageId = dto.SubStageId > 0 ? dto.SubStageId : null;
                int? subSpecialtyId = dto.SubSpecialtyId > 0 ? dto.SubSpecialtyId : null;
                int? partidaId = dto.PartidaId > 0 ? dto.PartidaId : null;

                var query = ctx.PhaseStageSubStageSubSpecialty
                    .Where(x => x.Active && x.State && x.PhaseId == dto.PhaseId);

                if (stageId.HasValue) query = query.Where(x => x.StageId == stageId);
                else query = query.Where(x => x.StageId == null);

                if (layerId.HasValue) query = query.Where(x => x.LayerId == layerId);
                else query = query.Where(x => x.LayerId == null);

                if (subStageId.HasValue) query = query.Where(x => x.SubStageId == subStageId);
                else query = query.Where(x => x.SubStageId == null);

                if (subSpecialtyId.HasValue) query = query.Where(x => x.SubSpecialtyId == subSpecialtyId);
                else query = query.Where(x => x.SubSpecialtyId == null);

                if (partidaId.HasValue) query = query.Where(x => x.PartidaId == partidaId);
                else query = query.Where(x => x.PartidaId == null);

                psssId = await query
                    .Select(x => (int?)x.PhaseStageSubStageSubSpecialtyId)
                    .FirstOrDefaultAsync();

                if (psssId == null)
                    return null;
            }

            var now = DateTimeOffset.UtcNow;
            var lesson = new Lesson
            {
                Period = now.ToString("MM-yyyy"),
                PeriodDate = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero),
                ProblemDescription = dto.ProblemDescription,
                ReasonDescription = dto.ReasonDescription,
                LessonDescription = dto.LessonDescription,
                ImpactDescription = dto.ImpactDescription,
                ProjectId = dto.ProjectId,
                AreaId = dto.AreaId,
                PhaseStageSubStageSubSpecialtyId = psssId,
                CatalogItemId = catalogItemId,
                LessonAreaId = dto.LessonAreaId,
                StateId = 2,
                CreatedDateTime = now,
                CreatedUserId = userId,
                UpdatedDateTime = null,
                Active = true,
                State = true
            };

            ctx.Lesson.Add(lesson);
            await ctx.SaveChangesAsync();

            if (dto.OpportunityImages?.Any() == true)
                await SaveImagesAsync(dto.OpportunityImages, lesson.LessonId, 1, ctx);

            if (dto.ImprovementImages?.Any() == true)
                await SaveImagesAsync(dto.ImprovementImages, lesson.LessonId, 2, ctx);

            return lesson.LessonId;
        }

        public async Task<List<PhaseStageSubStageSubSpecialtyDTO>> GetFiltersForCreateAsync(int areaId, int? subAreaId)
        {
            using var ctx = _factory.CreateDbContext();

            var scopedPsssIds = subAreaId.HasValue
                ? await ctx.PsssScope
                    .Where(sc => sc.State && sc.SubAreaId == subAreaId.Value)
                    .Select(sc => sc.PhaseStageSubStageSubSpecialtyId)
                    .ToListAsync()
                : await ctx.PsssScope
                    .Where(sc => sc.State && sc.AreaId == areaId)
                    .Select(sc => sc.PhaseStageSubStageSubSpecialtyId)
                    .ToListAsync();

            if (!scopedPsssIds.Any())
                return new List<PhaseStageSubStageSubSpecialtyDTO>();

            var data = await (
                from link in ctx.PhaseStageSubStageSubSpecialty

                join p in ctx.Phase on link.PhaseId equals p.PhaseId

                join s in ctx.Stage on link.StageId equals s.StageId into sj
                from s in sj.DefaultIfEmpty()

                join l in ctx.Layer on link.LayerId equals l.LayerId into lj
                from l in lj.DefaultIfEmpty()

                join ss in ctx.SubStage on link.SubStageId equals ss.SubStageId into ssj
                from ss in ssj.DefaultIfEmpty()

                join sp in ctx.SubSpecialty on link.SubSpecialtyId equals sp.SubSpecialtyId into spj
                from sp in spj.DefaultIfEmpty()

                join pa in ctx.Partida on link.PartidaId equals pa.PartidaId into paj
                from pa in paj.DefaultIfEmpty()

                where link.Active && link.State
                      && scopedPsssIds.Contains(link.PhaseStageSubStageSubSpecialtyId)
                      && p.Active && p.State
                      && (s == null || (s.Active && s.State))
                      && (l == null || (l.Active && l.State))
                      && (ss == null || (ss.Active && ss.State))
                      && (sp == null || (sp.Active && sp.State))
                      && (pa == null || (pa.Active && pa.State))

                select new
                {
                    link.PhaseStageSubStageSubSpecialtyId,
                    p.PhaseId,
                    p.PhaseDescription,
                    PhaseOrder = p.Order,
                    StageId = (int?)s.StageId,
                    StageDescription = s != null ? s.StageDescription : null,
                    LayerId = (int?)l.LayerId,
                    LayerDescription = l != null ? l.LayerDescription : null,
                    SubStageId = (int?)ss.SubStageId,
                    SubStageDescription = ss != null ? ss.SubStageDescription : null,
                    SubSpecialtyId = (int?)sp.SubSpecialtyId,
                    SubSpecialtyDescription = sp != null ? sp.SubSpecialtyDescription : null,
                    PartidaId = (int?)pa.PartidaId,
                    PartidaDescription = pa != null ? pa.PartidaDescription : null
                }
            )
            .OrderBy(x => x.PhaseOrder ?? int.MaxValue)
            .ThenBy(x => x.PhaseDescription)
            .ThenBy(x => x.StageDescription)
            .ThenBy(x => x.LayerDescription)
            .ThenBy(x => x.SubStageDescription)
            .ThenBy(x => x.SubSpecialtyDescription)
            .ThenBy(x => x.PartidaDescription)
            .ToListAsync();

            var result = data
                .GroupBy(x => new { x.PhaseId, x.PhaseDescription, x.PhaseOrder })
                .OrderBy(p => p.Key.PhaseOrder ?? int.MaxValue)
                .Select(p => new PhaseStageSubStageSubSpecialtyDTO
                {
                    PhaseId = p.Key.PhaseId,
                    PhaseDescription = p.Key.PhaseDescription,
                    LinkId = p.Where(x => x.StageId == null)
                               .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                               .FirstOrDefault(),
                    Stages = p
                        .Where(x => x.StageId != null)
                        .GroupBy(x => new { x.StageId, x.StageDescription })
                        .Select(s => new StageFilterDTO
                        {
                            StageId = s.Key.StageId!.Value,
                            StageDescription = s.Key.StageDescription!,
                            LinkId = s.Where(x => x.LayerId == null && x.SubStageId == null && x.PartidaId == null)
                                      .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                                      .FirstOrDefault(),
                            Partidas = s
                                .Where(x => x.LayerId == null && x.SubStageId == null && x.PartidaId != null)
                                .GroupBy(x => new { x.PartidaId, x.PartidaDescription })
                                .Select(pa => new PartidaFilterDTO
                                {
                                    PartidaId = pa.Key.PartidaId!.Value,
                                    PartidaDescription = pa.Key.PartidaDescription!,
                                    LinkId = pa.Select(x => x.PhaseStageSubStageSubSpecialtyId).First()
                                })
                                .ToList(),
                            SubStages = s
                                .Where(x => x.LayerId == null && x.SubStageId != null)
                                .GroupBy(x => new { x.SubStageId, x.SubStageDescription })
                                .Select(ss => new SubStageFilterDTO
                                {
                                    SubStageId = ss.Key.SubStageId!.Value,
                                    SubStageDescription = ss.Key.SubStageDescription!,
                                    LinkId = ss.Where(x => x.SubSpecialtyId == null)
                                               .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                                               .FirstOrDefault(),
                                    SubSpecialties = ss
                                        .Where(x => x.SubSpecialtyId != null)
                                        .GroupBy(x => new { x.SubSpecialtyId, x.SubSpecialtyDescription })
                                        .Select(sp => new SubSpecialtyFilterDTO
                                        {
                                            SubSpecialtyId = sp.Key.SubSpecialtyId!.Value,
                                            SubSpecialtyDescription = sp.Key.SubSpecialtyDescription!,
                                            LinkId = sp.Select(x => x.PhaseStageSubStageSubSpecialtyId).First()
                                        })
                                        .ToList()
                                })
                                .ToList(),
                            Layers = s
                                .Where(x => x.LayerId != null)
                                .GroupBy(x => new { x.LayerId, x.LayerDescription })
                                .Select(l => new LayerFilterDTO
                                {
                                    LayerId = l.Key.LayerId!.Value,
                                    LayerDescription = l.Key.LayerDescription!,
                                    LinkId = l.Where(x => x.SubStageId == null)
                                               .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                                               .FirstOrDefault(),
                                    SubStages = l
                                        .Where(x => x.SubStageId != null)
                                        .GroupBy(x => new { x.SubStageId, x.SubStageDescription })
                                        .Select(ss => new SubStageFilterDTO
                                        {
                                            SubStageId = ss.Key.SubStageId!.Value,
                                            SubStageDescription = ss.Key.SubStageDescription!,
                                            LinkId = ss.Where(x => x.SubSpecialtyId == null)
                                                       .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                                                       .FirstOrDefault(),
                                            SubSpecialties = ss
                                                .Where(x => x.SubSpecialtyId != null)
                                                .GroupBy(x => new { x.SubSpecialtyId, x.SubSpecialtyDescription })
                                                .Select(sp => new SubSpecialtyFilterDTO
                                                {
                                                    SubSpecialtyId = sp.Key.SubSpecialtyId!.Value,
                                                    SubSpecialtyDescription = sp.Key.SubSpecialtyDescription!,
                                                    LinkId = sp.Select(x => x.PhaseStageSubStageSubSpecialtyId).First()
                                                })
                                                .ToList()
                                        })
                                        .ToList()
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .ToList();

            return result;
        }

        private async Task SaveImagesAsync(
            IEnumerable<IFormFile> files,
            int lessonId,
            int imageTypeId,
            AppDbContext ctx)
        {
            var container = _containerResolver.GetLessonsContainerName();
            var filesToUpload = new List<(Stream Stream, string FileName)>();

            foreach (var file in files)
            {
                if (file.Length == 0) continue;
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                filesToUpload.Add((file.OpenReadStream(), fileName));
            }

            if (!filesToUpload.Any()) return;

            List<string> uploadedUrls;
            try
            {
                uploadedUrls = await _fileStorageService.UploadFilesAsync(filesToUpload, container);
            }
            finally
            {
                foreach (var f in filesToUpload) f.Stream.Dispose();
            }

            for (int i = 0; i < uploadedUrls.Count; i++)
            {
                ctx.LessonImages.Add(new LessonImages
                {
                    ImageUrl = uploadedUrls[i],
                    LessonId = lessonId,
                    ImageTypeId = imageTypeId,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    CreatedUserId = 1,
                    UpdatedDateTime = null,
                    Active = true,
                    State = true
                });
            }

            await ctx.SaveChangesAsync();
        }
    }
}
