using Microsoft.EntityFrameworkCore;
using Moq;
using VerafinViewer.Entities;
using VerafinViewer.Models;
using VerafinViewer.Services;

namespace VerafinViewer.Tests
{
    public class DataServiceTests
    {
        private static VerafinFileMoverContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<VerafinFileMoverContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new VerafinFileMoverContext(options);
        }

        [Fact]
        public async Task DailyFilesProcessed_ReturnsRecentRecords()
        {
            var ctx = CreateContext(nameof(DailyFilesProcessed_ReturnsRecentRecords));

            // seed directory info (set required-ish fields)
            ctx.DirectoryInfo.Add(new Entities.DirectoryInfo
            {
                Id = 1,
                Pickup = "Pickup-A",
                DateFormat = "fmt",
                Dropoff = "drop",
                FriendlyLocationName = "Friendly",
                NamingScheme = "name",
                NewDateFormat = "newfmt"
            });

            // recent file (within "today")
            ctx.FilesProcessed.Add(new FilesProcessed
            {
                Id = 10,
                DirectoryInfoId = 1,
                Filename = "file1.txt",
                NewFilename = "file1_renamed.txt",
                DateCopied = DateTime.Now.AddHours(-1)
            });

            await ctx.SaveChangesAsync();

            var mockLog = new Mock<ILogService>();
            var svc = new DataService(ctx, mockLog.Object);

            var result = await svc.DailyFilesProcessed("unit-test");

            Assert.NotNull(result);
            Assert.Single(result);
            var dto = result.First();
            Assert.Equal(10, dto.Id);
            Assert.Equal("file1.txt", dto.FileName);
            Assert.Equal("file1_renamed.txt", dto.NewFileName);
            Assert.Equal("Pickup-A", dto.Pickup);
        }

        [Fact]
        public async Task DailyFilesProcessed_ReturnsEmptyList_WhenNoRecent()
        {
            var ctx = CreateContext(nameof(DailyFilesProcessed_ReturnsEmptyList_WhenNoRecent));

            ctx.DirectoryInfo.Add(new Entities.DirectoryInfo
            {
                Id = 2,
                Pickup = "Pickup-B",
                DateFormat = "fmt",
                Dropoff = "drop",
                FriendlyLocationName = "FriendlyB",
                NamingScheme = "name",
                NewDateFormat = "newfmt"
            });

            // old file (older than today)
            ctx.FilesProcessed.Add(new FilesProcessed
            {
                Id = 11,
                DirectoryInfoId = 2,
                Filename = "oldfile.txt",
                NewFilename = "oldfile_renamed.txt",
                DateCopied = DateTime.Now.AddDays(-3)
            });

            await ctx.SaveChangesAsync();

            var mockLog = new Mock<ILogService>();
            var svc = new DataService(ctx, mockLog.Object);

            var result = await svc.DailyFilesProcessed("unit-test");

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFileCount_ReturnsRecords_InRange()
        {
            var ctx = CreateContext(nameof(GetFileCount_ReturnsRecords_InRange));

            ctx.FileCount.AddRange(
                new FileCount
                {
                    Id = 1,
                    CopiedCount = 5,
                    DateInserted = DateTime.Today.AddDays(-1),
                    PickupLocation = "P1",
                    PickupLocationCount = 3
                },
                new FileCount
                {
                    Id = 2,
                    CopiedCount = 7,
                    DateInserted = DateTime.Today.AddDays(-10), // out of range later
                    PickupLocation = "P2",
                    PickupLocationCount = 4
                });

            await ctx.SaveChangesAsync();

            var mockLog = new Mock<ILogService>();
            var svc = new DataService(ctx, mockLog.Object);

            var parameters = new DateParameters
            {
                StartDate = DateTime.Today.AddDays(-2),
                EndDate = DateTime.Today
            };

            var result = await svc.GetFileCount(parameters, "unit-test");

            Assert.NotNull(result);
            Assert.Single(result);
            var dto = result.First();
            Assert.Equal(1, dto.Id);
            Assert.Equal(5, dto.CopiedCount);
            Assert.Equal("P1", dto.PickupLocation);
        }

        [Fact]
        public async Task GetLogs_ReturnsRecords_InRange()
        {
            var ctx = CreateContext(nameof(GetLogs_ReturnsRecords_InRange));

            ctx.Logs.AddRange(
                new Logs
                {
                    Id = 100,
                    MessageType = "Info",
                    DateInserted = DateTime.Today.AddHours(-2),
                    ProcessMessage = "Process OK"
                },
                new Logs
                {
                    Id = 101,
                    MessageType = "Error",
                    DateInserted = DateTime.Today.AddDays(-5), // out of range
                    ProcessMessage = "Old error"
                });

            await ctx.SaveChangesAsync();

            var mockLog = new Mock<ILogService>();
            var svc = new DataService(ctx, mockLog.Object);

            var parameters = new DateParameters
            {
                StartDate = DateTime.Today.AddDays(-1),
                EndDate = DateTime.Today.AddDays(1)
            };

            var result = await svc.GetLogs(parameters, "unit-test");

            Assert.NotNull(result);
            Assert.Single(result);
            var dto = result.First();
            Assert.Equal(100, dto.Id);
            Assert.Equal("Info", dto.MessageType);
            Assert.Equal("Process OK", dto.ProcessMessage);
        }
    }
}





