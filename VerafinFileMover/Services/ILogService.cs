using VerafinFileMover.Models;

namespace VerafinFileMover.Services;

public interface ILogService
{
    Task<bool> LogAlert(AppLog appLog);
}