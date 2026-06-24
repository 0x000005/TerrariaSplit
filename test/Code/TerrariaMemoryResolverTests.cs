using System.Diagnostics.CodeAnalysis;

namespace TerrariaSplit.Tests;

internal static class TerrariaMemoryResolverTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("TerrariaMemoryResolver keeps primary UpdateTime menu address when readable", KeepsPrimaryUpdateTimeMenuAddressWhenReadable);
        yield return ("TerrariaMemoryResolver keeps fallback menu address before boss progression route", KeepsFallbackMenuAddressBeforeBossProgressionRoute);
        yield return ("TerrariaMemoryResolver infers menu address from boss progression route", InfersMenuAddressFromBossProgressionRoute);
        yield return ("TerrariaMemoryResolver emits boss facts from progression fallback", EmitsBossFactsFromProgressionFallback);
        yield return ("ItemFactProvider returns unknown without item layout", ItemFactProviderReturnsUnknownWithoutItemLayout);
        yield return ("ItemFactProvider aggregates inventory and bank stacks", ItemFactProviderAggregatesInventoryAndBankStacks);
        yield return ("ItemFactProvider counts mouse item once", ItemFactProviderCountsMouseItemOnce);
        yield return ("ItemFactProvider skips stale inventory cursor clone", ItemFactProviderSkipsStaleInventoryCursorClone);
        yield return ("ItemFactProvider deduplicates shared item references", ItemFactProviderDeduplicatesSharedItemReferences);
        yield return ("ItemFactProvider suppresses unconfirmed count increase", ItemFactProviderSuppressesUnconfirmedCountIncrease);
        yield return ("ItemFactProvider confirms stable count increase", ItemFactProviderConfirmsStableCountIncrease);
        yield return ("ItemFactProvider filters observed item facts", ItemFactProviderFiltersObservedItemFacts);
        yield return ("ItemFactProvider skips reads without observed item facts", ItemFactProviderSkipsReadsWithoutObservedItemFacts);
        yield return ("ItemFactProvider keeps inventory counts when optional banks are unavailable", ItemFactProviderKeepsInventoryCountsWhenOptionalBanksAreUnavailable);
        yield return ("NpcFactProvider returns unknown without NPC layout", NpcFactProviderReturnsUnknownWithoutNpcLayout);
        yield return ("NpcFactProvider emits town NPC presence facts", NpcFactProviderEmitsTownNpcPresenceFacts);
        yield return ("NpcFactProvider filters observed NPC facts", NpcFactProviderFiltersObservedNpcFacts);
        yield return ("BiomeFactProvider returns unknown without biome layout", BiomeFactProviderReturnsUnknownWithoutBiomeLayout);
        yield return ("BiomeFactProvider emits derived biome facts", BiomeFactProviderEmitsDerivedBiomeFacts);
        yield return ("BiomeFactProvider reads only observed biome zone fields", BiomeFactProviderReadsOnlyObservedBiomeZoneFields);
    }

    private static void KeepsPrimaryUpdateTimeMenuAddressWhenReadable()
    {
        var resolver = new TerrariaMemoryResolver(Terraria1456Memory.Profile);
        var memory = new FakeProcessMemoryReader(is64Bit: false);

        memory.WriteExecutablePage(new IntPtr(0x2000), BuildUpdateTimeCode(new IntPtr(0x4000)));
        memory.WriteBool(new IntPtr(0x4000), true);

        TerrariaMemoryResolveResult result = resolver.Resolve(memory);
        TerrariaMemoryResolution resolution = resolver.Resolution;

        TestAssert.Equal(true, result.ObservedGameMenu);
        TestAssert.Equal(new IntPtr(0x4000), resolution.GameMenuAddress);
        TestAssert.Equal(IntPtr.Zero, resolution.GameMenuSecondaryAddress);
        TestAssert.Equal(false, resolution.UsingBossProgressionMenuFallback);
        TestAssert.Equal(false, resolution.UsingGameMenuFallback);
    }

    private static void KeepsFallbackMenuAddressBeforeBossProgressionRoute()
    {
        var resolver = new TerrariaMemoryResolver(Terraria1456Memory.Profile);
        var memory = new FakeProcessMemoryReader(is64Bit: false);

        byte[] code = new byte[0x100];
        WriteGameMenuFallbackSignature(code, 0x00, menuModeAddress: new IntPtr(0x3000), gameMenuAddress: new IntPtr(0x4000));
        WriteBossProgressionSignature(code, 0x40, skeletronAddress: new IntPtr(0x5000), hardmodeAddress: new IntPtr(0x6000));

        memory.WriteExecutablePage(new IntPtr(0x2000), code);
        memory.WriteBool(new IntPtr(0x4000), false);
        memory.WriteBool(new IntPtr(0x5000), true);
        memory.WriteBool(new IntPtr(0x5011), true);
        memory.WriteBool(new IntPtr(0x6000), true);
        memory.WriteBool(new IntPtr(0x604E), true);

        TerrariaMemoryResolveResult result = resolver.Resolve(memory);
        TerrariaMemoryResolution resolution = resolver.Resolution;

        TestAssert.Equal(false, result.ObservedGameMenu);
        TestAssert.Equal(new IntPtr(0x4000), resolution.GameMenuAddress);
        TestAssert.Equal(false, resolution.UsingBossProgressionMenuFallback);
        TestAssert.Equal(true, resolution.UsingGameMenuFallback);
    }

    private static void InfersMenuAddressFromBossProgressionRoute()
    {
        var resolver = new TerrariaMemoryResolver(Terraria1456Memory.Profile);
        var memory = new FakeProcessMemoryReader(is64Bit: false);

        memory.WriteExecutablePage(
            new IntPtr(0x2000),
            BuildBossProgressionCode(skeletronAddress: new IntPtr(0x5000), hardmodeAddress: new IntPtr(0x6000)));
        for (int offset = -4; offset <= 31; offset++)
        {
            memory.WriteBool(IntPtr.Add(new IntPtr(0x5002), offset), false);
        }

        memory.WriteBool(new IntPtr(0x5000), true);
        memory.WriteBool(new IntPtr(0x5011), true);
        memory.WriteBool(new IntPtr(0x6000), true);
        memory.WriteBool(new IntPtr(0x604E), false);

        TerrariaMemoryResolveResult result = resolver.Resolve(memory);
        TerrariaMemoryResolution resolution = resolver.Resolution;

        TestAssert.Equal(false, result.ObservedGameMenu);
        TestAssert.Equal(new IntPtr(0x604E), resolution.GameMenuAddress);
        TestAssert.Equal(new IntPtr(0x5002), resolution.BossFlagsBaseAddress);
        TestAssert.Equal(new IntPtr(0x6000), resolution.HardmodeAddress);
        TestAssert.Equal(true, resolution.UsingBossProgressionMenuFallback);
        TestAssert.Equal(true, resolution.UsingBossProgressionFallback);
        TestAssert.Equal(false, resolution.UsingGameMenuFallback);
    }

    private static void EmitsBossFactsFromProgressionFallback()
    {
        var resolver = new TerrariaMemoryResolver(Terraria1456Memory.Profile);
        var memory = new FakeProcessMemoryReader(is64Bit: false);

        memory.WriteExecutablePage(
            new IntPtr(0x2000),
            BuildBossProgressionCode(skeletronAddress: new IntPtr(0x5000), hardmodeAddress: new IntPtr(0x6000)));
        for (int offset = -4; offset <= 31; offset++)
        {
            memory.WriteBool(IntPtr.Add(new IntPtr(0x5002), offset), false);
        }

        memory.WriteBool(new IntPtr(0x5000), true);
        memory.WriteBool(new IntPtr(0x5011), true);
        memory.WriteBool(new IntPtr(0x6000), true);
        memory.WriteBool(new IntPtr(0x604E), false);

        _ = resolver.Resolve(memory);
        TerrariaGameFacts facts = resolver.ReadGameFacts(memory);

        TestAssert.Equal(true, GetBossFact(facts, SplitCatalog.Skeletron));
        TestAssert.Equal(true, GetBossFact(facts, SplitCatalog.MoonLord));
        TestAssert.Equal(true, GetBossFact(facts, SplitCatalog.WallOfFlesh));
    }

    private static void ItemFactProviderReturnsUnknownWithoutItemLayout()
    {
        var provider = new ItemFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);

        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, IntPtr.Zero, null, null, null, Is64Bit: false));

        TestAssert.Equal(FactValueKind.Unknown, facts.Get(SplitCatalog.CreateItemFactKey(50)).Kind);
    }

    private static void ItemFactProviderAggregatesInventoryAndBankStacks()
    {
        var provider = new ItemFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaItemMemoryLayout layout = CreateTestItemLayout();
        IntPtr player = new(0x1000);
        IntPtr playerArray = new(0x1800);
        IntPtr inventory = new(0x2000);
        IntPtr bank = new(0x3000);
        IntPtr bankItems = new(0x4000);

        memory.WriteInt32(layout.MyPlayerStaticFieldAddress, 0);
        memory.WritePointerValue(layout.PlayerArrayStaticFieldAddress, playerArray);
        memory.WriteInt32(IntPtr.Add(playerArray, layout.ManagedArrayLengthOffset), 1);
        memory.WritePointerValue(IntPtr.Add(playerArray, layout.ManagedArrayFirstElementOffset), player);

        WriteEmptyItemArray(memory, layout, player, layout.PlayerArmorFieldOffset, new IntPtr(0x2100));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerDyeFieldOffset, new IntPtr(0x2200));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscEquipsFieldOffset, new IntPtr(0x2300));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscDyesFieldOffset, new IntPtr(0x2400));
        WriteEmptyItem(memory, layout, player, layout.PlayerTrashItemFieldOffset, new IntPtr(0x5100));

        memory.WritePointerValue(IntPtr.Add(player, layout.PlayerInventoryFieldOffset), inventory);
        memory.WriteInt32(IntPtr.Add(inventory, layout.ManagedArrayLengthOffset), 2);
        memory.WritePointerValue(IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset), new IntPtr(0x5000));
        memory.WritePointerValue(IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset + layout.ObjectReferenceSize), new IntPtr(0x5020));
        WriteItem(memory, layout, new IntPtr(0x5000), itemId: 50, stack: 3);
        WriteItem(memory, layout, new IntPtr(0x5020), itemId: 51, stack: 1);

        memory.WritePointerValue(IntPtr.Add(player, layout.PlayerBankFieldOffset), bank);
        memory.WritePointerValue(IntPtr.Add(bank, layout.ChestItemArrayFieldOffset), bankItems);
        memory.WriteInt32(IntPtr.Add(bankItems, layout.ManagedArrayLengthOffset), 1);
        memory.WritePointerValue(IntPtr.Add(bankItems, layout.ManagedArrayFirstElementOffset), new IntPtr(0x5040));
        WriteItem(memory, layout, new IntPtr(0x5040), itemId: 50, stack: 4);

        WriteEmptyBank(memory, layout, player, layout.PlayerBank2FieldOffset, new IntPtr(0x3100), new IntPtr(0x4100));
        WriteEmptyBank(memory, layout, player, layout.PlayerBank3FieldOffset, new IntPtr(0x3200), new IntPtr(0x4200));
        WriteEmptyBank(memory, layout, player, layout.PlayerBank4FieldOffset, new IntPtr(0x3300), new IntPtr(0x4300));

        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, IntPtr.Zero, layout, null, null, Is64Bit: false));

        TestAssert.Equal(7, facts.Get(SplitCatalog.CreateItemFactKey(50)).AsInteger());
        TestAssert.Equal(1, facts.Get(SplitCatalog.CreateItemFactKey(51)).AsInteger());
        TestAssert.Equal(0, facts.Get(SplitCatalog.CreateItemFactKey(52)).AsInteger());
    }

    private static void ItemFactProviderCountsMouseItemOnce()
    {
        var provider = new ItemFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaItemMemoryLayout layout = CreateTestItemLayout();
        IntPtr player = new(0x1000);
        IntPtr inventory = new(0x2000);
        IntPtr inventoryStack = new(0x5000);
        IntPtr cursorClone = new(0x5100);
        IntPtr mouseItem = new(0x5200);

        WriteEmptyItemArray(memory, layout, player, layout.PlayerArmorFieldOffset, new IntPtr(0x2100));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerDyeFieldOffset, new IntPtr(0x2200));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscEquipsFieldOffset, new IntPtr(0x2300));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscDyesFieldOffset, new IntPtr(0x2400));
        memory.WritePointerValue(IntPtr.Add(player, layout.PlayerInventoryFieldOffset), inventory);
        memory.WriteInt32(IntPtr.Add(inventory, layout.ManagedArrayLengthOffset), 59);
        memory.WritePointerValue(IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset), inventoryStack);
        memory.WritePointerValue(
            IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset + 58 * layout.ObjectReferenceSize),
            cursorClone);
        memory.WritePointerValue(layout.MouseItemStaticFieldAddress, mouseItem);
        WriteItem(memory, layout, inventoryStack, itemId: 50, stack: 48);
        WriteItem(memory, layout, cursorClone, itemId: 50, stack: 1);
        WriteItem(memory, layout, mouseItem, itemId: 50, stack: 1);

        TerrariaFactReadPlan readPlan = TerrariaFactReadPlan.FromObservedFactKeys(
            [SplitCatalog.CreateItemEverOwnedFactKey(50)]);
        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, player, layout, null, null, Is64Bit: false),
            readPlan);

        TestAssert.Equal(49, facts.Get(SplitCatalog.CreateItemFactKey(50)).AsInteger());
        TestAssert.Equal(49, facts.Get(SplitCatalog.CreateItemEverOwnedFactKey(50)).AsInteger());
    }

    private static void ItemFactProviderSkipsStaleInventoryCursorClone()
    {
        var provider = new ItemFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaItemMemoryLayout layout = CreateTestItemLayout();
        IntPtr player = new(0x1000);
        IntPtr inventory = new(0x2000);
        IntPtr inventoryStack = new(0x5000);
        IntPtr cursorClone = new(0x5100);
        IntPtr emptyMouseItem = new(0x5200);

        WriteEmptyItemArray(memory, layout, player, layout.PlayerArmorFieldOffset, new IntPtr(0x2100));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerDyeFieldOffset, new IntPtr(0x2200));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscEquipsFieldOffset, new IntPtr(0x2300));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscDyesFieldOffset, new IntPtr(0x2400));
        memory.WritePointerValue(IntPtr.Add(player, layout.PlayerInventoryFieldOffset), inventory);
        memory.WriteInt32(IntPtr.Add(inventory, layout.ManagedArrayLengthOffset), 59);
        memory.WritePointerValue(IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset), inventoryStack);
        memory.WritePointerValue(
            IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset + 58 * layout.ObjectReferenceSize),
            cursorClone);
        memory.WritePointerValue(layout.MouseItemStaticFieldAddress, emptyMouseItem);
        WriteItem(memory, layout, inventoryStack, itemId: 50, stack: 49);
        WriteItem(memory, layout, cursorClone, itemId: 50, stack: 1);
        WriteItem(memory, layout, emptyMouseItem, itemId: 0, stack: 0);

        TerrariaFactReadPlan readPlan = TerrariaFactReadPlan.FromObservedFactKeys(
            [SplitCatalog.CreateItemEverOwnedFactKey(50)]);
        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, player, layout, null, null, Is64Bit: false),
            readPlan);

        TestAssert.Equal(49, facts.Get(SplitCatalog.CreateItemFactKey(50)).AsInteger());
        TestAssert.Equal(49, facts.Get(SplitCatalog.CreateItemEverOwnedFactKey(50)).AsInteger());
    }

    private static void ItemFactProviderDeduplicatesSharedItemReferences()
    {
        var provider = new ItemFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaItemMemoryLayout layout = CreateTestItemLayout();
        IntPtr player = new(0x1000);
        IntPtr inventory = new(0x2000);
        IntPtr sharedStack = new(0x5000);

        WriteEmptyItemArray(memory, layout, player, layout.PlayerArmorFieldOffset, new IntPtr(0x2100));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerDyeFieldOffset, new IntPtr(0x2200));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscEquipsFieldOffset, new IntPtr(0x2300));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscDyesFieldOffset, new IntPtr(0x2400));
        memory.WritePointerValue(IntPtr.Add(player, layout.PlayerInventoryFieldOffset), inventory);
        memory.WriteInt32(IntPtr.Add(inventory, layout.ManagedArrayLengthOffset), 2);
        memory.WritePointerValue(IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset), sharedStack);
        memory.WritePointerValue(
            IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset + layout.ObjectReferenceSize),
            sharedStack);
        WriteItem(memory, layout, sharedStack, itemId: 50, stack: 1);

        TerrariaFactReadPlan readPlan = TerrariaFactReadPlan.FromObservedFactKeys(
            [SplitCatalog.CreateItemEverOwnedFactKey(50)]);
        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, player, layout, null, null, Is64Bit: false),
            readPlan);

        TestAssert.Equal(1, facts.Get(SplitCatalog.CreateItemFactKey(50)).AsInteger());
        TestAssert.Equal(1, facts.Get(SplitCatalog.CreateItemEverOwnedFactKey(50)).AsInteger());
    }

    private static void ItemFactProviderSuppressesUnconfirmedCountIncrease()
    {
        var provider = new ItemFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaItemMemoryLayout layout = CreateTestItemLayout();
        IntPtr player = new(0x1000);
        IntPtr inventoryStack = new(0x5000);
        IntPtr mouseItem = new(0x5100);
        TerrariaFactReadPlan readPlan = TerrariaFactReadPlan.FromObservedFactKeys(
            [SplitCatalog.CreateItemEverOwnedFactKey(50)]);

        WriteBasicInventory(memory, layout, player, inventoryStack, mouseItem);
        WriteItem(memory, layout, inventoryStack, itemId: 50, stack: 48);
        WriteItem(memory, layout, mouseItem, itemId: 0, stack: 0);

        TerrariaGameFacts initialFacts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, player, layout, null, null, Is64Bit: false),
            readPlan);

        WriteItem(memory, layout, inventoryStack, itemId: 50, stack: 48);
        WriteItem(memory, layout, mouseItem, itemId: 50, stack: 2);
        TerrariaGameFacts transientFacts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, player, layout, null, null, Is64Bit: false),
            readPlan);

        WriteItem(memory, layout, inventoryStack, itemId: 50, stack: 48);
        WriteItem(memory, layout, mouseItem, itemId: 0, stack: 0);
        TerrariaGameFacts settledFacts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, player, layout, null, null, Is64Bit: false),
            readPlan);

        TestAssert.Equal(48, initialFacts.Get(SplitCatalog.CreateItemEverOwnedFactKey(50)).AsInteger());
        TestAssert.Equal(48, transientFacts.Get(SplitCatalog.CreateItemEverOwnedFactKey(50)).AsInteger());
        TestAssert.Equal(48, settledFacts.Get(SplitCatalog.CreateItemEverOwnedFactKey(50)).AsInteger());
    }

    private static void ItemFactProviderConfirmsStableCountIncrease()
    {
        var provider = new ItemFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaItemMemoryLayout layout = CreateTestItemLayout();
        IntPtr player = new(0x1000);
        IntPtr inventoryStack = new(0x5000);
        IntPtr mouseItem = new(0x5100);
        TerrariaFactReadPlan readPlan = TerrariaFactReadPlan.FromObservedFactKeys(
            [SplitCatalog.CreateItemEverOwnedFactKey(50)]);

        WriteBasicInventory(memory, layout, player, inventoryStack, mouseItem);
        WriteItem(memory, layout, inventoryStack, itemId: 50, stack: 48);
        WriteItem(memory, layout, mouseItem, itemId: 0, stack: 0);
        _ = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, player, layout, null, null, Is64Bit: false),
            readPlan);

        WriteItem(memory, layout, inventoryStack, itemId: 50, stack: 48);
        WriteItem(memory, layout, mouseItem, itemId: 50, stack: 2);
        TerrariaGameFacts firstIncreasedFacts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, player, layout, null, null, Is64Bit: false),
            readPlan);
        TerrariaGameFacts confirmedFacts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, player, layout, null, null, Is64Bit: false),
            readPlan);

        TestAssert.Equal(48, firstIncreasedFacts.Get(SplitCatalog.CreateItemEverOwnedFactKey(50)).AsInteger());
        TestAssert.Equal(50, confirmedFacts.Get(SplitCatalog.CreateItemEverOwnedFactKey(50)).AsInteger());
    }

    private static void ItemFactProviderFiltersObservedItemFacts()
    {
        var provider = new ItemFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaItemMemoryLayout layout = CreateTestItemLayout();
        IntPtr player = new(0x1000);
        IntPtr inventory = new(0x2000);
        IntPtr firstItem = new(0x5000);
        IntPtr secondItem = new(0x5020);

        WriteEmptyItemArray(memory, layout, player, layout.PlayerArmorFieldOffset, new IntPtr(0x2100));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerDyeFieldOffset, new IntPtr(0x2200));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscEquipsFieldOffset, new IntPtr(0x2300));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscDyesFieldOffset, new IntPtr(0x2400));
        memory.WritePointerValue(IntPtr.Add(player, layout.PlayerInventoryFieldOffset), inventory);
        memory.WriteInt32(IntPtr.Add(inventory, layout.ManagedArrayLengthOffset), 2);
        memory.WritePointerValue(IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset), firstItem);
        memory.WritePointerValue(IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset + layout.ObjectReferenceSize), secondItem);
        WriteItem(memory, layout, firstItem, itemId: 50, stack: 3);
        WriteItem(memory, layout, secondItem, itemId: 51, stack: 4);

        TerrariaFactReadPlan readPlan = TerrariaFactReadPlan.FromObservedFactKeys(
            [SplitCatalog.CreateItemEverOwnedFactKey(50)]);
        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, player, layout, null, null, Is64Bit: false),
            readPlan);

        TestAssert.Equal(2, facts.Values.Count);
        TestAssert.Equal(3, facts.Get(SplitCatalog.CreateItemFactKey(50)).AsInteger());
        TestAssert.Equal(3, facts.Get(SplitCatalog.CreateItemEverOwnedFactKey(50)).AsInteger());
        TestAssert.Equal(FactValueKind.Unknown, facts.Get(SplitCatalog.CreateItemFactKey(51)).Kind);
        TestAssert.Equal(FactValueKind.Unknown, facts.Get(SplitCatalog.CreateItemEverOwnedFactKey(51)).Kind);
    }

    private static void ItemFactProviderSkipsReadsWithoutObservedItemFacts()
    {
        var provider = new ItemFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaItemMemoryLayout layout = CreateTestItemLayout();
        TerrariaFactReadPlan readPlan = TerrariaFactReadPlan.FromObservedFactKeys(
            [SplitCatalog.BossFacts[0].FactKey]);

        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, new IntPtr(0x1000), layout, null, null, Is64Bit: false),
            readPlan);

        TestAssert.Equal(0, facts.Values.Count);
        TestAssert.Equal(0, memory.ReadBytesCallCount);
    }

    private static void ItemFactProviderKeepsInventoryCountsWhenOptionalBanksAreUnavailable()
    {
        var provider = new ItemFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaItemMemoryLayout layout = CreateTestItemLayout();
        IntPtr player = new(0x1000);
        IntPtr playerArray = new(0x1800);
        IntPtr inventory = new(0x2000);

        memory.WriteInt32(layout.MyPlayerStaticFieldAddress, 0);
        memory.WritePointerValue(layout.PlayerArrayStaticFieldAddress, playerArray);
        memory.WriteInt32(IntPtr.Add(playerArray, layout.ManagedArrayLengthOffset), 1);
        memory.WritePointerValue(IntPtr.Add(playerArray, layout.ManagedArrayFirstElementOffset), player);

        WriteEmptyItemArray(memory, layout, player, layout.PlayerArmorFieldOffset, new IntPtr(0x2100));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerDyeFieldOffset, new IntPtr(0x2200));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscEquipsFieldOffset, new IntPtr(0x2300));
        WriteEmptyItemArray(memory, layout, player, layout.PlayerMiscDyesFieldOffset, new IntPtr(0x2400));

        memory.WritePointerValue(IntPtr.Add(player, layout.PlayerInventoryFieldOffset), inventory);
        memory.WriteInt32(IntPtr.Add(inventory, layout.ManagedArrayLengthOffset), 1);
        memory.WritePointerValue(IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset), new IntPtr(0x5000));
        WriteItem(memory, layout, new IntPtr(0x5000), itemId: 50, stack: 3);

        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, IntPtr.Zero, layout, null, null, Is64Bit: false));

        TestAssert.Equal(3, facts.Get(SplitCatalog.CreateItemFactKey(50)).AsInteger());
        TestAssert.Equal(0, facts.Get(SplitCatalog.CreateItemFactKey(51)).AsInteger());
    }

    private static void NpcFactProviderReturnsUnknownWithoutNpcLayout()
    {
        var provider = new NpcFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);

        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, IntPtr.Zero, null, null, null, Is64Bit: false));

        TestAssert.Equal(FactValueKind.Unknown, facts.Get(SplitCatalog.CreateNpcPresentFactKey(17)).Kind);
    }

    private static void NpcFactProviderEmitsTownNpcPresenceFacts()
    {
        var provider = new NpcFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaNpcMemoryLayout layout = CreateTestNpcLayout();
        IntPtr npcArray = new(0xA000);
        IntPtr merchant = new(0xA100);
        IntPtr activeEnemyWithMerchantId = new(0xA200);
        IntPtr guideInactive = new(0xA300);
        IntPtr guide = new(0xA400);

        memory.WritePointerValue(layout.NpcArrayStaticFieldAddress, npcArray);
        memory.WriteInt32(IntPtr.Add(npcArray, layout.ManagedArrayLengthOffset), 4);
        memory.WritePointerValue(IntPtr.Add(npcArray, layout.ManagedArrayFirstElementOffset), merchant);
        memory.WritePointerValue(IntPtr.Add(npcArray, layout.ManagedArrayFirstElementOffset + layout.ObjectReferenceSize), activeEnemyWithMerchantId);
        memory.WritePointerValue(IntPtr.Add(npcArray, layout.ManagedArrayFirstElementOffset + layout.ObjectReferenceSize * 2), guideInactive);
        memory.WritePointerValue(IntPtr.Add(npcArray, layout.ManagedArrayFirstElementOffset + layout.ObjectReferenceSize * 3), guide);
        WriteNpc(memory, layout, merchant, npcId: 17, active: true, townNpc: true);
        WriteNpc(memory, layout, activeEnemyWithMerchantId, npcId: 18, active: true, townNpc: false);
        WriteNpc(memory, layout, guideInactive, npcId: 22, active: false, townNpc: true);
        WriteNpc(memory, layout, guide, npcId: 22, active: true, townNpc: true);

        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, IntPtr.Zero, null, layout, null, Is64Bit: false));

        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateNpcPresentFactKey(17)).AsBoolean());
        TestAssert.Equal(false, facts.Get(SplitCatalog.CreateNpcPresentFactKey(18)).AsBoolean());
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateNpcPresentFactKey(22)).AsBoolean());
    }

    private static void NpcFactProviderFiltersObservedNpcFacts()
    {
        var provider = new NpcFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaNpcMemoryLayout layout = CreateTestNpcLayout();
        IntPtr npcArray = new(0xA000);
        IntPtr merchant = new(0xA100);
        IntPtr guide = new(0xA200);

        memory.WritePointerValue(layout.NpcArrayStaticFieldAddress, npcArray);
        memory.WriteInt32(IntPtr.Add(npcArray, layout.ManagedArrayLengthOffset), 2);
        memory.WritePointerValue(IntPtr.Add(npcArray, layout.ManagedArrayFirstElementOffset), merchant);
        memory.WritePointerValue(IntPtr.Add(npcArray, layout.ManagedArrayFirstElementOffset + layout.ObjectReferenceSize), guide);
        WriteNpc(memory, layout, merchant, npcId: 17, active: true, townNpc: true);
        WriteNpc(memory, layout, guide, npcId: 22, active: true, townNpc: true);

        TerrariaFactReadPlan readPlan = TerrariaFactReadPlan.FromObservedFactKeys(
            [SplitCatalog.CreateNpcPresentFactKey(17)]);
        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, IntPtr.Zero, null, layout, null, Is64Bit: false),
            readPlan);

        TestAssert.Equal(1, facts.Values.Count);
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateNpcPresentFactKey(17)).AsBoolean());
        TestAssert.Equal(FactValueKind.Unknown, facts.Get(SplitCatalog.CreateNpcPresentFactKey(22)).Kind);
    }

    private static void BiomeFactProviderReturnsUnknownWithoutBiomeLayout()
    {
        var provider = new BiomeFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);

        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, IntPtr.Zero, null, null, null, Is64Bit: false));

        TestAssert.Equal(FactValueKind.Unknown, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("jungle")).Kind);
    }

    private static void BiomeFactProviderEmitsDerivedBiomeFacts()
    {
        var provider = new BiomeFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaBiomeMemoryLayout layout = CreateTestBiomeLayout();
        IntPtr player = new(0xB000);
        IntPtr playerArray = new(0xB800);

        memory.WriteInt32(layout.MyPlayerStaticFieldAddress, 0);
        memory.WritePointerValue(layout.PlayerArrayStaticFieldAddress, playerArray);
        memory.WriteInt32(IntPtr.Add(playerArray, layout.ManagedArrayLengthOffset), 1);
        memory.WritePointerValue(IntPtr.Add(playerArray, layout.ManagedArrayFirstElementOffset), player);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone1"]), 0b0011_0010);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone2"]), 0b0010_0000);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone3"]), 0b0000_0010);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone4"]), 0);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone5"]), 0b0000_0001);

        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, IntPtr.Zero, null, null, layout, Is64Bit: false));

        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("corruption")).AsBoolean());
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("jungle")).AsBoolean());
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("desert")).AsBoolean());
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("snow")).AsBoolean());
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("aether")).AsBoolean());
        TestAssert.Equal(false, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("forest")).AsBoolean());
        TestAssert.Equal(false, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("underground-corruption")).AsBoolean());
        TestAssert.Equal(false, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("dungeon")).AsBoolean());
        TestAssert.Equal(false, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("hallow")).AsBoolean());

        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone1"]), 0b0011_0010);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone2"]), 0b1100_0000);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone3"]), 0b0000_1000);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone4"]), 0);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone5"]), 0);

        facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, IntPtr.Zero, null, null, layout, Is64Bit: false));

        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("cavern")).AsBoolean());
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("underground-corruption")).AsBoolean());
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("underground-jungle")).AsBoolean());
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("underground-ice")).AsBoolean());
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("underground-desert")).AsBoolean());
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("glowing-mushroom")).AsBoolean());
        TestAssert.Equal(false, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("corruption")).AsBoolean());
        TestAssert.Equal(false, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("jungle")).AsBoolean());
        TestAssert.Equal(false, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("snow")).AsBoolean());
        TestAssert.Equal(false, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("aether")).AsBoolean());
    }

    private static void BiomeFactProviderReadsOnlyObservedBiomeZoneFields()
    {
        var provider = new BiomeFactProvider();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaBiomeMemoryLayout layout = CreateTestBiomeLayout();
        IntPtr player = new(0xB000);

        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone1"]), 0b0001_0000);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone2"]), 0);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone3"]), 0b0000_0010);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone4"]), 0);
        memory.WriteByte(IntPtr.Add(player, layout.ZoneBitsByteFieldOffsets["zone5"]), 0);

        TerrariaFactReadPlan readPlan = TerrariaFactReadPlan.FromObservedFactKeys(
            [SplitCatalog.CreateBiomeActiveFactKey("jungle")]);
        TerrariaGameFacts facts = provider.Read(
            memory,
            new TerrariaMemoryContext(null, IntPtr.Zero, player, null, null, layout, Is64Bit: false),
            readPlan);

        TestAssert.Equal(1, facts.Values.Count);
        TestAssert.Equal(true, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("jungle")).AsBoolean());
        TestAssert.Equal(FactValueKind.Unknown, facts.Get(SplitCatalog.CreateBiomeActiveFactKey("desert")).Kind);
        TestAssert.Equal(2, memory.ReadBytesCallCount);
    }

    private static byte[] BuildUpdateTimeCode(IntPtr gameMenuAddress)
    {
        byte[] bytes = new byte[0xA0];
        byte[] signaturePrefix =
        [
            0x55, 0x8B, 0xEC, 0x57, 0x56, 0x83, 0xEC, 0x78,
            0x8D, 0x7D, 0x94, 0xB9, 0x19, 0x00, 0x00, 0x00,
            0x33, 0xC0, 0xF3, 0xAB, 0x80, 0x3D
        ];
        Array.Copy(signaturePrefix, bytes, signaturePrefix.Length);
        WritePointer(bytes, 0x16, new IntPtr(0x7000));
        bytes[0x1A] = 0x00;
        bytes[0x1B] = 0x75;
        bytes[0x1C] = 0x09;
        bytes[0x1D] = 0x0F;
        bytes[0x1E] = 0xB6;
        bytes[0x1F] = 0x05;
        WritePointer(bytes, 0x20, new IntPtr(0x7001));
        WritePointer(bytes, 0x90, gameMenuAddress);
        return bytes;
    }

    private static byte[] BuildBossProgressionCode(IntPtr skeletronAddress, IntPtr hardmodeAddress)
    {
        byte[] bytes = new byte[0x80];
        WriteBossProgressionSignature(bytes, 0, skeletronAddress, hardmodeAddress);
        return bytes;
    }

    private static void WritePointer(byte[] bytes, int offset, IntPtr value)
    {
        Array.Copy(BitConverter.GetBytes(value.ToInt32()), 0, bytes, offset, 4);
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 4);
    }

    private static void WriteGameMenuFallbackSignature(
        byte[] bytes,
        int offset,
        IntPtr menuModeAddress,
        IntPtr gameMenuAddress)
    {
        bytes[offset] = 0x83;
        bytes[offset + 1] = 0x3D;
        WritePointer(bytes, offset + 2, menuModeAddress);
        bytes[offset + 6] = 0x01;
        bytes[offset + 7] = 0x74;
        bytes[offset + 8] = 0x09;
        bytes[offset + 9] = 0x80;
        bytes[offset + 10] = 0x3D;
        WritePointer(bytes, offset + 11, gameMenuAddress);
        bytes[offset + 15] = 0x00;
        bytes[offset + 16] = 0x74;
        bytes[offset + 17] = 0x0D;
        bytes[offset + 18] = 0x83;
        bytes[offset + 19] = 0x3D;
        WritePointer(bytes, offset + 20, menuModeAddress);
        bytes[offset + 24] = 0x02;
        bytes[offset + 25] = 0x0F;
        bytes[offset + 26] = 0x85;
    }

    private static void WriteBossProgressionSignature(
        byte[] bytes,
        int offset,
        IntPtr skeletronAddress,
        IntPtr hardmodeAddress)
    {
        int index = offset;
        WriteBossProgressionCheck(bytes, ref index, skeletronAddress, 0x2B, includeCall: true);
        WriteBossProgressionCheck(bytes, ref index, hardmodeAddress, 0x2C, includeCall: true);
        WriteBossProgressionCheck(bytes, ref index, new IntPtr(0x5010), 0x90, includeCall: true);
        WriteBossProgressionCheck(bytes, ref index, new IntPtr(0x5012), 0x2D, includeCall: false);
    }

    private static void WriteBossProgressionCheck(
        byte[] bytes,
        ref int index,
        IntPtr flagAddress,
        int npcId,
        bool includeCall)
    {
        bytes[index++] = 0x80;
        bytes[index++] = 0x3D;
        WritePointer(bytes, index, flagAddress);
        index += 4;
        bytes[index++] = 0x00;
        bytes[index++] = 0x74;
        bytes[index++] = 0x0C;
        bytes[index++] = 0x8B;
        bytes[index++] = 0xCE;
        bytes[index++] = 0xBA;
        WriteInt32(bytes, index, npcId);
        index += 4;
        if (includeCall)
        {
            bytes[index++] = 0xE8;
            WriteInt32(bytes, index, 0);
            index += 4;
        }
    }

    private static bool? GetBossFact(TerrariaGameFacts facts, string bossTargetId)
    {
        return SplitCatalog.TryGetBossFact(bossTargetId, out BossFactDescriptor descriptor)
            ? facts.Get(descriptor.FactKey).AsBoolean()
            : null;
    }

    private static TerrariaItemMemoryLayout CreateTestItemLayout()
    {
        return new TerrariaItemMemoryLayout(
            PlayerArrayStaticFieldAddress: new IntPtr(0x9000),
            MyPlayerStaticFieldAddress: new IntPtr(0x9010),
            MouseItemStaticFieldAddress: new IntPtr(0x9018),
            PlayerArmorFieldOffset: 0x10,
            PlayerDyeFieldOffset: 0x14,
            PlayerMiscEquipsFieldOffset: 0x18,
            PlayerMiscDyesFieldOffset: 0x1C,
            PlayerTrashItemFieldOffset: 0x20,
            PlayerInventoryFieldOffset: 0x24,
            PlayerBankFieldOffset: 0x28,
            PlayerBank2FieldOffset: 0x2C,
            PlayerBank3FieldOffset: 0x30,
            PlayerBank4FieldOffset: 0x34,
            ChestItemArrayFieldOffset: 0x8,
            ItemTypeFieldOffset: 0xC,
            ItemStackFieldOffset: 0x10,
            ManagedArrayLengthOffset: 0x4,
            ManagedArrayFirstElementOffset: 0x8,
            ObjectReferenceSize: 4);
    }

    private static TerrariaNpcMemoryLayout CreateTestNpcLayout()
    {
        return new TerrariaNpcMemoryLayout(
            NpcArrayStaticFieldAddress: new IntPtr(0x9020),
            NpcTypeFieldOffset: 0xC,
            NpcActiveFieldOffset: 0x10,
            NpcTownNpcFieldOffset: 0x11,
            NpcHomelessFieldOffset: 0x12,
            NpcHomeTileXFieldOffset: 0x14,
            NpcHomeTileYFieldOffset: 0x18,
            ManagedArrayLengthOffset: 0x4,
            ManagedArrayFirstElementOffset: 0x8,
            ObjectReferenceSize: 4);
    }

    private static TerrariaBiomeMemoryLayout CreateTestBiomeLayout()
    {
        return new TerrariaBiomeMemoryLayout(
            PlayerArrayStaticFieldAddress: new IntPtr(0x9030),
            MyPlayerStaticFieldAddress: new IntPtr(0x9040),
            ZoneBitsByteFieldOffsets: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["zone1"] = 0x10,
                ["zone2"] = 0x11,
                ["zone3"] = 0x12,
                ["zone4"] = 0x13,
                ["zone5"] = 0x14
            },
            ManagedArrayLengthOffset: 0x4,
            ManagedArrayFirstElementOffset: 0x8,
            ObjectReferenceSize: 4);
    }

    private static void WriteItem(FakeProcessMemoryReader memory, TerrariaItemMemoryLayout layout, IntPtr itemAddress, int itemId, int stack)
    {
        memory.WriteInt32(IntPtr.Add(itemAddress, layout.ItemTypeFieldOffset), itemId);
        memory.WriteInt32(IntPtr.Add(itemAddress, layout.ItemStackFieldOffset), stack);
    }

    private static void WriteNpc(
        FakeProcessMemoryReader memory,
        TerrariaNpcMemoryLayout layout,
        IntPtr npcAddress,
        int npcId,
        bool active,
        bool townNpc)
    {
        memory.WriteInt32(IntPtr.Add(npcAddress, layout.NpcTypeFieldOffset), npcId);
        memory.WriteBool(IntPtr.Add(npcAddress, layout.NpcActiveFieldOffset), active);
        memory.WriteBool(IntPtr.Add(npcAddress, layout.NpcTownNpcFieldOffset), townNpc);
    }

    private static void WriteEmptyItemArray(
        FakeProcessMemoryReader memory,
        TerrariaItemMemoryLayout layout,
        IntPtr playerAddress,
        int fieldOffset,
        IntPtr arrayAddress)
    {
        memory.WritePointerValue(IntPtr.Add(playerAddress, fieldOffset), arrayAddress);
        memory.WriteInt32(IntPtr.Add(arrayAddress, layout.ManagedArrayLengthOffset), 0);
    }

    private static void WriteEmptyItem(
        FakeProcessMemoryReader memory,
        TerrariaItemMemoryLayout layout,
        IntPtr playerAddress,
        int fieldOffset,
        IntPtr itemAddress)
    {
        memory.WritePointerValue(IntPtr.Add(playerAddress, fieldOffset), itemAddress);
        WriteItem(memory, layout, itemAddress, itemId: 0, stack: 0);
    }

    private static void WriteBasicInventory(
        FakeProcessMemoryReader memory,
        TerrariaItemMemoryLayout layout,
        IntPtr playerAddress,
        IntPtr firstInventoryItemAddress,
        IntPtr mouseItemAddress)
    {
        IntPtr inventory = new(0x2000);
        WriteEmptyItemArray(memory, layout, playerAddress, layout.PlayerArmorFieldOffset, new IntPtr(0x2100));
        WriteEmptyItemArray(memory, layout, playerAddress, layout.PlayerDyeFieldOffset, new IntPtr(0x2200));
        WriteEmptyItemArray(memory, layout, playerAddress, layout.PlayerMiscEquipsFieldOffset, new IntPtr(0x2300));
        WriteEmptyItemArray(memory, layout, playerAddress, layout.PlayerMiscDyesFieldOffset, new IntPtr(0x2400));
        memory.WritePointerValue(IntPtr.Add(playerAddress, layout.PlayerInventoryFieldOffset), inventory);
        memory.WriteInt32(IntPtr.Add(inventory, layout.ManagedArrayLengthOffset), 59);
        memory.WritePointerValue(IntPtr.Add(inventory, layout.ManagedArrayFirstElementOffset), firstInventoryItemAddress);
        memory.WritePointerValue(layout.MouseItemStaticFieldAddress, mouseItemAddress);
    }

    private static void WriteEmptyBank(
        FakeProcessMemoryReader memory,
        TerrariaItemMemoryLayout layout,
        IntPtr playerAddress,
        int fieldOffset,
        IntPtr bankAddress,
        IntPtr itemArrayAddress)
    {
        memory.WritePointerValue(IntPtr.Add(playerAddress, fieldOffset), bankAddress);
        memory.WritePointerValue(IntPtr.Add(bankAddress, layout.ChestItemArrayFieldOffset), itemArrayAddress);
        memory.WriteInt32(IntPtr.Add(itemArrayAddress, layout.ManagedArrayLengthOffset), 0);
    }

    private sealed class FakeProcessMemoryReader : IProcessMemoryReader
    {
        private readonly Dictionary<long, byte> bytes = new();
        private readonly List<MemoryPage> executablePages = new();

        public FakeProcessMemoryReader(bool is64Bit)
        {
            Is64Bit = is64Bit;
        }

        public bool Is64Bit { get; }

        public int ReadBytesCallCount { get; private set; }

        public IEnumerable<MemoryPage> ExecutablePages() => executablePages;

        public IEnumerable<MemoryPage> ExecutablePrivatePages() =>
            executablePages.Where(page => page.Type == MemoryPageType.Private);

        public bool TryReadBool(IntPtr address, out bool value)
        {
            value = false;
            if (!TryReadBytes(address, 1, out byte[]? buffer))
            {
                return false;
            }

            value = buffer[0] != 0;
            return true;
        }

        public bool TryReadInt32(IntPtr address, out int value)
        {
            value = 0;
            if (!TryReadBytes(address, 4, out byte[]? buffer))
            {
                return false;
            }

            value = BitConverter.ToInt32(buffer, 0);
            return true;
        }

        public bool TryReadDouble(IntPtr address, out double value)
        {
            value = 0d;
            if (!TryReadBytes(address, 8, out byte[]? buffer))
            {
                return false;
            }

            value = BitConverter.ToDouble(buffer, 0);
            return true;
        }

        public bool TryReadPointer(IntPtr address, out IntPtr value)
        {
            bool success = TryReadPointerValue(address, out value);
            return success && value != IntPtr.Zero;
        }

        public bool TryReadPointerValue(IntPtr address, out IntPtr value)
        {
            value = IntPtr.Zero;
            int size = Is64Bit ? 8 : 4;
            if (!TryReadBytes(address, size, out byte[]? buffer))
            {
                return false;
            }

            value = Is64Bit
                ? new IntPtr(BitConverter.ToInt64(buffer, 0))
                : new IntPtr(BitConverter.ToInt32(buffer, 0));
            return true;
        }

        public bool TryReadBytes(IntPtr address, int count, [NotNullWhen(true)] out byte[]? result)
        {
            ReadBytesCallCount++;
            result = new byte[count];
            long start = address.ToInt64();
            for (int index = 0; index < count; index++)
            {
                if (!bytes.TryGetValue(start + index, out byte value))
                {
                    result = null;
                    return false;
                }

                result[index] = value;
            }

            return true;
        }

        public void WriteExecutablePage(IntPtr address, byte[] value)
        {
            WriteBytes(address, value);
            executablePages.Add(new MemoryPage(
                address,
                value.Length,
                MemoryPageProtect.PageExecuteReadWrite,
                MemoryPageType.Private));
        }

        public void WriteBool(IntPtr address, bool value)
        {
            WriteBytes(address, [value ? (byte)1 : (byte)0]);
        }

        public void WriteByte(IntPtr address, byte value)
        {
            WriteBytes(address, [value]);
        }

        public void WriteInt32(IntPtr address, int value)
        {
            WriteBytes(address, BitConverter.GetBytes(value));
        }

        public void WritePointerValue(IntPtr address, IntPtr value)
        {
            WriteBytes(address, Is64Bit
                ? BitConverter.GetBytes(value.ToInt64())
                : BitConverter.GetBytes(value.ToInt32()));
        }

        private void WriteBytes(IntPtr address, byte[] value)
        {
            long start = address.ToInt64();
            for (int index = 0; index < value.Length; index++)
            {
                bytes[start + index] = value[index];
            }
        }
    }
}
