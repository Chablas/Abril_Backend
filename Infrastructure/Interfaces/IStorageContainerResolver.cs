namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IStorageContainerResolver
    {
        string GetLessonsContainerName();
        string GetIvtContainerName();
    }
}