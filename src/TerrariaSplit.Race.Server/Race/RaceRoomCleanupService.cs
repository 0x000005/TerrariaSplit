using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TerrariaSplit.Race.Contracts;

namespace TerrariaSplit.Race.Server;

public sealed class RaceRoomCleanupService : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaximumIdleTime = TimeSpan.FromHours(24);
    private readonly RaceRoomManager rooms;
    private readonly RaceWorldUploadCoordinator worldUploads;
    private readonly IHubContext<RaceHub> hubContext;
    private readonly ILogger<RaceRoomCleanupService> logger;
    private readonly TimeProvider timeProvider;

    public RaceRoomCleanupService(
        RaceRoomManager rooms,
        RaceWorldUploadCoordinator worldUploads,
        IHubContext<RaceHub> hubContext,
        ILogger<RaceRoomCleanupService> logger,
        TimeProvider timeProvider)
    {
        this.rooms = rooms;
        this.worldUploads = worldUploads;
        this.hubContext = hubContext;
        this.logger = logger;
        this.timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ScanInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            IReadOnlyList<RaceRoomState> closed = rooms.CloseInactiveRooms(
                timeProvider.GetUtcNow() - MaximumIdleTime);
            foreach (RaceRoomState state in closed)
            {
                try
                {
                    await hubContext.Clients.Group(state.RoomCode).SendAsync(
                        "RaceRosterChanged",
                        new RaceRosterChanged(RaceRoomStateUpdateKind.RoomClosed, state),
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Race inactive-room close broadcast failed. Room={RoomCode}", state.RoomCode);
                }
                finally
                {
                    await worldUploads.DeleteRoomAsync(state.RoomCode, CancellationToken.None);
                }
            }
        }
    }
}
