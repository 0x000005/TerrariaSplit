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
    private readonly RaceWorldFileStore worldFiles;
    private readonly IHubContext<RaceHub> hubContext;
    private readonly ILogger<RaceRoomCleanupService> logger;

    public RaceRoomCleanupService(
        RaceRoomManager rooms,
        RaceWorldFileStore worldFiles,
        IHubContext<RaceHub> hubContext,
        ILogger<RaceRoomCleanupService> logger)
    {
        this.rooms = rooms;
        this.worldFiles = worldFiles;
        this.hubContext = hubContext;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ScanInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            IReadOnlyList<RaceRoomState> closed = rooms.CloseInactiveRooms(
                DateTimeOffset.UtcNow - MaximumIdleTime);
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
                    worldFiles.DeleteRoom(state.RoomCode);
                }
            }
        }
    }
}
