using Listener.Data;
using Listener.Operations;
using Listener.Recordings;
using Listener.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Listener.Tests;

public sealed class RecordingServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"listener-tests-{Guid.NewGuid():N}");
    private readonly TestDbFactory _factory;
    private readonly RecordingFiles _files;
    private readonly RecordingService _service;

    public RecordingServiceTests()
    {
        Directory.CreateDirectory(_root);
        _factory = new TestDbFactory(Path.Combine(_root, "test.db"));
        using var db = _factory.CreateDbContext(); db.Database.EnsureCreated();
        _files = new RecordingFiles(new TestEnvironment(_root));
        var log = new OperationLog(_factory, NullLogger<OperationLog>.Instance);
        var options = Options.Create(new ListenerOptions { SupportedLocales = [new SupportedLocale()] });
        _service = new RecordingService(_factory, _files, log, options);
    }

    [Fact]
    public async Task BulkDelete_IsEntirelyBlocked_WhenAnyRecordingIsProtected()
    {
        var protectedRow = await AddRecording(true, 10);
        var ordinaryRow = await AddRecording(false, 20);

        var result = await _service.DeleteAsync([protectedRow.Id, ordinaryRow.Id]);

        Assert.False(result.Succeeded);
        Assert.Contains(protectedRow.Id, result.ProtectedIds);
        await using var db = await _factory.CreateDbContextAsync();
        Assert.Equal(2, await db.Recordings.CountAsync());
        Assert.True(File.Exists(_files.FullPath(protectedRow.RelativeFileName)));
        Assert.True(File.Exists(_files.FullPath(ordinaryRow.RelativeFileName)));
    }

    [Fact]
    public async Task Retention_DeletesOnlyExpiredUnprotectedRecordings()
    {
        var protectedRow = await AddRecording(true, 10);
        var expiredRow = await AddRecording(false, 10);
        var recentRow = await AddRecording(false, 1);

        var deleted = await _service.ApplyRetentionAsync();

        Assert.Equal(1, deleted);
        await using var db = await _factory.CreateDbContextAsync();
        var remaining = await db.Recordings.Select(x => x.Id).ToListAsync();
        Assert.Contains(protectedRow.Id, remaining);
        Assert.Contains(recentRow.Id, remaining);
        Assert.DoesNotContain(expiredRow.Id, remaining);
    }

    private async Task<Recording> AddRecording(bool isProtected, int ageDays)
    {
        _files.EnsureCreated();
        var row = new Recording { StartedAt = DateTimeOffset.UtcNow.AddDays(-ageDays).AddMinutes(-1), EndedAt = DateTimeOffset.UtcNow.AddDays(-ageDays), State = RecordingState.Complete, RelativeFileName = $"{Guid.NewGuid():N}.wav", IsProtected = isProtected, ByteCount = 4 };
        await File.WriteAllBytesAsync(_files.FullPath(row.RelativeFileName), [1, 2, 3, 4]);
        await using var db = await _factory.CreateDbContextAsync(); db.Recordings.Add(row); await db.SaveChangesAsync();
        return row;
    }

    public void Dispose() { _factory.Dispose(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class TestDbFactory(string path) : IDbContextFactory<ListenerDbContext>, IDisposable
    {
        private readonly DbContextOptions<ListenerDbContext> _options = new DbContextOptionsBuilder<ListenerDbContext>().UseSqlite($"Data Source={path}").Options;
        public ListenerDbContext CreateDbContext() => new(_options);
        public Task<ListenerDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
        public void Dispose() { }
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Listener.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}
