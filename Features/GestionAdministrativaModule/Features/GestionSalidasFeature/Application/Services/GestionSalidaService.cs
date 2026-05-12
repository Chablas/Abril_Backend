using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces;
using ClosedXML.Excel;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Services
{
    public class GestionSalidaService : IGestionSalidaService
    {
        private readonly IGestionSalidaRepository _repo;

        public GestionSalidaService(IGestionSalidaRepository repo)
        {
            _repo = repo;
        }

        public Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters)
            => _repo.GetAll(filters);

        public Task<GestionSalidaFilterDataDto> GetFilterData()
            => _repo.GetFilterData();

        public async Task<byte[]> GetExcel(GestionSalidaFiltersDto filters)
        {
            var salidas = await _repo.GetAll(filters);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Gestión de Salidas");

            // ── Encabezado ──────────────────────────────────────────────────────
            string[] headers =
            [
                "#", "Trabajador", "Fecha salida", "Hora salida", "Hora retorno",
                "Motivo", "Origen", "Destino", "Estado", "Registrada",
            ];

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.FromHtml("#64BC04");
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E5F7D1");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // ── Filas de datos ──────────────────────────────────────────────────
            for (int r = 0; r < salidas.Count; r++)
            {
                var s   = salidas[r];
                int row = r + 2;

                ws.Cell(row, 1).Value  = r + 1;
                ws.Cell(row, 2).Value  = s.Trabajador;
                ws.Cell(row, 3).Value  = s.FechaSalida.ToString("dd/MM/yyyy");
                ws.Cell(row, 4).Value  = s.HoraSalida.ToString("HH:mm");
                ws.Cell(row, 5).Value  = s.HoraRetorno.HasValue ? s.HoraRetorno.Value.ToString("HH:mm") : "—";
                ws.Cell(row, 6).Value  = s.Motivo;
                ws.Cell(row, 7).Value  = s.LugarOrigen  ?? "—";
                ws.Cell(row, 8).Value  = s.LugarDestino ?? "—";
                ws.Cell(row, 9).Value  = s.Estado;
                ws.Cell(row, 10).Value = s.CreatedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm");

                var rowRange = ws.Range(row, 1, row, headers.Length);
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
                rowRange.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;

                if (r % 2 == 0)
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFAFA");

                var estadoCell = ws.Cell(row, 9);
                estadoCell.Style.Font.Bold = true;
                estadoCell.Style.Font.FontColor = s.Estado switch
                {
                    "Aprobado"  => XLColor.FromHtml("#009C87"),
                    "Rechazado" => XLColor.FromHtml("#D30000"),
                    _           => XLColor.FromHtml("#92400E"),
                };
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public Task Aprobar(int id, int reviewerUserId)
            => _repo.Aprobar(id, reviewerUserId);

        public Task Rechazar(int id, int reviewerUserId)
            => _repo.Rechazar(id, reviewerUserId);
    }
}
