namespace Abril_Backend.Shared.Services.Email.Dtos
{
    public class MailAttachmentDto
    {
        public string FileName { get; set; } = null!;
        public string ContentType { get; set; } = "application/octet-stream";
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }
}
