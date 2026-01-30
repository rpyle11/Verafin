using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Reflection;
using VerafinFileMover.Entities;
using VerafinFileMover.Models;

namespace VerafinFileMover.Services
{
    public class FileMoverService(ILogService logService, IOptions<AppSettings> settings, IServiceScopeFactory scopeFactory) : IFileMoverService
    {
        public async Task<bool> FileCopyProcess()
        {
            try
            {
                if (settings.Value.AppWriteToFileLogging) Log.Information("Starting file copy process");
                if (settings.Value.CopyOldFiles)
                {
                    if (settings.Value.AppWriteToFileLogging) Log.Information("Copy Old Files Process");
                    return await CopyOldFiles();
                }

                var insertLog = SetLogInsert(MethodName.GetMethodName(MethodBase.GetCurrentMethod()));

                if (settings.Value.AppWriteToFileLogging) Log.Information("Getting Watch Directories list");
                var allRows = await GetWatchDirectories();
                if (allRows == null) return true;

                foreach (var row in allRows)
                {
                    var timeSinceLastExecution = DateTime.Now.Subtract(row.LastRunTime).TotalMinutes;
                    if (!(timeSinceLastExecution > row.FrequencyMinutes)) continue;

                    var containsDate = row.Pickup.Contains("YYYYMMDD");

                    if (containsDate)
                    {
                        //insert today's date into the pickup string
                        var pickup = row.Pickup.Replace("YYYYMMDD", DateTime.Now.ToString("yyyyMMdd"));
                        //var pickup = row.Pickup.Replace("YYYYMMDD", DateTime.Now.AddDays(-8).ToString("yyyyMMdd"));

                        //Then check if the file exists
                        if (!Directory.Exists(pickup))
                        {
                            //Don't need to send error messages here, sometimes the folder won't be created if there are no files to process
                            await UpdateLastRunTime(row);
                            continue;
                        }
                    }
                    else
                    {
                        if (!Directory.Exists(row.Pickup))
                        {
                            if (!await WriteLogs($"Source path does not exist: {row.Pickup}", "Error"))
                                return false;

                            await SetError(insertLog, $"Source pickup path does not exist: {row.Pickup}");
                            await UpdateLastRunTime(row);
                            continue;
                        }

                        if (!Directory.Exists(row.Dropoff))

                        {
                            if (!await WriteLogs($"Source path drop off does not exist: {row.Dropoff}", "Error"))
                                return false;
                            await SetError(insertLog, $"Source drop off path does not exist: {row.Dropoff}");
                            await UpdateLastRunTime(row);
                            continue;
                        }
                    }

                    //If timespan is 12 - 1 search for files from previous day
                    //needs optional date parameter, if it's null run as normal, if it's not null use the days parameter to search for files
                    List<string> pickupFiles;
                    var start = new TimeSpan(00, 0, 0); //12:00 AM
                    var end = new TimeSpan(01, 0, 0); //01:00 AM
                    var now = DateTime.Now.TimeOfDay;

                    //If current hour is between midnight and 1 am
                    if ((now > start) && (now < end))
                    {
                        pickupFiles = await SearchForFiles(row, true);
                    }
                    else
                        pickupFiles = await SearchForFiles(row, false);

                    var newFiles = await CheckForDuplicates(pickupFiles, row);

                    if (newFiles.Count > 0)
                    {
                        var addCountRecord = await AddCountRecord(new FileCount
                        {
                            PickupLocation = row.Pickup,
                            PickupLocationCount = newFiles.Count
                        });

                        var fileCount = 0;
                        foreach (var fileName in newFiles.Select(Path.GetFileName))
                        {
                            var isMoved = await Copy(fileName, row);
                            if (isMoved)
                            {
                                if (await LogFileMove(row, fileName))
                                {
                                    fileCount++;
                                }
                            }
                            else
                            {
                                throw new IOException($"Can't copy file. Details: Filename: {fileName}, " +
                                                      $"Source: {row.Pickup}, Destination: {row.Dropoff}");
                            }
                        }

                        var runTimeUpdated = await UpdateLastRunTime(row);
                        addCountRecord.CopiedCount = fileCount;
                        await UpdateCountRecord(addCountRecord);

                        if (!runTimeUpdated) continue;
                        if (!await WriteLogs($"{newFiles.Count} File(s) were copied to {row.Dropoff}", "Message"))
                            return false;
                        if (!await WriteLogs($"File transfer complete for {row.FriendlyLocationName}", "Message"))
                            return false;

                    }
                    else
                    {
                        await UpdateLastRunTime(row);
                        if (!await WriteLogs($"No new files to pick up for {row.FriendlyLocationName}", "Message"))
                            return false;

                    }


                }
                return true;
            }
            catch (Exception ex)
            {
              
                var logMsg = $"Error: {ex.Message}";

                if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                {
                    logMsg += $"Inner Message {ex.InnerException.Message}";
                }
                if (settings.Value.AppWriteToFileLogging) Log.Error("Error {logMsg}", logMsg);
                await logService.LogAlert(new AppLog
                {
                    AppUser = ServiceAcctName.GetServiceAccountName(settings.Value),
                    LogMsg =
                        $"{ex.GetType().Name} Error in {MethodName.GetMethodName(MethodBase.GetCurrentMethod())}, {logMsg}",
                    MessageType = AppLog.MessageTypeEnum.Error,
                    SendEmail = true,
                });
            }

            return false;
        }

