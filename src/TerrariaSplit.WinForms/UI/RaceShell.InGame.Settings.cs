using TerrariaSplit.Race.Contracts;
using TerrariaSplit.Terraria.Automation;

namespace TerrariaSplit.UI;

internal sealed partial class RaceShell
{
    private RaceWorldSettings BuildInGameWorldSettings(RaceWorldSetupSettings setup)
    {
        bool fixedSeed = string.Equals(
            setup.Source,
            RacePreferredWorldSource.CustomSeed,
            StringComparison.OrdinalIgnoreCase);
        bool advancedFiltersEligible =
            !fixedSeed && AutoCreateAdvancedFilterEligibility.IsEligible(setup);
        bool hasCrimson = setup.WorldEvil switch
        {
            AutoCreateWorldEvil.Corruption => false,
            AutoCreateWorldEvil.Random => Random.Shared.Next(2) == 0,
            _ => true
        };
        RaceCheatSettings cheats = new(
            !fixedSeed,
            !fixedSeed && setup.PyramidEnabled,
            setup.PyramidItemMask,
            advancedFiltersEligible && setup.CrimsonEnabled,
            setup.CrimsonDistance,
            0,
            0,
            0,
            0,
            advancedFiltersEligible
                ? setup.JungleRouteDepth
                : AutoCreateJungleRouteDepth.None);
        int worldDifficultyCode =
            TerrariaWorldSeedOptions.CopiedDifficultyCode(setup.WorldDifficulty);
        return new RaceWorldSettings(
            getTerrariaVersion() ?? string.Empty,
            TerrariaWorldSeedOptions.SizeCode(setup.WorldSize),
            worldDifficultyCode,
            hasCrimson,
            TerrariaWorldSeedOptions.SpecialSeedMask(setup.SpecialSeeds),
            cheats,
            SecretSeeds: setup.SecretSeeds,
            PlayerDifficultyCode:
                RacePlayerDifficultyCodes.ForWorldDifficulty(worldDifficultyCode),
            RngControlEnabled: setup.RngControlEnabled);
    }

    private void PersistInGameWorldSetup()
    {
        RaceWorldSetupSettings normalized = NormalizeWorldSetup(EnsureInGameWorldSetup());
        inGameWorldSetup = normalized;
        SaveDraftState(draftState with
        {
            SeedText = normalized.SeedText,
            WorldSource = string.Equals(
                normalized.Source,
                RacePreferredWorldSource.CustomSeed,
                StringComparison.OrdinalIgnoreCase)
                    ? RacePanelWorldSource.CustomSeed
                    : RacePanelWorldSource.Random
        });

        settingsCoordinator.Update(
            "Race world settings update",
            next =>
            {
                next.WorldSetup = CloneWorldSetup(normalized);
            });
    }

