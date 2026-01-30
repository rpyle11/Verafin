namespace VerafinFileMover.Services;

public interface IFileMoverService
{
    Task<bool> FileCopyProcess();
}