        private async Task<bool> UpdateLastRunTime(Entities.DirectoryInfo row)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VerafinFileMoverContext>();
            var insertLog = SetLogInsert(MethodName.GetMethodName(MethodBase.GetCurrentMethod()));

            try
            {

                var entity = context.DirectoryInfo.FirstOrDefault(x => x.Id == row.Id);
                if (entity != null)
                {
                    entity.LastRunTime = DateTime.Now;
                    await context.SaveChangesAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (settings.Value.AppWriteToFileLogging) Log.Error("Error {Message}", ex.Message);
                await SetError(insertLog, $"{ex.GetType().Name} Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    if (!await WriteLogs($"Error: {ex.Message}, Inner: {ex.InnerException} ", "Error"))
                        return false;

                }
                else
                {
                    if (!await WriteLogs($"Error: {ex.Message}", "Error"))
                        return false;
                }
            }
            finally
            {
                await context.DisposeAsync();
            }

            return false;
        }

        private async Task<List<Entities.DirectoryInfo>?> GetWatchDirectories()
        {

            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VerafinFileMoverContext>();
            try
            {
                return await context.DirectoryInfo.Where(w => w.Active).ToListAsync();

            }
            finally
            {
                await context.DisposeAsync();
            }

        }

        private async Task<bool> WriteLogs(string logMessage, string messageType)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VerafinFileMoverContext>();
            var insertLog = SetLogInsert(MethodName.GetMethodName(MethodBase.GetCurrentMethod()));


            try
            {

                await context.Logs.AddAsync(new Logs
                {
                    ProcessMessage = logMessage,
                    MessageType = messageType
                });
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                if (settings.Value.AppWriteToFileLogging) Log.Error("Error {Message}", ex.Message);
                await SetError(insertLog, $"{ex.GetType().Name} Error: {ex.Message}");
            }
            finally
            {
                await context.DisposeAsync();
            }

            return false;
        }

        private async Task SetError(LogToInsertDto log, string message, bool sendEmail = true)
        {
            log.Message = message;

            await logService.LogAlert(new AppLog
            {
                AppUser = log.AppUser,
                SendEmail = sendEmail,
                MessageType = sendEmail ? AppLog.MessageTypeEnum.Error : AppLog.MessageTypeEnum.Warning,
                LogMsg = $"{log.Message}"
            });


        }

        private LogToInsertDto SetLogInsert(string methodName)
        {
            return new LogToInsertDto
            {
                AppMethod = methodName,
                AppUser = ServiceAcctName.GetServiceAccountName(settings.Value),
                InsertDate = DateTime.Now
            };
        }