    private void ToggleSpecialSeed(string seed)
    {
        RaceWorldSetupSettings setup = EnsureInGameWorldSetup();
        List<string> selected = AutoCreateSpecialWorldSeed.ParseList(setup.SpecialSeeds).ToList();
        int removed = selected.RemoveAll(item =>
            string.Equals(item, seed, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            selected.Add(seed);
        }

        setup.SpecialSeeds = string.Join("|", selected);
        PersistInGameWorldSetup();
    }

    private void ToggleMask(
        int bit,
        Func<RaceWorldSetupSettings, int> get,
        Action<RaceWorldSetupSettings, int> set)
    {
        RaceWorldSetupSettings setup = EnsureInGameWorldSetup();
        set(setup, get(setup) ^ bit);
        PersistInGameWorldSetup();
    }

    private RaceWorldSetupSettings EnsureInGameWorldSetup()
    {
        return inGameWorldSetup ??= NormalizeWorldSetup(
            CloneWorldSetup(getSettings().Race?.WorldSetup));
    }

    private static RaceWorldSetupSettings NormalizeWorldSetup(RaceWorldSetupSettings source)
    {
        RaceWorldSetupSettings setup = CloneWorldSetup(source);
        setup.Source = string.Equals(
            setup.Source,
            RacePreferredWorldSource.CustomSeed,
            StringComparison.OrdinalIgnoreCase)
                ? RacePreferredWorldSource.CustomSeed
                : RacePreferredWorldSource.Random;
        setup.SeedText = setup.SeedText.Trim();
        setup.WorldSize = AutoCreateWorldSize.Normalize(setup.WorldSize);
        setup.WorldDifficulty = AutoCreateWorldDifficulty.Normalize(setup.WorldDifficulty);
        setup.WorldEvil = AutoCreateWorldEvil.Normalize(setup.WorldEvil);
        setup.SpecialSeeds = string.Join("|", AutoCreateSpecialWorldSeed.ParseList(setup.SpecialSeeds));
        setup.SecretSeeds = setup.SecretSeeds.Trim();
        setup.PyramidItemMask = AutoCreatePyramidFilterItem.NormalizeMask(setup.PyramidItemMask);
        setup.CheatsEnabled = true;
        setup.CrimsonDistance = AutoCreateCrimsonDistance.Normalize(setup.CrimsonDistance);
        setup.JungleRouteDepth = AutoCreateJungleRouteDepth.Normalize(setup.JungleRouteDepth);
        setup.ResourceItemMask = 0;
        setup.LifeCrystalMinimum = 0;
        setup.SpelunkerPotionMinimum = 0;
        setup.FeatherfallPotionMinimum = 0;
        AutoCreateAdvancedFilterEligibility.ClearUnsupportedFilters(setup);
        return setup;
    }

    private static RaceWorldSetupSettings CloneWorldSetup(RaceWorldSetupSettings? source)
    {
        source ??= new RaceWorldSetupSettings();
        return new RaceWorldSetupSettings
        {
            Source = source.Source,
            SeedText = source.SeedText ?? string.Empty,
            WorldSize = source.WorldSize,
            WorldDifficulty = source.WorldDifficulty,
            WorldEvil = source.WorldEvil,
            SpecialSeeds = source.SpecialSeeds ?? string.Empty,
            SecretSeeds = source.SecretSeeds ?? string.Empty,
            RngControlEnabled = source.RngControlEnabled,
            CheatsEnabled = source.CheatsEnabled,
            PyramidEnabled = source.PyramidEnabled,
            PyramidItemMask = source.PyramidItemMask,
            CrimsonEnabled = source.CrimsonEnabled,
            CrimsonDistance = source.CrimsonDistance,
            JungleRouteDepth = source.JungleRouteDepth,
            ResourceItemMask = source.ResourceItemMask,
            LifeCrystalMinimum = source.LifeCrystalMinimum,
            SpelunkerPotionMinimum = source.SpelunkerPotionMinimum,
            FeatherfallPotionMinimum = source.FeatherfallPotionMinimum
        };
    }

    private bool TryBeginInGameOperation(
        string status,
        out long operationId,
        out CancellationToken cancellationToken,
        bool dedicatedProgress = false)
    {
        operationId = 0;
        cancellationToken = default;
        if (Interlocked.CompareExchange(ref inGameMenuBusy, 1, 0) != 0)
        {
            return false;
        }

        operationId = Interlocked.Increment(ref inGameOperationId);
        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous = Interlocked.Exchange(
            ref inGameOperationCancellation,
            cancellation);
        previous?.Cancel();
        previous?.Dispose();
        cancellationToken = cancellation.Token;
        Interlocked.Exchange(
            ref inGameMenuDedicatedProgress,
            dedicatedProgress ? 1 : 0);
        inGameMenuStatus = status;
        Volatile.Write(ref inGameMenuProgress, 0);
        MarkInGameMenuDirty();
        return true;
    }

    private void EndInGameOperation(long operationId)
    {
        if (operationId != Volatile.Read(ref inGameOperationId))
        {
            return;
        }

        Interlocked.Exchange(ref inGameMenuBusy, 0);
        Interlocked.Exchange(ref inGameMenuDedicatedProgress, 0);
        CancellationTokenSource? cancellation = Interlocked.Exchange(
            ref inGameOperationCancellation,
            null);
        cancellation?.Dispose();
        MarkInGameMenuDirty();
    }

    private void CancelInGameOperation()
    {
        Interlocked.Increment(ref inGameOperationId);
        CancellationTokenSource? cancellation = Interlocked.Exchange(
            ref inGameOperationCancellation,
            null);
        cancellation?.Cancel();
        cancellation?.Dispose();
        Interlocked.Exchange(ref inGameMenuBusy, 0);
        Interlocked.Exchange(ref inGameMenuDedicatedProgress, 0);
        inGameMenuStatus = Localize("Canceled.");
        MarkInGameMenuDirty();
    }

    private void SetInGameOperationState(long operationId, string status, int progress)
    {
        if (operationId != Volatile.Read(ref inGameOperationId))
        {
            return;
        }

        inGameMenuStatus = status;
        Volatile.Write(ref inGameMenuProgress, Math.Clamp(progress, 0, 100));
        MarkInGameMenuDirty();
    }

    private void SetInGameOperationFailure(long operationId, string message)
    {
        SetInGameOperationState(
            operationId,
            string.IsNullOrWhiteSpace(message) ? Localize("Operation failed.") : message,
            0);
    }

    private void StopInGameMenu()
    {
        Interlocked.Exchange(ref inGameMenuAttachedOnce, 0);
        CancellationTokenSource? operation = Interlocked.Exchange(
            ref inGameOperationCancellation,
            null);
        operation?.Cancel();
        operation?.Dispose();
        Interlocked.Exchange(ref inGameMenuBusy, 0);
        Interlocked.Exchange(ref inGameMenuDedicatedProgress, 0);
        Interlocked.Increment(ref inGameOperationId);
        ResetInGameActionSnapshots();

        CancellationTokenSource? cancellation = Interlocked.Exchange(
            ref inGameMenuCancellation,
            null);
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
        _ = CloseInGameMenuBestEffortAsync();
    }

    private async Task CloseInGameMenuBestEffortAsync()
    {
        try
        {
            await worldLock.CloseRaceMenuAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        {
            logger.Info("Race menu close failed: " + ex.Message);
        }
    }

    private void MarkInGameMenuDirty()
    {
        if (Volatile.Read(ref inGameMenuCancellation) is not null)
        {
            Interlocked.Exchange(ref inGameMenuDirty, 1);
        }
    }

    private long NextInGameMenuRevision()
    {
        return Interlocked.Increment(ref inGameMenuRevision);
    }

    private void HandleInGameMenuFailure(string message)
    {
        if (Interlocked.Exchange(ref inGameMenuFailureReported, 1) != 0)
        {
            return;
        }

        CancellationTokenSource? cancellation = Volatile.Read(ref inGameMenuCancellation);
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _ = CloseInGameMenuBestEffortAsync();
        string detail = string.IsNullOrWhiteSpace(message)
            ? Localize("Unable to open the Terraria Race page.")
            : message;
        logger.Info("Terraria Race page failed: " + detail);
        _ = PostOwnerThread(() => ReportInGameMenuFailureOnOwnerThread(detail));
    }

    private void ReportInGameMenuFailureOnOwnerThread(string detail)
    {
        if (disposed || owner.IsDisposed)
        {
            return;
        }

        if (IsRaceEnabled && !IsInRoom)
        {
            SaveRaceEnabled(false);
        }

        SettingsMessageDialog.ShowThemed(
            owner,
            Localize("Race"),
            detail,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error,
            Localize);
    }

}
