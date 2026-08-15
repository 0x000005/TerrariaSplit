using System.Net;
using System.Net.Http.Json;
using System.Text;
using TerrariaSplit.Race.Client;

namespace TerrariaSplit.Tests;

internal static class RaceWorldUploadFlowTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Async(
            "race world upload transaction preserves the live file and commits each operation once",
            TestSuite.Flow,
            UploadTransactionJourney);
        yield return TestCase.Async(
            "race client world upload retries reuse one operation id",
            TestSuite.Flow,
            ClientRetryOperationIdentity);
    }

    private static async Task UploadTransactionJourney(CancellationToken cancellationToken)
    {
        using var directory = new TestDirectory();
        string worldRoot = directory.Combine("worlds");
        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        var store = new RaceWorldFileStore(worldRoot);
        var uploads = new RaceWorldUploadCoordinator(manager, store);
        RaceRoomState created = Success(manager.CreateRoom(new RaceRoomCreateRequest("host")));
        string roomCode = created.RoomCode;
        byte[] firstWorld = TerrariaIntegrationTests.CreateMinimalWorld("first-world", 1001);
        byte[] secondWorld = TerrariaIntegrationTests.CreateMinimalWorld("second-world", 1002);
        Guid firstOperation = Guid.NewGuid();

        RaceWorldUploadOutcome first = await UploadAsync(
            uploads,
            firstOperation,
            roomCode,
            "first.wld",
            firstWorld,
            cancellationToken);
        Check.True(first.PackageChanged);
        RaceRoomState firstState = Success(first.Result);
        Check.Equal(1L, firstState.PackageRevision);
        Check.True(store.TryGetPath(roomCode, firstState.WorldFile!, out string firstPath));

        RaceRoomState prepared = Success(manager.UpdatePreparationStatus(
            new RacePreparationStatusRequest(
                roomCode,
                "host",
                RacePlayerFileStatus.Ready,
                RaceWorldFileStatus.Ready,
                RaceRngControlStatus.Enabled,
                PackageRevision: 1)));
        Check.Equal(RacePlayerFileStatus.Ready, prepared.Players.Single().PlayerFileStatus);

        RaceWorldUploadOutcome retry = await UploadAsync(
            uploads,
            firstOperation,
            roomCode,
            "first.wld",
            firstWorld,
            cancellationToken);
        Check.False(retry.PackageChanged);
        RaceRoomState retryState = Success(retry.Result);
        Check.Equal(1L, retryState.PackageRevision);
        Check.Equal(RacePlayerFileStatus.Ready, retryState.Players.Single().PlayerFileStatus);
        Check.True(File.Exists(firstPath));

        RaceWorldUploadOutcome conflict = await UploadAsync(
            uploads,
            firstOperation,
            roomCode,
            "second.wld",
            secondWorld,
            cancellationToken);
        Check.False(conflict.PackageChanged);
        Check.Equal(RaceErrors.UploadOperationConflict, conflict.Result.ErrorCode);
        Check.True(File.Exists(firstPath));
        Check.Equal(1, StoredWorldFileCount(worldRoot, roomCode));

        RaceWorldUploadOutcome replacement = await UploadAsync(
            uploads,
            Guid.NewGuid(),
            roomCode,
            "second.wld",
            secondWorld,
            cancellationToken);
        Check.True(replacement.PackageChanged);
        RaceRoomState replacementState = Success(replacement.Result);
        Check.Equal(2L, replacementState.PackageRevision);
        Check.False(File.Exists(firstPath));
        Check.True(store.TryGetPath(roomCode, replacementState.WorldFile!, out string replacementPath));

        RaceWorldUploadOutcome lateRetry = await UploadAsync(
            uploads,
            firstOperation,
            roomCode,
            "first.wld",
            firstWorld,
            cancellationToken);
        Check.False(lateRetry.PackageChanged);
        Check.Equal(2L, Success(lateRetry.Result).PackageRevision);
        Check.True(File.Exists(replacementPath));
        Check.Equal(1, StoredWorldFileCount(worldRoot, roomCode));

        RaceWorldUploadOutcome invalidRepublish = await UploadAsync(
            uploads,
            Guid.NewGuid(),
            roomCode,
            "second.wld",
            secondWorld,
            cancellationToken,
            route: new RaceRoutePayload("empty", "Empty", "{}", []));
        Check.False(invalidRepublish.Result.Succeeded);
        Check.Equal(RaceErrors.RouteRequired, invalidRepublish.Result.ErrorCode);
        Check.True(File.Exists(replacementPath));

        byte[] thirdWorld = TerrariaIntegrationTests.CreateMinimalWorld("third-world", 1003);
        byte[] fourthWorld = TerrariaIntegrationTests.CreateMinimalWorld("fourth-world", 1004);
        RaceWorldUploadOutcome[] concurrent = await Task.WhenAll(
            UploadAsync(
                uploads,
                Guid.NewGuid(),
                roomCode,
                "third.wld",
                thirdWorld,
                cancellationToken),
            UploadAsync(
                uploads,
                Guid.NewGuid(),
                roomCode,
                "fourth.wld",
                fourthWorld,
                cancellationToken));
        Check.True(concurrent.All(static outcome => outcome.PackageChanged));
        RaceRoomState finalState = Success(manager.GetRoomState(roomCode));
        Check.Equal(4L, finalState.PackageRevision);
        Check.True(store.TryGetPath(roomCode, finalState.WorldFile!, out string finalPath));
        Check.True(File.Exists(finalPath));
        Check.Equal(1, StoredWorldFileCount(worldRoot, roomCode));

        await uploads.DeleteRoomAsync(roomCode, cancellationToken);
        Check.False(Directory.Exists(Path.Combine(worldRoot, roomCode)));
    }

    private static async Task ClientRetryOperationIdentity(CancellationToken cancellationToken)
    {
        using var directory = new TestDirectory();
        string worldPath = directory.Combine("retry.wld");
        await File.WriteAllBytesAsync(
            worldPath,
            TerrariaIntegrationTests.CreateMinimalWorld("retry-world", 2001),
            cancellationToken);

        var manager = new RaceRoomManager(new InMemoryRaceRecordStore());
        RaceRoomState responseState = Success(manager.CreateRoom(new RaceRoomCreateRequest("host")));
        var handler = new RetryRecordingHandler(responseState);
        using var httpClient = new HttpClient(handler);
        int delayCount = 0;
        await using var session = new RaceClientSession(
            httpClient,
            "https://race.test",
            (_, _) =>
            {
                delayCount++;
                return Task.CompletedTask;
            });

        RaceOperationResult<RaceRoomState> result = await session.UploadWorldFileWithRetriesAsync(
            responseState.RoomCode,
            "host",
            worldPath,
            Route(),
            WorldSettings(),
            new RaceSeedAssignment("1234", RaceSeedSource.Fixed),
            progress: null,
            cancellationToken);

        Check.True(result.Succeeded);
        Check.Equal(1, delayCount);
        Check.Equal(2, handler.OperationIds.Count);
        Check.False(string.IsNullOrWhiteSpace(handler.OperationIds[0]));
        Check.Equal(handler.OperationIds[0], handler.OperationIds[1]);
        Check.True(Guid.TryParseExact(
            handler.OperationIds[0],
            RaceWorldUploadProtocol.OperationIdFormat,
            out _));
    }

    private static async Task<RaceWorldUploadOutcome> UploadAsync(
        RaceWorldUploadCoordinator uploads,
        Guid operationId,
        string roomCode,
        string fileName,
        byte[] world,
        CancellationToken cancellationToken,
        RaceRoutePayload? route = null)
    {
        await using var stream = new MemoryStream(world, writable: false);
        return await uploads.PublishAsync(
            new RaceWorldUploadRequest(
                operationId,
                roomCode,
                "host",
                fileName,
                route ?? Route(),
                WorldSettings(),
                new RaceSeedAssignment("1234", RaceSeedSource.Fixed)),
            stream,
            cancellationToken);
    }

    private static int StoredWorldFileCount(string worldRoot, string roomCode)
    {
        string roomDirectory = Path.Combine(worldRoot, roomCode);
        return Directory.Exists(roomDirectory)
            ? Directory.EnumerateFiles(roomDirectory, "world-*.wld").Count()
            : 0;
    }

    private static RaceRoutePayload Route() => new(
        "route-hash",
        "Route",
        "{}",
        [new RaceSplitDefinition(0, "split-0", "Final")]);

    private static RaceWorldSettings WorldSettings() => new(
        "1.4.4.9",
        1,
        1,
        true,
        0,
        RaceCheatSettings.Disabled,
        "race");

    private static T Success<T>(RaceOperationResult<T> result)
    {
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException($"Expected success, got {result.ErrorCode}: {result.Message}");
        }

        return result.Value;
    }

    private sealed class RetryRecordingHandler : HttpMessageHandler
    {
        private readonly RaceRoomState responseState;
        private int requestCount;

        public RetryRecordingHandler(RaceRoomState responseState)
        {
            this.responseState = responseState;
        }

        public List<string> OperationIds { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            OperationIds.Add(await ReadMultipartFieldAsync(
                request.Content,
                RaceWorldUploadProtocol.OperationIdFormField,
                cancellationToken));
            requestCount++;
            if (requestCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("temporary upload failure", Encoding.UTF8)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(RaceOperationResult<RaceRoomState>.Success(responseState))
            };
        }

        private static async Task<string> ReadMultipartFieldAsync(
            HttpContent? content,
            string name,
            CancellationToken cancellationToken)
        {
            if (content is not MultipartFormDataContent multipart)
            {
                return string.Empty;
            }

            foreach (HttpContent part in multipart)
            {
                string partName = part.Headers.ContentDisposition?.Name?.Trim('"') ?? string.Empty;
                if (string.Equals(partName, name, StringComparison.Ordinal))
                {
                    return await part.ReadAsStringAsync(cancellationToken);
                }
            }

            return string.Empty;
        }
    }
}
