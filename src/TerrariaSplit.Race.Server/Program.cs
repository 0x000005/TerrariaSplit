using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using TerrariaSplit.Race.Contracts;
using TerrariaSplit.Race.Server;
using TerrariaSplit.Infrastructure;

AppLog.Initialize(new AppLogOptions
{
    FilePath = Path.Combine(AppContext.BaseDirectory, "terrariasplit-race-server.log"),
    MinimumLevel = AppLog.ParseLevel(Environment.GetEnvironmentVariable(AppLog.LevelEnvironmentVariable)),
    ConsoleOutput = true
});
using AppLogProvider logProvider = AppLog.Provider;
AppDomain.CurrentDomain.UnhandledException += (_, failure) =>
{
    AppLog.Failure("Race.Server", "server.unhandled", "The race server encountered an unhandled exception.",
        "Source: process", failure.ExceptionObject as Exception);
    try { logProvider.FlushAsync().GetAwaiter().GetResult(); }
    catch (TimeoutException) { }
};
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Trace);
builder.Logging.AddFilter<AppLogProvider>(null, LogLevel.Trace);
builder.Logging.AddProvider(logProvider);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 128L * 1024 * 1024;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 128L * 1024 * 1024;
});
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 8 * 1024 * 1024;
    options.AddFilter<RaceHubLoggingFilter>();
});
builder.Services.AddSingleton<IRaceRecordStore>(_ =>
    new FileRaceRecordStore(Path.Combine(AppContext.BaseDirectory, "Data", "RaceRecords")));
builder.Services.AddSingleton(TimeProvider.System);
var raceWorldFiles = new RaceWorldFileStore(
    Path.Combine(AppContext.BaseDirectory, "Data", "RaceWorlds"),
    TimeProvider.System);
raceWorldFiles.DeleteAllRooms();
builder.Services.AddSingleton(raceWorldFiles);
builder.Services.AddSingleton<RaceRoomManager>();
builder.Services.AddSingleton<RaceWorldUploadCoordinator>();
builder.Services.AddHostedService<RaceRoomCleanupService>();

