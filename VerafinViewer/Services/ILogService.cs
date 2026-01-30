using VerafinViewer.Models;

namespace VerafinViewer.Services;

public interface ILogService
{
    Task<bool> LogAlert(AppLog appLog);
}