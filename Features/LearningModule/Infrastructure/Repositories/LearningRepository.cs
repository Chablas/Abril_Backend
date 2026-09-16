using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.LearningModule.Application.Dtos;
using Abril_Backend.Features.LearningModule.Infrastructure.Interfaces;
using Abril_Backend.Features.LearningModule.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.LearningModule.Infrastructure.Repositories
{
    public class LearningRepository : ILearningRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public LearningRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // ─────────────────────────────── Display ───────────────────────────────

        public async Task<List<LearningCategoryDto>> GetLoginCategories()
        {
            using var ctx = _factory.CreateDbContext();

            // /auth/login es público: se muestran todas las categorías de superficie LOGIN
            // activas, sin filtrar por rol (no hay sesión). Los videos de contratistas caen aquí.
            return await ctx.LearningCategory
                .Where(c => c.State && c.Active && c.Surface!.Code == "LOGIN")
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new LearningCategoryDto
                {
                    Id = c.LearningCategoryId,
                    Nombre = c.Name,
                    Videos = c.Videos
                        .Where(v => v.State && v.Active)
                        .OrderBy(v => v.DisplayOrder).ThenBy(v => v.LearningVideoId)
                        .Select(v => new LearningVideoDto { Titulo = v.Title, Url = v.Url, Img = v.ThumbnailUrl })
                        .ToList(),
                })
                .ToListAsync();
        }

        public async Task<List<LearningCategoryDto>> GetInicioCategories(int[] roleIds)
        {
            using var ctx = _factory.CreateDbContext();

            // /inicio requiere sesión: una categoría es visible si es "pública interna"
            // (todo Abril) o si el usuario tiene alguno de sus roles autorizados.
            // Se muestran todos los grupos visibles aunque no tengan videos ni manuales
            // (los encabezados vacíos son intencionales, igual que en la superficie LOGIN).
            return await ctx.LearningCategory
                .Where(c => c.State && c.Active && c.Surface!.Code == "INICIO"
                    && (c.EsPublicoInterno || c.Roles.Any(r => roleIds.Contains(r.RoleId))))
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new LearningCategoryDto
                {
                    Id = c.LearningCategoryId,
                    Nombre = c.Name,
                    Videos = c.Videos
                        .Where(v => v.State && v.Active)
                        .OrderBy(v => v.DisplayOrder).ThenBy(v => v.LearningVideoId)
                        .Select(v => new LearningVideoDto { Titulo = v.Title, Url = v.Url, Img = v.ThumbnailUrl })
                        .ToList(),
                })
                .ToListAsync();
        }

        // ─────────────────────────────── Admin ───────────────────────────────

        public async Task<LearningAdminDataDto> GetAdminData()
        {
            using var ctx = _factory.CreateDbContext();

            var categorias = await ctx.LearningCategory
                .Where(c => c.State)
                .OrderBy(c => c.Surface!.Code).ThenBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new LearningCategoryAdminDto
                {
                    Id = c.LearningCategoryId,
                    Nombre = c.Name,
                    Orden = c.DisplayOrder,
                    SurfaceId = c.LearningSurfaceId,
                    SurfaceCode = c.Surface!.Code,
                    SurfaceNombre = c.Surface!.Name,
                    EsPublicoInterno = c.EsPublicoInterno,
                    Activo = c.Active,
                    RoleIds = c.Roles.Select(r => r.RoleId).ToList(),
                    Videos = c.Videos
                        .Where(v => v.State)
                        .OrderBy(v => v.DisplayOrder).ThenBy(v => v.LearningVideoId)
                        .Select(v => new LearningVideoAdminDto
                        {
                            Id = v.LearningVideoId,
                            Titulo = v.Title,
                            Url = v.Url,
                            Img = v.ThumbnailUrl,
                            Orden = v.DisplayOrder,
                            Activo = v.Active,
                            ArchivoNombre = v.FileName,
                        }).ToList(),
                })
                .ToListAsync();

            var superficies = await ctx.LearningSurface
                .OrderBy(s => s.LearningSurfaceId)
                .Select(s => new LearningSurfaceDto { Id = s.LearningSurfaceId, Code = s.Code, Nombre = s.Name })
                .ToListAsync();

            var roles = await ctx.Role
                .Where(r => r.State && r.Active)
                .OrderBy(r => r.RoleDescription)
                .Select(r => new LearningRoleOptionDto { Id = r.RoleId, Descripcion = r.RoleDescription })
                .ToListAsync();

            return new LearningAdminDataDto { Categorias = categorias, Superficies = superficies, Roles = roles };
        }

        public async Task<int> CreateCategory(LearningCategoryCreateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var nombre = (dto.Nombre ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombre))
                throw new AbrilException("El nombre del grupo no puede estar vacío.", 400);

            var superficieExiste = await ctx.LearningSurface.AnyAsync(s => s.LearningSurfaceId == dto.SurfaceId);
            if (!superficieExiste)
                throw new AbrilException("La superficie indicada no existe.", 400);

            var duplicado = await ctx.LearningCategory.AnyAsync(c =>
                c.State && c.LearningSurfaceId == dto.SurfaceId && c.Name.ToLower() == nombre.ToLower());
            if (duplicado)
                throw new AbrilException("Ya existe un grupo con ese nombre en esa superficie.", 409);

            var cat = new LearningCategory
            {
                Name = nombre,
                LearningSurfaceId = dto.SurfaceId,
                DisplayOrder = dto.Orden,
                EsPublicoInterno = dto.EsPublicoInterno,
                Active = true,
                State = true,
                CreatedDateTime = DateTimeOffset.UtcNow,
                Roles = BuildRoles(dto.RoleIds, dto.EsPublicoInterno),
            };

            ctx.LearningCategory.Add(cat);
            await ctx.SaveChangesAsync();
            return cat.LearningCategoryId;
        }

        public async Task EditCategory(int id, LearningCategoryEditDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var nombre = (dto.Nombre ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombre))
                throw new AbrilException("El nombre del grupo no puede estar vacío.", 400);

            var cat = await ctx.LearningCategory
                .Include(c => c.Roles)
                .FirstOrDefaultAsync(c => c.LearningCategoryId == id && c.State)
                ?? throw new AbrilException("Grupo no encontrado.", 404);

            var surfaceCode = await ctx.LearningSurface
                .Where(s => s.LearningSurfaceId == dto.SurfaceId)
                .Select(s => s.Code)
                .FirstOrDefaultAsync();
            if (surfaceCode == null)
                throw new AbrilException("La superficie indicada no existe.", 400);

            // El login es público y un archivo subido a SharePoint solo se abre con cuenta de Abril:
            // un grupo con archivos no puede pasar al login (el alta de archivos ya lo bloquea ahí).
            if (surfaceCode == "LOGIN" && cat.LearningSurfaceId != dto.SurfaceId
                && await ctx.LearningVideo.AnyAsync(v => v.LearningCategoryId == id && v.State && v.ItemId != null))
                throw new AbrilException(
                    "El grupo tiene archivos subidos, que no se abren sin cuenta de Abril. " +
                    "Cámbialos a enlace para mostrar el grupo en el login.", 409);

            var duplicado = await ctx.LearningCategory.AnyAsync(c =>
                c.State && c.LearningCategoryId != id
                && c.LearningSurfaceId == dto.SurfaceId && c.Name.ToLower() == nombre.ToLower());
            if (duplicado)
                throw new AbrilException("Ya existe un grupo con ese nombre en esa superficie.", 409);

            cat.Name = nombre;
            cat.LearningSurfaceId = dto.SurfaceId;
            cat.DisplayOrder = dto.Orden;
            cat.EsPublicoInterno = dto.EsPublicoInterno;
            cat.UpdatedDateTime = DateTimeOffset.UtcNow;

            ctx.LearningCategoryRole.RemoveRange(cat.Roles);
            cat.Roles = BuildRoles(dto.RoleIds, dto.EsPublicoInterno);

            await ctx.SaveChangesAsync();
        }

        public async Task<bool> ToggleCategory(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var cat = await ctx.LearningCategory.FirstOrDefaultAsync(c => c.LearningCategoryId == id && c.State)
                ?? throw new AbrilException("Grupo no encontrado.", 404);

            cat.Active = !cat.Active;
            cat.UpdatedDateTime = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
            return cat.Active;
        }

        public async Task DeleteCategory(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var cat = await ctx.LearningCategory
                .Include(c => c.Videos)
                .FirstOrDefaultAsync(c => c.LearningCategoryId == id && c.State)
                ?? throw new AbrilException("Grupo no encontrado.", 404);

            // Soft delete del grupo y de sus videos y manuales (auditoría: nada se borra físicamente).
            cat.State = false;
            cat.UpdatedDateTime = DateTimeOffset.UtcNow;
            foreach (var v in cat.Videos.Where(v => v.State))
            {
                v.State = false;
                v.UpdatedDateTime = DateTimeOffset.UtcNow;
            }
            await ctx.SaveChangesAsync();
        }

        public async Task<int> CreateVideo(LearningVideoCreateDto dto, LearningVideoArchivoDto? archivo)
        {
            using var ctx = _factory.CreateDbContext();

            var titulo = (dto.Titulo ?? string.Empty).Trim();
            var url = archivo?.Url ?? (dto.Url ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(titulo))
                throw new AbrilException("El título no puede estar vacío.", 400);
            if (string.IsNullOrWhiteSpace(url))
                throw new AbrilException("El enlace no puede estar vacío.", 400);

            var catExiste = await ctx.LearningCategory.AnyAsync(c => c.LearningCategoryId == dto.CategoriaId && c.State);
            if (!catExiste)
                throw new AbrilException("Grupo no encontrado.", 404);

            var video = new LearningVideo
            {
                LearningCategoryId = dto.CategoriaId,
                Title = titulo,
                Url = url,
                FileName = archivo?.FileName,
                DriveId = archivo?.DriveId,
                ItemId = archivo?.ItemId,
                ThumbnailUrl = string.IsNullOrWhiteSpace(dto.Img) ? null : dto.Img.Trim(),
                DisplayOrder = dto.Orden,
                Active = true,
                State = true,
                CreatedDateTime = DateTimeOffset.UtcNow,
            };

            ctx.LearningVideo.Add(video);
            await ctx.SaveChangesAsync();
            return video.LearningVideoId;
        }

        public async Task EditVideo(int id, LearningVideoEditDto dto, LearningVideoArchivoDto? archivo)
        {
            using var ctx = _factory.CreateDbContext();

            var titulo = (dto.Titulo ?? string.Empty).Trim();
            var url = (dto.Url ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(titulo))
                throw new AbrilException("El título no puede estar vacío.", 400);
            if (archivo == null && !dto.EsArchivo && string.IsNullOrWhiteSpace(url))
                throw new AbrilException("El enlace no puede estar vacío.", 400);

            var video = await ctx.LearningVideo.FirstOrDefaultAsync(v => v.LearningVideoId == id && v.State)
                ?? throw new AbrilException("Video o manual no encontrado.", 404);

            if (archivo != null)
            {
                // Archivo nuevo (reemplazo, o de enlace a archivo). El anterior se queda en SharePoint.
                video.Url = archivo.Url;
                video.FileName = archivo.FileName;
                video.DriveId = archivo.DriveId;
                video.ItemId = archivo.ItemId;
            }
            else if (dto.EsArchivo)
            {
                // Sigue siendo archivo y no se eligió otro: se conserva el que ya tenía.
                if (video.ItemId == null)
                    throw new AbrilException("Selecciona el archivo.", 400);
            }
            else
            {
                video.Url = url;
                video.FileName = null;
                video.DriveId = null;
                video.ItemId = null;
            }

            video.Title = titulo;
            video.ThumbnailUrl = string.IsNullOrWhiteSpace(dto.Img) ? null : dto.Img.Trim();
            video.DisplayOrder = dto.Orden;
            video.UpdatedDateTime = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task<bool> ToggleVideo(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var video = await ctx.LearningVideo.FirstOrDefaultAsync(v => v.LearningVideoId == id && v.State)
                ?? throw new AbrilException("Video o manual no encontrado.", 404);

            video.Active = !video.Active;
            video.UpdatedDateTime = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
            return video.Active;
        }

        public async Task DeleteVideo(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var video = await ctx.LearningVideo.FirstOrDefaultAsync(v => v.LearningVideoId == id && v.State)
                ?? throw new AbrilException("Video o manual no encontrado.", 404);

            video.State = false;
            video.UpdatedDateTime = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task<LearningVideoDestinoDto?> GetDestinoPorCategoria(int categoriaId)
        {
            using var ctx = _factory.CreateDbContext();

            // Una sola consulta: la superficie del grupo y, como subconsulta, el link de la carpeta.
            var folderLink = CarpetaVigente(ctx);
            return await ctx.LearningCategory
                .Where(c => c.LearningCategoryId == categoriaId && c.State)
                .Select(c => new LearningVideoDestinoDto
                {
                    EsLogin = c.Surface!.Code == "LOGIN",
                    FolderLink = folderLink.FirstOrDefault(),
                })
                .FirstOrDefaultAsync();
        }

        public async Task<LearningVideoDestinoDto?> GetDestinoPorVideo(int videoId)
        {
            using var ctx = _factory.CreateDbContext();

            var folderLink = CarpetaVigente(ctx);
            return await ctx.LearningVideo
                .Where(v => v.LearningVideoId == videoId && v.State)
                .Select(v => new LearningVideoDestinoDto
                {
                    EsLogin = v.Category!.Surface!.Code == "LOGIN",
                    FolderLink = folderLink.FirstOrDefault(),
                })
                .FirstOrDefaultAsync();
        }

        /// <summary>Link de la fila vigente de learning_video_folder (singleton), para usar como subconsulta.</summary>
        private static IQueryable<string> CarpetaVigente(AppDbContext ctx) =>
            ctx.LearningVideoFolder
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.LearningVideoFolderId)
                .Select(f => f.LinkUrl);

        /// <summary>
        /// Construye las filas de rol para una categoría. Si es pública interna, no se
        /// persiste ninguna (la visibilidad ignora roles); si no, se dedup­lican los IDs.
        /// </summary>
        private static List<LearningCategoryRole> BuildRoles(List<int>? roleIds, bool esPublicoInterno)
        {
            if (esPublicoInterno || roleIds == null || roleIds.Count == 0)
                return new List<LearningCategoryRole>();

            return roleIds.Distinct()
                .Select(rid => new LearningCategoryRole { RoleId = rid })
                .ToList();
        }
    }
}