WebApplication app = builder.Build();
AppLog.BindFactory(app.Services.GetRequiredService<ILoggerFactory>());
app.Use(async (context, next) =>
{
    using var operation = AppLog.BeginOperation("Race HTTP request", new
    {
        method = context.Request.Method, path = context.Request.Path.Value, requestId = context.TraceIdentifier
    });
    await next(context);
    AppLog.Debug("server.request.completed", new { context.Response.StatusCode });
});
app.MapGet("/", () => "TerrariaSplit Race Server");
app.MapPost(
    "/api/race/rooms/{roomCode}/world",
    async (
        string roomCode,
        HttpRequest request,
        RaceRoomManager rooms,
        RaceWorldUploadCoordinator worldUploads,
        IHubContext<RaceHub> hubContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            string nickname = request.Query["nickname"].ToString();
            RaceOperationResult<RaceRoomState> authorization = rooms.AuthorizeWorldUpload(roomCode, nickname);
            if (!authorization.Succeeded)
            {
                return Results.Json(authorization, statusCode: StatusCodes.Status403Forbidden);
            }

            if (!request.HasFormContentType)
            {
                return Results.Json(
                    RaceOperationResult<RaceRoomState>.Failure(RaceErrors.InvalidRequest, "Upload must use multipart form data."),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            IFormCollection form = await request.ReadFormAsync(cancellationToken);
            IFormFile? file = form.Files.GetFile("world") ?? form.Files.FirstOrDefault();
            if (file is null ||
                file.Length <= 0 ||
                !string.Equals(Path.GetExtension(file.FileName), ".wld", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(
                    RaceOperationResult<RaceRoomState>.Failure(RaceErrors.WorldUploadRequired, "A valid .wld file is required."),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            RaceWorldSettings? worldSettings = DeserializeFormJson<RaceWorldSettings>(form["worldSettings"]);
            if (worldSettings is null)
            {
                return Results.Json(
                    RaceOperationResult<RaceRoomState>.Failure(RaceErrors.InvalidRequest, "World settings are required."),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            RaceRoutePayload? route = DeserializeFormJson<RaceRoutePayload>(form["route"]);
            if (route is null || route.Splits.Count == 0)
            {
                return Results.Json(
                    RaceOperationResult<RaceRoomState>.Failure(RaceErrors.RouteRequired, "Route must contain at least one split."),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            RaceSeedAssignment? seed = DeserializeFormJson<RaceSeedAssignment>(form["seed"]);
            if (!Guid.TryParseExact(
                    form[RaceWorldUploadProtocol.OperationIdFormField].ToString(),
                    RaceWorldUploadProtocol.OperationIdFormat,
                    out Guid uploadOperationId) ||
                uploadOperationId == Guid.Empty)
            {
                return Results.Json(
                    RaceOperationResult<RaceRoomState>.Failure(
                        RaceErrors.InvalidRequest,
                        "Upload operation id is required."),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            await using Stream stream = file.OpenReadStream();
            RaceWorldUploadOutcome upload = await worldUploads.PublishAsync(
                new RaceWorldUploadRequest(
                    uploadOperationId,
                    roomCode,
                    nickname,
                    file.FileName,
                    route,
                    worldSettings,
                    seed),
                stream,
                cancellationToken);
            RaceOperationResult<RaceRoomState> result = upload.Result;

            if (upload.PackageChanged && result.Value is RaceRoomState state)
            {
                _ = BroadcastPackageChangedBestEffortAsync(
                    hubContext,
                    state,
                    nickname.Trim());
            }

            return Results.Json(
                result,
                statusCode: result.Succeeded ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        }
        catch (OperationCanceledException)
        {
            return Results.Json(
                RaceOperationResult<RaceRoomState>.Failure(RaceErrors.InvalidRequest, "Upload was cancelled."),
                statusCode: StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex) when (ex is BadHttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            AppLog.Failure("Race.Server", "race.world.upload.failed", "World upload failed.", "Room: " + roomCode, ex);
            return Results.Json(
                RaceOperationResult<RaceRoomState>.Failure(RaceErrors.InvalidRequest, "Upload failed: " + ex.Message),
                statusCode: StatusCodes.Status400BadRequest);
        }
    });
app.MapGet(
    "/api/race/rooms/{roomCode}/world",
    (string roomCode, string nickname, RaceRoomManager rooms, RaceWorldFileStore worldFiles) =>
    {
        RaceOperationResult<RaceWorldFileInfo> authorization = rooms.AuthorizeWorldDownload(roomCode, nickname);
        if (!authorization.Succeeded || authorization.Value is null)
        {
            return Results.Json(authorization, statusCode: StatusCodes.Status404NotFound);
        }

        if (!worldFiles.TryGetPath(roomCode, authorization.Value, out string path))
        {
            return Results.Json(
                RaceOperationResult<RaceWorldFileInfo>.Failure(RaceErrors.WorldRequired, "The uploaded world file is missing from the server."),
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.File(path, "application/octet-stream", authorization.Value.FileName);
    });
app.MapHub<RaceHub>("/raceHub");
app.Run();

static T? DeserializeFormJson<T>(string? json)
{
    if (string.IsNullOrWhiteSpace(json))
    {
        return default;
    }

    try
    {
        return JsonSerializer.Deserialize<T>(json);
    }
    catch (JsonException)
    {
        return default;
    }
}

static async Task BroadcastPackageChangedBestEffortAsync(
    IHubContext<RaceHub> hubContext,
    RaceRoomState state,
    string nickname)
{
    try
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await hubContext.Clients.Group(state.RoomCode).SendAsync(
            "RacePackageChanged",
            new RacePackageChanged(
                state,
                nickname,
                RacePackageRevisionCalculator.Create(state),
                RacePackageChangeKind.Published),
            timeout.Token);
    }
    catch
    {
        // The upload is already committed. A failed notification must not turn
        // the successful upload response into a failure or trigger a retry.
    }
}
