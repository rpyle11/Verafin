using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using VerafinFileMover.Entities;
using VerafinFileMover.Models;
using VerafinFileMover.Services;
using Xunit;


namespace VerafinFileMover.Tests
{
    public class FileMoverServiceTests
    {
        private class TestServiceScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; }
            public TestServiceScope(IServiceProvider provider) => ServiceProvider = provider;
            public void Dispose() { }
        }

        private class TestServiceScopeFactory : IServiceScopeFactory
        {
            private readonly IServiceProvider _provider;
            public TestServiceScopeFactory(IServiceProvider provider) => _provider = provider;
            public IServiceScope CreateScope() => new TestServiceScope(_provider);
        }

        private static (IServiceScopeFactory scopeFactory, VerafinFileMoverContext context) BuildScopeFactoryWithInMemoryDb(string dbName)
        {
            var services = new ServiceCollection();
            services.AddDbContext<VerafinFileMoverContext>(opts => opts.UseInMemoryDatabase(dbName));
            var provider = services.BuildServiceProvider();
            var context = provider.GetRequiredService<VerafinFileMoverContext>();
            return (new TestServiceScopeFactory(provider), context);
        }

        [Fact]
        public async Task FileCopyProcess_ReturnsTrue_When_NoActiveDirectories()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var (scopeFactory, context) = BuildScopeFactoryWithInMemoryDb(dbName);

            // ensure no DirectoryInfo rows exist
            context.DirectoryInfo.RemoveRange(context.DirectoryInfo);
            await context.SaveChangesAsync();

            var mockLogService = new Mock<ILogService>();
            mockLogService.Setup(x => x.LogAlert(It.IsAny<AppLog>())).ReturnsAsync(true);

            var settings = Options.Create(new AppSettings
            {
                CopyOldFiles = false,
                TimerInterval = 1000,
                ServiceName = "TestSvc",
                AppWriteToFileLogging = false,
            });

            var svc = new FileMoverService(mockLogService.Object, settings, scopeFactory);

            // Act
            var result = await svc.FileCopyProcess();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task FileCopyProcess_ReturnsTrue_When_CopyOldFiles_Enabled_But_NoRows()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var (scopeFactory, context) = BuildScopeFactoryWithInMemoryDb(dbName);

            // ensure no DirectoryInfo rows exist
            context.DirectoryInfo.RemoveRange(context.DirectoryInfo);
            await context.SaveChangesAsync();

            var mockLogService = new Mock<ILogService>();
            mockLogService.Setup(x => x.LogAlert(It.IsAny<AppLog>())).ReturnsAsync(true);

            var settings = Options.Create(new AppSettings
            {
                CopyOldFiles = true,
                TimerInterval = 1000,
                ServiceName = "TestSvc",
                AppWriteToFileLogging = false,
                RecoveryBeginning = DateTime.MinValue,
                RecoveryEnding = DateTime.MaxValue,
                OldFileDropoffLocation = null
            });

            var svc = new FileMoverService(mockLogService.Object, settings, scopeFactory);

            // Act
            var result = await svc.FileCopyProcess();

            // Assert
            Assert.True(result);
        }
    }
}
