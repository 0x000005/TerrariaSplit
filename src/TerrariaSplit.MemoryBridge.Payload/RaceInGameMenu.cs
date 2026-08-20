using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using TerrariaSplit.Race.InGame;

namespace TerrariaSplit.MemoryBridge.Payload
{
    public static partial class EntryPoint
    {
        private static readonly object RaceUiSync = new object();
        private static readonly Queue<RaceInGameAction> RaceUiActions = new Queue<RaceInGameAction>();
        private static Assembly raceUiTerraria;
        private static Type raceUiElementType;
        private static Type raceUiStateType;
        private static Type raceUiPanelType;
        private static Type raceUiTextType;
        private static Type raceUiTextPanelType;
        private static Type raceUiListType;
        private static Type raceUiScrollbarType;
        private static Type raceUiScrollbarThemeType;
        private static Type raceUiKeyboardType;
        private static Type raceUiLocalizedTextType;
        private static Type raceUiColorType;
        private static Type raceUiCharacterNameButtonType;
        private static Type raceUiGroupOptionButtonOpenType;
        private static Type raceUiProgressBarType;
        private static Type raceUiWorldGenerationOptionsType;
        private static PropertyInfo raceUiWorldGenerationOptionsProperty;
        private static MethodInfo raceUiLanguageGetTextValueMethod;
        private static FieldInfo raceUiMenuField;
        private static FieldInfo raceUiGameMenuField;
        private static FieldInfo raceUiMenuModeField;
        private static MethodInfo raceUiQueueMethod;
        private static MethodInfo raceUiSaveAndQuitMethod;
        private static MethodInfo raceUiSetStateMethod;
        private static MethodInfo raceUiAppendMethod;
        private static MethodInfo raceUiListAddMethod;
        private static MethodInfo raceUiSetScrollbarMethod;
        private static MethodInfo raceUiSetPaddingMethod;
        private static MethodInfo raceUiSetSnapPointMethod;
        private static MethodInfo raceUiNewTextMethod;
        private static MethodInfo raceUiGetDimensionsMethod;
        private static MethodInfo raceUiLinkSetPositionMethod;
        private static MethodInfo raceUiLinkChangePointMethod;
        private static IDictionary raceUiLinkPoints;
        private static FieldInfo raceUiFancyHighestIndexField;
        private static FieldInfo raceUiManualSortField;
        private static PropertyInfo raceUiLocalPlayerProperty;
        private static FieldInfo raceUiPlayerDeadField;
        private static FieldInfo raceUiPlayerNameField;
        private static MethodInfo raceUiDeathReasonGetTextMethod;
        private static string raceUiLocalDeathMessage;
        private static EventInfo raceUiLeftClickEvent;
        private static EventInfo raceUiMouseOverEvent;
        private static EventInfo raceUiMouseOutEvent;
        private static EventInfo raceUiUpdateEvent;
        private static EventInfo raceUiTickEvent;
        private static MethodInfo raceUiPlaySoundMethod;
        private static MethodInfo raceUiColorMultiplyMethod;
        private static object raceUiFancyButtonHoverColor;
        private static RaceInGameSnapshot raceUiSnapshot;
        private static object raceUiState;
        private static object raceUiStatusText;
        private static bool raceUiStatusTextLarge;
        private static string raceUiStructureKey;
        private static volatile string raceUiRuntimeFailure;
        private static readonly Dictionary<string, Action<RaceInGameControl>> RaceUiRefreshers =
            new Dictionary<string, Action<RaceInGameControl>>(StringComparer.Ordinal);
        private static long raceUiActionId;
        private static long raceUiLastHostContactUtcTicks;
        private static Timer raceUiEmergencyExitTimer;
        private static Delegate raceUiHomeRestoreHandler;
        private static int raceUiDefaultNavigationPoint = -1;
        private static bool raceUiLocalPlayerWasDead;

        private static bool TryHandleRaceUiCommand(string command, out PayloadCommandResult result)
        {
            string[] parts = (command ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
            if (parts.Length == 0 || !parts[0].StartsWith("race-ui-", StringComparison.Ordinal))
            {
                result = null;
                return false;
            }

            try
            {
                Interlocked.Exchange(ref raceUiLastHostContactUtcTicks, DateTime.UtcNow.Ticks);
                if (string.Equals(parts[0], "race-ui-open", StringComparison.Ordinal) && parts.Length == 2)
                {
                    RaceInGameSnapshot snapshot = RaceInGameProtocol.DecodeSnapshot(parts[1]);
                    EnsureRaceUiInitialized();
                    raceUiRuntimeFailure = null;
                    // A fresh, unlocked menu may recover from an earlier hook attempt.
                    // Never hide a runtime failure while an active Race package is locked.
                    if (configuration == null)
                    {
                        runtimeFailure = null;
                    }

                    QueueRaceUiSnapshot(snapshot, true);
                    if (TryCreateRaceUiFailureResult(out result))
                    {
                        return true;
                    }

                    result = new PayloadCommandResult(0, RaceInGameProtocol.EncodeActions(DrainRaceUiActions()), false);
                    return true;
                }

                if (string.Equals(parts[0], "race-ui-exchange", StringComparison.Ordinal) &&
                    (parts.Length == 2 || parts.Length == 3))
                {
                    long knownRevision;
                    if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out knownRevision))
                    {
                        result = new PayloadCommandResult(2, "The Race menu revision is invalid.", false);
                        return true;
                    }

                    if (TryCreateRaceUiFailureResult(out result))
                    {
                        return true;
                    }

                    EnsureRaceUiInitialized();
                    if (parts.Length == 3 && parts[2].Length > 0)
                    {
                        RaceInGameSnapshot snapshot = RaceInGameProtocol.DecodeSnapshot(parts[2]);
                        if (snapshot.Revision > knownRevision)
                        {
                            QueueRaceUiSnapshot(snapshot, false);
                        }
                    }

                    if (TryCreateRaceUiFailureResult(out result))
                    {
                        return true;
                    }

                    result = new PayloadCommandResult(0, RaceInGameProtocol.EncodeActions(DrainRaceUiActions()), false);
                    return true;
                }

                if (string.Equals(parts[0], "race-ui-close", StringComparison.Ordinal) && parts.Length == 1)
                {
                    EnsureRaceUiInitialized();
                    raceUiRuntimeFailure = null;
                    QueueOnTerrariaMainThread(CloseRaceUiOnMainThread);
                    result = new PayloadCommandResult(0, RaceInGameProtocol.EncodeActions(DrainRaceUiActions()), false);
                    return true;
                }

                if (string.Equals(parts[0], "race-ui-message", StringComparison.Ordinal) &&
                    parts.Length == 3)
                {
                    int kind;
                    byte[] encodedMessage;
                    if (!int.TryParse(
                            parts[1],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out kind) ||
                        (kind != 0 && kind != 1))
                    {
                        result = new PayloadCommandResult(2, "The Race game message kind is invalid.", false);
                        return true;
                    }

                    try
                    {
                        encodedMessage = Convert.FromBase64String(parts[2]);
                    }
                    catch (FormatException)
                    {
                        result = new PayloadCommandResult(2, "The Race game message is invalid.", false);
                        return true;
                    }

                    if (encodedMessage.Length == 0 || encodedMessage.Length > 1024)
                    {
                        result = new PayloadCommandResult(2, "The Race game message length is invalid.", false);
                        return true;
                    }

                    string message = new UTF8Encoding(false, true).GetString(encodedMessage);
                    EnsureRaceUiInitialized();
                    QueueRaceGameMessage(message, kind);
                    result = new PayloadCommandResult(0, string.Empty, false);
                    return true;
                }

                result = new PayloadCommandResult(2, "The Race menu command is invalid.", false);
                return true;
            }
            catch (Exception ex)
            {
                result = new PayloadCommandResult(70, "The Terraria Race menu failed: " + Unwrap(ex).Message, false);
                return true;
            }
        }

        private static bool TryCreateRaceUiFailureResult(out PayloadCommandResult result)
        {
            string failure;
            if (!RaceUiRuntimeFailure.TryResolve(
                raceUiRuntimeFailure,
                runtimeFailure,
                out failure))
            {
                result = null;
                return false;
            }

            result = new PayloadCommandResult(
                RaceUiRuntimeFailure.ErrorCode,
                failure,
                false);
            return true;
        }

        private static void EnsureRaceUiInitialized()
        {
            if (raceUiTerraria != null)
            {
                return;
            }

            Assembly terraria = null;
            foreach (Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(candidate.GetName().Name, "Terraria", StringComparison.Ordinal))
                {
                    terraria = candidate;
                    break;
                }
            }

            if (terraria == null || terraria.GetName().Version != new Version(1, 4, 5, 6) ||
                terraria.ManifestModule.ModuleVersionId != SupportedMvid)
            {
                throw new InvalidOperationException("The Terraria Race menu is not compatible with this client.");
            }

            Type mainType = RequireType(terraria, "Terraria.Main");
            Type playerType = RequireType(terraria, "Terraria.Player");
            Type worldGenType = RequireType(terraria, "Terraria.WorldGen");
            Type colorsType = RequireType(terraria, "Terraria.ID.Colors");
            raceUiElementType = RequireType(terraria, "Terraria.UI.UIElement");
            raceUiStateType = RequireType(terraria, "Terraria.UI.UIState");
            raceUiPanelType = RequireType(terraria, "Terraria.GameContent.UI.Elements.UIPanel");
            raceUiColorType = RequireField(raceUiPanelType, "BackgroundColor").FieldType;
            raceUiTextType = RequireType(terraria, "Terraria.GameContent.UI.Elements.UIText");
            raceUiTextPanelType = RequireType(terraria, "Terraria.GameContent.UI.Elements.UITextPanel`1")
                .MakeGenericType(typeof(object));
            raceUiListType = RequireType(terraria, "Terraria.GameContent.UI.Elements.UIList");
            raceUiScrollbarType = RequireType(terraria, "Terraria.GameContent.UI.Elements.UIScrollbar");
            raceUiScrollbarThemeType = raceUiScrollbarType.GetNestedType(
                "ColorTheme",
                BindingFlags.Public);
            raceUiKeyboardType = RequireType(terraria, "Terraria.GameContent.UI.States.UIVirtualKeyboard");
            raceUiLocalizedTextType = RequireType(terraria, "Terraria.Localization.LocalizedText");
            Type languageType = RequireType(terraria, "Terraria.Localization.Language");
            raceUiLanguageGetTextValueMethod = languageType.GetMethod(
                "GetTextValue",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);
            if (raceUiLanguageGetTextValueMethod == null)
            {
                throw new MissingMemberException(languageType.FullName, "GetTextValue");
            }

