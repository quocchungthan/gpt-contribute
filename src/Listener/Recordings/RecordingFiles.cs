namespace Listener.Recordings;

public sealed class RecordingFiles(IWebHostEnvironment environment)
{
    public string Root { get; } = Path.Combine(environment.ContentRootPath, "data", "recordings");
    public string FullPath(string relativeName) => Path.Combine(Root, Path.GetFileName(relativeName));
    public void EnsureCreated() => Directory.CreateDirectory(Root);
    public long UsedBytes()
    {
        EnsureCreated();
        return Directory.EnumerateFiles(Root).Sum(path => new FileInfo(path).Length);
    }
}
