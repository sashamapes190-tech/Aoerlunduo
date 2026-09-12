using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using Core.Systems;
using Core.Units;
using Engine;
using StarterKit;
using StarterKit.Commands;
using UnityEngine;
using UnityEngine.UIElements;
using Map.Rendering;
using Map.Interaction;
using Unity.Collections;

namespace Aoerlunduo
{
    /// <summary>
    /// Thin game-layer bootstrap for the Archon Engine version of Aoerlunduo.
    /// Simulation, map ownership, economy, buildings and units stay in Archon/StarterKit.
    /// </summary>
    public sealed class AoerlunduoArchonRuntime : MonoBehaviour
    {
        private ArchonEngine engine;
        private Initializer starter;
        private CountrySelectionUI countrySelection;
        private UIDocument countrySelectionDocument;
        private Label instructionLabel;
        private Button startButton;
        private AoerlunduoCameraControls cameraControls;
        private AoerlunduoArmyVisualization armyVisualization;
        private ProvinceSelector provinceSelector;
        private Label controlsLabel;
        private Label armyStatusLabel;
        private Label dateLabel;
        private ushort selectedUnitId;
        private VisualElement eventPanel;
        private Label eventTitle;
        private Label eventDescription;
        private IDisposable monthlySubscription;
        private IDisposable dailySubscription;
        private IDisposable countrySubscription;
        private bool eventOpen;
        private bool resumeAfterEvent;

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => ArchonEngine.Instance != null && ArchonEngine.Instance.IsInitialized);
            engine = ArchonEngine.Instance;
            yield return new WaitUntil(() => Initializer.Instance != null && Initializer.Instance.IsInitialized);
            starter = Initializer.Instance;
            cameraControls = GetComponent<AoerlunduoCameraControls>();
            armyVisualization = GetComponent<AoerlunduoArmyVisualization>();
            provinceSelector = FindFirstObjectByType<ProvinceSelector>();

            countrySelection = FindFirstObjectByType<CountrySelectionUI>();
            countrySelection?.Show();
            countrySelectionDocument = countrySelection != null ? countrySelection.GetComponent<UIDocument>() : null;
            instructionLabel = countrySelectionDocument?.rootVisualElement.Q<Label>("instruction-label");
            startButton = countrySelectionDocument?.rootVisualElement.Q<Button>("start-button");
            if (startButton != null)
            {
                startButton.text = "开始游戏";
                startButton.clicked += EnsureCampaignStarted;
            }

            CreateOverlay(countrySelectionDocument);
            ConfigureUiPicking();
            ConfigureThinBorders();
            SelectDefaultCountry(countrySelection);