            raceUiCharacterNameButtonType = RequireType(
                terraria,
                "Terraria.GameContent.UI.Elements.UICharacterNameButton");
            raceUiGroupOptionButtonOpenType = RequireType(
                terraria,
                "Terraria.GameContent.UI.Elements.GroupOptionButton`1");
            raceUiProgressBarType = RequireType(
                terraria,
                "Terraria.GameContent.UI.Elements.UIProgressBar");
            raceUiWorldGenerationOptionsType = RequireType(
                terraria,
                "Terraria.WorldBuilding.WorldGenerationOptions");
            raceUiWorldGenerationOptionsProperty = raceUiWorldGenerationOptionsType.GetProperty(
                "Options",
                BindingFlags.Static | BindingFlags.Public);
            if (raceUiWorldGenerationOptionsProperty == null)
            {
                throw new MissingMemberException(
                    raceUiWorldGenerationOptionsType.FullName,
                    "Options");
            }
            Type soundEngineType = RequireType(terraria, "Terraria.Audio.SoundEngine");

            raceUiMenuField = RequireField(mainType, "MenuUI");
            raceUiGameMenuField = RequireField(mainType, "gameMenu");
            raceUiMenuModeField = RequireField(mainType, "menuMode");
            raceUiQueueMethod = RequireMethod(mainType, "QueueMainThreadAction", BindingFlags.Static | BindingFlags.Public);
            raceUiSaveAndQuitMethod = worldGenType.GetMethod(
                "SaveAndQuit",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(Action) },
                null);
            raceUiAppendMethod = RequireMethod(raceUiElementType, "Append", BindingFlags.Instance | BindingFlags.Public);
            raceUiListAddMethod = RequireMethod(raceUiListType, "Add", BindingFlags.Instance | BindingFlags.Public);
            raceUiSetScrollbarMethod = RequireMethod(raceUiListType, "SetScrollbar", BindingFlags.Instance | BindingFlags.Public);
            raceUiManualSortField = RequireField(raceUiListType, "ManualSortMethod");
            raceUiSetPaddingMethod = RequireMethod(raceUiElementType, "SetPadding", BindingFlags.Instance | BindingFlags.Public);
            raceUiSetSnapPointMethod = RequireMethod(raceUiElementType, "SetSnapPoint", BindingFlags.Instance | BindingFlags.Public);
            raceUiNewTextMethod = mainType.GetMethod(
                "NewText",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(byte), typeof(byte), typeof(byte) },
                null);
            raceUiLocalPlayerProperty = mainType.GetProperty(
                "LocalPlayer",
                BindingFlags.Static | BindingFlags.Public);
            raceUiPlayerDeadField = playerType.GetField(
                "dead",
                BindingFlags.Instance | BindingFlags.Public);
            TryInitializePlainMenuNavigation(terraria);
            raceUiLeftClickEvent = raceUiElementType.GetEvent("OnLeftClick", BindingFlags.Instance | BindingFlags.Public);
            raceUiMouseOverEvent = raceUiElementType.GetEvent("OnMouseOver", BindingFlags.Instance | BindingFlags.Public);
            raceUiMouseOutEvent = raceUiElementType.GetEvent("OnMouseOut", BindingFlags.Instance | BindingFlags.Public);
            raceUiUpdateEvent = raceUiElementType.GetEvent("OnUpdate", BindingFlags.Instance | BindingFlags.Public);
            raceUiTickEvent = mainType.GetEvent(
                "OnTickForThirdPartySoftwareOnly",
                BindingFlags.Static | BindingFlags.Public);
            raceUiColorMultiplyMethod = raceUiColorType.GetMethod(
                "op_Multiply",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { raceUiColorType, typeof(float) },
                null);
            raceUiFancyButtonHoverColor = RequireField(
                colorsType,
                "FancyUIFatButtonMouseOver").GetValue(null);
            raceUiPlaySoundMethod = soundEngineType.GetMethod(
                "PlaySound",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(float),
                    typeof(float)
                },
                null);
            object menu = raceUiMenuField.GetValue(null);
            raceUiSetStateMethod = menu == null
                ? null
                : menu.GetType().GetMethod("SetState", BindingFlags.Instance | BindingFlags.Public);
            if (raceUiSaveAndQuitMethod == null || raceUiLeftClickEvent == null ||
                raceUiMouseOverEvent == null || raceUiMouseOutEvent == null ||
                raceUiUpdateEvent == null || raceUiTickEvent == null ||
                raceUiColorMultiplyMethod == null || raceUiPlaySoundMethod == null ||
                raceUiSetStateMethod == null || raceUiScrollbarThemeType == null ||
                raceUiNewTextMethod == null || raceUiLocalPlayerProperty == null ||
                raceUiPlayerDeadField == null)
            {
                throw new MissingMemberException("The Terraria Race menu API is incomplete.");
            }

            raceUiTerraria = terraria;
        }

        private static void QueueRaceUiSnapshot(RaceInGameSnapshot snapshot, bool saveAndQuit)
        {
            QueueOnTerrariaMainThread(delegate
            {
                if (raceUiSnapshot != null && snapshot.Revision < raceUiSnapshot.Revision)
                {
                    return;
                }

                raceUiSnapshot = snapshot;
                if (!snapshot.Visible)
                {
                    CloseRaceUiOnMainThread();
                    return;
                }

                ArmRaceUiHomeRestore();
                Action show = delegate
                {
                    try
                    {
                        ShowRaceUiOnMainThread(snapshot, saveAndQuit);
                    }
                    catch (Exception ex)
                    {
                        raceUiRuntimeFailure =
                            "The Terraria Race menu could not be displayed: " +
                            Unwrap(ex).Message;
                        CloseRaceUiOnMainThread();
                    }
                };

                if (saveAndQuit && !(bool)raceUiGameMenuField.GetValue(null))
                {
                    raceUiSaveAndQuitMethod.Invoke(null, new object[] { show });
                    return;
                }

                show();
            });
        }

        private static void QueueOnTerrariaMainThread(Action action)
        {
            raceUiQueueMethod.Invoke(null, new object[] { action });
        }

        private static void ShowRaceUiOnMainThread(RaceInGameSnapshot snapshot, bool forceShow)
        {
            string structureKey = BuildRaceUiStructureKey(snapshot);
            if (raceUiState != null &&
                string.Equals(raceUiStructureKey, structureKey, StringComparison.Ordinal))
            {
                raceUiSnapshot = snapshot;
                RefreshRaceUiSnapshot(snapshot);
                if (forceShow)
                {
                    object existingMenu = raceUiMenuField.GetValue(null);
                    raceUiMenuModeField.SetValue(null, 888);
                    raceUiSetStateMethod.Invoke(existingMenu, new[] { raceUiState });
                    ApplyDefaultRaceUiNavigationPoint();
                }

                return;
            }

            raceUiSnapshot = snapshot;
            raceUiStructureKey = structureKey;
            raceUiStatusText = null;
            raceUiStatusTextLarge = false;
            raceUiDefaultNavigationPoint = -1;
            RaceUiRefreshers.Clear();

            object state;
            switch (snapshot.PageKind)
            {
                case RaceInGamePageKind.Home:
                    state = BuildPlainMenuPage(snapshot, false);
                    break;
                case RaceInGamePageKind.WorldSource:
                    state = BuildPlainMenuPage(snapshot, true);
                    break;
                case RaceInGamePageKind.WorldCreation:
                    state = BuildWorldCreationPage(snapshot);
                    break;
                case RaceInGamePageKind.SpecialSeeds:
                    state = BuildSpecialSeedsPage(snapshot);
                    break;
                case RaceInGamePageKind.WorldFilters:
                    state = BuildOptionGridPage(snapshot);
                    break;
                case RaceInGamePageKind.MemberJoin:
                    state = BuildMemberJoinPage(snapshot);
                    break;
                case RaceInGamePageKind.Progress:
                    state = BuildProgressPage(snapshot);
                    break;
                case RaceInGamePageKind.Lobby:
                    state = BuildLobbyPage(snapshot);
                    break;
                default:
                    throw new InvalidOperationException("The Race menu page is not supported.");
            }

            raceUiState = state;
            RefreshRaceUiSnapshot(snapshot);
            object menu = raceUiMenuField.GetValue(null);
            raceUiMenuModeField.SetValue(null, 888);
            raceUiSetStateMethod.Invoke(menu, new[] { state });
            ApplyDefaultRaceUiNavigationPoint();
        }

        private static object BuildPlainMenuPage(RaceInGameSnapshot snapshot, bool showTitle)
        {
            object state = Activator.CreateInstance(raceUiStateType);
            if (showTitle)
            {
                object title = CreateText(snapshot.Title, 0.8f, true);
                SetDimension(title, "Top", 155f, 0f);
                SetDimension(title, "Width", 0f, 1f);
                SetDimension(title, "Height", 50f, 0f);
                SetFloatField(title, "HAlign", 0.5f);
                raceUiAppendMethod.Invoke(state, new[] { title });
            }

            RaceInGameControl[] controls = snapshot.Controls ?? new RaceInGameControl[0];
            int startTop = 220;
            int snapId = 0;
            int visualRow = 0;
            bool lowerMenuStarted = false;
            // Terraria's Fancy UI page enters at point 3002 when keyboard
            // navigation is activated, so bind the first menu item there.
            int navigationStart = 3002;
            for (int index = 0; index < controls.Length; index++)
            {
                RaceInGameControl control = controls[index];
                if (string.Equals(
                        control.LayoutGroup,
                        "menu-lower",
                        StringComparison.Ordinal) &&
                    !lowerMenuStarted)
                {
                    visualRow += 3;
                    lowerMenuStarted = true;
                }

                object item = CreateText(PlainMenuText(control), 0.8f, true);
                SetDimension(item, "Top", startTop + visualRow * 52, 0f);
                SetDimension(item, "Width", 0f, 0.8f);
                SetDimension(item, "MaxWidth", 650f, 0f);
                SetDimension(item, "Height", 50f, 0f);
                SetFloatField(item, "HAlign", 0.5f);
                AddPlainMenuAnimation(item, control.Id);
                AddControlClick(item, control.Id);
                SetSnapPoint(item, snapId++);
                if (IsPlainMenuNavigationAvailable())
                {
                    AddPlainMenuNavigation(
                        item,
                        control.Id,
                        navigationStart + index,
                        index == 0 ? -1 : navigationStart + index - 1,
                        index == controls.Length - 1 ? -2 : navigationStart + index + 1);
                }
                raceUiAppendMethod.Invoke(state, new[] { item });
                visualRow++;
            }

            if (controls.Length > 0 && IsPlainMenuNavigationAvailable())
            {
                raceUiDefaultNavigationPoint = navigationStart;
                raceUiFancyHighestIndexField.SetValue(
                    null,
                    navigationStart + controls.Length - 1);
            }

            return state;
        }

        private static object BuildWorldCreationPage(RaceInGameSnapshot snapshot)
        {
            object state = Activator.CreateInstance(raceUiStateType);
            object root = CreateRoot(500f, 190f, 350f);
            raceUiAppendMethod.Invoke(state, new[] { root });

            object panel = Activator.CreateInstance(raceUiPanelType);
            SetDimension(panel, "Width", 0f, 1f);
            SetDimension(panel, "Height", 250f, 0f);
            SetPanelColor(panel, 33, 43, 79, 204);
            raceUiSetPaddingMethod.Invoke(panel, new object[] { 10f });
            raceUiAppendMethod.Invoke(root, new[] { panel });

            RaceInGameControl advanced = FindControlOrNull(snapshot, "host-world-seeds");
            float firstOptionTop = 0f;
            if (advanced != null)
            {
                object advancedButton = CreateOptionButton(advanced, false);
                SetDimension(advancedButton, "Top", 0f, 0f);
                SetDimension(advancedButton, "Width", 0f, 1f);
                SetDimension(advancedButton, "Height", 42f, 0f);
                raceUiAppendMethod.Invoke(panel, new[] { advancedButton });
                firstOptionTop = 48f;
            }

            AppendWorldOptionRow(panel, snapshot, "world-size", firstOptionTop);
            AppendWorldOptionRow(panel, snapshot, "world-difficulty", firstOptionTop + 52f);
            AppendWorldOptionRow(panel, snapshot, "world-evil", firstOptionTop + 104f);
            AppendStatusText(root, 254f, 42f);
            AppendFooter(root, snapshot);
            return state;
        }

        private static object BuildSpecialSeedsPage(RaceInGameSnapshot snapshot)
        {
            object state = Activator.CreateInstance(raceUiStateType);
            object root = CreateRoot(500f, 202f, -200f);
            SetDimension(root, "MaxHeight", 400f, 0f);
            raceUiAppendMethod.Invoke(state, new[] { root });

            AppendPageTitle(root, snapshot.Title);
            object panel = CreateOptionPagePanel(root);
            object list = CreateList(panel, 0f, -70f);

            RaceInGameControl[] seeds = FindControlsByGroup(snapshot, "special-seeds");
            object seedRegion = Activator.CreateInstance(raceUiElementType);
            SetDimension(seedRegion, "Width", 0f, 1f);
            SetDimension(
                seedRegion,
                "Height",
                (float)Math.Ceiling(seeds.Length / 6d) * 70f - 10f,
                0f);
            for (int index = 0; index < seeds.Length; index++)
            {
                object option = CreateOptionButton(seeds[index], true);
                SetDimension(option, "Width", 60f, 0f);
                SetDimension(option, "Height", 60f, 0f);
                SetFloatField(option, "HAlign", index % 6 / 5f);
                SetDimension(option, "Top", index / 6 * 67f + 3f, 0f);
                SetIntField(option, "InnerHighlightRim", 4);
                raceUiAppendMethod.Invoke(seedRegion, new[] { option });
            }

            raceUiListAddMethod.Invoke(list, new[] { seedRegion });
            RaceInGameControl secret = FindControlOrNull(snapshot, "secret-seeds");
            if (secret != null)
            {
                object field = CreateCharacterNameButton(secret);
                SetDimension(field, "Width", 0f, 1f);
                SetDimension(field, "Height", 40f, 0f);
                raceUiListAddMethod.Invoke(list, new[] { field });
            }

            AppendStatusText(panel, -62f, 56f, true);
            AppendFooter(root, snapshot);
            return state;
        }

        private static object BuildOptionGridPage(RaceInGameSnapshot snapshot)
        {
            object state = Activator.CreateInstance(raceUiStateType);
            object root = CreateRoot(750f, 150f, -130f);
            SetDimension(root, "MaxHeight", 470f, 0f);
            raceUiAppendMethod.Invoke(state, new[] { root });

            AppendPageTitle(root, snapshot.Title);
            object panel = CreateOptionPagePanel(root);

            object list = CreateList(panel, 0f, 0f);
            AppendGroupedControls(list, snapshot);
            AppendFooter(root, snapshot);
            return state;
        }

        private static void AppendPageTitle(object root, string titleText)
        {
            object title = CreateText(titleText, 0.72f, true);
            SetDimension(title, "Top", -48f, 0f);
            SetDimension(title, "Width", 0f, 1f);
            SetDimension(title, "Height", 44f, 0f);
            SetFloatField(title, "HAlign", 0.5f);
            raceUiAppendMethod.Invoke(root, new[] { title });
        }

        private static object CreateOptionPagePanel(object root)
        {
            object panel = Activator.CreateInstance(raceUiPanelType);
            SetDimension(panel, "Width", 0f, 1f);
            SetDimension(panel, "Height", -102f, 1f);
            SetPanelColor(panel, 33, 43, 79, 204);
            raceUiSetPaddingMethod.Invoke(panel, new object[] { 10f });
            raceUiAppendMethod.Invoke(root, new[] { panel });
            return panel;
        }

        private static object BuildMemberJoinPage(RaceInGameSnapshot snapshot)
        {
            object state = Activator.CreateInstance(raceUiStateType);
            object root = CreateRoot(500f, 230f, 240f);
            raceUiAppendMethod.Invoke(state, new[] { root });

            object title = CreateText(snapshot.Title, 0.8f, true);
            SetDimension(title, "Top", -58f, 0f);
            SetDimension(title, "Width", 0f, 1f);
            SetDimension(title, "Height", 50f, 0f);
            SetFloatField(title, "HAlign", 0.5f);
            raceUiAppendMethod.Invoke(root, new[] { title });

            object panel = Activator.CreateInstance(raceUiPanelType);
            SetDimension(panel, "Width", 0f, 1f);
            SetDimension(panel, "Height", 105f, 0f);
            SetPanelColor(panel, 33, 43, 79, 204);
            raceUiSetPaddingMethod.Invoke(panel, new object[] { 12f });
            raceUiAppendMethod.Invoke(root, new[] { panel });

            RaceInGameControl roomCode = FindControl(snapshot, "room-code");
            object field = CreateCharacterNameButton(roomCode);
            SetDimension(field, "Width", 0f, 1f);
            SetDimension(field, "Top", 5f, 0f);
            raceUiAppendMethod.Invoke(panel, new[] { field });
            AppendStatusText(panel, 50f, 35f);
            AppendFooter(root, snapshot);
            return state;
        }

        private static object BuildProgressPage(RaceInGameSnapshot snapshot)
        {
            object state = Activator.CreateInstance(raceUiStateType);
            object root = CreateRoot(650f, 250f, 300f);
            raceUiAppendMethod.Invoke(state, new[] { root });

            object title = CreateText(snapshot.Status, 0.8f, true);
            SetDimension(title, "Width", 0f, 1f);
            SetDimension(title, "Height", 50f, 0f);
            SetFloatField(title, "HAlign", 0.5f);
            raceUiAppendMethod.Invoke(root, new[] { title });
            raceUiStatusText = title;
            raceUiStatusTextLarge = true;

            AppendFooter(root, snapshot);
            return state;
        }

        private static object BuildLobbyPage(RaceInGameSnapshot snapshot)
        {
            object state = Activator.CreateInstance(raceUiStateType);
            object root = CreateRoot(650f, 220f, -220f);
            SetDimension(root, "Width", 0f, 0.8f);
            raceUiAppendMethod.Invoke(state, new[] { root });

            RaceInGameControl room = FindControl(snapshot, "room-code-label");
            RaceInGameControl localPreparation = FindControlOrNull(snapshot, "local-preparation");
            object roomPanel = CreateTextPanel(ControlText(room), 0.8f, true);
            SetDimension(roomPanel, "Top", -40f, 0f);
            SetDimension(roomPanel, "Width", -5f, 0.5f);
            SetDimension(roomPanel, "Height", 50f, 0f);
            raceUiSetPaddingMethod.Invoke(roomPanel, new object[] { 10f });
            raceUiAppendMethod.Invoke(root, new[] { roomPanel });
            BindText(room.Id, roomPanel, ControlText, true);

            object preparationPanel = CreateTextPanel(
                localPreparation == null ? string.Empty : ControlText(localPreparation),
                0.8f,
                true);
            SetDimension(preparationPanel, "Top", -40f, 0f);
            SetDimension(preparationPanel, "Left", 5f, 0.5f);
            SetDimension(preparationPanel, "Width", -5f, 0.5f);
            SetDimension(preparationPanel, "Height", 50f, 0f);
            raceUiSetPaddingMethod.Invoke(preparationPanel, new object[] { 10f });
            raceUiAppendMethod.Invoke(root, new[] { preparationPanel });
            if (localPreparation != null)
            {
                BindText(localPreparation.Id, preparationPanel, ControlText, true);
            }

            object panel = Activator.CreateInstance(raceUiPanelType);
            SetDimension(panel, "Top", 20f, 0f);
            SetDimension(panel, "Width", 0f, 1f);
            SetDimension(panel, "Height", -125f, 1f);
            SetPanelColor(panel, 33, 43, 79, 204);
            raceUiAppendMethod.Invoke(root, new[] { panel });

            object list = CreateList(panel, 0f, 0f);
            RaceInGameControl[] controls = snapshot.Controls ?? new RaceInGameControl[0];
            for (int index = 0; index < controls.Length; index++)
            {
                RaceInGameControl control = controls[index];
                if (!string.Equals(control.LayoutGroup, "members", StringComparison.Ordinal))
                {
                    continue;
                }

                string suffix = control.Id.Substring("member:".Length);
                RaceInGameControl kick = FindControlOrNull(snapshot, "kick:" + suffix);
                object row = CreateLobbyMemberRow(control, kick);
                raceUiListAddMethod.Invoke(list, new[] { row });
            }

            AppendLobbyFooter(root, snapshot);
            return state;
        }

        private static object CreateRoot(float width, float top, float height)
        {
            object root = Activator.CreateInstance(raceUiElementType);
            SetDimension(root, "Width", width, 0f);
            SetDimension(root, "Top", top, 0f);
            SetDimension(root, "Height", height, height < 0f ? 1f : 0f);
            SetFloatField(root, "HAlign", 0.5f);
            raceUiSetPaddingMethod.Invoke(root, new object[] { 0f });
            return root;
        }

        private static object CreateList(object parent, float top, float height)
        {
            object list = Activator.CreateInstance(raceUiListType);
            SetDimension(list, "Top", top, 0f);
            SetDimension(list, "Width", -25f, 1f);
            SetDimension(list, "Height", height, 1f);
            SetFloatField(list, "ListPadding", 6f);
            raceUiManualSortField.SetValue(
                list,
                CreateNoOpDelegate(raceUiManualSortField.FieldType));
            raceUiAppendMethod.Invoke(parent, new[] { list });

            object scrollbar = Activator.CreateInstance(
                raceUiScrollbarType,
                new[] { Enum.ToObject(raceUiScrollbarThemeType, 0) });
            SetDimension(scrollbar, "Top", top, 0f);
            SetDimension(scrollbar, "Left", -20f, 1f);
            SetDimension(scrollbar, "Height", height, 1f);
            SetBoolField(scrollbar, "AutoHide", true);
            raceUiAppendMethod.Invoke(parent, new[] { scrollbar });
            raceUiSetScrollbarMethod.Invoke(list, new[] { scrollbar });
            return list;
        }

        private static object CreateListWithoutScrollbar(object parent, float top, float height)
        {
            object list = Activator.CreateInstance(raceUiListType);
            SetDimension(list, "Top", top, 0f);
            SetDimension(list, "Width", 0f, 1f);
            SetDimension(list, "Height", height, 1f);
            SetFloatField(list, "ListPadding", 6f);
            raceUiManualSortField.SetValue(
                list,
                CreateNoOpDelegate(raceUiManualSortField.FieldType));
            raceUiAppendMethod.Invoke(parent, new[] { list });
            return list;
        }

        private static void AppendWorldOptionRow(
            object panel,
            RaceInGameSnapshot snapshot,
            string group,
            float top)
        {
            RaceInGameControl[] controls = FindControlsByGroup(snapshot, group);
            for (int index = 0; index < controls.Length; index++)
            {
                object option = CreateOptionButton(controls[index], false);
                SetDimension(option, "Top", top, 0f);
                SetDimension(option, "Left", 0f, 0f);
                SetDimension(
                    option,
                    "Width",
                    -4f * (controls.Length - 1),
                    1f / controls.Length);
                SetFloatField(
                    option,
                    "HAlign",
                    controls.Length == 1 ? 0.5f : index / (float)(controls.Length - 1));
                SetDimension(option, "Height", 48f, 0f);
                raceUiAppendMethod.Invoke(panel, new[] { option });
            }
        }

        private static void AppendGroupedControls(object list, RaceInGameSnapshot snapshot)
        {
            RaceInGameControl[] controls = snapshot.Controls ?? new RaceInGameControl[0];
            var handled = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < controls.Length; index++)
            {
                RaceInGameControl control = controls[index];
                if (string.Equals(control.LayoutGroup, "footer", StringComparison.Ordinal) ||
                    handled.Contains(control.Id))
                {
                    continue;
                }

                if (control.Kind == RaceInGameControlKind.TextField)
                {
                    object field = CreateCharacterNameButton(control);
                    SetDimension(field, "Width", 0f, 1f);
                    SetDimension(field, "Height", 40f, 0f);
                    raceUiListAddMethod.Invoke(list, new[] { field });
                    handled.Add(control.Id);
                    continue;
                }

                if (control.Kind == RaceInGameControlKind.Label)
                {
                    object label = CreateText(ControlText(control), 0.78f, false);
                    SetDimension(label, "Width", 0f, 1f);
                    SetDimension(label, "Height", 28f, 0f);
                    raceUiListAddMethod.Invoke(list, new[] { label });
                    BindText(control.Id, label, ControlText, false);
                    handled.Add(control.Id);
                    continue;
                }

                string group = control.LayoutGroup;
                RaceInGameControl[] grouped = FindControlsByGroup(snapshot, group);
                if (group.StartsWith("primary-choice:", StringComparison.Ordinal) &&
                    grouped.Length >= 2)
                {
                    AppendPrimaryChoiceRow(list, grouped);
                    for (int groupedIndex = 0; groupedIndex < grouped.Length; groupedIndex++)
                    {
                        handled.Add(grouped[groupedIndex].Id);
                    }

                    continue;
                }

                int maximumItemsPerRow = string.Equals(
                    group,
                    "boss-penalty-kinds",
                    StringComparison.Ordinal)
                        ? 8
                        : 6;
                for (int offset = 0; offset < grouped.Length; offset += maximumItemsPerRow)
                {
                    int count = Math.Min(maximumItemsPerRow, grouped.Length - offset);
                    object row = Activator.CreateInstance(raceUiElementType);
                    SetDimension(row, "Width", 0f, 1f);
                    SetDimension(row, "Height", 62f, 0f);
                    for (int item = 0; item < count; item++)
                    {
                        RaceInGameControl optionControl = grouped[offset + item];
                        object option = CreateOptionButton(optionControl, false);
                        SetDimension(option, "Left", 0f, item / (float)Math.Max(1, count));
                        SetDimension(option, "Width", -6f, 1f / count);
                        SetDimension(option, "Height", 58f, 0f);
                        raceUiAppendMethod.Invoke(row, new[] { option });
                        handled.Add(optionControl.Id);
                    }

                    raceUiListAddMethod.Invoke(list, new[] { row });
                }
            }
        }

        private static void AppendPrimaryChoiceRow(
            object list,
            RaceInGameControl[] controls)
        {
            const float primaryWidth = 0.25f;
            const float optionStart = primaryWidth;
            const float optionWidth = 0.75f;
            int optionCount = controls.Length - 1;
            object row = Activator.CreateInstance(raceUiElementType);
            SetDimension(row, "Width", 0f, 1f);
            SetDimension(row, "Height", 62f, 0f);
            for (int index = 0; index < controls.Length; index++)
            {
                object option = CreateOptionButton(controls[index], false);
                if (index == 0)
                {
                    SetDimension(option, "Left", 0f, 0f);
                    SetDimension(option, "Width", 0f, primaryWidth);
                }
                else
                {
                    SetDimension(
                        option,
                        "Left",
                        0f,
                        optionStart + (index - 1) * optionWidth / optionCount);
                    SetDimension(option, "Width", -6f, optionWidth / optionCount);
                }

                SetDimension(option, "Height", 58f, 0f);
                raceUiAppendMethod.Invoke(row, new[] { option });
            }

            raceUiListAddMethod.Invoke(list, new[] { row });
        }

        private static object CreateCharacterNameButton(RaceInGameControl control)
        {
            object element = Activator.CreateInstance(
                raceUiCharacterNameButtonType,
                new[]
                {
                    CreateLocalizedText(control.Label),
                    CreateLocalizedText(control.AllowEmpty ? "Random" : string.Empty),
                    CreateLocalizedText(control.Description)
                });
            MethodInfo setContents = raceUiCharacterNameButtonType.GetMethod(
                "SetContents",
                BindingFlags.Instance | BindingFlags.Public);
            setContents.Invoke(element, new object[] { control.Value });
            AddControlClick(element, control.Id);
            Bind(control.Id, delegate(RaceInGameControl next)
            {
                setContents.Invoke(element, new object[] { next.Value });
            });
            return element;
        }

        private static object CreateOptionButton(RaceInGameControl control, bool iconOnly)
        {
            Type optionType = raceUiGroupOptionButtonOpenType.MakeGenericType(typeof(string));
            ConstructorInfo constructor = null;
            ConstructorInfo[] constructors = optionType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            for (int index = 0; index < constructors.Length; index++)
            {
                if (constructors[index].GetParameters().Length == 8)
                {
                    constructor = constructors[index];
                    break;
                }
            }

            if (constructor == null)
            {
                throw new MissingMethodException(optionType.FullName, ".ctor");
            }

            if (raceUiColorType == null)
            {
                raceUiColorType = constructor.GetParameters()[3].ParameterType;
            }

            bool originalSeedIcon = control.IconPath.StartsWith(
                "terraria-seed:",
                StringComparison.Ordinal);
            bool hasIcon = !string.IsNullOrWhiteSpace(control.IconPath);
            object title = iconOnly ? null : CreateLocalizedText(control.Label);
            object white = GetStaticColor("White");
            object element = constructor.Invoke(new[]
            {
                (object)control.Id,
                title,
                CreateLocalizedText(control.Description),
                white,
                !hasIcon || originalSeedIcon ? null : control.IconPath,
                (object)(hasIcon && !iconOnly ? 0.82f : 0.9f),
                (object)(hasIcon && !iconOnly ? 1f : 0.5f),
                (object)(hasIcon && !iconOnly ? 16f : 10f)
            });
            MethodInfo setCurrent = optionType.GetMethod(
                "SetCurrentOption",
                BindingFlags.Instance | BindingFlags.Public);
            ApplyOptionState(element, setCurrent, control);
            if (originalSeedIcon)
            {
                object seedIcon = CreateOriginalSpecialSeedElement(
                    control.IconPath.Substring("terraria-seed:".Length));
                SetFloatField(seedIcon, "HAlign", 0.5f);
                SetFloatField(seedIcon, "VAlign", 0.5f);
                SetBoolField(seedIcon, "IgnoresMouseInteraction", true);
                raceUiAppendMethod.Invoke(element, new[] { seedIcon });
            }
            else if (hasIcon)
            {
                AddCenteredOptionIconLayout(element, iconOnly);
            }

            AddControlClick(element, control.Id);
            AddDescriptionHandlers(element, control);
            Bind(control.Id, delegate(RaceInGameControl next)
            {
                ApplyOptionState(element, setCurrent, next);
            });
            return element;
        }

        private static void ApplyOptionState(
            object element,
            MethodInfo setCurrent,
            RaceInGameControl control)
        {
            setCurrent.Invoke(
                element,
                new object[] { control.Selected ? control.Id : string.Empty });
            FieldInfo fade = element.GetType().GetField(
                "FadeFromBlack",
                BindingFlags.Instance | BindingFlags.Public);
            if (fade != null)
            {
                fade.SetValue(element, control.Enabled ? 1f : 0.35f);
            }
        }

        private static object CreateOriginalSpecialSeedElement(string serverConfigName)
        {
            IEnumerable options = raceUiWorldGenerationOptionsProperty.GetValue(
                null,
                null) as IEnumerable;
            if (options == null)
            {
                throw new InvalidOperationException(
                    "Terraria did not provide its world generation options.");
            }

            foreach (object option in options)
            {
                if (option == null)
                {
                    continue;
                }

                PropertyInfo serverName = option.GetType().GetProperty(
                    "ServerConfigName",
                    BindingFlags.Instance | BindingFlags.Public);
                string value = serverName == null
                    ? string.Empty
                    : serverName.GetValue(option, null) as string;
                if (!string.Equals(value, serverConfigName, StringComparison.Ordinal))
                {
                    continue;
                }

                MethodInfo load = option.GetType().GetMethod(
                    "Load",
                    BindingFlags.Instance | BindingFlags.Public);
                MethodInfo provide = option.GetType().GetMethod(
                    "ProvideUIElement",
                    BindingFlags.Instance | BindingFlags.Public);
                if (load == null || provide == null)
                {
                    throw new MissingMethodException(
                        option.GetType().FullName,
                        "ProvideUIElement");
                }

                load.Invoke(option, null);
                object element = provide.Invoke(option, null);
                if (element == null)
                {
                    throw new InvalidOperationException(
                        "Terraria returned an empty special seed icon.");
                }

                return element;
            }

            throw new InvalidOperationException(
                "Terraria does not contain the special seed option '" +
                serverConfigName +
                "'.");
        }

        private static void AddCenteredOptionIconLayout(
            object element,
            bool centerHorizontally)
        {
            PropertyInfo iconProperty = element.GetType().GetProperty(
                "Icon",
                BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo scaleProperty = element.GetType().GetProperty(
                "IconScale",
                BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo offsetProperty = element.GetType().GetProperty(
                "IconOffset",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo getDimensions = raceUiElementType.GetMethod(
                "GetDimensions",
                BindingFlags.Instance | BindingFlags.Public);
            if (iconProperty == null ||
                scaleProperty == null ||
                offsetProperty == null ||
                getDimensions == null)
            {
                throw new MissingMemberException(
                    element.GetType().FullName,
                    "Icon layout");
            }

            AddEventHandler(element, raceUiUpdateEvent, delegate
            {
                object icon = iconProperty.GetValue(element, null);
                if (icon == null)
                {
                    return;
                }

                float iconWidth = ReadNumericMember(icon, "Width");
                float iconHeight = ReadNumericMember(icon, "Height");
                object dimensions = getDimensions.Invoke(element, null);
                float width = ReadNumericMember(dimensions, "Width");
                float height = ReadNumericMember(dimensions, "Height");
                if (iconWidth <= 0f || iconHeight <= 0f || width <= 0f || height <= 0f)
                {
                    return;
                }

                float availableWidth = centerHorizontally ? width - 8f : 40f;
                float scale = Math.Min(
                    1f,
                    Math.Min(
                        Math.Max(1f, availableWidth) / iconWidth,
                        Math.Max(1f, height - 8f) / iconHeight));
                float x = centerHorizontally
                    ? (width - iconWidth * scale) * 0.5f - 1f
                    : 4f;
                float y = (height - iconHeight * scale) * 0.5f - 1f;
                object offset = Activator.CreateInstance(
                    offsetProperty.PropertyType,
                    new object[] { x, y });
                scaleProperty.SetValue(element, scale, null);
                offsetProperty.SetValue(element, offset, null);
            });
        }

        private static float ReadNumericMember(object value, string name)
        {
            PropertyInfo property = value.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                return Convert.ToSingle(
                    property.GetValue(value, null),
                    CultureInfo.InvariantCulture);
            }

            FieldInfo field = value.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                return Convert.ToSingle(
                    field.GetValue(value),
                    CultureInfo.InvariantCulture);
            }

            throw new MissingMemberException(value.GetType().FullName, name);
        }

        private static object CreateLobbyMemberRow(
            RaceInGameControl member,
            RaceInGameControl kick)
        {
            object row = Activator.CreateInstance(raceUiPanelType);
            SetDimension(row, "Width", 0f, 1f);
            SetDimension(row, "Height", 58f, 0f);
            SetPanelColor(row, 63, 82, 151, 178);
            raceUiSetPaddingMethod.Invoke(row, new object[] { 10f });

            object name = CreateText(member.Label, 0.92f, false);
            SetDimension(name, "Width", -12f, 0.55f);
            SetDimension(name, "Height", 48f, 0f);
            SetFloatField(name, "HAlign", 0f);
            SetFloatField(name, "VAlign", 0.5f);
            raceUiAppendMethod.Invoke(row, new[] { name });

            object status = CreateText(member.Value, 0.9f, false);
            SetDimension(status, "Width", kick == null ? -12f : -95f, 1f);
            SetDimension(status, "Height", 48f, 0f);
            SetFloatField(status, "HAlign", 0f);
            SetFloatField(status, "VAlign", 0.5f);
            SetFloatMember(status, "TextOriginX", 1f);
            raceUiAppendMethod.Invoke(row, new[] { status });
            Bind(member.Id, delegate(RaceInGameControl next)
            {
                SetTextValue(name, next.Label, 0.92f, false);
                SetTextValue(status, next.Value, 0.9f, false);
            });

            if (kick != null)
            {
                object action = CreateText(kick.Label, 0.68f, false);
                SetDimension(action, "Left", -80f, 1f);
                SetDimension(action, "Width", 75f, 0f);
                SetDimension(action, "Height", 42f, 0f);
                SetFloatField(action, "VAlign", 0.5f);
                AddControlClick(action, kick.Id);
                raceUiAppendMethod.Invoke(row, new[] { action });
            }

            return row;
        }

        private static void AppendStatusText(
            object parent,
            float top,
            float height,
            bool alignBottom)
        {
            object status = CreateText(
                raceUiSnapshot == null ? string.Empty : raceUiSnapshot.Status,
                0.7f,
                false);
            SetDimension(status, "Top", top, 0f);
            SetDimension(status, "Width", -20f, 1f);
            SetDimension(status, "Height", height, 0f);
            SetFloatField(status, "HAlign", 0.5f);
            if (alignBottom)
            {
                SetFloatField(status, "VAlign", 1f);
            }

            raceUiAppendMethod.Invoke(parent, new[] { status });
            raceUiStatusText = status;
        }

        private static void AppendStatusText(object parent, float top, float height)
        {
            AppendStatusText(parent, top, height, false);
        }

        private static void AppendFooter(object root, RaceInGameSnapshot snapshot)
        {
            RaceInGameControl[] footer = FindControlsByGroup(snapshot, "footer");
            int count = Math.Max(1, footer.Length);
            for (int index = 0; index < footer.Length; index++)
            {
                RaceInGameControl control = footer[index];
                object button = CreateTextPanel(control.Label, 0.7f, true);
                SetDimension(button, "Top", -45f, 0f);
                SetDimension(button, "Left", 0f, index / (float)count);
                SetDimension(button, "Width", -10f, 1f / count);
                SetDimension(button, "Height", 50f, 0f);
                SetFloatField(button, "VAlign", 1f);
                AddFadedPanelHover(button, control.Id);
                AddControlClick(button, control.Id);
                SetSnapPoint(button, index);
                raceUiAppendMethod.Invoke(root, new[] { button });
                BindText(control.Id, button, ControlText, true);
            }
        }

        private static void AppendLobbyFooter(object root, RaceInGameSnapshot snapshot)
        {
            RaceInGameControl[] footer = FindControlsByGroup(snapshot, "footer");
            if (footer.Length != 2)
            {
                AppendFooter(root, snapshot);
                return;
            }

            for (int index = 0; index < footer.Length; index++)
            {
                RaceInGameControl control = footer[index];
                object button = CreateTextPanel(control.Label, 0.7f, true);
                SetDimension(button, "Top", -45f, 0f);
                SetDimension(button, "Left", index == 0 ? 0f : 5f, index * 0.5f);
                SetDimension(button, "Width", -5f, 0.5f);
                SetDimension(button, "Height", 50f, 0f);
                SetFloatField(button, "VAlign", 1f);
                AddFadedPanelHover(button, control.Id);
                AddControlClick(button, control.Id);
                SetSnapPoint(button, index);
                raceUiAppendMethod.Invoke(root, new[] { button });
                BindText(control.Id, button, ControlText, true);
            }
        }

        private static void AddControlClick(object element, string controlId)
        {
            AddLeftClickHandler(element, delegate
            {
                RaceInGameControl current = FindCurrentControl(controlId);
                if (current == null || !current.Enabled)
                {
                    return;
                }

                if (current.Kind == RaceInGameControlKind.TextField)
                {
                    ShowRaceUiKeyboard(current);
                    return;
                }

                if (string.Equals(controlId, "leave-room", StringComparison.Ordinal))
                {
                    PlayRaceUiSound(10);
                    ShowRaceUiConfirmation(current);
                    return;
                }

                PlayRaceUiSound(string.Equals(controlId, "close", StringComparison.Ordinal) ? 11 : 10);
                if (string.Equals(controlId, "terraria-single-player", StringComparison.Ordinal))
                {
                    NavigateToTerrariaMenu(1);
                    return;
                }

                if (string.Equals(controlId, "terraria-settings", StringComparison.Ordinal))
                {
                    NavigateToTerrariaMenu(11);
                    return;
                }

                if (string.Equals(controlId, "close", StringComparison.Ordinal))
                {
                    QueueRaceUiAction("close", RaceInGameActionKind.Close, string.Empty);
                    CloseRaceUiOnMainThread();
                    return;
                }

                QueueRaceUiAction(controlId, RaceInGameActionKind.Activate, current.Value);
                if (string.Equals(controlId, "leave-room", StringComparison.Ordinal) ||
                    string.Equals(controlId, "room-close", StringComparison.Ordinal))
                {
                    ArmRaceUiEmergencyExit();
                }
            });
        }

        private static void ShowRaceUiConfirmation(RaceInGameControl control)
        {
            object state = Activator.CreateInstance(raceUiStateType);
            object root = CreateRoot(500f, 260f, 180f);
            raceUiAppendMethod.Invoke(state, new[] { root });

            object prompt = CreateText(control.Description, 0.8f, true);
            SetDimension(prompt, "Width", 0f, 1f);
            SetDimension(prompt, "Height", 80f, 0f);
            SetFloatField(prompt, "HAlign", 0.5f);
            raceUiAppendMethod.Invoke(root, new[] { prompt });

            object yes = CreateTextPanel(GetTerrariaTextValue("Yes", "Yes"), 0.7f, true);
            SetDimension(yes, "Top", 100f, 0f);
            SetDimension(yes, "Width", -5f, 0.5f);
            SetDimension(yes, "Height", 50f, 0f);
            AddFadedPanelHover(yes, control.Id);
            AddLeftClickHandler(yes, delegate
            {
                PlayRaceUiSound(10);
                QueueRaceUiAction(control.Id, RaceInGameActionKind.Activate, control.Value);
                ArmRaceUiEmergencyExit();
                ReturnToRaceUiState();
            });
            SetSnapPoint(yes, 0);
            raceUiAppendMethod.Invoke(root, new[] { yes });

            object no = CreateTextPanel(GetTerrariaTextValue("No", "No"), 0.7f, true);
            SetDimension(no, "Top", 100f, 0f);
            SetDimension(no, "Left", 5f, 0.5f);
            SetDimension(no, "Width", -5f, 0.5f);
            SetDimension(no, "Height", 50f, 0f);
            AddFadedPanelHover(no, control.Id);
            AddLeftClickHandler(no, delegate
            {
                PlayRaceUiSound(11);
                ReturnToRaceUiState();
            });
            SetSnapPoint(no, 1);
            raceUiAppendMethod.Invoke(root, new[] { no });

            object menu = raceUiMenuField.GetValue(null);
            raceUiMenuModeField.SetValue(null, 888);
            raceUiSetStateMethod.Invoke(menu, new[] { state });
        }

        private static void NavigateToTerrariaMenu(int menuMode)
        {
            object menu = raceUiMenuField.GetValue(null);
            raceUiSetStateMethod.Invoke(menu, new object[] { null });
            raceUiMenuModeField.SetValue(null, menuMode);
        }

        private static void ArmRaceUiHomeRestore()
        {
            if (raceUiHomeRestoreHandler != null)
            {
                return;
            }

            Action callback = delegate
            {
                try
                {
                    PollLocalRacePlayerDeath();
                    RestoreRaceUiAtMainMenu();
                }
                catch
                {
                }
            };
            raceUiHomeRestoreHandler = callback;
            raceUiTickEvent.AddEventHandler(null, callback);
        }

        private static void PollLocalRacePlayerDeath()
        {
            if (configuration == null ||
                raceUiSnapshot == null ||
                !raceUiSnapshot.Visible ||
                (bool)raceUiGameMenuField.GetValue(null))
            {
                raceUiLocalPlayerWasDead = false;
                return;
            }

            object player = raceUiLocalPlayerProperty.GetValue(null, null);
            bool isDead =
                player != null &&
                (bool)raceUiPlayerDeadField.GetValue(player);
            if (isDead && !raceUiLocalPlayerWasDead)
            {
                string deathMessage = Interlocked.Exchange(ref raceUiLocalDeathMessage, null) ??
                    string.Empty;
                QueueRaceUiAction(
                    "race-player-died",
                    RaceInGameActionKind.Activate,
                    deathMessage);
            }

            raceUiLocalPlayerWasDead = isDead;
        }

        private static void RestoreRaceUiAtMainMenu()
        {
            if (raceUiSnapshot == null ||
                !raceUiSnapshot.Visible ||
                raceUiState == null ||
                !(bool)raceUiGameMenuField.GetValue(null) ||
                (int)raceUiMenuModeField.GetValue(null) != 0)
            {
                return;
            }

            object menu = raceUiMenuField.GetValue(null);
            raceUiMenuModeField.SetValue(null, 888);
            raceUiSetStateMethod.Invoke(menu, new[] { raceUiState });
        }

        private static void AddFadedPanelHover(object panel, string controlId)
        {
            bool highlighted = false;
            PropertyInfo hover = raceUiElementType.GetProperty(
                "IsMouseHovering",
                BindingFlags.Instance | BindingFlags.Public);
            AddEventHandler(panel, raceUiUpdateEvent, delegate
            {
                RaceInGameControl current = FindCurrentControl(controlId);
                bool shouldHighlight =
                    hover != null &&
                    (bool)hover.GetValue(panel, null) &&
                    current != null &&
                    current.Enabled;
                if (shouldHighlight == highlighted)
                {
                    return;
                }

                highlighted = shouldHighlight;
                if (highlighted)
                {
                    PlayRaceUiSound(12);
                    SetPanelColor(panel, 73, 94, 171, 255);
                    SetPanelMemberColor(panel, "BorderColor", raceUiFancyButtonHoverColor);
                }
                else
                {
                    object background = CreateColor(63, 82, 151, 255);
                    background = raceUiColorMultiplyMethod.Invoke(
                        null,
                        new[] { background, (object)0.8f });
                    SetPanelMemberColor(panel, "BackgroundColor", background);
                    SetPanelMemberColor(panel, "BorderColor", GetStaticColor("Black"));
                }
            });
        }

        private static void AddPlainMenuAnimation(object element, string controlId)
        {
            float scale = 0.8f;
            RaceInGameControl initial = FindCurrentControl(controlId);
            string displayedText = initial == null ? string.Empty : PlainMenuText(initial);
            Bind(controlId, delegate(RaceInGameControl current)
            {
                string nextText = PlainMenuText(current);
                if (!string.Equals(displayedText, nextText, StringComparison.Ordinal))
                {
                    displayedText = nextText;
                    SetTextValue(element, displayedText, scale, true);
                }
            });
            AddEventHandler(element, raceUiMouseOverEvent, delegate
            {
                RaceInGameControl current = FindCurrentControl(controlId);
                if (current != null && current.Enabled)
                {
                    PlayRaceUiSound(12);
                }
            });
            AddEventHandler(element, raceUiUpdateEvent, delegate
            {
                PropertyInfo hover = raceUiElementType.GetProperty(
                    "IsMouseHovering",
                    BindingFlags.Instance | BindingFlags.Public);
                bool hovering = hover != null && (bool)hover.GetValue(element, null);
                RaceInGameControl current = FindCurrentControl(controlId);
                float target = hovering && current != null && current.Enabled ? 1f : 0.8f;
                float previousScale = scale;
                if (scale < target)
                {
                    scale = Math.Min(target, scale + 0.02f);
                }
                else if (scale > target)
                {
                    scale = Math.Max(target, scale - 0.02f);
                }

                if (scale != previousScale)
                {
                    SetTextValue(element, displayedText, scale, true);
                }
            });
        }

        private static void AddDescriptionHandlers(object element, RaceInGameControl control)
        {
            if (string.IsNullOrWhiteSpace(control.Description))
            {
                return;
            }

            AddEventHandler(element, raceUiMouseOverEvent, delegate
            {
                if (raceUiStatusText != null)
                {
                    SetTextValue(raceUiStatusText, control.Description, 0.7f, false);
                }
            });
            AddEventHandler(element, raceUiMouseOutEvent, delegate
            {
                if (raceUiStatusText != null && raceUiSnapshot != null)
                {
                    SetTextValue(raceUiStatusText, raceUiSnapshot.Status, 0.7f, false);
                }
            });
        }

        private static void Bind(string id, Action<RaceInGameControl> refresh)
        {
            RaceUiRefreshers[id] = refresh;
        }

        private static void BindText(
            string id,
            object element,
            Func<RaceInGameControl, string> text,
            bool large)
        {
            Bind(id, delegate(RaceInGameControl control)
            {
                SetTextValue(element, text(control), large ? 0.7f : 0.75f, large);
            });
        }

        private static void BindProgress(string id, object element)
        {
            MethodInfo setProgress = raceUiProgressBarType.GetMethod(
                "SetProgress",
                BindingFlags.Instance | BindingFlags.Public);
            Bind(id, delegate(RaceInGameControl control)
            {
                setProgress.Invoke(element, new object[] { control.ProgressValue / 100f });
            });
        }

        private static void RefreshRaceUiSnapshot(RaceInGameSnapshot snapshot)
        {
            RaceInGameControl[] controls = snapshot.Controls ?? new RaceInGameControl[0];
            for (int index = 0; index < controls.Length; index++)
            {
                Action<RaceInGameControl> refresh;
                if (RaceUiRefreshers.TryGetValue(controls[index].Id, out refresh))
                {
                    refresh(controls[index]);
                }
            }

            if (raceUiStatusText != null)
            {
                SetTextValue(
                    raceUiStatusText,
                    snapshot.Status,
                    raceUiStatusTextLarge ? 0.8f : 0.7f,
                    raceUiStatusTextLarge);
            }

        }

        private static string BuildRaceUiStructureKey(RaceInGameSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append((int)snapshot.PageKind);
            RaceInGameControl[] controls = snapshot.Controls ?? new RaceInGameControl[0];
            for (int index = 0; index < controls.Length; index++)
            {
                RaceInGameControl control = controls[index];
                builder.Append('|')
                    .Append(control.Id).Append(':')
                    .Append((int)control.Kind).Append(':')
                    .Append(control.LayoutGroup).Append(':')
                    .Append(control.IconPath);
            }

            return builder.ToString();
        }

        private static RaceInGameControl FindControl(
            RaceInGameSnapshot snapshot,
            string id)
        {
            RaceInGameControl control = FindControlOrNull(snapshot, id);
            if (control == null)
            {
                throw new InvalidDataException("The Race menu is missing " + id + ".");
            }

            return control;
        }

        private static RaceInGameControl FindControlOrNull(
            RaceInGameSnapshot snapshot,
            string id)
        {
            RaceInGameControl[] controls = snapshot.Controls ?? new RaceInGameControl[0];
            for (int index = 0; index < controls.Length; index++)
            {
                if (string.Equals(controls[index].Id, id, StringComparison.Ordinal))
                {
                    return controls[index];
                }
            }

            return null;
        }

        private static RaceInGameControl[] FindControlsByGroup(
            RaceInGameSnapshot snapshot,
            string group)
        {
            var result = new List<RaceInGameControl>();
            RaceInGameControl[] controls = snapshot.Controls ?? new RaceInGameControl[0];
            for (int index = 0; index < controls.Length; index++)
            {
                if (string.Equals(controls[index].LayoutGroup, group, StringComparison.Ordinal))
                {
                    result.Add(controls[index]);
                }
            }

            return result.ToArray();
        }

        private static RaceInGameControl[] FindControlsByKind(
            RaceInGameSnapshot snapshot,
            RaceInGameControlKind kind)
        {
            var result = new List<RaceInGameControl>();
            RaceInGameControl[] controls = snapshot.Controls ?? new RaceInGameControl[0];
            for (int index = 0; index < controls.Length; index++)
            {
                if (controls[index].Kind == kind)
                {
                    result.Add(controls[index]);
                }
            }

            return result.ToArray();
        }

        private static RaceInGameControl FindCurrentControl(string id)
        {
            return raceUiSnapshot == null
                ? null
                : FindControlOrNull(raceUiSnapshot, id);
        }

        private static string PlainMenuText(RaceInGameControl control)
        {
            if (control.Kind == RaceInGameControlKind.TextField &&
                string.Equals(control.LayoutGroup, "menu", StringComparison.Ordinal))
            {
                return control.Label;
            }

            return control.Kind == RaceInGameControlKind.Toggle
                ? (control.Selected ? "[x] " : "[ ] ") + control.Label
                : ControlText(control);
        }

        private static string ControlText(RaceInGameControl control)
        {
            return string.IsNullOrWhiteSpace(control.Value)
                ? control.Label
                : control.Label + ": " + control.Value;
        }

        private static object CreateLocalizedText(string text)
        {
            return Activator.CreateInstance(
                raceUiLocalizedTextType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { "TerrariaSplit.Race", text ?? string.Empty },
                CultureInfo.InvariantCulture);
        }

        private static string GetTerrariaTextValue(string key, string fallback)
        {
            object value = raceUiLanguageGetTextValueMethod.Invoke(null, new object[] { key });
            string text = value as string;
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static object GetStaticColor(string name)
        {
            PropertyInfo property = raceUiColorType.GetProperty(
                name,
                BindingFlags.Static | BindingFlags.Public);
            if (property != null)
            {
                return property.GetValue(null, null);
            }

            FieldInfo field = raceUiColorType.GetField(
                name,
                BindingFlags.Static | BindingFlags.Public);
            if (field == null)
            {
                throw new MissingMemberException(raceUiColorType.FullName, name);
            }

            return field.GetValue(null);
        }

        private static void SetPanelColor(
            object panel,
            byte red,
            byte green,
            byte blue,
            byte alpha)
        {
            object color = CreateColor(red, green, blue, alpha);
            SetPanelMemberColor(panel, "BackgroundColor", color);
        }

        private static object CreateColor(
            byte red,
            byte green,
            byte blue,
            byte alpha)
        {
            return Activator.CreateInstance(
                raceUiColorType,
                new object[] { red, green, blue, alpha });
        }

        private static void SetPanelMemberColor(
            object panel,
            string memberName,
            object color)
        {
            PropertyInfo property = panel.GetType().GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public);
            FieldInfo field = panel.GetType().GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                property.SetValue(panel, color, null);
            }
            else if (field != null)
            {
                field.SetValue(panel, color);
            }
            else
            {
                throw new MissingMemberException(panel.GetType().FullName, memberName);
            }
        }

        private static void SetTextValue(
            object element,
            string text,
            float scale,
            bool large)
        {
            MethodInfo setText = element.GetType().GetMethod(
                "SetText",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(float), typeof(bool) },
                null);
            if (setText != null)
            {
                setText.Invoke(element, new object[] { text ?? string.Empty, scale, large });
                return;
            }

            setText = element.GetType().GetMethod(
                "SetText",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(object), typeof(float), typeof(bool) },
                null);
            if (setText != null)
            {
                setText.Invoke(element, new object[] { text ?? string.Empty, scale, large });
            }
        }

        private static void PlayRaceUiSound(int id)
        {
            try
            {
                raceUiPlaySoundMethod.Invoke(
                    null,
                    new object[] { id, -1, -1, 1, 1f, 0f });
            }
            catch
            {
            }
        }

        private static void AddEventHandler(
            object element,
            EventInfo eventInfo,
            Action callback)
        {
            Type delegateType = eventInfo.EventHandlerType;
            MethodInfo invoke = delegateType.GetMethod("Invoke");
            ParameterInfo[] parameters = invoke.GetParameters();
            var expressions = new ParameterExpression[parameters.Length];
            for (int index = 0; index < expressions.Length; index++)
            {
                expressions[index] = Expression.Parameter(
                    parameters[index].ParameterType,
                    "arg" + index);
            }

            MethodInfo actionInvoke = typeof(Action).GetMethod("Invoke");
            Delegate handler = Expression.Lambda(
                delegateType,
                Expression.Call(Expression.Constant(callback), actionInvoke),
                expressions).Compile();
            eventInfo.AddEventHandler(element, handler);
        }

        private static Delegate CreateNoOpDelegate(Type delegateType)
        {
            MethodInfo invoke = delegateType.GetMethod("Invoke");
            ParameterInfo[] parameters = invoke.GetParameters();
            var expressions = new ParameterExpression[parameters.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                expressions[index] = Expression.Parameter(
                    parameters[index].ParameterType,
                    "arg" + index);
            }

            return Expression.Lambda(delegateType, Expression.Empty(), expressions).Compile();
        }

        private static void SetDrawPanel(object panel, bool value)
        {
            PropertyInfo drawPanel = raceUiTextPanelType.GetProperty(
                "DrawPanel",
                BindingFlags.Instance | BindingFlags.Public);
            if (drawPanel != null)
            {
                drawPanel.SetValue(panel, value, null);
            }
        }

        private static object CreateTextPanel(string text, float scale, bool large)
        {
            return Activator.CreateInstance(raceUiTextPanelType, new object[] { (object)(text ?? string.Empty), scale, large });
        }

        private static object CreateText(string text, float scale, bool large)
        {
            object element = Activator.CreateInstance(
                raceUiTextType,
                new object[] { text ?? string.Empty, scale, large });
            SetFloatMember(element, "TextOriginY", 0.5f);
            return element;
        }

        private static void ShowRaceUiKeyboard(RaceInGameControl control)
        {
            Type submitType = raceUiKeyboardType.GetNestedType("KeyboardSubmitEvent", BindingFlags.Public);
            if (submitType == null)
            {
                throw new MissingMemberException(raceUiKeyboardType.FullName, "KeyboardSubmitEvent");
            }

            Delegate submit = CreateStringCallback(submitType, delegate(string value)
            {
                QueueRaceUiAction(control.Id, RaceInGameActionKind.TextSubmitted, value);
                ReturnToRaceUiState();
            });
            Action cancel = ReturnToRaceUiState;
            object keyboard = Activator.CreateInstance(
                raceUiKeyboardType,
                new object[]
                {
                    string.Equals(control.Id, "flow-member", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(control.Description)
                        ? control.Description
                        : control.Label,
                    control.Value,
                    submit,
                    cancel,
                    0,
                    control.AllowEmpty,
                    Math.Max(1, control.MaxLength)
                });
            object menu = raceUiMenuField.GetValue(null);
            raceUiSetStateMethod.Invoke(menu, new[] { keyboard });
        }

        private static void ReturnToRaceUiState()
        {
            if (raceUiState == null)
            {
                return;
            }

            object menu = raceUiMenuField.GetValue(null);
            raceUiMenuModeField.SetValue(null, 888);
            raceUiSetStateMethod.Invoke(menu, new[] { raceUiState });
        }

        private static Delegate CreateStringCallback(Type delegateType, Action<string> callback)
        {
            MethodInfo invoke = delegateType.GetMethod("Invoke");
            ParameterInfo[] parameters = invoke.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string))
            {
                throw new InvalidOperationException("The Terraria virtual keyboard callback has changed.");
            }

            ParameterExpression value = Expression.Parameter(typeof(string), "value");
            MethodInfo actionInvoke = typeof(Action<string>).GetMethod("Invoke");
            return Expression.Lambda(
                delegateType,
                Expression.Call(Expression.Constant(callback), actionInvoke, value),
                value).Compile();
        }

        private static void AddLeftClickHandler(object element, Action callback)
        {
            Type delegateType = raceUiLeftClickEvent.EventHandlerType;
            MethodInfo invoke = delegateType.GetMethod("Invoke");
            ParameterInfo[] parameters = invoke.GetParameters();
            var expressions = new ParameterExpression[parameters.Length];
            for (int index = 0; index < expressions.Length; index++)
            {
                expressions[index] = Expression.Parameter(parameters[index].ParameterType, "arg" + index);
            }

            MethodInfo actionInvoke = typeof(Action).GetMethod("Invoke");
            Delegate handler = Expression.Lambda(
                delegateType,
                Expression.Call(Expression.Constant(callback), actionInvoke),
                expressions).Compile();
            raceUiLeftClickEvent.AddEventHandler(element, handler);
        }

        private static void SetSnapPoint(object element, int id)
        {
            raceUiSetSnapPointMethod.Invoke(element, new object[] { "TerrariaSplitRace", id, null, null });
        }

        private static void TryInitializePlainMenuNavigation(Assembly terraria)
        {
            try
            {
                raceUiGetDimensionsMethod = RequireMethod(
                    raceUiElementType,
                    "GetDimensions",
                    BindingFlags.Instance | BindingFlags.Public);
                Type linkNavigatorType = RequireType(
                    terraria,
                    "Terraria.UI.Gamepad.UILinkPointNavigator");
                raceUiLinkSetPositionMethod = RequireMethod(
                    linkNavigatorType,
                    "SetPosition",
                    BindingFlags.Static | BindingFlags.Public);
                raceUiLinkChangePointMethod = RequireMethod(
                    linkNavigatorType,
                    "ChangePoint",
                    BindingFlags.Static | BindingFlags.Public);
                raceUiLinkPoints =
                    RequireField(linkNavigatorType, "Points").GetValue(null) as IDictionary;
                Type linkShortcutsType = linkNavigatorType.GetNestedType(
                    "Shortcuts",
                    BindingFlags.Public);
                raceUiFancyHighestIndexField = linkShortcutsType == null
                    ? null
                    : linkShortcutsType.GetField(
                        "FANCYUI_HIGHEST_INDEX",
                        BindingFlags.Static | BindingFlags.Public);
            }
            catch
            {
                ClearPlainMenuNavigation();
            }

            if (!IsPlainMenuNavigationAvailable())
            {
                ClearPlainMenuNavigation();
            }
        }

        private static bool IsPlainMenuNavigationAvailable()
        {
            return raceUiGetDimensionsMethod != null &&
                raceUiLinkSetPositionMethod != null &&
                raceUiLinkChangePointMethod != null &&
                raceUiLinkPoints != null &&
                raceUiFancyHighestIndexField != null;
        }

        private static void ClearPlainMenuNavigation()
        {
            raceUiGetDimensionsMethod = null;
            raceUiLinkSetPositionMethod = null;
            raceUiLinkChangePointMethod = null;
            raceUiLinkPoints = null;
            raceUiFancyHighestIndexField = null;
            raceUiDefaultNavigationPoint = -1;
        }

        private static void AddPlainMenuNavigation(
            object element,
            string controlId,
            int pointId,
            int upPointId,
            int downPointId)
        {
            object point = raceUiLinkPoints[pointId];
            if (point == null)
            {
                ClearPlainMenuNavigation();
                return;
            }

            if (!TryConfigurePlainMenuNavigationPoint(
                point,
                upPointId,
                downPointId))
            {
                ClearPlainMenuNavigation();
                return;
            }

            AddEventHandler(element, raceUiUpdateEvent, delegate
            {
                try
                {
                    if (!IsPlainMenuNavigationAvailable())
                    {
                        return;
                    }

                    RaceInGameControl current = FindCurrentControl(controlId);
                    if (!RaceUiReflection.TrySetPublicInstanceField(
                        point,
                        "Enabled",
                        current != null && current.Enabled))
                    {
                        ClearPlainMenuNavigation();
                        return;
                    }

                    object dimensions = raceUiGetDimensionsMethod.Invoke(element, null);
                    float x = ReadNumericMember(dimensions, "X") +
                        ReadNumericMember(dimensions, "Width") * 0.5f;
                    float y = ReadNumericMember(dimensions, "Y") +
                        ReadNumericMember(dimensions, "Height") * 0.5f;
                    Type vectorType =
                        raceUiLinkSetPositionMethod.GetParameters()[1].ParameterType;
                    object position = Activator.CreateInstance(
                        vectorType,
                        new object[] { x, y });
                    raceUiLinkSetPositionMethod.Invoke(
                        null,
                        new[] { (object)pointId, position });
                }
                catch
                {
                    ClearPlainMenuNavigation();
                }
            });
        }

        private static bool TryConfigurePlainMenuNavigationPoint(
            object point,
            int upPointId,
            int downPointId)
        {
            return RaceUiReflection.TrySetPublicInstanceField(point, "Left", -3) &&
                RaceUiReflection.TrySetPublicInstanceField(point, "Right", -4) &&
                RaceUiReflection.TrySetPublicInstanceField(point, "Up", upPointId) &&
                RaceUiReflection.TrySetPublicInstanceField(point, "Down", downPointId);
        }

        private static void ApplyDefaultRaceUiNavigationPoint()
        {
            if (raceUiDefaultNavigationPoint < 0 ||
                !IsPlainMenuNavigationAvailable())
            {
                return;
            }

            try
            {
                raceUiLinkChangePointMethod.Invoke(
                    null,
                    new object[] { raceUiDefaultNavigationPoint });
            }
            catch
            {
                ClearPlainMenuNavigation();
            }
        }

        private static void SetDimension(object element, string fieldName, float pixels, float percent)
        {
            FieldInfo field = raceUiElementType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            object dimension = field.GetValue(element);
            MethodInfo set = dimension.GetType().GetMethod("Set", new[] { typeof(float), typeof(float) });
            set.Invoke(dimension, new object[] { pixels, percent });
            field.SetValue(element, dimension);
        }

        private static void SetFloatField(object element, string fieldName, float value)
        {
            RaceUiReflection.TrySetPublicInstanceField(element, fieldName, value);
        }

        private static void SetIntField(object element, string fieldName, int value)
        {
            RaceUiReflection.TrySetPublicInstanceField(element, fieldName, value);
        }

        private static void SetBoolField(object element, string fieldName, bool value)
        {
            RaceUiReflection.TrySetPublicInstanceField(element, fieldName, value);
        }

        private static void SetFloatMember(object element, string name, float value)
        {
            PropertyInfo property = element.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite)
            {
                property.SetValue(element, value, null);
                return;
            }

            SetFloatField(element, name, value);
        }

        private static void QueueRaceUiAction(string controlId, RaceInGameActionKind kind, string value)
        {
            long actionId = Interlocked.Increment(ref raceUiActionId);
            long revision = raceUiSnapshot == null ? 0 : raceUiSnapshot.Revision;
            lock (RaceUiSync)
            {
                if (RaceUiActions.Count >= 64)
                {
                    RaceUiActions.Dequeue();
                }

                RaceUiActions.Enqueue(new RaceInGameAction(actionId, revision, controlId, kind, value));
            }
        }

        private static void QueueRaceGameMessage(string message, int kind)
        {
            QueueOnTerrariaMainThread(delegate
            {
                byte red = kind == 1 ? (byte)225 : (byte)255;
                byte green = kind == 1 ? (byte)25 : (byte)240;
                byte blue = kind == 1 ? (byte)25 : (byte)20;
                raceUiNewTextMethod.Invoke(
                    null,
                    new object[] { message, red, green, blue });
            });
        }

        private static void ArmRaceUiEmergencyExit()
        {
            long observedContact = Interlocked.Read(ref raceUiLastHostContactUtcTicks);
            Timer next = null;
            next = new Timer(
                delegate
                {
                    try
                    {
                        if (Interlocked.Read(ref raceUiLastHostContactUtcTicks) != observedContact)
                        {
                            return;
                        }

                        configuration = null;
                        ResetAdvancedDeterminismState();
                        runtimeFailure = null;
                        raceUiRuntimeFailure = null;
                        PrepareRestart();
                        QueueOnTerrariaMainThread(CloseRaceUiOnMainThread);
                    }
                    finally
                    {
                        Timer current = Interlocked.CompareExchange(
                            ref raceUiEmergencyExitTimer,
                            null,
                            next);
                        if (ReferenceEquals(current, next))
                        {
                            next.Dispose();
                        }
                    }
                },
                null,
                3000,
                Timeout.Infinite);
            Timer previous = Interlocked.Exchange(ref raceUiEmergencyExitTimer, next);
            previous?.Dispose();
        }

        private static RaceInGameAction[] DrainRaceUiActions()
        {
            lock (RaceUiSync)
            {
                RaceInGameAction[] actions = RaceUiActions.ToArray();
                RaceUiActions.Clear();
                return actions;
            }
        }

        private static void CloseRaceUiOnMainThread()
        {
            Timer emergencyExit = Interlocked.Exchange(ref raceUiEmergencyExitTimer, null);
            emergencyExit?.Dispose();
            Delegate homeRestore = raceUiHomeRestoreHandler;
            raceUiHomeRestoreHandler = null;
            if (homeRestore != null)
            {
                raceUiTickEvent.RemoveEventHandler(null, homeRestore);
            }
            try
            {
                object menu = raceUiMenuField.GetValue(null);
                raceUiSetStateMethod.Invoke(menu, new object[] { null });
                raceUiMenuModeField.SetValue(null, 0);
                raceUiSnapshot = null;
                raceUiState = null;
                raceUiStatusText = null;
                raceUiStatusTextLarge = false;
                raceUiStructureKey = null;
                raceUiLocalPlayerWasDead = false;
                Interlocked.Exchange(ref raceUiLocalDeathMessage, null);
                RaceUiRefreshers.Clear();
            }
            catch
            {
            }
        }
    }
}
