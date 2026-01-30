using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VerafinViewer.Entities;
using VerafinViewer.Models;

namespace VerafinViewer.Services
{
    public class DataService(VerafinFileMoverContext context, ILogService logService) : IDataService
    {
        public async Task<List<FilesProcessedDto>?> DailyFilesProcessed(string? appUser)
        {
            try
            {
                var data = from fp in context.FilesProcessed
                    join di in context.DirectoryInfo on fp.DirectoryInfoId equals di.Id
                    where fp.DateCopied > DateTime.Now.Date
                    select new FilesProcessedDto
                    {
                        Id = fp.Id,
                        DateCopied = fp.DateCopied,
                        FileName = fp.Filename,
                        NewFileName = fp.NewFilename,
                        Pickup = di.Pickup

                    };


                if (await data.AnyAsync())
                {
                    return await data.OrderByDescending(o => o.DateCopied).ToListAsync();
                }

                return [];


            }
            catch (Exception ex)
            {
                await logService.LogAlert(AppLogPrep.AppLogSetup(appUser, nameof(DataService),
                    MethodName.GetMethodName(MethodBase.GetCurrentMethod()), ex));
            }

            return null;
        }

        public async Task<List<FileCountDto>?> GetFileCount(DateParameters parameters, string? appUser)
        {
            try
            {
                var data = from fc in context.FileCount where fc.DateInserted >= parameters.StartDate && fc.DateInserted<= parameters.EndDate
                    select new FileCountDto
                    {
                        Id = fc.Id,
                        CopiedCount = fc.CopiedCount,
                        DateInserted = fc.DateInserted,
                        PickupLocation = fc.PickupLocation,
                        PickupLocationCount = fc.PickupLocationCount
                    };

                if (await data.AnyAsync())
                {
                    return await data.OrderByDescending(o => o.DateInserted).ToListAsync();
                }

                return [];
            }
            catch (Exception ex)
            {
                await logService.LogAlert(AppLogPrep.AppLogSetup(appUser, nameof(DataService),
                    MethodName.GetMethodName(MethodBase.GetCurrentMethod()), ex));
            }

            return null;
        }

        public async Task<List<LogsDto>?> GetLogs(DateParameters parameters, string? appUser)
        {
            try
            {
                var data = from lg in context.Logs
                    where lg.DateInserted >= parameters.StartDate && lg.DateInserted <= parameters.EndDate
                           select new LogsDto
                    {
                        Id = lg.Id,
                        MessageType = lg.MessageType,
                        DateInserted = lg.DateInserted,
                       ProcessMessage = lg.ProcessMessage
                    };

                if (await data.AnyAsync())
                {
                    return await data.OrderByDescending(o => o.DateInserted).ToListAsync();
                }

                return [];
            }
            catch (Exception ex)
            {
                await logService.LogAlert(AppLogPrep.AppLogSetup(appUser, nameof(DataService),
                    MethodName.GetMethodName(MethodBase.GetCurrentMethod()), ex));
            }

            return null;
        }


    }
}