            monthlySubscription = engine.GameState.EventBus.Subscribe<MonthlyTickEvent>(OnMonthlyTick);
            dailySubscription = engine.GameState.EventBus.Subscribe<DailyTickEvent>(OnDailyTick);
            countrySubscription = engine.GameState.EventBus.Subscribe<PlayerCountrySelectedEvent>(OnCountrySelected);
        }

        private void LateUpdate()
        {
            if (controlsLabel != null && cameraControls != null)
            {
                controlsLabel.text = cameraControls.ShowingProvinceBorders
                    ? "省份视图 · 单击查看/选择军队 · 右键移动 · 滚轮缩放"
                    : "国家视图 · 滚轮放大进入省份视图 · 拖拽/WASD移动";
            }

            if (dateLabel != null && engine?.TimeManager != null)
            {
                var gameTime = engine.TimeManager.GetCurrentGameTime();
                string speed = engine.TimeManager.IsPaused ? "暂停" : $"{engine.TimeManager.GameSpeed}×";
                dateLabel.text = $"AO纪元 {gameTime.Year:D4}.{gameTime.Month:D2}.{gameTime.Day:D2} · {speed}";
            }

            if (countrySelection == null || !countrySelection.IsInitialized || !countrySelection.IsVisible) return;

            if (startButton != null) startButton.text = "开始游戏";
            if (instructionLabel == null) return;

            if (!countrySelection.HasSelectedCountry)
            {
                instructionLabel.text = "请在地图上选择一个国家";
                return;
            }

            string tag = engine.GameState.CountryQueries.GetTag(countrySelection.SelectedCountryId);
            instructionLabel.text = $"已选择：{GetChineseCountryName(tag)}\n点击开始进入游戏";
        }

        private void EnsureCampaignStarted()
        {
            if (starter?.PlayerState == null || starter.PlayerState.HasPlayerCountry || countrySelection == null || !countrySelection.HasSelectedCountry)
                return;

            ushort countryId = countrySelection.SelectedCountryId;
            starter.PlayerState.SetPlayerCountry(countryId);
            engine.GameState.EventBus.Emit(new PlayerCountrySelectedEvent
            {
                CountryId = countryId,
                TimeStamp = Time.time
            });
            countrySelection.Hide();
            engine.TimeManager.SetGameTime(2136, 1, 1, 0);
        }

        private void ConfigureUiPicking()
        {
            var documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var document in documents)
            {
                if (document.rootVisualElement != null)
                    document.rootVisualElement.pickingMode = PickingMode.Ignore;
            }
        }

        private void ConfigureThinBorders()
        {
            var dispatcher = FindFirstObjectByType<BorderComputeDispatcher>();
            dispatcher?.SetPixelPerfectParameters(0, 0, 0f);
        }

        private void SelectDefaultCountry(CountrySelectionUI countrySelection)
        {
            if (countrySelection == null || countrySelection.HasSelectedCountry) return;
            ushort countryId = engine.GameState.Countries.GetCountryIdFromTag("NWV");
            if (countryId == 0) return;

            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(CountrySelectionUI).GetField("selectedCountryId", flags)?.SetValue(countrySelection, countryId);
            typeof(CountrySelectionUI).GetField("hasSelectedCountry", flags)?.SetValue(countrySelection, true);

            startButton?.SetEnabled(true);
        }

        private static string GetChineseCountryName(string tag)
        {
            return tag switch
            {
                "NWV" => "诺德维恩", "VLS" => "瓦尔瑟", "EST" => "埃斯缇尔", "ALV" => "阿尔维昂",
                "VSK" => "维斯卡恩", "OSL" => "奥瑟兰", "NVK" => "诺尔瓦克", "HLV" => "赫尔维克",
                "ISD" => "伊斯兰德尔", "KLD" => "卡尔登", "MRC" => "梅瑞西亚", "SVL" => "塞维伦",
                "TLV" => "塔尔维斯", "ELS" => "埃伦萨", "LSV" => "洛萨维亚", "VRS" => "维拉辛",
                "DKS" => "多尔卡斯", "BRN" => "布雷恩", "OST" => "奥斯缇亚", "PLS" => "佩尔萨",
                "SLT" => "萨兰提尔", "ARD" => "艾瑞汀", "SLN" => "塞洛恩", "MRV" => "马尔维亚",
                "ISR" => "伊瑟拉", "KMO" => "凯尔莫", "ZVL" => "扎维伦", "SLD" => "苏兰德",
                _ => tag
            };
        }

        private void CreateOverlay(UIDocument document)
        {
            if (document == null || document.rootVisualElement == null) return;
            var root = document.rootVisualElement;

            var title = new VisualElement { name = "ao-title" };
            title.style.position = Position.Absolute;
            title.style.left = 18;
            title.style.top = 14;
            title.style.paddingLeft = 16;
            title.style.paddingRight = 16;
            title.style.paddingTop = 10;
            title.style.paddingBottom = 10;
            title.style.backgroundColor = new Color(0.035f, 0.055f, 0.09f, 0.92f);
            title.style.borderBottomLeftRadius = 8;
            title.style.borderBottomRightRadius = 8;
            title.style.borderTopLeftRadius = 8;
            title.style.borderTopRightRadius = 8;

            var name = new Label("奥尔伦多");
            name.style.fontSize = 25;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = new Color(0.95f, 0.82f, 0.46f);
            title.Add(name);

            dateLabel = new Label("AO纪元 2136.01.01 · 暂停");
            dateLabel.style.fontSize = 13;
            dateLabel.style.color = new Color(0.78f, 0.82f, 0.88f);
            title.Add(dateLabel);
            root.Add(title);

            controlsLabel = new Label("国家视图 · 滚轮放大进入省份视图 · 拖拽/WASD移动");
            controlsLabel.name = "ao-camera-help";
            controlsLabel.style.position = Position.Absolute;
            controlsLabel.style.right = 16;
            controlsLabel.style.bottom = 12;
            controlsLabel.style.paddingLeft = 12;
            controlsLabel.style.paddingRight = 12;
            controlsLabel.style.paddingTop = 7;
            controlsLabel.style.paddingBottom = 7;
            controlsLabel.style.backgroundColor = new Color(0.035f, 0.055f, 0.09f, 0.82f);
            controlsLabel.style.color = new Color(0.86f, 0.89f, 0.93f);
            controlsLabel.style.fontSize = 12;
            controlsLabel.pickingMode = PickingMode.Ignore;
            root.Add(controlsLabel);

            armyStatusLabel = new Label("进入游戏后，单击带有数字标记的省份选择军队");
            armyStatusLabel.name = "ao-army-status";
            armyStatusLabel.style.position = Position.Absolute;
            armyStatusLabel.style.right = 16;
            armyStatusLabel.style.bottom = 48;
            armyStatusLabel.style.paddingLeft = 12;
            armyStatusLabel.style.paddingRight = 12;
            armyStatusLabel.style.paddingTop = 7;
            armyStatusLabel.style.paddingBottom = 7;
            armyStatusLabel.style.backgroundColor = new Color(0.12f, 0.16f, 0.22f, 0.9f);
            armyStatusLabel.style.color = new Color(0.95f, 0.82f, 0.46f);
            armyStatusLabel.style.fontSize = 12;
            armyStatusLabel.pickingMode = PickingMode.Ignore;
            root.Add(armyStatusLabel);

            eventPanel = new VisualElement { name = "ao-random-event" };
            eventPanel.style.position = Position.Absolute;
            eventPanel.style.left = new Length(50, LengthUnit.Percent);
            eventPanel.style.top = new Length(50, LengthUnit.Percent);
            eventPanel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));
            eventPanel.style.width = 480;
            eventPanel.style.paddingLeft = 24;
            eventPanel.style.paddingRight = 24;
            eventPanel.style.paddingTop = 22;
            eventPanel.style.paddingBottom = 22;
            eventPanel.style.backgroundColor = new Color(0.05f, 0.065f, 0.1f, 0.97f);
            eventPanel.style.borderBottomLeftRadius = 10;
            eventPanel.style.borderBottomRightRadius = 10;
            eventPanel.style.borderTopLeftRadius = 10;
            eventPanel.style.borderTopRightRadius = 10;
            eventPanel.style.display = DisplayStyle.None;

            eventTitle = new Label();
            eventTitle.style.fontSize = 23;
            eventTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            eventTitle.style.color = new Color(0.95f, 0.82f, 0.46f);
            eventTitle.style.marginBottom = 12;
            eventPanel.Add(eventTitle);

            eventDescription = new Label();
            eventDescription.style.fontSize = 15;
            eventDescription.style.whiteSpace = WhiteSpace.Normal;
            eventDescription.style.color = Color.white;
            eventDescription.style.marginBottom = 18;
            eventPanel.Add(eventDescription);

            root.Add(eventPanel);
        }

        private void OnCountrySelected(PlayerCountrySelectedEvent evt)
        {
            var countryName = Core.Localization.LocalizationManager.Get(engine.GameState.CountryQueries.GetTag(evt.CountryId));
            Debug.Log($"[Aoerlunduo] AO 2136 campaign started as {countryName}");
            armyVisualization?.Initialize(engine.GameState, starter.UnitSystem);
            if (provinceSelector != null)
            {
                provinceSelector.OnProvinceClicked -= SelectArmyInProvince;
                provinceSelector.OnProvinceRightClicked -= MoveSelectedArmy;
                provinceSelector.OnProvinceClicked += SelectArmyInProvince;
                provinceSelector.OnProvinceRightClicked += MoveSelectedArmy;
            }
            StartCoroutine(CreateInitialArmies(evt.CountryId));
        }

        private IEnumerator CreateInitialArmies(ushort playerCountryId)
        {
            yield return null;
            var infantry = starter.UnitSystem?.GetUnitType("infantry");
            if (infantry == null) yield break;

            using var countries = engine.GameState.CountryQueries.GetAllCountryIds(Allocator.Temp);
            for (int c = 0; c < countries.Length; c++)
            {
                ushort countryId = countries[c];
                if (countryId == 0 || engine.GameState.Units.GetCountryUnits(countryId).Count > 0) continue;

                using var provinces = engine.GameState.CountryQueries.GetProvinces(countryId, Allocator.Temp);
                if (provinces.Length == 0) continue;
                int stackCount = countryId == playerCountryId ? Mathf.Min(3, provinces.Length) : 1;
                for (int i = 0; i < stackCount; i++)
                {
                    int provinceIndex = Mathf.Min(provinces.Length - 1, i * provinces.Length / stackCount);
                    ushort troopCount = (ushort)(countryId == playerCountryId ? 12 : 8);
                    engine.GameState.Units.CreateUnit(provinces[provinceIndex], countryId, infantry.ID, troopCount);
                }
            }

            if (armyStatusLabel != null)
                armyStatusLabel.text = "军队已部署 · 右键下达行军命令；暂停时可规划，恢复时间后行军";
        }

        private void SelectArmyInProvince(ushort provinceId)
        {
            if (starter?.PlayerState == null || !starter.PlayerState.HasPlayerCountry) return;
            selectedUnitId = 0;
            var units = starter.UnitSystem.GetUnitsInProvince(provinceId);
            foreach (ushort unitId in units)
            {
                var unit = starter.UnitSystem.GetUnit(unitId);
                if (unit.countryID == starter.PlayerState.PlayerCountryId && unit.unitCount > 0)
                {
                    selectedUnitId = unitId;
                    if (armyStatusLabel != null)
                        armyStatusLabel.text = $"已选择第 {unitId} 军团（{unit.unitCount}）· 右键目标省份移动";
                    return;
                }
            }

            if (armyStatusLabel != null)
                armyStatusLabel.text = $"省份 {provinceId} 没有己方军队";
        }

        private void MoveSelectedArmy(ushort targetProvinceId)
        {
            if (selectedUnitId == 0 || starter?.PlayerState == null) return;
            var unit = starter.UnitSystem.GetUnit(selectedUnitId);
            var path = engine.GameState.Pathfinding.FindPath(unit.provinceID, targetProvinceId, unit.countryID, unit.unitTypeID);
            if (path == null || path.Count < 2)
            {
                if (armyStatusLabel != null) armyStatusLabel.text = "无法到达该省份";
                return;
            }

            var unitType = starter.UnitSystem.GetUnitType(unit.unitTypeID);
            int daysPerProvince = Mathf.Max(1, unitType?.Speed ?? 2);
            var command = new QueueUnitMovementCommand
            {
                UnitId = selectedUnitId,
                Path = new List<ushort>(path),
                MovementDays = daysPerProvince,
                CountryId = unit.countryID
            };

            if (engine.GameState.TryExecuteCommand(command, out string message))
            {
                int totalDays = (path.Count - 1) * daysPerProvince;
                string paused = engine.TimeManager.IsPaused ? "；时间暂停，行军等待中" : string.Empty;
                if (armyStatusLabel != null)
                    armyStatusLabel.text = $"第 {selectedUnitId} 军团：{path.Count - 1} 段行程，预计 {totalDays} 天{paused}";
            }
            else if (armyStatusLabel != null)
            {
                armyStatusLabel.text = $"移动命令失败：{message}";
            }
        }

        private void OnDailyTick(DailyTickEvent evt)
        {
            ResolveBattles();
        }

        private void ResolveBattles()
        {
            var forcesByProvince = new Dictionary<ushort, Dictionary<ushort, List<ushort>>>();
            using var countries = engine.GameState.CountryQueries.GetAllCountryIds(Allocator.Temp);
            for (int c = 0; c < countries.Length; c++)
            {
                ushort countryId = countries[c];
                if (countryId == 0) continue;
                var unitIds = engine.GameState.Units.GetCountryUnits(countryId);
                for (int i = 0; i < unitIds.Count; i++)
                {
                    ushort unitId = unitIds[i];
                    var unit = engine.GameState.Units.GetUnit(unitId);
                    if (unit.unitCount == 0) continue;
                    if (!forcesByProvince.TryGetValue(unit.provinceID, out var countryForces))
                    {
                        countryForces = new Dictionary<ushort, List<ushort>>();
                        forcesByProvince.Add(unit.provinceID, countryForces);
                    }
                    if (!countryForces.TryGetValue(countryId, out var force))
                    {
                        force = new List<ushort>();
                        countryForces.Add(countryId, force);
                    }
                    force.Add(unitId);
                }
            }

            foreach (var provincePair in forcesByProvince)
            {
                if (provincePair.Value.Count < 2) continue;
                var sides = new List<KeyValuePair<ushort, List<ushort>>>(provincePair.Value);
                sides.Sort((a, b) => a.Key.CompareTo(b.Key));
                ResolveBattlePair(provincePair.Key, sides[0], sides[1]);
            }
        }

        private void ResolveBattlePair(ushort provinceId, KeyValuePair<ushort, List<ushort>> sideA, KeyValuePair<ushort, List<ushort>> sideB)
        {
            int strengthA = GetStrength(sideA.Value);
            int strengthB = GetStrength(sideB.Value);
            if (strengthA <= 0 || strengthB <= 0) return;

            foreach (ushort unitId in sideA.Value) engine.GameState.Units.MovementQueue.CancelMovement(unitId);
            foreach (ushort unitId in sideB.Value) engine.GameState.Units.MovementQueue.CancelMovement(unitId);

            int lossA = Mathf.Clamp(Mathf.CeilToInt(strengthB * 0.18f), 1, strengthA);
            int lossB = Mathf.Clamp(Mathf.CeilToInt(strengthA * 0.18f), 1, strengthB);
            ApplyLosses(sideA.Value, lossA, lossB, provinceId);
            ApplyLosses(sideB.Value, lossB, lossA, provinceId);

            ushort player = starter?.PlayerState?.PlayerCountryId ?? 0;
            if (armyStatusLabel != null && (sideA.Key == player || sideB.Key == player))
                armyStatusLabel.text = $"省份 {provinceId} 正在交战 · 我军与敌军每日结算伤亡";
        }

        private int GetStrength(List<ushort> unitIds)
        {
            int total = 0;
            for (int i = 0; i < unitIds.Count; i++)
                total += engine.GameState.Units.GetUnit(unitIds[i]).unitCount;
            return total;
        }

        private void ApplyLosses(List<ushort> unitIds, int losses, int kills, ushort provinceId)
        {
            int remaining = losses;
            for (int i = 0; i < unitIds.Count && remaining > 0; i++)
            {
                ushort unitId = unitIds[i];
                var unit = engine.GameState.Units.GetUnit(unitId);
                if (unit.unitCount == 0) continue;
                ushort applied = (ushort)Mathf.Min(unit.unitCount, remaining);
                var cold = engine.GameState.Units.GetColdData(unitId);
                cold.BattlesCount++;
                cold.TotalKills += kills;
                cold.RecentCombatHistory.Add(provinceId);
                engine.GameState.Units.RemoveTroops(unitId, applied);
                remaining -= applied;
            }
        }

        private void OnMonthlyTick(MonthlyTickEvent evt)
        {
            if (eventOpen || starter?.PlayerState == null || !starter.PlayerState.HasPlayerCountry || eventPanel == null) return;
            ushort countryId = starter.PlayerState.PlayerCountryId;
            int roll = Math.Abs(evt.GameTime.Year * 37 + evt.GameTime.Month * 11 + countryId * 7) % 6;
            if (roll != 0) return;

            int eventIndex = Math.Abs(evt.GameTime.Year + evt.GameTime.Month + countryId) % 3;
            if (eventIndex == 0)
            {
                OpenEvent("丰收季", "多地粮食丰收，地方议会请求决定新增收入的用途。",
                    "建立国家储备（国库 +40）", 40,
                    "扩大粮食出口（国库 +70）", 70);
            }
            else if (eventIndex == 1)
            {
                OpenEvent("边境摩擦", "边境巡逻队报告数起冲突，军方要求拨款稳定局势。",
                    "增援边防（国库 -30）", -30,
                    "派遣谈判团（国库 -10）", -10);
            }
            else
            {
                OpenEvent("行会请愿", "城市行会要求减轻负担，以扩大工坊与贸易网络。",
                    "提供补贴（国库 -50）", -50,
                    "维持税制（国库 +25）", 25);
            }
        }

        private void OpenEvent(string title, string description, string leftText, int leftGold, string rightText, int rightGold)
        {
            eventOpen = true;
            eventTitle.text = title;
            eventDescription.text = description;

            while (eventPanel.childCount > 2) eventPanel.RemoveAt(2);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.Add(CreateChoice(leftText, leftGold));
            row.Add(CreateChoice(rightText, rightGold));
            eventPanel.Add(row);
            eventPanel.style.display = DisplayStyle.Flex;
            resumeAfterEvent = !engine.TimeManager.IsPaused;
            engine.TimeManager.PauseTime();
        }

        private Button CreateChoice(string text, int goldDelta)
        {
            var button = new Button(() => ResolveEvent(goldDelta)) { text = text };
            button.style.width = 205;
            button.style.minHeight = 44;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.backgroundColor = new Color(0.18f, 0.25f, 0.34f);
            button.style.color = Color.white;
            return button;
        }

        private void ResolveEvent(int goldDelta)
        {
            if (!eventOpen || starter?.PlayerState == null) return;
            starter.EconomySystem.AddGoldToCountry(starter.PlayerState.PlayerCountryId, goldDelta);
            eventPanel.style.display = DisplayStyle.None;
            eventOpen = false;
            if (resumeAfterEvent) engine.TimeManager.StartTime();
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.clicked -= EnsureCampaignStarted;
            if (provinceSelector != null)
            {
                provinceSelector.OnProvinceClicked -= SelectArmyInProvince;
                provinceSelector.OnProvinceRightClicked -= MoveSelectedArmy;
            }
            monthlySubscription?.Dispose();
            dailySubscription?.Dispose();
            countrySubscription?.Dispose();
        }
    }
}
