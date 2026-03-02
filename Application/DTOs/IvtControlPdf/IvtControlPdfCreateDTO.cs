using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Abril_Backend.Application.DTOs
{
    public class IvtControlPdfCreateDTO
    {
        public IFormFile Pdf {get; set;}
        public int ScheduleId {get; set;}
    }
}