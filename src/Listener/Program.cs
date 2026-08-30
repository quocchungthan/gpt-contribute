using Listener.Capture;
using Listener.Data;
using Listener.Operations;
using Listener.Recordings;
using Listener.Settings;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5187");
builder.Services.AddControllersWithViews();
builder.Services.AddOptions<ListenerOptions>().Bind(builder.Configuration).ValidateDataAnnotations().ValidateOnStart();
var dataRoot = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataRoot);
builder.Services.AddPooledDbContextFactory<ListenerDbContext>(options => options.UseSqlite($"Data Source={Path.Combine(dataRoot, "listener.db")}"));
builder.Services.AddSingleton<RecordingFiles>();
builder.Services.AddSingleton<AppMonitor>();
builder.Services.AddSingleton<OperationLog>();
builder.Services.AddSingleton<CaptureManager>();
builder.Services.AddSingleton<RecordingService>();
builder.Services.AddHostedService<RetentionWorker>();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllers();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();

await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ListenerDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Preferences.AnyAsync()) { db.Preferences.Add(new AppPreference()); await db.SaveChangesAsync(); }
    foreach (var row in await db.Recordings.Where(x => x.State == RecordingState.Writing || x.State == RecordingState.Deleting).ToListAsync()) row.State = RecordingState.Quarantined;
    await db.SaveChangesAsync();
}

app.Run();
public partial class Program;
