using VerafinViewer.Models;

namespace VerafinViewer.Services;

public interface IDataService
{
    Task<List<FilesProcessedDto>?> DailyFilesProcessed(string? appUser);

    Task<List<FileCountDto>?> GetFileCount(DateParameters parameters, string? appUser);

    Task<List<LogsDto>?> GetLogs(DateParameters parameters, string? appUser);
}