        private async Task<List<string>> SearchForFiles(Entities.DirectoryInfo row, bool isAfterMidnight)
        {
            var insertLog = SetLogInsert(MethodName.GetMethodName(MethodBase.GetCurrentMethod()));
            var pickupFiles = new List<string>();

            try
            {
                //If it's after midnight search -1 day, -2 days for Bank Return files
                //Else do the normal search 0 day, -1 day for Bank Return files
                if (isAfterMidnight)
                {
                    await WriteLogs($"Attempting to copy files from the previous day for {row.FriendlyLocationName}", "Midnight Message");
                    pickupFiles = PickupFiles(row, -1, -2);

                }
                else
                    pickupFiles = PickupFiles(row, 0, -1);
                return pickupFiles;
            }

            catch (Exception ex)
            {
                if (settings.Value.AppWriteToFileLogging) Log.Error("Error {Message}", ex.Message);
                await SetError(insertLog, $"{ex.GetType().Name} Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    if (!await WriteLogs($"Error: {ex.Message}, Inner: {ex.InnerException} ", "Error"))
                        return pickupFiles;

                }
                else
                {
                    if (!await WriteLogs($"Error: {ex.Message}", "Error"))
                        return pickupFiles;
                }


            }

            return [];
        }

        private static List<string> PickupFiles(Entities.DirectoryInfo row, int normalSearch, int bankReturnSearch)
        {
            List<string>? pickupFiles;
            var containsDate = row.Pickup.Contains("YYYYMMDD");
            if (containsDate)
            {
                //insert today's date into the pickup string
                var pickup = row.Pickup.Replace("YYYYMMDD", DateTime.Now.ToString("yyyyMMdd"));
              
                pickupFiles = new System.IO.DirectoryInfo(pickup)
                    .EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                    .Where(x => x.CreationTime.Date.ToShortDateString() == DateTime.Now.Date.AddDays(normalSearch).ToShortDateString())
                    .Select(x => x.FullName).ToList();

            }
            else
            {
                //switch statement here?
                pickupFiles = row.FriendlyLocationName switch
                {
                    "US Bank Return" => new System.IO.DirectoryInfo(row.Pickup)
                        .EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                        .Where(x => x.CreationTime.Date.ToShortDateString() ==
                                    DateTime.Now.Date.AddDays(bankReturnSearch).ToShortDateString())
                        .Select(x => x.FullName)
                        .ToList(),
                    "ATM" => new System.IO.DirectoryInfo(row.Pickup)
                        .EnumerateFiles("*atmc*", SearchOption.TopDirectoryOnly)
                        .Where(x => x.CreationTime.Date.ToShortDateString() ==
                                    DateTime.Now.Date.AddDays(normalSearch).ToShortDateString())
                        .Select(x => x.FullName)
                        .ToList(),
                    "ITM" => new System.IO.DirectoryInfo(row.Pickup)
                        .EnumerateFiles("*itmc*", SearchOption.TopDirectoryOnly)
                        .Where(x => x.CreationTime.Date.ToShortDateString() ==
                                    DateTime.Now.Date.AddDays(normalSearch).ToShortDateString())
                        .Select(x => x.FullName)
                        .ToList(),
                    "FRB" => new System.IO.DirectoryInfo(row.Pickup)
                        .EnumerateFiles("*FWD*", SearchOption.TopDirectoryOnly)
                        .Where(x => x.CreationTime.Date.ToShortDateString() ==
                                    DateTime.Now.Date.AddDays(normalSearch).ToShortDateString())
                        .Select(x => x.FullName)
                        .ToList(),
                    "FRB Return" => new System.IO.DirectoryInfo(row.Pickup)
                        .EnumerateFiles("*RET*", SearchOption.TopDirectoryOnly)
                        .Where(x => x.CreationTime.Date.ToShortDateString() ==
                                    DateTime.Now.Date.AddDays(normalSearch).ToShortDateString())
                        .Select(x => x.FullName)
                        .ToList(),
                    _ => new System.IO.DirectoryInfo(row.Pickup).EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                        .Where(x => x.CreationTime.Date.ToShortDateString() ==
                                    DateTime.Now.Date.AddDays(normalSearch).ToShortDateString())
                        .Select(x => x.FullName)
                        .ToList()
                };
            }

            return pickupFiles;
        }

