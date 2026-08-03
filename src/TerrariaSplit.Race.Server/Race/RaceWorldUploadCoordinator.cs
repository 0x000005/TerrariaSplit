using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Server;

public sealed record RaceWorldUploadRequest(
    Guid OperationId,
    string RoomCode,
    string Nickname,
    string FileName,
    RaceRoutePayload Route,
    RaceWorldSettings WorldSettings,
    RaceSeedAssignment? Seed);

public sealed record RaceWorldUploadOutcome(
    RaceOperationResult<RaceRoomState> Result,
    bool PackageChanged);

public sealed class RaceWorldUploadCoordinator
{
    private const int MaximumRememberedOperationsPerRoom = 64;
    private readonly ConcurrentDictionary<string, RoomUploadState> roomStates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly RaceRoomManager rooms;
    private readonly RaceWorldFileStore worldFiles;

    public RaceWorldUploadCoordinator(
        RaceRoomManager rooms,
        RaceWorldFileStore worldFiles)
    {
        this.rooms = rooms;
        this.worldFiles = worldFiles;
    }

    public async Task<RaceWorldUploadOutcome> PublishAsync(
        RaceWorldUploadRequest request,
        Stream source,
        CancellationToken cancellationToken)
    {
        if (request.OperationId == Guid.Empty)
        {
            return Failure(RaceErrors.InvalidRequest, "Upload operation id is required.");
        }

        string roomCode = NormalizeRoomCode(request.RoomCode);
        RoomUploadState roomState = roomStates.GetOrAdd(
            roomCode,
            static _ => new RoomUploadState());
        await roomState.Gate.WaitAsync(cancellationToken);
        RaceStoredWorldFile? stored = null;
        try
        {
            RaceOperationResult<RaceRoomState> authorization = rooms.AuthorizeWorldUpload(
                roomCode,
                request.Nickname);
            if (!authorization.Succeeded)
            {
                return new RaceWorldUploadOutcome(authorization, PackageChanged: false);
            }

            stored = await worldFiles.SaveAsync(
                roomCode,
                request.Nickname,
                request.FileName,
                source,
                cancellationToken);
            string fingerprint = CreateFingerprint(request, stored.Info);

            if (roomState.TryGetReceipt(request.OperationId, out string previousFingerprint))
            {
                DiscardStagedFileIfUnreferenced(roomCode, stored);
                if (!string.Equals(previousFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return Failure(
                        RaceErrors.UploadOperationConflict,
                        "The upload operation id was already used for different package content.");
                }

                RaceOperationResult<RaceRoomState> current = rooms.GetRoomState(roomCode);
                return new RaceWorldUploadOutcome(current, PackageChanged: false);
            }

            RaceOperationResult<RaceRoomState> result = rooms.PublishWorldFile(
                new RaceWorldFilePublishRequest(
                    roomCode,
                    request.Nickname,
                    request.Route,
                    request.WorldSettings,
                    request.Seed,
                    stored.Info),
                out RaceWorldFileInfo? replacedWorldFile);
            if (!result.Succeeded)
            {
                DiscardStagedFileIfUnreferenced(roomCode, stored);
                return new RaceWorldUploadOutcome(result, PackageChanged: false);
            }

            roomState.Remember(request.OperationId, fingerprint);
            if (replacedWorldFile is not null &&
                !HasSameStorageIdentity(replacedWorldFile, stored.Info))
            {
                worldFiles.DeleteStoredFile(roomCode, replacedWorldFile);
            }

            return new RaceWorldUploadOutcome(result, PackageChanged: true);
        }
        catch
        {
            if (stored is not null)
            {
                DiscardStagedFileIfUnreferenced(roomCode, stored);
            }

            throw;
        }
        finally
        {
            roomState.Gate.Release();
        }
    }

    public async Task DeleteRoomAsync(
        string roomCode,
        CancellationToken cancellationToken = default)
    {
        string normalizedRoomCode = NormalizeRoomCode(roomCode);
        RoomUploadState roomState = roomStates.GetOrAdd(
            normalizedRoomCode,
            static _ => new RoomUploadState());
        await roomState.Gate.WaitAsync(cancellationToken);
        try
        {
            worldFiles.DeleteRoom(normalizedRoomCode);
            roomState.ClearReceipts();
        }
        finally
        {
            roomState.Gate.Release();
        }
    }

    private void DiscardStagedFileIfUnreferenced(
        string roomCode,
        RaceStoredWorldFile stored)
    {
        if (!stored.WasCreated)
        {
            return;
        }

        RaceOperationResult<RaceRoomState> current = rooms.GetRoomState(roomCode);
        if (current.Succeeded &&
            current.Value?.WorldFile is RaceWorldFileInfo currentWorldFile &&
            HasSameStorageIdentity(currentWorldFile, stored.Info))
        {
            return;
        }

        worldFiles.DeleteStoredFile(stored);
    }

    private static string CreateFingerprint(
        RaceWorldUploadRequest request,
        RaceWorldFileInfo worldFile)
    {
        var payload = new UploadFingerprintPayload(
            request.Nickname.Trim(),
            worldFile.FileName,
            worldFile.Length,
            worldFile.Sha256,
            request.Route,
            request.WorldSettings,
            request.Seed);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload);
        return Convert.ToHexString(SHA256.HashData(json));
    }

    private static bool HasSameStorageIdentity(
        RaceWorldFileInfo left,
        RaceWorldFileInfo right)
    {
        return left.Length == right.Length &&
            string.Equals(left.Sha256, right.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static RaceWorldUploadOutcome Failure(string errorCode, string message)
    {
        return new RaceWorldUploadOutcome(
            RaceOperationResult<RaceRoomState>.Failure(errorCode, message),
            PackageChanged: false);
    }

    private static string NormalizeRoomCode(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private sealed class RoomUploadState
    {
        private readonly Dictionary<Guid, string> receipts = [];
        private readonly Queue<Guid> receiptOrder = [];

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public bool TryGetReceipt(Guid operationId, out string fingerprint)
        {
            return receipts.TryGetValue(operationId, out fingerprint!);
        }

        public void Remember(Guid operationId, string fingerprint)
        {
            if (!receipts.TryAdd(operationId, fingerprint))
            {
                return;
            }

            receiptOrder.Enqueue(operationId);
            while (receiptOrder.Count > MaximumRememberedOperationsPerRoom)
            {
                receipts.Remove(receiptOrder.Dequeue());
            }
        }

        public void ClearReceipts()
        {
            receipts.Clear();
            receiptOrder.Clear();
        }
    }

    private sealed record UploadFingerprintPayload(
        string Nickname,
        string FileName,
        long Length,
        string Sha256,
        RaceRoutePayload Route,
        RaceWorldSettings WorldSettings,
        RaceSeedAssignment? Seed);
}
