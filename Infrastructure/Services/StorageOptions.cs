namespace Abril_Backend.Infrastructure.Services
{
    public class StorageOptions
    {
        public string StorageProvider { get; set; }
        public AzureStorageOptions AzureStorage { get; set; }
        public LocalStorageOptions LocalStorage { get; set; }
    }

    public class AzureStorageOptions
    {
        public string ConnectionString { get; set; }
        public string LessonsContainer { get; set; }
        public string IvtContainer { get; set; }
        public string ConstructionSiteLogbookContainer { get; set; }
        public string ResidentReportIncidence { get; set; }
    }

    public class LocalStorageOptions
    {
        public string LessonsContainer { get; set; }
        public string IvtContainer { get; set; }
        public string ConstructionSiteLogbookContainer { get; set; }
        public string ResidentReportIncidence { get; set; }
    }
}