        private async Task<List<string>> CheckForDuplicates(List<string> pickupFiles, Entities.DirectoryInfo row)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VerafinFileMoverContext>();

            var insertLog = SetLogInsert(MethodName.GetMethodName(MethodBase.GetCurrentMethod()));

            var verifiedFiles = new List<string>();

            try
            {

                foreach (var file in pickupFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var inDb = await context.FilesProcessed.FirstOrDefaultAsync(w => w.DirectoryInfoId == row.Id && w.Filename == fileName);

                    if (inDb == null)
                    {
                        verifiedFiles.Add(file);
                    }
                   
                }
            }

            catch (Exception ex)
            {
                if (settings.Value.AppWriteToFileLogging) Log.Error("Error {Message}", ex.Message);

                await SetError(insertLog, $"{ex.GetType().Name} Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    if (!await WriteLogs($"Error: {ex.Message}, Inner: {ex.InnerException} ", "Error"))
                        return verifiedFiles;

                }
                else
                {
                    if (!await WriteLogs($"Error: {ex.Message}", "Error"))
                        return verifiedFiles;
                }

            }
            finally
            {
                await context.DisposeAsync();
            }

            return verifiedFiles;
        }

        private async Task<FileCount> AddCountRecord(FileCount count)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VerafinFileMoverContext>();
            try
            {
                await context.FileCount.AddAsync(count);
                await context.SaveChangesAsync();
                return count;
            }
            finally
            {
                await context.DisposeAsync();
            }


        }
        private async Task UpdateCountRecord(FileCount count)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VerafinFileMoverContext>();
            try
            {
                var inDatabase = await context.FileCount.FirstOrDefaultAsync(x => x.Id == count.Id);
                if (inDatabase != null)
                {
                    inDatabase.CopiedCount = count.CopiedCount;
                    context.FileCount.Update(inDatabase);
                    await context.SaveChangesAsync();
                }
            }
            finally
            {
                await context.DisposeAsync();
            }


        }

        private async Task<bool> Copy(string? fileName, Entities.DirectoryInfo row)
        {
            var insertLog = SetLogInsert(MethodName.GetMethodName(MethodBase.GetCurrentMethod()));

            try
            {
                var targetPath = row.Dropoff;
                var sourcePath = row.Pickup;

                var containsDate = row.Pickup.Contains("YYYYMMDD");
                if (containsDate)
                {
                    if (fileName!.Contains(".tmp"))
                    {
                        var pickup = row.Pickup.Replace("YYYYMMDD", DateTime.Now.ToString("yyyyMMdd"));
                        sourcePath = pickup;

                        var newFilename = Path.ChangeExtension(fileName, ".x937");

                        var sourceFile = Path.Combine(sourcePath, fileName);
                        var destFile = Path.Combine(targetPath, newFilename);

                        var fileInfo = new FileInfo(sourceFile);
                        var isLocked = IsFileLocked(fileInfo);
                        if (File.Exists(sourceFile))
                        {
                            if (!isLocked)
                            {
                                //Copy can happen so fast files are renamed exactly the same down to the second
                                //Sleeping 1 second so this does not occur
                                Thread.Sleep(1000);
                                File.Copy(sourceFile, destFile);
                                return true;

                            }

                        }

                        //retry or throw exception
                        if (!await WriteLogs($"Source file does not exist. {sourceFile}", "Error"))
                            return false;
                        await SetError(insertLog, $"Error: Source file does not exist: {sourceFile}");
                    }
                    else
                    {
                        var pickup = row.Pickup.Replace("YYYYMMDD", DateTime.Now.ToString("yyyyMMdd"));
                        sourcePath = pickup;

                        var sourceFile = Path.Combine(sourcePath, fileName);
                        var destFile = Path.Combine(targetPath, fileName);

                        var fileInfo = new FileInfo(sourceFile);
                        var isLocked = IsFileLocked(fileInfo);
                        if (File.Exists(sourceFile))
                        {
                            if (!isLocked)
                            {
                                Thread.Sleep(1000);
                                File.Copy(sourceFile, destFile);
                                return true;

                            }

                        }
                        //throw exception if file is locked
                        if (!await WriteLogs($"Source file does not exist. {sourceFile}", "Error"))
                            return false;
                        await SetError(insertLog, $"Error: Source file does not exist: {sourceFile}");
                    }

                    return false;
                }

                {
                    //If the file needs to be renamed, grab the new date format
                    //Convert to Datetime then swap the placeholder for the real date
                    var newDate = DateTime.Now.ToString(row.NewDateFormat);
                    var newFilename = row.NamingScheme.Replace(row.NewDateFormat, newDate);

                    var sourceFile = Path.Combine(sourcePath, fileName!);
                    var destFile = Path.Combine(targetPath, newFilename);

                    //Maybe use last write time to do the date check?
                    var fileInfo = new FileInfo(sourceFile);
                    var isLocked = IsFileLocked(fileInfo);
                    if (File.Exists(sourceFile))
                    {
                        if (!isLocked)
                        {
                            //Copy can happen so fast files are renamed exactly the same down to the second
                            //Sleeping 1 second so this does not occur
                            Thread.Sleep(1000);
                            File.Copy(sourceFile, destFile);
                            return true;

                        }


                    }
                    //retry or throw exception
                    if (!await WriteLogs($"Source file does not exist. {sourceFile}", "Error"))
                        return false;

                    await SetError(insertLog, $"Error: Source file does not exist: {sourceFile}");
                    return false;
                }
            }

            catch (Exception ex)
            {
                if (settings.Value.AppWriteToFileLogging) Log.Error("Error {Message}", ex.Message);
                await SetError(insertLog, $"{ex.GetType().Name} Error: {ex.Message}. Details: Filename: {fileName}, " +
                                          $"Source: {row.Pickup}, Destination: {row.Dropoff}");

                if (ex.InnerException != null)
                {
                    if (!await WriteLogs($"{ex.GetType().Name} Error: {ex.Message}. Details: Filename: {fileName}, " +
                                         $"Source: {row.Pickup}, Destination: {row.Dropoff} Inner: {ex.InnerException}", "Error"))
                        return false;

                }
                else
                {
                    if (!await WriteLogs($"{ex.GetType().Name} Error: {ex.Message}. Details: Filename: {fileName}, " +
                                         $"Source: {row.Pickup}, Destination: {row.Dropoff}", "Error"))
                        return false;
                }


            }

            return false;
        }

        private static bool IsFileLocked(FileInfo file)
        {
            using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            try
            {
                stream.Close();
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }

        private async Task<bool> LogFileMove(Entities.DirectoryInfo row, string? fileName)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VerafinFileMoverContext>();

            var insertLog = SetLogInsert(MethodName.GetMethodName(MethodBase.GetCurrentMethod()));

            try
            {
                FilesProcessed fileEntity;

                if (string.IsNullOrEmpty(row.NewDateFormat))
                {
                    fileEntity = new FilesProcessed
                    {
                        DateCopied = DateTime.Now,
                        DirectoryInfoId = row.Id,
                        Filename = fileName,
                    };
                }
                else
                {
                    var newDate = DateTime.Now.ToString(row.NewDateFormat);
                    var newFilename = row.NamingScheme.Replace(row.NewDateFormat, newDate);

                    fileEntity = new FilesProcessed
                    {
                        DateCopied = DateTime.Now,
                        DirectoryInfoId = row.Id,
                        Filename = fileName,
                        NewFilename = newFilename

                    };
                }


                await context.FilesProcessed.AddAsync(fileEntity);
                await context.SaveChangesAsync();
                //Could be helpful to add the renamed filename to the output below
                return await WriteLogs($" File: {fileName}, {row.Id}, {DateTime.Now} has been copied to {row.Dropoff}",
                    "Message");
            }
            catch (Exception ex)
            {
                if (settings.Value.AppWriteToFileLogging) Log.Error("Error {Message}", ex.Message);
                await SetError(insertLog, $"{ex.GetType().Name} Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    if (!await WriteLogs($"Error: {ex.Message}, Inner: {ex.InnerException} ", "Error"))
                        return false;

                }
                else
                {
                    if (!await WriteLogs($"Error: {ex.Message}", "Error"))
                        return false;
                }
            }
            finally
            {
                await context.DisposeAsync();
            }

            return false;
        }

        private async Task<bool> CopyOldFiles()
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VerafinFileMoverContext>();
            var insertLog = new LogToInsertDto
            {
                AppMethod = MethodName.GetMethodName(MethodBase.GetCurrentMethod()),
                AppUser = "VerafinFileMover",
                InsertDate = DateTime.Now
            };

            try
            {

                //Only grab rows that are active
                var allRows = context.DirectoryInfo.Where(x => x.Active == true).ToList();

                foreach (var row in allRows)
                {
                    var containsDate = row.Pickup.Contains("YYYYMMDD");
                    if (containsDate)
                    {

                        var beginningDate = settings.Value.RecoveryBeginning;
                        var endDate = settings.Value.RecoveryEnding;

                        //Use EnumerateFileSystemInfo here because I need to search the I0001 folder for the date sub folder
                        var pickupFolderByDate =
                            new System.IO.DirectoryInfo("\\\\greatsouthernbank.com\\gsb\\SummitData01\\I0001")
                                .EnumerateFileSystemInfos("*.*", SearchOption.AllDirectories)

                                .Where(x => x.CreationTime.Date > beginningDate && x.CreationTime.Date < endDate)
                                .Select(x => x.FullName).ToList();

                        //for each folder we want to look at the CASHLETTER folder and copy every file
                        foreach (var dateFolder in pickupFolderByDate)
                        {
                            var fileFolder = $@"{dateFolder}\CASHLETTER\{row.FriendlyLocationName}";
                            if (!Directory.Exists(fileFolder)) continue;
                            var pickupFiles = new System.IO.DirectoryInfo(fileFolder).EnumerateFiles("*.*")
                                .Select(x => x.FullName).ToList();
                            var newFiles = await CheckForDuplicates(pickupFiles, row);

                            foreach (var file in newFiles)
                            {
                                var filename = Path.GetFileName(file);

                                var sourcePath = fileFolder;

                                //Need to modify target path to match OldFileDropoffLocation in appSettings
                                var targetPath = settings.Value.OldFileDropoffLocation;
                                var creationTime = File.GetCreationTime(file);

                                //commented out as resharper finding, but will not be used since records for this location is set to inactive
                                //string newFileName;
                                //if (row.Pickup.Contains("US Bank Local Mixed"))
                                //{
                                //    newFileName = Path.ChangeExtension(filename, ".x937");
                                //}

                                var newFileName = $"{creationTime:dd-MM-yyyy}-{filename}";
                                var sourceFile = Path.Combine(sourcePath, filename);
                                var destFile = Path.Combine(targetPath!, newFileName);

                                var fileInfo = new FileInfo(sourceFile);
                                var isLocked = IsFileLocked(fileInfo);

                                if (File.Exists(sourceFile))
                                {
                                    if (!isLocked)
                                    {
                                        Thread.Sleep(1000);
                                        File.Copy(sourceFile, destFile);
                                        await LogFileMove(row, filename);
                                    }
                                    else
                                    {
                                        if (!await WriteLogs($"Source file is locked. {sourceFile}", "Error"))
                                            return false;
                                        await SetError(insertLog, $"Error: Source file is locked: {sourceFile}");
                                        return false;
                                    }
                                }
                                else
                                {
                                    if (!await WriteLogs($"Source file does not exist. {sourceFile}", "Error"))
                                        return false;
                                    await SetError(insertLog, $"Error: Source file does not exist: {sourceFile}");
                                    return false;
                                }
                            }

                        }
                    }
                    else
                    {
                        var beginningDate = settings.Value.RecoveryBeginning;
                        var endDate = settings.Value.RecoveryEnding;

                        var pickupFiles = row.FriendlyLocationName switch
                        {
                            "US Bank Return" => new System.IO.DirectoryInfo(row.Pickup)
                                .EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                                .Where(x => x.CreationTime.Date > beginningDate && x.CreationTime.Date < endDate)
                                .Select(x => x.FullName)
                                .ToList(),
                            "ATM" => new System.IO.DirectoryInfo(row.Pickup)
                                .EnumerateFiles("*atmc*", SearchOption.TopDirectoryOnly)
                                .Where(x => x.CreationTime.Date > beginningDate && x.CreationTime.Date < endDate)
                                .Select(x => x.FullName)
                                .ToList(),
                            "ITM" => new System.IO.DirectoryInfo(row.Pickup)
                                .EnumerateFiles("*itmc*", SearchOption.TopDirectoryOnly)
                                .Where(x => x.CreationTime.Date > beginningDate && x.CreationTime.Date < endDate)
                                .Select(x => x.FullName)
                                .ToList(),
                            "FRB" => new System.IO.DirectoryInfo(row.Pickup)
                                .EnumerateFiles("*FWD*", SearchOption.TopDirectoryOnly)
                                .Where(x => x.CreationTime.Date > beginningDate && x.CreationTime.Date < endDate)
                                .Select(x => x.FullName)
                                .ToList(),
                            "FRB Return" => new System.IO.DirectoryInfo(row.Pickup)
                                .EnumerateFiles("*RET*", SearchOption.TopDirectoryOnly)
                                .Where(x => x.CreationTime.Date > beginningDate && x.CreationTime.Date < endDate)
                                .Select(x => x.FullName)
                                .ToList(),
                            _ => new System.IO.DirectoryInfo(row.Pickup)
                                .EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                                .Where(x => x.CreationTime.Date > beginningDate && x.CreationTime.Date < endDate)
                                .Select(x => x.FullName)
                                .ToList()
                        };
                        var newFiles = await CheckForDuplicates(pickupFiles, row);
                        if (newFiles.Count == 0)
                        {
                            continue;
                        }

                        foreach (var file in newFiles)
                        {
                            if (file.Contains("Archive") || file.Contains("Files to Send"))
                            {
                                continue;
                            }

                            var sourcePath = row.Pickup;

                            var targetPath = settings.Value.OldFileDropoffLocation;
                            var creationDate = File.GetCreationTime(file);
                            var oldFilename = Path.GetFileName(file);
                            var newDate = creationDate.ToString(row.NewDateFormat);
                            var newFilename = row.NamingScheme.Replace(row.NewDateFormat, newDate);
                            var sourceFile = Path.Combine(sourcePath, oldFilename);


                            var fileInfo = new FileInfo(sourceFile);
                            var destFile = Path.Combine(targetPath!, newFilename);

                            var isLocked = IsFileLocked(fileInfo);

                            if (File.Exists(sourceFile))
                            {
                                if (!isLocked)
                                {
                                    Thread.Sleep(1000);
                                    File.Copy(sourceFile, destFile);
                                    await LogFileMove(row, oldFilename);
                                }
                                else
                                {

                                    if (!await WriteLogs($"Source file is locked. {sourceFile}", "Error"))
                                        return false;
                                    await SetError(insertLog, $"Error: Source file is locked: {sourceFile}");
                                    return false;
                                }


                            }
                            else
                            {
                                if (!await WriteLogs($"Source file does not exist. {sourceFile}", "Error"))
                                    return false;
                                await SetError(insertLog, $"Error: Source file does not exist: {sourceFile}");
                                return false;
                            }


                        }
                    }
                }
            }

            catch (Exception ex)
            {

                if (settings.Value.AppWriteToFileLogging) Log.Error("Error {Message}", ex.Message);
                await SetError(insertLog, $"{ex.GetType().Name} Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    if (!await WriteLogs($"Error: {ex.Message}, Inner: {ex.InnerException} ", "Error"))
                        return false;
                }
                else
                {
                    if (!await WriteLogs($"Error: {ex.Message}", "Error"))
                        return false;
                }
            }
            finally
            {
                await context.DisposeAsync();
            }


            return true;
        }




    }
}
