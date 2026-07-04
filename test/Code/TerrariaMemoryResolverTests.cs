using System.Diagnostics.CodeAnalysis;

namespace TerrariaSplit.Tests;

internal static class TerrariaMemoryResolverTests
{
    public static IEnumerable<(string Name, Action Test)> All()
    {
        yield return ("TerrariaMemoryResolver reads gameMenu from managed runtime layout", ReadsGameMenuFromManagedRuntimeLayout);
        yield return ("TerrariaMemoryResolver emits boss facts from managed static fields", EmitsBossFactsFromManagedStaticFields);
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

    private static void ReadsGameMenuFromManagedRuntimeLayout()
    {
        var resolver = new TerrariaMemoryResolver();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        TerrariaRuntimeMemoryLayout layout = CreateTestRuntimeLayout(
            gameMenuAddress: new IntPtr(0x4000),
            bossFactAddresses: new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase));
        resolver.SetRuntimeLayoutForTests(layout);
        memory.WriteBool(new IntPtr(0x4000), true);

        TerrariaMemoryResolveResult result = resolver.Resolve(memory);
        TerrariaMemoryResolution resolution = resolver.Resolution;

        TestAssert.Equal(true, result.ObservedGameMenu);
        TestAssert.Equal(new IntPtr(0x4000), resolution.GameMenuAddress);
        TestAssert.Equal("fact layouts pending", result.Stage);
    }

    private static void EmitsBossFactsFromManagedStaticFields()
    {
        var resolver = new TerrariaMemoryResolver();
        var memory = new FakeProcessMemoryReader(is64Bit: false);
        Dictionary<string, IntPtr> bossFactAddresses = new(StringComparer.OrdinalIgnoreCase)
        {
            ["boss:skeletron:defeated"] = new IntPtr(0x5000),
            ["boss:moon-lord:defeated"] = new IntPtr(0x5010),
            ["boss:wall-of-flesh:defeated"] = new IntPtr(0x6000)
        };
        resolver.SetRuntimeLayoutForTests(CreateTestRuntimeLayout(
            gameMenuAddress: new IntPtr(0x4000),
            bossFactAddresses));

        memory.WriteBool(new IntPtr(0x4000), false);
        memory.WriteBool(new IntPtr(0x5000), true);
        memory.WriteBool(new IntPtr(0x5010), true);
        memory.WriteBool(new IntPtr(0x6000), true);
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

    private static TerrariaRuntimeMemoryLayout CreateTestRuntimeLayout(
        IntPtr gameMenuAddress,
        IReadOnlyDictionary<string, IntPtr> bossFactAddresses)
    {
        return new TerrariaRuntimeMemoryLayout(
            TerrariaVersion: "test",
            new TerrariaCoreMemoryLayout(
                gameMenuAddress,
                StatusTextStaticFieldAddress: IntPtr.Zero,
                MenuUiStaticFieldAddress: IntPtr.Zero),
            new TerrariaBossMemoryLayout(bossFactAddresses),
            Item: null,
            Npc: null,
            Biome: null,
            SeedUi: null,
            WorldGeneration: new TerrariaWorldGenerationMemoryLayout(
                StatusTextStaticFieldAddress: IntPtr.Zero,
                CurrentGenerationProgressStaticFieldAddress: IntPtr.Zero,
                CurrentControllerStaticFieldAddress: IntPtr.Zero,
                GenerationProgressMessageFieldOffset: -1,
                GenerationProgressValueFieldOffset: -1,
                GenerationProgressTotalWeightedProgressFieldOffset: -1,
                GenerationProgressTotalWeightFieldOffset: -1,
                GenerationProgressCurrentPassWeightFieldOffset: -1,
                ControllerGeneratorFieldOffset: -1,
                WorldGeneratorCurrentPassFieldOffset: -1,
                GenPassNameFieldOffset: -1),
            ResolvedFieldCount: 1 + bossFactAddresses.Count);
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
