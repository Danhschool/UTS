# Script Catalog — Assets/Scripts

> **Cập nhật:** 2026-06-05  
> **Phạm vi:** 456 file `.cs` trong `Assets/Scripts/`  
> **Định dạng:** mỗi script 1–2 dòng mô tả chức năng chính.

Danh sách **theo thư mục** (A→Z). Tra cứu nhanh: Ctrl+F tên file.

Xem thêm module + thứ tự đọc gợi ý: [`Assets-Scripts-By-Module.md`](Assets-Scripts-By-Module.md).

---

## AI\Config

- **AIDifficultyLevel.cs** (AI/Config/AIDifficultyLevel.cs) - Ba mức độ khó ship mặc định (Dễ / Trung bình / Khó).
- **AIDifficultyRuntimeOverlay.cs** (AI/Config/AIDifficultyRuntimeOverlay.cs) - Type AIDifficultyRuntimeOverlay.
- **AIDifficultySO.cs** (AI/Config/AIDifficultySO.cs) - SRP: Tham số độ khó AI — một asset cho mỗi chế độ (Dễ / Trung bình / Khó). Planner đọc overlay từ SO này; không hardcode ngưỡng trong manager.
- **AIGameSessionConfigSO.cs** (AI/Config/AIGameSessionConfigSO.cs) - SRP: Chọn độ khó mặc định khi vào trận (menu / GameSetup).

## AI\Core

- **AICommandDispatcher.cs** (AI/Core/AICommandDispatcher.cs) - Thực thi lệnh gameplay qua BaseCommand — mirror GameDevTV.RTS.Player.PlayerInput, không UI/EventBus lệnh.
- **AICommandIntent.cs** (AI/Core/AICommandIntent.cs) - Định danh manager cho logging và ưu tiên intent (M2+).
- **AIController.cs** (AI/Core/AIController.cs) - Điều phối tick AI: registry → world state snapshot → (M2+) managers → dispatcher. Không gắn vào GameDevTV.RTS.Player.PlayerInput; không raise CommandSelectedEvent.
- **AIFactionSightQuery.cs** (AI/Core/AIFactionSightQuery.cs) - SRP: Tầm nhìn planner cho phe AI — union vòng SightRadius từ unit/building cùng owner (không dùng fog Player1).
- **AIHitUtility.cs** (AI/Core/AIHitUtility.cs) - Tạo RaycastHit cho dispatcher — mirror click người chơi, không UI.
- **AIOwnershipGuard.cs** (AI/Core/AIOwnershipGuard.cs) - SRP: Xác nhận entity thuộc phe AI đang chạy planner/dispatcher.
- **AIPlannerStatusFormatter.cs** (AI/Core/AIPlannerStatusFormatter.cs) - SRP: Một dòng trạng thái AI cho Game Event Log (thay thông báo gather từng lần).
- **AIPlannerTickBudget.cs** (AI/Core/AIPlannerTickBudget.cs) - Type AIPlannerTickBudget.
- **AISceneEntityCache.cs** (AI/Core/AISceneEntityCache.cs) - SRP: Cache entity toàn scene tối đa một lần mỗi frame — tránh FindObjectsByType lặp trong military/economy.
- **AIWorkerCommandGuard.cs** (AI/Core/AIWorkerCommandGuard.cs) - SRP: Chặn AI gán lệnh macro khi worker đang xây hoặc đang gather/return — tránh phá Behavior micro.

## AI\Managers

- **AIBaseConfigResolver.cs** (AI/Managers/AIBaseConfigResolver.cs) - SRP: Suy ra lệnh train/build/research và ngưỡng worker từ Civil Central + worker commands.
- **AIBaseManager.cs** (AI/Managers/AIBaseManager.cs) - SRP: Train worker tại Civil Central; queue Store → Corral → Forge → Barrack → Tower; research Forge.
- **AIBasePriority.cs** (AI/Managers/AIBasePriority.cs) - Priority bands — infra build luôn trên economy gather (400).
- **AIBuildCommandCatalog.cs** (AI/Managers/AIBuildCommandCatalog.cs) - SRP: Cache toàn bộ BuildBuildingCommand trong project (một lần) — fallback khi worker/Inspector chưa gán SO.
- **AIBuildingPlacementUtility.cs** (AI/Managers/AIBuildingPlacementUtility.cs) - SRP: Tìm điểm đặt nhà cho AI — quét từ anchor, hợp lệ chỉ khi BuildBuildingCommand Restrictions pass.
- **AIBuildUnitCommandCatalog.cs** (AI/Managers/AIBuildUnitCommandCatalog.cs) - SRP: Cache BuildUnitCommand trong project — fallback khi Barrack/Inspector chưa gán SO.
- **AIConstructionAssignment.cs** (AI/Managers/AIConstructionAssignment.cs) - Type AIConstructionAssignment.
- **AIEconomyConfigResolver.cs** (AI/Managers/AIEconomyConfigResolver.cs) - SRP: Suy ra thông số economy từ map + registry — không cần kéo asset/distance tay trên Inspector.
- **AIEconomyCorralPlanner.cs** (AI/Managers/AIEconomyCorralPlanner.cs) - SRP: Thiếu food → xây thêm Corral (passive food); không thay thế gather mỏ food.
- **AIEconomyFoodGatherUtility.cs** (AI/Managers/AIEconomyFoodGatherUtility.cs) - SRP: Nhận diện mỏ food còn khai thác và trạng thái thiếu food trên kho.
- **AIEconomyGatherSupplyIndex.cs** (AI/Managers/AIEconomyGatherSupplyIndex.cs) - SRP: Lập danh sách mỏ gather visible hợp lệ theo loại — một lần mỗi tick economy (không lặp theo worker).
- **AIEconomyGatherTerritoryGuard.cs** (AI/Managers/AIEconomyGatherTerritoryGuard.cs) - SRP: Chặn AI economy chọn mỏ/supply trong vùng nhà human (PvAI M5).
- **AIEconomyManager.cs** (AI/Managers/AIEconomyManager.cs) - SRP: Worker gather 40/40/20 hoặc 60% thiếu; hết mỏ food → 70/30 đá-gỗ; thiếu food → thêm Corral; Store xa CC. Mỗi worker: GatherCommand → BT Gather Sub Graph tự loop + tự return khi đầy (Petra: không spam ReturnSupplies). Chỉ mỏ GatherableSupply.IsVisible (fog gameplay chung với người chơi); khóa gather trên Worker.ShouldIssueGatherTo.
- **AIEconomyPriority.cs** (AI/Managers/AIEconomyPriority.cs) - Priority bands — gather thấp hơn base infra build (760+).
- **AIEconomySupplyKindClassifier.cs** (AI/Managers/AIEconomySupplyKindClassifier.cs) - SRP: Phân loại SupplySO thành đá / gỗ / food cho economy AI.
- **AIEconomyWildCorpseFoodUtility.cs** (AI/Managers/AIEconomyWildCorpseFoodUtility.cs) - SRP: Phân biệt mỏ food tĩnh (berry, farm) với xác thú sau săn — xác không được coi là "mỏ food" cho slot 7:3 đá-gỗ.
- **AIEconomyWildFoodPlanner.cs** (AI/Managers/AIEconomyWildFoodPlanner.cs) - SRP: Thiếu food và không có mỏ food visible — worker (hoặc lính rảnh) đi săn WildAnimal.
- **AIForgeResearchRoundUtility.cs** (AI/Managers/AIForgeResearchRoundUtility.cs) - SRP: Forge operational + gom lệnh research theo tier (dùng bởi AIForgeResearchTierPlanner).
- **AIForgeResearchTierPlanner.cs** (AI/Managers/AIForgeResearchTierPlanner.cs) - SRP: Xen kẽ research theo tier (Damage 1, Health 1, …) với phase economy (build/gather).
- **AIInfraBuildOrderTracker.cs** (AI/Managers/AIInfraBuildOrderTracker.cs) - SRP: Nhớ loại nhà AI đã enqueue/dispatch build — tránh giao trùng trước khi prefab vào registry.
- **AIInfraBuildUtility.cs** (AI/Managers/AIInfraBuildUtility.cs) - SRP: Thứ tự xây nhà base — Store → Corral → Forge → Barrack → Tower.
- **AIMilitaryArmyAssemblyTracker.cs** (AI/Managers/AIMilitaryArmyAssemblyTracker.cs) - Type AIMilitaryArmyAssemblyTracker.
- **AIMilitaryArmySquadExecutor.cs** (AI/Managers/AIMilitaryArmySquadExecutor.cs) - SRP: Một đội quân — Attack cả nhóm lên mục tiêu chung, rồi formation Move (không một điểm chồng).
- **AIMilitaryConfigResolver.cs** (AI/Managers/AIMilitaryConfigResolver.cs) - SRP: Resolve tham số quân sự (tỷ lệ dân/quân, ngưỡng tấn công, lệnh train) cho tick hiện tại.
- **AIMilitaryDefenseRingBuildCoordinator.cs** (AI/Managers/AIMilitaryDefenseRingBuildCoordinator.cs) - SRP: Đặt tháp trên vòng mỗi tick; nhịp 5 phút chỉ mở khóa thêm vòng bán kính.
- **AIMilitaryDefenseRingCombatUtility.cs** (AI/Managers/AIMilitaryDefenseRingCombatUtility.cs) - SRP: Combat trong vùng leash — đánh địch thấy ngay; Stop khi unit ra khỏi vùng.
- **AIMilitaryDefenseRingEconomyRecovery.cs** (AI/Managers/AIMilitaryDefenseRingEconomyRecovery.cs) - SRP: Thiếu tài nguyên khi spam tháp nhiều lần → yêu cầu thêm worker / Corral.
- **AIMilitaryDefenseRingPlacementUtility.cs** (AI/Managers/AIMilitaryDefenseRingPlacementUtility.cs) - SRP: Điểm đặt Barrack/Tháp trên vòng cố định quanh CC.
- **AIMilitaryDefenseRingPlanner.cs** (AI/Managers/AIMilitaryDefenseRingPlanner.cs) - SRP: Vòng tháp 100m +100m/vòng; nhịp 5 phút chỉ mở khóa thêm vòng (bán kính), không gate đặt tháp.
- **AIMilitaryDefenseRingTowerArmyGate.cs** (AI/Managers/AIMilitaryDefenseRingTowerArmyGate.cs) - SRP: Ngưỡng spawn quân trước mỗi tháp vòng — tổng đã spawn (kể cả chết), bậc × (số tháp + 1).
- **AIMilitaryEnemyTracker.cs** (AI/Managers/AIMilitaryEnemyTracker.cs) - SRP: Vị trí Civil Central địch đã từng thấy (fair fog) — mục tiêu attack wave.
- **AIMilitaryExpansionPlanner.cs** (AI/Managers/AIMilitaryExpansionPlanner.cs) - SRP: Phase XP đầu (hạ tầng quân) và phase mở rộng (train + tấn công).
- **AIMilitaryHostileScanner.cs** (AI/Managers/AIMilitaryHostileScanner.cs) - SRP: Quét địch / Civil Central địch trong scene (một lần mỗi tick military) — không cache registry owner khác.
- **AIMilitaryLoosePatrolPlanner.cs** (AI/Managers/AIMilitaryLoosePatrolPlanner.cs) - SRP: Tuần tra lẻ quanh CC trong vòng bán kính — không formation, mỗi lính một điểm Move riêng.
- **AIMilitaryManager.cs** (AI/Managers/AIMilitaryManager.cs) - SRP: Train Barrack, phòng thủ Civil Central, hàng Defense Tower, attack wave tới CC địch.
- **AIMilitaryMapExplorationUtility.cs** (AI/Managers/AIMilitaryMapExplorationUtility.cs) - SRP: Đánh giá vùng patrol đã được khám phá đủ chưa (bán kính patrol + fog explored).
- **AIMilitaryOperationalLeash.cs** (AI/Managers/AIMilitaryOperationalLeash.cs) - SRP: Giới hạn hoạt động quân trong đĩa quanh CC (vòng tháp ngoài + leash).
- **AIMilitaryPatrolUtility.cs** (AI/Managers/AIMilitaryPatrolUtility.cs) - SRP: Điểm patrol scout — vòng tròn mở rộng dần quanh Civil Central của phe AI (không hướng thẳng CC địch).
- **AIMilitaryPostContactOffensePlanner.cs** (AI/Managers/AIMilitaryPostContactOffensePlanner.cs) - SRP: Sau khi scout thấy địch không còn tụ — đợi khám phá + xây (mặc định 2 phút) rồi tổng tấn công về CC địch.
- **AIMilitaryPriority.cs** (AI/Managers/AIMilitaryPriority.cs) - Priority bands — phòng thủ CC trên train Barrack và tấn công.
- **AIMilitaryRallyCombatPlanner.cs** (AI/Managers/AIMilitaryRallyCombatPlanner.cs) - SRP: Phase Attacking — gán Attack lên địch visible; Move formation chỉ là fallback.
- **AIMilitaryRallyPlanner.cs** (AI/Managers/AIMilitaryRallyPlanner.cs) - SRP: Phát hiện nhà chính / đối thủ RTS (không animal) và chọn điểm rally tấn công formation.
- **AIMilitaryRallySessionTracker.cs** (AI/Managers/AIMilitaryRallySessionTracker.cs) - SRP: Phiên rally hai pha — lùi tập hợp rồi mới tấn công (theo Owner AI).
- **AIMilitaryStagingPlacementUtility.cs** (AI/Managers/AIMilitaryStagingPlacementUtility.cs) - SRP: Điều chỉnh điểm formation/staging — tránh chồng lên công trường đang xây.
- **AIMilitaryUnitMixPlanner.cs** (AI/Managers/AIMilitaryUnitMixPlanner.cs) - SRP: Chọn train Barrack theo <b>tỷ lệ %</b> trên tổng quân (active + queue) — deficit = targetShare − currentShare.
- **AIMilitaryUnitMixSlot.cs** (AI/Managers/AIMilitaryUnitMixSlot.cs) - Một loại quân trong cơ cấu Barrack — lệnh Build Unit + trọng số spawn.
- **AIWorkerGatherRefreshPlanner.cs** (AI/Managers/AIWorkerGatherRefreshPlanner.cs) - SRP: Mỗi N tick AI — Stop rồi Move ngắn để worker thoát gather cũ; economy vẫn gán mỏ cho worker rảnh cùng tick.
- **AIWorkerGatherSlotPlanner.cs** (AI/Managers/AIWorkerGatherSlotPlanner.cs) - SRP: Cân bằng 40% đá / 40% gỗ / 20% food; mất cân bằng 60% thiếu + 20% mỗi loại kia. Hết mỏ food visible → chỉ đá/gỗ theo tỷ lệ 7:3 (phần lớn tối thiểu 7 worker khi đủ quy mô).

## AI\Sandbox

- **AISandboxWinLoseChecklist.cs** (AI/Sandbox/AISandboxWinLoseChecklist.cs) - SRP: Theo dõi win/lose Civil Central trong scene sandbox — tick checklist cho QA. Gắn cùng GameObject có PlayerGameEventLogListener hoặc GameEventLog.

## AI\State

- **AIUnitRegistry.cs** (AI/State/AIUnitRegistry.cs) - Cache unit/building/supply từ EventBus theo Owner — không Find trong tick.
- **AIWorldState.cs** (AI/State/AIWorldState.cs) - Type AIWorldState.

## AI\Strategy

- **AIInfluenceMap.cs** (AI/Strategy/AIInfluenceMap.cs) - SRP: Lưới influence thô (XZ) — defense quanh Civil Central, threat địch, economic cụm mỏ xa. Dùng cho AIBaseManager / AIEconomyManager / AIMilitaryManager.
- **AIInfluenceMapConfigResolver.cs** (AI/Strategy/AIInfluenceMapConfigResolver.cs) - SRP: Tự tính tỷ lệ influence (cell, bán kính, weight) từ footprint nhà và phạm vi đặt base.
- **AIInfluenceMapTickContext.cs** (AI/Strategy/AIInfluenceMapTickContext.cs) - Type AIInfluenceMapTickContext.
- **AIInfluenceMapTickPlanner.cs** (AI/Strategy/AIInfluenceMapTickPlanner.cs) - SRP: Rebuild AIInfluenceMap một lần mỗi tick từ AIWorldStateSnapshot.
- **AIInfluencePlacementUtility.cs** (AI/Strategy/AIInfluencePlacementUtility.cs) - SRP: Chọn điểm đặt nhà — ưu tiên cell influence an toàn, fallback quét NavMesh + Restrictions + field 20m.
- **AIPriorityQueue.cs** (AI/Strategy/AIPriorityQueue.cs) - Priority bands theo plan V2 (cao → thấp).

## Audio

- **AttackAudioUtility.cs** (Audio/AttackAudioUtility.cs) - SRP: Chọn cue tấn công theo AttackConfigSO.
- **AudioAccess.cs** (Audio/AudioAccess.cs) - SRP: Static accessor cho playback service sau khi AudioBootstrap khởi tạo.
- **AudioBootstrap.cs** (Audio/AudioBootstrap.cs) - SRP: Khởi tạo AudioPlaybackService và giữ reference DontDestroyOnLoad.
- **AudioChannel.cs** (Audio/AudioChannel.cs) - Enum kênh mixer: Sfx, Ui, Voice, Music.
- **AudioClipCatalogSO.cs** (Audio/AudioClipCatalogSO.cs) - ScriptableObject cau hinh du lieu.
- **AudioCueId.cs** (Audio/AudioCueId.cs) - Type AudioCueId.
- **AudioMixerParameterNames.cs** (Audio/AudioMixerParameterNames.cs) - Exposed parameter names on RTS Audio Mixer — phải khớp tên trong Unity Audio Mixer.
- **AudioPlayback3DSettings.cs** (Audio/AudioPlayback3DSettings.cs) - Type AudioPlayback3DSettings.
- **AudioPlaybackService.cs** (Audio/AudioPlaybackService.cs) - SRP: Phát clip 2D từ catalog, pool AudioSource, nhạc nền loop riêng.
- **AudioSettingsVolumePanelBinder.cs** (Audio/AudioSettingsVolumePanelBinder.cs) - SRP: Gắn mọi Slider trong panel Settings theo tên hàng (Master, Music, Sfx, UI, Voice). Đặt trên root Dialog Setting hoặc Panel Setting.
- **AudioVolumeChannel.cs** (Audio/AudioVolumeChannel.cs) - Type AudioVolumeChannel.
- **AudioVolumeController.cs** (Audio/AudioVolumeController.cs) - SRP: Lưu/load volume (PlayerPrefs), áp dụng lên AudioMixer và/hoặc AudioPlaybackService.
- **AudioVolumeSliderBinder.cs** (Audio/AudioVolumeSliderBinder.cs) - SRP: Gắn Slider UI → AudioVolumeController cho một kênh volume.
- **IAudioPlaybackService.cs** (Audio/IAudioPlaybackService.cs) - Type IAudioPlaybackService.
- **MenuAudioController.cs** (Audio/MenuAudioController.cs) - SRP: Âm thanh menu — nhạc nền MenuMusic + click UiSelect trên mọi Button con. MainMenu: startMenuMusic=true. SSScene: startMenuMusic=false (giữ nhạc từ MainMenu qua AudioBootstrap DontDestroyOnLoad).
- **PlayerAudioListener.cs** (Audio/PlayerAudioListener.cs) - SRP: Map Bus{T} gameplay events → AudioCueId cho local player.
- **PregameAudioTransition.cs** (Audio/PregameAudioTransition.cs) - SRP: Chuyển giao audio giữa menu/setup và gameplay.
- **SupplyGatherAudioUtility.cs** (Audio/SupplyGatherAudioUtility.cs) - SRP: Map SupplySO → cue gather (stone / wood / food).

## Behavior\Animal

- **AnimalEatAnimationAction.cs** (Behavior/Animal/AnimalEatAnimationAction.cs) - Node hanh dong Unity Behavior Graph.
- **AnimalIdleAnimationAction.cs** (Behavior/Animal/AnimalIdleAnimationAction.cs) - Node hanh dong Unity Behavior Graph.
- **EvaluateAnimalAICommandAction.cs** (Behavior/Animal/EvaluateAnimalAICommandAction.cs) - Node hanh dong Unity Behavior Graph.
- **SpawnCorpseFoodSupplyAction.cs** (Behavior/Animal/SpawnCorpseFoodSupplyAction.cs) - Node hanh dong Unity Behavior Graph.

## Behavior

- **AttackTargetAction.cs** (Behavior/AttackTargetAction.cs) - Node hanh dong Unity Behavior Graph.
- **BuildBuildingAction.cs** (Behavior/BuildBuildingAction.cs) - Node hanh dong Unity Behavior Graph.
- **BuildingEventChannel.cs** (Behavior/BuildingEventChannel.cs) - Kenh event Behavior Graph.
- **BuildingIsInProgressCondition.cs** (Behavior/BuildingIsInProgressCondition.cs) - Type BuildingIsInProgressCondition.

## Behavior\Death

- **DeathBehaviorNodeUtility.cs** (Behavior/Death/DeathBehaviorNodeUtility.cs) - Static utility.
- **DisableUnitDeathGameplayAction.cs** (Behavior/Death/DisableUnitDeathGameplayAction.cs) - Node hanh dong Unity Behavior Graph.
- **FreezeUnitDeathPoseAction.cs** (Behavior/Death/FreezeUnitDeathPoseAction.cs) - Node hanh dong Unity Behavior Graph.
- **HoldUnitDeathPoseAction.cs** (Behavior/Death/HoldUnitDeathPoseAction.cs) - Node hanh dong Unity Behavior Graph.
- **SetUnitDeathAnimatorAction.cs** (Behavior/Death/SetUnitDeathAnimatorAction.cs) - Node hanh dong Unity Behavior Graph.
- **SinkUnitDownAction.cs** (Behavior/Death/SinkUnitDownAction.cs) - Node hanh dong Unity Behavior Graph.
- **WaitUnitDeathAnimationAction.cs** (Behavior/Death/WaitUnitDeathAnimationAction.cs) - Node hanh dong Unity Behavior Graph.

## Behavior

- **FindClosestCommandPostAction.cs** (Behavior/FindClosestCommandPostAction.cs) - Node hanh dong Unity Behavior Graph.
- **GameObjectListSizeCondition.cs** (Behavior/GameObjectListSizeCondition.cs) - Type GameObjectListSizeCondition.
- **GatherSuppliesAction.cs** (Behavior/GatherSuppliesAction.cs) - Node hanh dong Unity Behavior Graph.
- **GatherSuppliesEventChannel.cs** (Behavior/GatherSuppliesEventChannel.cs) - Kenh event Behavior Graph.
- **LoadUnitEventChannel.cs** (Behavior/LoadUnitEventChannel.cs) - Kenh event Behavior Graph.
- **MoveToGatherableSupplyAction.cs** (Behavior/MoveToGatherableSupplyAction.cs) - Node hanh dong Unity Behavior Graph.
- **MoveToTargetGameObjectAction.cs** (Behavior/MoveToTargetGameObjectAction.cs) - Node hanh dong Unity Behavior Graph.
- **MoveToTargetLocationAction.cs** (Behavior/MoveToTargetLocationAction.cs) - Node hanh dong Unity Behavior Graph.
- **PickClosestPointOnColliderAction.cs** (Behavior/PickClosestPointOnColliderAction.cs) - Node hanh dong Unity Behavior Graph.
- **PickClosestPointOnTargetColliderAction.cs** (Behavior/PickClosestPointOnTargetColliderAction.cs) - Node hanh dong Unity Behavior Graph.
- **PickRandomLocationWithinRendererBoundsAction.cs** (Behavior/PickRandomLocationWithinRendererBoundsAction.cs) - Node hanh dong Unity Behavior Graph.
- **SamplePositionAction.cs** (Behavior/SamplePositionAction.cs) - Node hanh dong Unity Behavior Graph.
- **SetAgentAvoidanceAction.cs** (Behavior/SetAgentAvoidanceAction.cs) - Node hanh dong Unity Behavior Graph.
- **SetNavMeshAgentEnabledAction.cs** (Behavior/SetNavMeshAgentEnabledAction.cs) - Node hanh dong Unity Behavior Graph.
- **SetTargetFromFirstObjectInListAction.cs** (Behavior/SetTargetFromFirstObjectInListAction.cs) - Node hanh dong Unity Behavior Graph.
- **StopAgentAction.cs** (Behavior/StopAgentAction.cs) - Node hanh dong Unity Behavior Graph.
- **TranslatePositionAction.cs** (Behavior/TranslatePositionAction.cs) - Node hanh dong Unity Behavior Graph.

## Commands

- **AttackCommand.cs** (Commands/AttackCommand.cs) - Ra lệnh tấn công mục tiêu dưới con trỏ (sau selection).
- **AvailableCommandsResolver.cs** (Commands/AvailableCommandsResolver.cs) - Gom danh sách lệnh hiệu lực cho unit (override trước, rồi lệnh gốc), giống GameDevTV.RTS.Player.PlayerInput.
- **BaseCommand.cs** (Commands/BaseCommand.cs) - Abstract ScriptableObject lệnh — slot UI, icon, ghost prefab, restrictions đặt nhà.
- **BuildBuildingCommand.cs** (Commands/BuildBuildingCommand.cs) - Ghost đặt nhà + spawn placeholder → BuildBuildingAction (M17).
- **BuildingRestrictionSO.cs** (Commands/BuildingRestrictionSO.cs) - ScriptableObject cau hinh du lieu.
- **BuildUnitCommand.cs** (Commands/BuildUnitCommand.cs) - Đưa unit vào queue sản xuất tại building có queue.
- **CancelBuildingCommand.cs** (Commands/CancelBuildingCommand.cs) - SO lenh CanHandle/Handle.
- **CommandContext.cs** (Commands/CommandContext.cs) - Context thực thi lệnh: Owner local, unit đích, RaycastHit, danh sách selection.
- **CommandFactionVisibility.cs** (Commands/CommandFactionVisibility.cs) - SRP: Tầm nhìn khi thực thi lệnh — human dùng fog UI; AI dùng FactionFogQuery (sight radius phe bot).
- **CommandOwnershipUtility.cs** (Commands/CommandOwnershipUtility.cs) - SRP: Xác nhận unit/building nhận lệnh trùng phe với CommandContext.Owner.
- **CommandSupplyCostUtility.cs** (Commands/CommandSupplyCostUtility.cs) - Lấy chi phí / nhãn hành động từ các lệnh build & research (SRP cho cảnh báo tài nguyên).
- **GatherCommand.cs** (Commands/GatherCommand.cs) - Trong input: arm gather — Worker nhận lệnh trước khi BT loop (chi tiết M3).
- **ICommand.cs** (Commands/ICommand.cs) - Interface lệnh — CanHandle(CommandContext) và Handle(context); triển khai bởi BaseCommand SO.
- **IUnlockableCommand.cs** (Commands/IUnlockableCommand.cs) - SO lenh CanHandle/Handle.
- **LoadIntoCommand.cs** (Commands/LoadIntoCommand.cs) - SO lenh CanHandle/Handle.
- **LoadUnitCommand.cs** (Commands/LoadUnitCommand.cs) - SO lenh CanHandle/Handle.
- **MoveCommand.cs** (Commands/MoveCommand.cs) - Gán đích điểm/formation khi người chơi click bản đồ (sau khi chọn unit).
- **OverrideCommandsCommand.cs** (Commands/OverrideCommandsCommand.cs) - SO lenh CanHandle/Handle.
- **ResearchUpgradeCommand.cs** (Commands/ResearchUpgradeCommand.cs) - SO lenh CanHandle/Handle.
- **StopCommand.cs** (Commands/StopCommand.cs) - Dừng mọi hành động hiện tại của selection.
- **UnloadAllUnitsCommand.cs** (Commands/UnloadAllUnitsCommand.cs) - SO lenh CanHandle/Handle.

## Environment

- **GatherableSupply.cs** (Environment/GatherableSupply.cs) - Type GatherableSupply.
- **IGatherable.cs** (Environment/IGatherable.cs) - Type IGatherable.

## EventBus

- **Bus.cs** (EventBus/Bus.cs) - Event bus static generic theo Owner — đăng ký delegate và Raise(event) tách từng phe.
- **IEvent.cs** (EventBus/IEvent.cs) - Marker interface — mọi payload event phải implement để dùng Bus<T>.
- **SupplySO.cs** (EventBus/SupplySO.cs) - ScriptableObject loại tài nguyên (Food, Wood, Stone…) — icon, tên hiển thị.

## Events

- **ActiveCommandChangedEvent.cs** (Events/ActiveCommandChangedEvent.cs) - Lệnh đang chờ xác nhận trên map (ghost / placement). Command = null khi hủy hoặc hoàn tất.
- **BuildingConstructStartedEvent.cs** (Events/BuildingConstructStartedEvent.cs) - Phát khi GameDevTV.RTS.Behavior.BuildBuildingAction bắt đầu (nhà được spawn / tiếp tục xây) — dùng để gỡ ghost đặt chỗ trên UI.
- **BuildingDeathEvent.cs** (Events/BuildingDeathEvent.cs) - Payload Bus<T> theo Owner.
- **BuildingSpawnEvent.cs** (Events/BuildingSpawnEvent.cs) - Payload Bus<T> theo Owner.
- **CommandSelectedEvent.cs** (Events/CommandSelectedEvent.cs) - Payload Bus<T> theo Owner.
- **HotkeyTriggeredEvent.cs** (Events/HotkeyTriggeredEvent.cs) - Payload Bus<T> theo Owner.
- **PlaceholderDestroyEvent.cs** (Events/PlaceholderDestroyEvent.cs) - Payload Bus<T> theo Owner.
- **PlaceholderSpawnEvent.cs** (Events/PlaceholderSpawnEvent.cs) - Payload Bus<T> theo Owner.
- **SupplyDepletedEvent.cs** (Events/SupplyDepletedEvent.cs) - Payload Bus<T> theo Owner.
- **SupplyEvent.cs** (Events/SupplyEvent.cs) - Payload Bus<T> theo Owner.
- **SupplySpawnEvent.cs** (Events/SupplySpawnEvent.cs) - Payload Bus<T> theo Owner.
- **UnitDeathEvent.cs** (Events/UnitDeathEvent.cs) - Payload Bus<T> theo Owner.
- **UnitDeselectedEvent.cs** (Events/UnitDeselectedEvent.cs) - Payload Bus<T> theo Owner.
- **UnitLoadEvent.cs** (Events/UnitLoadEvent.cs) - Payload Bus<T> theo Owner.
- **UnitSelectedEvent.cs** (Events/UnitSelectedEvent.cs) - Payload Bus<T> theo Owner.
- **UnitSpawnEvent.cs** (Events/UnitSpawnEvent.cs) - Payload Bus<T> theo Owner.
- **UnitUnloadEvent.cs** (Events/UnitUnloadEvent.cs) - Payload Bus<T> theo Owner.
- **UpgradeResearchedEvent.cs** (Events/UpgradeResearchedEvent.cs) - Payload Bus<T> theo Owner.

## Game

- **GameMatchOverlayStateSync.cs** (Game/GameMatchOverlayStateSync.cs) - SRP: Đồng bộ overlay in-game (tốc độ, pause, panel, đầu hàng) cho cả hai client MP. Settings chỉ local — không gửi qua lớp này.
- **GamePauseService.cs** (Game/GamePauseService.cs) - SRP: Tạm dừng simulation in-game (Time.timeScale) — tách pause chia sẻ MP và pause local (settings).
- **GameSpeedController.cs** (Game/GameSpeedController.cs) - SRP: Điều khiển tốc độ simulation (1x / 2x) qua Time.timeScale.
- **MatchOutcomeFlow.cs** (Game/MatchOutcomeFlow.cs) - SRP: Chuyển scene khi trận kết thúc (đầu hàng / thua).

## Game\Pregame

- **PregameApplicationQuit.cs** (Game/Pregame/PregameApplicationQuit.cs) - SRP: Thoát game / dừng Play mode trong Editor.
- **PregameMenuSceneNavigator.cs** (Game/Pregame/PregameMenuSceneNavigator.cs) - SRP: Chuyển scene menu / setup — không đi qua Loading (chỉ gameplay mới qua Loading).
- **PregamePlayMode.cs** (Game/Pregame/PregamePlayMode.cs) - Enum chế độ pregame (Offline PvAI, Multiplayer host/client…) — lưu trong PregameSessionState.
- **PregameSessionState.cs** (Game/Pregame/PregameSessionState.cs) - SRP: Lưu lựa chọn từ menu / màn setup trước khi vào Loading → gameplay.

## Game\Startup\Editor

- **GameplayLoadingSceneSetupEditor.cs** (Game/Startup/Editor/GameplayLoadingSceneSetupEditor.cs) - SRP: Tạo Loading.unity và cập nhật Build Settings cho luồng load scene.

## Game\Startup

- **GameplayInGameReadyController.cs** (Game/Startup/GameplayInGameReadyController.cs) - SRP: Phase B — trong scene game: chờ owner + Civil Central, bar 85%→100%, mở input/audio.
- **GameplayLoadingSceneController.cs** (Game/Startup/GameplayLoadingSceneController.cs) - SRP: Phase A — Loading scene: async load file (offline) hoặc chờ host chuyển scene (MP).
- **GameplayLoadingSceneView.cs** (Game/Startup/GameplayLoadingSceneView.cs) - SRP: UI loading scene — fill bar Image (không dùng ProgressBar mask).
- **GameplaySceneLoader.cs** (Game/Startup/GameplaySceneLoader.cs) - SRP: API chuyển scene gameplay qua Loading scene (Phase A — load file / chờ MP).
- **GameplaySceneLoaderHost.cs** (Game/Startup/GameplaySceneLoaderHost.cs) - SRP: Host DontDestroyOnLoad — coroutine loading sống sót khi LoadSceneAsync(Single) hủy Loading scene.
- **GameplaySceneLoadRequest.cs** (Game/Startup/GameplaySceneLoadRequest.cs) - SRP: Gắn lên UI MainMenu / lobby offline — gọi GameplaySceneLoader.RequestLoad.
- **GameplayStartupGate.cs** (Game/Startup/GameplayStartupGate.cs) - SRP: Cổng "gameplay đã mở" — input/audio/UI gameplay chỉ chạy khi unlocked. Không đụng Time.timeScale.
- **GameplayStartupReadiness.cs** (Game/Startup/GameplayStartupReadiness.cs) - SRP: Kiểm tra client/local đã sẵn sàng chơi (owner + Civil Central).
- **GameplayStartupScenes.cs** (Game/Startup/GameplayStartupScenes.cs) - SRP: Nhận diện scene gameplay (Game*, RtsNet_Game, …) — không áp loading lên menu/lobby.
- **RtsNetworkSceneLoadHooksRegistration.cs** (Game/Startup/RtsNetworkSceneLoadHooksRegistration.cs) - Type RtsNetworkSceneLoadHooksRegistration.

## Gameplay

- **GameplayMapCoreMarker.cs** (Gameplay/GameplayMapCoreMarker.cs) - SRP: Đánh dấu root prefab lõi map (spawn PvE + PvP) — Editor tìm và cập nhật scene.

## Hotkeys

- **BuildingTypeHotkeySetup.cs** (Hotkeys/BuildingTypeHotkeySetup.cs) - SRP: Gắn prefab nhà → HotkeyId A/S/D và đăng ký delegate lên HotkeyService.
- **CompositeHotkeyGate.cs** (Hotkeys/CompositeHotkeyGate.cs) - ISP: Kết hợp nhiều gate nhỏ (UI focus, MP local owner…).

## Hotkeys\Handlers

- **ActionBarSlotHotkeyHandler.cs** (Hotkeys/Handlers/ActionBarSlotHotkeyHandler.cs) - Phím 1–9 → kích hoạt lệnh slot tương ứng trên unit đang chọn.
- **CameraFollowUnitHotkeyHandler.cs** (Hotkeys/Handlers/CameraFollowUnitHotkeyHandler.cs) - F → pan camera tới unit/nhà phe local đang chọn.
- **CameraResetHotkeyHandler.cs** (Hotkeys/Handlers/CameraResetHotkeyHandler.cs) - Home → reset zoom / góc camera.
- **CancelSelectionHotkeyHandler.cs** (Hotkeys/Handlers/CancelSelectionHotkeyHandler.cs) - Esc → gọi IHotkeyCancelTarget. Mẫu handler mở rộng theo từng HotkeyId.
- **DeleteSelectionHotkeyHandler.cs** (Hotkeys/Handlers/DeleteSelectionHotkeyHandler.cs) - Delete → xóa selection phe local (chậm / có hiệu ứng Die).
- **DeleteSelectionImmediateHotkeyHandler.cs** (Hotkeys/Handlers/DeleteSelectionImmediateHotkeyHandler.cs) - Shift+Delete → xóa ngay selection phe local.
- **HotkeyDebugLogHandler.cs** (Hotkeys/Handlers/HotkeyDebugLogHandler.cs) - Debug: log mọi hotkey được kích hoạt (tắt trên build release).
- **StopUnitsHotkeyHandler.cs** (Hotkeys/Handlers/StopUnitsHotkeyHandler.cs) - H → gọi IHotkeyStopUnitsTarget. Mẫu handler lệnh unit.
- **UnitTypeSelectHotkeyHandlerDelegate.cs** (Hotkeys/Handlers/UnitTypeSelectHotkeyHandlerDelegate.cs) - SRP: Handler không-MonoBehaviour — một cặp HotkeyId + prefab unit.

## Hotkeys

- **HotkeyBindingCatalogSO.cs** (Hotkeys/HotkeyBindingCatalogSO.cs) - Dữ liệu cấu hình mapping phím → HotkeyId. Tạo asset: Create > RTS > Hotkeys > Binding Catalog.
- **HotkeyBindingEntry.cs** (Hotkeys/HotkeyBindingEntry.cs) - Một hành động có thể gán nhiều tổ hợp phím thay thế (OR).
- **HotkeyBindingPreferences.cs** (Hotkeys/HotkeyBindingPreferences.cs) - SRP: Lưu/đọc override hotkey trong PlayerPrefs để runtime và UI dùng chung.
- **HotkeyChord.cs** (Hotkeys/HotkeyChord.cs) - Một tổ hợp phím (modifier + phím chính). So khớp theo Input System Key.
- **HotkeyComponentUtility.cs** (Hotkeys/HotkeyComponentUtility.cs) - Tìm interface trên cùng GameObject (Unity không hỗ trợ GetComponent trực tiếp với interface).
- **HotkeyContext.cs** (Hotkeys/HotkeyContext.cs) - Type HotkeyContext.
- **HotkeyDefaults.cs** (Hotkeys/HotkeyDefaults.cs) - Mapping mặc định UTS (QWERTY). Dùng khi chưa gán HotkeyBindingCatalogSO.
- **HotkeyHandlerBase.cs** (Hotkeys/HotkeyHandlerBase.cs) - Base MonoBehaviour cho handler: kéo vào HotkeySystem hoặc để con của cùng GameObject.
- **HotkeyId.cs** (Hotkeys/HotkeyId.cs) - Định danh phím tắt. Thêm giá trị mới ở cuối enum khi mở rộng tính năng. Tham chiếu mô tả người chơi: Resources/UI/hotkeys_uts_vi.json
- **HotkeyService.cs** (Hotkeys/HotkeyService.cs) - SRP: Quét binding, gọi handler đã đăng ký. Không phụ thuộc MonoBehaviour.
- **HotkeySystem.cs** (Hotkeys/HotkeySystem.cs) - Entry point: gắn vào scene, tick HotkeyService mỗi frame.
- **IHotkeyGate.cs** (Hotkeys/IHotkeyGate.cs) - Chặn hotkey khi đang nhập chat/UI (ISP: gate nhỏ, một nhiệm vụ).
- **IHotkeyHandler.cs** (Hotkeys/IHotkeyHandler.cs) - Một handler xử lý đúng một HotkeyId. Thêm class mới implement interface này để mở rộng.
- **IHotkeyInputSource.cs** (Hotkeys/IHotkeyInputSource.cs) - Đọc trạng thái bàn phím (DIP: HotkeyService không phụ thuộc Unity Input trực tiếp).
- **LocalHumanHotkeyGate.cs** (Hotkeys/LocalHumanHotkeyGate.cs) - Chặn hotkey khi MP chưa gán LocalOwner (P2 vào game trễ, lobby chưa sync…). Offline: không chặn (LocalHumanOwnerService tự bootstrap P1).

## Hotkeys\Targets

- **IHotkeyActionBarTarget.cs** (Hotkeys/Targets/IHotkeyActionBarTarget.cs) - Bridge phím 1–9 → lệnh slot trên thanh action của selection hiện tại.
- **IHotkeyCameraTarget.cs** (Hotkeys/Targets/IHotkeyCameraTarget.cs) - Bridge hotkey Home / F → camera gameplay.
- **IHotkeyCancelTarget.cs** (Hotkeys/Targets/IHotkeyCancelTarget.cs) - Abstraction cho hủy chọn / hủy đặt công trình (Esc). PlayerInput có thể implement sau.
- **IHotkeyDeleteSelectionTarget.cs** (Hotkeys/Targets/IHotkeyDeleteSelectionTarget.cs) - Bridge hotkey Delete / Shift+Delete → xóa selection phe local.
- **IHotkeyStopUnitsTarget.cs** (Hotkeys/Targets/IHotkeyStopUnitsTarget.cs) - Abstraction cho lệnh Stop (H). Unit selection controller implement sau.
- **IHotkeyUnitTypeSelectTarget.cs** (Hotkeys/Targets/IHotkeyUnitTypeSelectTarget.cs) - Bridge hotkey Q/W/E/R → chọn unit theo prefab archetype trên màn hình.

## Hotkeys

- **UiFocusHotkeyGate.cs** (Hotkeys/UiFocusHotkeyGate.cs) - Chặn hotkey khi người chơi đang focus ô nhập liệu (chat, search…).
- **UnitTypeHotkeySetup.cs** (Hotkeys/UnitTypeHotkeySetup.cs) - SRP: Gắn prefab archetype → HotkeyId và đăng ký delegate lên HotkeyService.
- **UnityHotkeyInputSource.cs** (Hotkeys/UnityHotkeyInputSource.cs) - Đọc phím qua Input System. Keyboard được cache một lần mỗi frame trong Tick.

## MapTools\Editor

- **QuickPrefabScatterWindow.cs** (MapTools/Editor/QuickPrefabScatterWindow.cs) - Type QuickPrefabScatterWindow.

## Minimap

- **IMinimapCameraNavigator.cs** (Minimap/IMinimapCameraNavigator.cs) - Type IMinimapCameraNavigator.
- **MinimapController.cs** (Minimap/MinimapController.cs) - Gắn trên Minimap Container: hiển thị RT từ MinimapRenderCamera và click di chuyển camera.
- **MinimapExploredFogOverlay.cs** (Minimap/MinimapExploredFogOverlay.cs) - Lớp UI fog minimap: đen (chưa explore), mờ (explored), trong suốt (đang nhìn thấy).
- **MinimapFogSystemReference.cs** (Minimap/MinimapFogSystemReference.cs) - Tham chiếu camera/RT fog explored + vision dùng chung cho minimap overlay và icon.
- **MinimapIconStyleSO.cs** (Minimap/MinimapIconStyleSO.cs) - ScriptableObject cau hinh du lieu.
- **MinimapIconView.cs** (Minimap/MinimapIconView.cs) - Type MinimapIconView.
- **MinimapInputHandler.cs** (Minimap/MinimapInputHandler.cs) - Handler phim tat.
- **MinimapMapBoundsSO.cs** (Minimap/MinimapMapBoundsSO.cs) - ScriptableObject cau hinh du lieu.
- **MinimapMarkerPresentation.cs** (Minimap/MinimapMarkerPresentation.cs) - Type MinimapMarkerPresentation.
- **MinimapRenderCamera.cs** (Minimap/MinimapRenderCamera.cs) - Camera ortho nhìn từ trên, render scene vào RT hiển thị trên minimap UI.
- **MinimapSupplyIconsController.cs** (Minimap/MinimapSupplyIconsController.cs) - Icon supply trên minimap: hiện khi đang trong vision; sau lần đầu mất vision thì luôn hiện.
- **MinimapUnitIconsController.cs** (Minimap/MinimapUnitIconsController.cs) - Hiển thị icon unit/building trên minimap UI (overlay), dùng UnitSO.Icon — không gắn object 3D trên đầu unit.

## Movement

- **MovementCursor.cs** (Movement/MovementCursor.cs) - Positions the move marker and plays looped animation until the instance is destroyed.

## Netplay

- **MpFogRefreshThrottle.cs** (Netplay/MpFogRefreshThrottle.cs) - SRP: Giới hạn tần suất refresh fog/visibility MP — tránh lag do FindObjects + full pass lặp.
- **MpFogVisionSpawnRefresh.cs** (Netplay/MpFogVisionSpawnRefresh.cs) - SRP: Sau khi unit/building MP replicate — refresh layer vision + fog plane texture cho local human.
- **MpLocalOwnerSceneSync.cs** (Netplay/MpLocalOwnerSceneSync.cs) - SRP: Sau load RtsNet_Game — gán LocalOwner từ RtsLobbyPlayer và bật presentation rig.
- **OwnerTeamMapping.cs** (Netplay/OwnerTeamMapping.cs) - SRP: Ánh xạ slot/team Mirror sang Owner human của UTS.
- **PlayerInputNetworkBridge.cs** (Netplay/PlayerInputNetworkBridge.cs) - DIP: PlayerInput (game) gọi relay lệnh MP qua handler đăng ký (không phụ thuộc Mirror trực tiếp).
- **RtsMatchServerSpawnOrchestrator.cs** (Netplay/RtsMatchServerSpawnOrchestrator.cs) - SRP: Khi server vào scene trận — spawn Civil Central + worker UTS cho mỗi connection.
- **RtsMatchServerSpawnRunner.cs** (Netplay/RtsMatchServerSpawnRunner.cs) - SRP: Retry spawn UTS sau khi Mirror chuyển scene — identity lobby đôi khi chưa sẵn sàng ngay OnServerSceneChanged.
- **RtsNetGameSceneBootstrap.cs** (Netplay/RtsNetGameSceneBootstrap.cs) - SRP: Bootstrap RtsNet_Game — sync LocalOwner, presentation MP, tắt capsule input.
- **RtsUtsClientCommandRelay.cs** (Netplay/RtsUtsClientCommandRelay.cs) - Type RtsUtsClientCommandRelay.
- **RtsUtsCommandMirrorUtility.cs** (Netplay/RtsUtsCommandMirrorUtility.cs) - SRP: Tìm mỏ / target combat gần điểm click cho ClientRpc mirror (supply thường không có NetworkIdentity).
- **RtsUtsGameSceneSetup.cs** (Netplay/RtsUtsGameSceneSetup.cs) - SRP: Cấu hình prefab UTS và spawn điểm cho scene RtsNet_Game (thay capsule MVP khi đã wire).
- **RtsUtsNetworkEntity.cs** (Netplay/RtsUtsNetworkEntity.cs) - Type RtsUtsNetworkEntity.
- **RtsUtsPlayerCommands.cs** (Netplay/RtsUtsPlayerCommands.cs) - SRP: Commands Mirror cho unit/building UTS (game assembly, gắn cùng player prefab với RtsGameCommander).
- **RtsUtsServerSpawnHandler.cs** (Netplay/RtsUtsServerSpawnHandler.cs) - SRP: Server spawn Civil Central + worker UTS theo team khi vào RtsNet_Game.

## Player

- **CameraConfig.cs** (Player/CameraConfig.cs) - Type CameraConfig.
- **FactionFogPresentation.cs** (Player/FactionFogPresentation.cs) - SRP: Bật/tắt một cặp camera fog (explored + visibility) cho Player1 hoặc Player2.
- **FactionFogQuery.cs** (Player/FactionFogQuery.cs) - SRP: API tầm nhìn cho AI và gameplay — map viewer → fog faction tương ứng.
- **FactionFogSystemReference.cs** (Player/FactionFogSystemReference.cs) - SRP: Holder camera + RT fog cho một human owner; đăng ký FactionFogSystemsRegistry.
- **FactionFogSystemsBootstrap.cs** (Player/FactionFogSystemsBootstrap.cs) - SRP: Đảm bảo mọi FactionFogSystemReference trong scene đăng ký registry khi load.
- **FactionFogSystemsRegistry.cs** (Player/FactionFogSystemsRegistry.cs) - SRP: Registry Owner human → IFogMapQuery (không Find mỗi frame).
- **FactionHudBinder.cs** (Player/FactionHudBinder.cs) - SRP: Gắn HUD / minimap / event log theo ILocalHumanOwner.LocalOwner.
- **FactionVisibilityUpdater.cs** (Player/FactionVisibilityUpdater.cs) - SRP: Đọc vision RT của local human và cập nhật IHideable (không phải phe local).
- **FogCpuTextureMirror.cs** (Player/FogCpuTextureMirror.cs) - SRP: Mirror RenderTexture fog sang Texture2D CPU — ReadPixels có throttle, sample bilinear.
- **FogLocalOwnerDebugHotkeys.cs** (Player/FogLocalOwnerDebugHotkeys.cs) - SRP: Phím tắt debug đổi local human owner khi test fog P1/P2 (Editor / Development Build).
- **FogOrthographicUvUtility.cs** (Player/FogOrthographicUvUtility.cs) - SRP: Map world XZ → UV texture fog ortho (dùng chung minimap, visibility, IFogMapQuery).
- **FogRenderTextureBootstrap.cs** (Player/FogRenderTextureBootstrap.cs) - SRP: Khởi tạo RT fog (explored/vision) về đen một lần mỗi texture — vùng chưa khám phá hiện đúng trên plane.
- **FogVisibilityManager.cs** (Player/FogVisibilityManager.cs) - Giữ tên script trên scene/prefab cũ; logic nằm ở FactionVisibilityUpdater.
- **GameplayFogOverlayCamera.cs** (Player/GameplayFogOverlayCamera.cs) - SRP: Camera con trên Main Camera — chỉ đổi culling mask theo local owner (layer plane P1/P2), không sync mỗi frame.
- **GameplayWorldRaycastUtility.cs** (Player/GameplayWorldRaycastUtility.cs) - SRP: Raycast world thống nhất cho chuột phải / hover cursor — tránh floor che supply (Move thay Gather).
- **HumanFogVisionUtility.cs** (Player/HumanFogVisionUtility.cs) - SRP: Quy tắc unit/building human nào phát tín hiệu fog vision trên client.
- **IFogMapQuery.cs** (Player/IFogMapQuery.cs) - ISP: Truy vấn explored/vision từ RT fog của một human faction.
- **IHideable.cs** (Player/IHideable.cs) - Type IHideable.
- **ILocalHumanOwner.cs** (Player/ILocalHumanOwner.cs) - ISP: Nguồn sự thật cho human player đang điều khiển trên client hiện tại.
- **LocalHumanCameraSpawnFocus.cs** (Player/LocalHumanCameraSpawnFocus.cs) - SRP: Đặt cameraTarget tại spawn Civil Central của phe local (spawn point → vị trí CC khi replicate).
- **LocalHumanOwnerAccess.cs** (Player/LocalHumanOwnerAccess.cs) - SRP: Truy cập LocalOwner cho HUD/input mà không hardcode Player1.
- **LocalHumanOwnerBootstrap.cs** (Player/LocalHumanOwnerBootstrap.cs) - SRP: Gán Owner.Player1 khi chơi offline trên scene không có Mirror local player. Gắn trên Game 1 (hoặc scene single-player); bỏ qua khi Mirror client đang active.
- **LocalHumanOwnerMirrorBridge.cs** (Player/LocalHumanOwnerMirrorBridge.cs) - DIP: Lobby Mirror → LocalHumanOwnerService + refresh presentation.
- **LocalHumanOwnerService.cs** (Player/LocalHumanOwnerService.cs) - SRP: Lưu và cung cấp ILocalHumanOwner.LocalOwner cho presentation/input trên client.
- **LocalHumanPresentationRefresh.cs** (Player/LocalHumanPresentationRefresh.cs) - SRP: Điểm gọi refresh presentation — MP Director ưu tiên, offline dùng PlayerViewBinder.
- **MpHudSuppliesResolver.cs** (Player/MpHudSuppliesResolver.cs) - SRP: Tìm component Supplies HUD đúng phe (Runtime UI UGUI vs (1)).
- **MpPlayerPresentationDirector.cs** (Player/MpPlayerPresentationDirector.cs) - SRP: MP — mỗi client chỉ bật rig P1 hoặc P2 (fog + UI + PlayerInput riêng).
- **MpPlayerPresentationRig.cs** (Player/MpPlayerPresentationRig.cs) - SRP: Một bộ presentation MP — fog + HUD + PlayerInput cho P1 hoặc P2 (gắn trên scene RtsNet_Game).
- **OwnerFogPlaneLayers.cs** (Player/OwnerFogPlaneLayers.cs) - SRP: Map Owner → layer Unity cho Fog of War Plane (overlay Main Camera).
- **OwnerFogVisionLayers.cs** (Player/OwnerFogVisionLayers.cs) - Type OwnerFogVisionLayers.
- **Placeholder.cs** (Player/Placeholder.cs) - Prefab marker tạm khi đặt building — preview vị trí trước khi spawn nhà thật.
- **PlayerInput.cs** (Player/PlayerInput.cs) - Điều khiển người chơi local: camera Cinemachine, box/chọn unit, raycast lệnh, ghost đặt nhà, formation, MP bridge.
- **PlayerInput.Voice.cs** (Player/PlayerInput.Voice.cs) - SRP: Thực thi CommandId từ voice → selection / arm command / stop / gather.
- **PlayerInputHotkeyIntegration.cs** (Player/PlayerInputHotkeyIntegration.cs) - SRP: Gắn hotkey stack lên PlayerInput và bridge Esc/H vào gameplay. Tương thích MP: event bus theo LocalOwner; Stop relay qua PlayerInputNetworkBridge.
- **PlayerViewBinder.cs** (Player/PlayerViewBinder.cs) - SRP: Orchestrator — bật đúng nhánh fog presentation theo ILocalHumanOwner.LocalOwner.
- **Supplies.cs** (Player/Supplies.cs) - MonoBehaviour kho phe — cộng/trừ Food/Wood/Stone, population; bind TMP UI.
- **SupplyAffordability.cs** (Player/SupplyAffordability.cs) - Kiểm tra đủ tài nguyên và đăng cảnh báo lên khung sự kiện (Player1).
- **UnitSelectionHoverCursor.cs** (Player/UnitSelectionHoverCursor.cs) - Khi có ít nhất một AbstractUnit được chọn: raycast theo chuột trên world; nếu với <b>mọi</b> unit đã chọn, lệnh thắng khi chuột phải cùng là GatherCommand hoặc cùng là AttackCommand (cùng quy tắc thứ tự <c>CanHandle</c> như click phải) thì đổi cursor tương ứng — nhiều worker cùng khai thác vẫn thấy gather cursor. Vật có IHideable và <c>IsVisible == false</c> thì bỏ qua hit đó.

## PvAI

- **PregameAiDifficultyApplicator.cs** (PvAI/PregameAiDifficultyApplicator.cs) - SRP: Hook scene Game 1 — gọi PregameAiDifficultyApplyService khi vào play mode. Có thể gắn cùng PvAiGameSceneBootstrap; không bắt buộc nếu bootstrap đã gọi service.
- **PregameAiDifficultyApplyService.cs** (PvAI/PregameAiDifficultyApplyService.cs) - SRP: Áp PregameSessionState.SelectedDifficulty lên mọi AIController trong scene game offline.
- **PvAiDebugSessionLog.cs** (PvAI/PvAiDebugSessionLog.cs) - Ghi NDJSON vào debug-559c4e.log (workspace root) khi kiểm tra PvAI ở Play Mode.
- **PvAiGameSceneBootstrap.cs** (PvAI/PvAiGameSceneBootstrap.cs) - SRP: Khi Play map offline — chạy spawn PvE (không chạy khi Mirror active).
- **PvAiGameSceneSetup.cs** (PvAI/PvAiGameSceneSetup.cs) - SRP: Cấu hình spawn offline PvE trên scene Game 1 (prefab + điểm xuất phát, không dùng CC cắm sẵn).
- **PvAiHealthFinding.cs** (PvAI/PvAiHealthFinding.cs) - Type PvAiHealthFinding.
- **PvAiOfflineAiCoordinator.cs** (PvAI/PvAiOfflineAiCoordinator.cs) - SRP: Bật AI bot cho PvE offline — không tắt vĩnh viễn khi Prepare map cho MP.
- **PvAiOfflineEntityFactory.cs** (PvAI/PvAiOfflineEntityFactory.cs) - SRP: Tạo bản instance offline từ prefab (gỡ Mirror nếu prefab từng wire MP).
- **PvAiOfflineSessionPrep.cs** (PvAI/PvAiOfflineSessionPrep.cs) - SRP: Chuẩn bị session PvE offline và điều phối spawn căn cứ theo lần load scene.
- **PvAiOfflineSpawnCoordinator.cs** (PvAI/PvAiOfflineSpawnCoordinator.cs) - SRP: Spawn PvE offline khi bootstrap hoặc loading bar cần căn cứ Player1.
- **PvAiOfflineSpawnService.cs** (PvAI/PvAiOfflineSpawnService.cs) - SRP: Spawn Civil Central + unit khởi đầu cho human và AI khi Play PvE offline.
- **PvAiRuntimeHealthCheck.cs** (PvAI/PvAiRuntimeHealthCheck.cs) - SRP: Chạy kiểm tra PvAI sau khi bootstrap (Play Mode) và ghi báo cáo + debug log. Gắn trên root Game 1 hoặc object bootstrap; chỉ chạy một lần mỗi lần vào Play.
- **PvAiSceneValidator.cs** (PvAI/PvAiSceneValidator.cs) - SRP: Kiểm tra invariant M5 PvAI (Game 1 offline) — dùng chung Editor và Play Mode.

## SpeechRecognition\Core

- **FuzzyVoiceCommandResolver.cs** (SpeechRecognition/Core/FuzzyVoiceCommandResolver.cs) - So khớp chuỗi STT với primary/alias; câu tương tự được ánh xạ về PrimaryPhrase mẫu (SRP: fuzzy map).
- **ISpeechRecognitionBackend.cs** (SpeechRecognition/Core/ISpeechRecognitionBackend.cs) - Abstraction for streaming speech-to-text. High-level game code depends on this instead of Vosk types (DIP/ISP).
- **IVoiceCommandResolver.cs** (SpeechRecognition/Core/IVoiceCommandResolver.cs) - Ánh xạ chuỗi STT sang id lệnh game (DIP: gameplay không phụ thuộc Vosk).
- **RecognizedSpeechPhraseMapper.cs** (SpeechRecognition/Core/RecognizedSpeechPhraseMapper.cs) - Tìm cụm mẫu gần nhất và trả PrimaryPhrase chuẩn (SRP: ánh xạ câu tương tự → mẫu dataset).
- **RecognizedSpeechPhraseNormalizer.cs** (SpeechRecognition/Core/RecognizedSpeechPhraseNormalizer.cs) - Đưa text STT về cùng dạng với mẫu câu trong dataset (không dấu, không dấu câu, chữ thường).
- **SpeechRecognitionBackendBehaviour.cs** (SpeechRecognition/Core/SpeechRecognitionBackendBehaviour.cs) - Unity-facing hook for backends. Lets the microphone driver reference a single inspector field (OCP: swap backends).
- **SpeechRecognitionDebugLogger.cs** (SpeechRecognition/Core/SpeechRecognitionDebugLogger.cs) - Ghi partial/final ra Console để kiểm tra nhanh (SRP: chỉ log).
- **StringSimilarity.cs** (SpeechRecognition/Core/StringSimilarity.cs) - Khoảng cách Levenshtein và độ tương đồng chuẩn hóa (SRP: chỉ so khớp chuỗi).
- **VietnameseTextNormalizer.cs** (SpeechRecognition/Core/VietnameseTextNormalizer.cs) - Chuẩn hóa chuỗi tiếng Việt cho STT / fuzzy / grammar Vosk (SRP: chỉ xử lý text).
- **VoiceCommandDatasetFile.cs** (SpeechRecognition/Core/VoiceCommandDatasetFile.cs) - Root JSON cho tập lệnh thoại (OCP: mở rộng bằng file, không sửa code gameplay). JsonUtility: tên field khớp với JSON (schemaVersion, minSimilarity, commands).
- **VoiceCommandDocsDatasetImporter.cs** (SpeechRecognition/Core/VoiceCommandDocsDatasetImporter.cs) - Chuyển file docs/voice-command-dataset.vi.json (schema intents) sang VoiceCommandDatasetFile (commands).
- **VoiceCommandIdActionRules.cs** (SpeechRecognition/Core/VoiceCommandIdActionRules.cs) - Quy tắc ánh xạ CommandId (JSON voice) → kiểu hành động / predicate tìm BaseCommand trên selection.
- **VoiceCommandOneShotTranscriptMapper.cs** (SpeechRecognition/Core/VoiceCommandOneShotTranscriptMapper.cs) - Nối transcript one-shot sang command id theo dataset JSON trong Resources.
- **VoiceCommandProfile.cs** (SpeechRecognition/Core/VoiceCommandProfile.cs) - Một lệnh thoại: id game + câu chính + alias (OCP: mở rộng bằng asset, không sửa code).
- **VoiceCommandRouter.cs** (SpeechRecognition/Core/VoiceCommandRouter.cs) - Nối STT → resolver → sự kiện Unity cho gameplay (SRP: không chứa logic Vosk).
- **WhisperSpeechDefaults.cs** (SpeechRecognition/Core/WhisperSpeechDefaults.cs) - Đường dẫn model Whisper trong StreamingAssets (SRP: hằng số cấu hình).
- **WhisperStreamingTuning.cs** (SpeechRecognition/Core/WhisperStreamingTuning.cs) - Gợi ý thông số streaming Whisper (SRP: chỉ tuning, không chạy inference).
- **WhisperVoicePromptBuilder.cs** (SpeechRecognition/Core/WhisperVoicePromptBuilder.cs) - Sinh initial prompt cho Whisper từ tập lệnh RTS (SRP: chỉ build prompt).

## SpeechRecognition\Editor

- **SpeechRecognitionSandboxSceneBuilder.cs** (SpeechRecognition/Editor/SpeechRecognitionSandboxSceneBuilder.cs) - Tạo scene test STT (Whisper) — không gắn map lệnh / gameplay voice.

## SpeechRecognition

- **SpeechRecognitionSandboxFeedback.cs** (SpeechRecognition/SpeechRecognitionSandboxFeedback.cs) - Hiển thị STT và kết quả fuzzy trên Canvas trong scene sandbox (SRP: chỉ UI phản hồi).
- **VoiceCommandGameplayExecutor.cs** (SpeechRecognition/VoiceCommandGameplayExecutor.cs) - Nối CommandId (sau fuzzy voice) → thực thi gameplay qua PlayerInput.
- **VoiceCommandOneShotCapture.cs** (SpeechRecognition/VoiceCommandOneShotCapture.cs) - Thu một câu STT (Whisper) → đăng transcript lên chat — không ánh xạ lệnh game.
- **VoiceCommandPushToTalkInput.cs** (SpeechRecognition/VoiceCommandPushToTalkInput.cs) - Phím giữ để thu thoại (SRP: chỉ input qua Input System).
- **VoiceCommandRuntimeDiagnostics.cs** (SpeechRecognition/VoiceCommandRuntimeDiagnostics.cs) - Log một lần khi Play để kiểm tra voice đã gắn trong scene chính (SRP: chẩn đoán).

## SpeechRecognition\Whisper

- **WhisperSpeechRecognitionBackend.cs** (SpeechRecognition/Whisper/WhisperSpeechRecognitionBackend.cs) - Adapter Whisper streaming (theo mẫu StreamingSampleMic) → ISpeechRecognitionBackend (DIP).
- **WhisperSttOnlyDriver.cs** (SpeechRecognition/Whisper/WhisperSttOnlyDriver.cs) - Chỉ nhận diện giọng nói (STT) — copy luồng StreamingSampleMic từ whisper.unity (SRP: không map lệnh game).
- **WhisperSttRecordControls.cs** (SpeechRecognition/Whisper/WhisperSttRecordControls.cs) - Một nút bật/tắt thu âm STT (SRP: chỉ điều khiển UI, không xử lý Whisper).

## TechTree

- **AdditiveFloatModifierSO.cs** (TechTree/AdditiveFloatModifierSO.cs) - ScriptableObject cau hinh du lieu.
- **AdditiveIntModifierSO.cs** (TechTree/AdditiveIntModifierSO.cs) - ScriptableObject cau hinh du lieu.
- **IModifier.cs** (TechTree/IModifier.cs) - Type IModifier.
- **InvalidPathSpecifiedException.cs** (TechTree/InvalidPathSpecifiedException.cs) - Type InvalidPathSpecifiedException.
- **TechTreeSO.cs** (TechTree/TechTreeSO.cs) - ScriptableObject cau hinh du lieu.
- **UnlockableSO.cs** (TechTree/UnlockableSO.cs) - ScriptableObject cau hinh du lieu.
- **UpgradeSO.cs** (TechTree/UpgradeSO.cs) - ScriptableObject cau hinh du lieu.

## UI

- **ActionBarCommandExecution.cs** (UI/ActionBarCommandExecution.cs) - SRP: Kiểm tra có thể kích hoạt lệnh từ thanh action / phím số hay không.
- **ActionBarCommandResolver.cs** (UI/ActionBarCommandResolver.cs) - SRP: Lấy lệnh theo slot (0–8) cho selection hiện tại — cùng quy tắc với Containers.ActionsUI.

## UI\Components

- **FreeDraggablePanel.cs** (UI/Components/FreeDraggablePanel.cs) - Kéo panel UI tự do trong canvas cha (giống chat log). Gắn lên vùng kéo (title bar / header); gán Panel = khung cần di chuyển.
- **HoverRevealPanel.cs** (UI/Components/HoverRevealPanel.cs) - Hover vào object này → hiện panel; rời chuột → tắt panel. Gắn cùng GameObject có Image/Button (raycastTarget bật).
- **MultiUnitTypeSlotUI.cs** (UI/Components/MultiUnitTypeSlotUI.cs) - Một ô loại unit trong panel chọn nhiều unit (icon + số lượng).
- **ProgressBar.cs** (UI/Components/ProgressBar.cs) - Type ProgressBar.
- **Tooltip.cs** (UI/Components/Tooltip.cs) - Type Tooltip.
- **UIActionButton.cs** (UI/Components/UIActionButton.cs) - Type UIActionButton.
- **UIBuildQueueButton.cs** (UI/Components/UIBuildQueueButton.cs) - Type UIBuildQueueButton.
- **UiExclusiveSelectGroup.cs** (UI/Components/UiExclusiveSelectGroup.cs) - Nhóm chọn một trong nhiều option (map list, độ khó AI…). Đặt trên parent; tự thu thập UiExclusiveSelectOption ở con nếu chưa gán.
- **UiExclusiveSelectOption.cs** (UI/Components/UiExclusiveSelectOption.cs) - Một lựa chọn trong nhóm exclusive — Button + ảnh chọn / không chọn + text (text nằm ngoài Button cũng được).
- **UiPanelActivation.cs** (UI/Components/UiPanelActivation.cs) - SRP: Bật panel/dialog sau khi Awake/OnEnable lần đầu của hierarchy con đã chạy xong.
- **UIUnitButton.cs** (UI/Components/UIUnitButton.cs) - Type UIUnitButton.
- **UnitStatSlotUI.cs** (UI/Components/UnitStatSlotUI.cs) - Một ô stat (prefab Armor Damage Icon): level upgrade, giá trị, icon, tooltip.
- **UnitWorldHealthBar.cs** (UI/Components/UnitWorldHealthBar.cs) - Thanh máu world-space: fill theo HP và đổi sprite theo Owner (Player1 / Player2).

## UI\Containers

- **ActionsUI.cs** (UI/Containers/ActionsUI.cs) - Thanh action bar — render nút lệnh theo selection và ActiveCommand.
- **BuildingBuildingUI.cs** (UI/Containers/BuildingBuildingUI.cs) - Type BuildingBuildingUI.
- **BuildingSelectedUI.cs** (UI/Containers/BuildingSelectedUI.cs) - Type BuildingSelectedUI.
- **BuildingUnderConstructionUI.cs** (UI/Containers/BuildingUnderConstructionUI.cs) - Type BuildingUnderConstructionUI.
- **MultiUnitSelectionUI.cs** (UI/Containers/MultiUnitSelectionUI.cs) - Panel giữa màn hình khi chọn từ 2 unit trở lên: icon theo loại + số lượng.
- **SingleUnitSelectedUI.cs** (UI/Containers/SingleUnitSelectedUI.cs) - Type SingleUnitSelectedUI.
- **UnitIconUI.cs** (UI/Containers/UnitIconUI.cs) - Type UnitIconUI.
- **UnitStatsPanelUI.cs** (UI/Containers/UnitStatsPanelUI.cs) - Panel các ô stat khi chọn một unit (Damage, tốc chạy, tốc đánh, tầm…).
- **UnitTransportUI.cs** (UI/Containers/UnitTransportUI.cs) - Type UnitTransportUI.

## UI\GameEventLog

- **GameEventLog.cs** (UI/GameEventLog/GameEventLog.cs) - In-memory event log buffer; UI and other systems subscribe to LineAdded.
- **GameEventLogCategory.cs** (UI/GameEventLog/GameEventLogCategory.cs) - Enum phân loại dòng log (economy, combat, system) cho format/màu.
- **GameEventLogLine.cs** (UI/GameEventLog/GameEventLogLine.cs) - Type GameEventLogLine.
- **GameEventLogLineFormatter.cs** (UI/GameEventLog/GameEventLogLineFormatter.cs) - Builds display text for a single log line (TMP rich text).
- **GameEventLogLineView.cs** (UI/GameEventLog/GameEventLogLineView.cs) - One chat row prefab; height driven by LayoutElement preferred height for VerticalLayoutGroup.
- **GameEventLogMessageFormatter.cs** (UI/GameEventLog/GameEventLogMessageFormatter.cs) - Converts game bus events into player-facing log strings.
- **GameEventLogScrollController.cs** (UI/GameEventLog/GameEventLogScrollController.cs) - Tutorial-style chat scroll: when Content children change, scroll to the newest line at the bottom.
- **GameEventLogUI.cs** (UI/GameEventLog/GameEventLogUI.cs) - Game event log: append text + ScrollRect/Viewport/Mask (không tràn viền, cuộn được).
- **PlayerGameEventLogListener.cs** (UI/GameEventLog/PlayerGameEventLogListener.cs) - Subscribes to Bus{T} events for Player1 and posts messages to GameEventLog.

## UI\InGame

- **InGameOverlayMenuController.cs** (UI/InGame/InGameOverlayMenuController.cs) - SRP: HUD in-game — nút tốc độ / menu, panel con (speed, menu, pause, settings, đầu hàng).
- **InGameSettingsPanelOpener.cs** (UI/InGame/InGameSettingsPanelOpener.cs) - Type InGameSettingsPanelOpener.

## UI

- **IUIElement.cs** (UI/IUIElement.cs) - Type IUIElement.
- **OwnerHealthBarStyleSO.cs** (UI/OwnerHealthBarStyleSO.cs) - ScriptableObject cau hinh du lieu.

## UI\Pregame

- **ExitConfirmDialog.cs** (UI/Pregame/ExitConfirmDialog.cs) - SRP: Dialog xác nhận 2 nút (OK / Hủy) — thoát app hoặc hành động tùy chỉnh (đầu hàng…).
- **GameManualScrollViewBinder.cs** (UI/Pregame/GameManualScrollViewBinder.cs) - Start: đọc JSON và gán rich text (in đậm theo từng dòng) vào TextMeshProUGUI.
- **HotkeySettingItemView.cs** (UI/Pregame/HotkeySettingItemView.cs) - SRP: View item một dòng hotkey trong scroll view.
- **HotkeySettingsScrollViewBinder.cs** (UI/Pregame/HotkeySettingsScrollViewBinder.cs) - SRP: Đổ danh sách hotkey vào ScrollView Content bằng prefab HotKeySetting.
- **MainMenuSettingsDialogController.cs** (UI/Pregame/MainMenuSettingsDialogController.cs) - SRP: Quản lý dialog cài đặt MainMenu (tab Audio/Hotkey, tên người chơi, Save/Reset theo cơ chế staging).
- **MainMenuUIController.cs** (UI/Pregame/MainMenuUIController.cs) - SRP: Điều khiển menu chính MainMenu — nút Play, dialog, thoát game. Gắn lên Canvas hoặc Panel; có thể auto-bind theo tên GameObject nếu để trống SerializeField.
- **ParallaxMenuBackground.cs** (UI/Pregame/ParallaxMenuBackground.cs) - Trượt Image trái/phải trong khoảng ±Move Distance. Lớp 1 đứng yên, lớp 2 = X% tốc độ, lớp 3 = 100%.
- **PregameMapEntry.cs** (UI/Pregame/PregameMapEntry.cs) - Dữ liệu một map trên màn setup (tên hiển thị, ảnh minh họa, scene gameplay).
- **PregameMapSelectItemView.cs** (UI/Pregame/PregameMapSelectItemView.cs) - SRP: Một nút map trong scrollview — nhãn, trạng thái chọn (Img Select / Unselect).
- **PregameMapSelectScrollBinder.cs** (UI/Pregame/PregameMapSelectScrollBinder.cs) - SRP: Nguồn cấu hình map duy nhất trên SSScene — catalog, spawn nút, preview, scene đã chọn. Chỉ chỉnh mảng Maps + template/Content trên component này (không khai báo lại trên PregameSetupUIController).
- **PregameMpLobbyCoordinator.cs** (UI/Pregame/PregameMpLobbyCoordinator.cs) - SRP: Điều phối quyền lobby MP trên SSScene — client chỉ Ready/Back, host chọn map và Start.
- **PregameSetupUIController.cs** (UI/Pregame/PregameSetupUIController.cs) - SRP: Màn setup SSScene — điều phối chọn map, độ khó AI, bắt đầu trận / lobby.

## UI

- **RuntimeUI.cs** (UI/RuntimeUI.cs) - SRP: Panel lệnh / selection UI — subscribe Bus theo một Owner (P1 hoặc P2).

## Units

- **AbstractCommandable.cs** (Units/AbstractCommandable.cs) - Base MonoBehaviour cho unit/nhà — health, owner, commands, fog visibility, selection decal.
- **AbstractUnit.cs** (Units/AbstractUnit.cs) - Type AbstractUnit.
- **AbstractUnitSO.cs** (Units/AbstractUnitSO.cs) - ScriptableObject cau hinh du lieu.
- **AirTransport.cs** (Units/AirTransport.cs) - Type AirTransport.
- **AnimalAIConfigSO.cs** (Units/AnimalAIConfigSO.cs) - ScriptableObject cau hinh du lieu.
- **Archer.cs** (Units/Archer.cs) - Type Archer.
- **AttackConfigSO.cs** (Units/AttackConfigSO.cs) - ScriptableObject cau hinh du lieu.
- **BaseBuilding.cs** (Units/BaseBuilding.cs) - Type BaseBuilding.
- **BaseMilitaryUnit.cs** (Units/BaseMilitaryUnit.cs) - Type BaseMilitaryUnit.
- **BuildingEventType.cs** (Units/BuildingEventType.cs) - Type BuildingEventType.
- **BuildingProgress.cs** (Units/BuildingProgress.cs) - Type BuildingProgress.

## Units\Buildings

- **BuildingAutoAttack.cs** (Units/Buildings/BuildingAutoAttack.cs) - Automatically attacks the nearest hostile unit in range (owner different from this building). Spawns a projectile from an elevated fire point and fires straight down toward DamageableSensor.
- **BuildingAutoAttackConfigSO.cs** (Units/Buildings/BuildingAutoAttackConfigSO.cs) - ScriptableObject cau hinh du lieu.
- **BuildingEffectUtility.cs** (Units/Buildings/BuildingEffectUtility.cs) - Shared checks for passive building effects (food gen, tower attack, …).
- **PassiveFoodGeneratorBuilding.cs** (Units/Buildings/PassiveFoodGeneratorBuilding.cs) - Periodically adds food to the building owner's supply pool when the structure is operational.
- **PassiveFoodGeneratorConfigSO.cs** (Units/Buildings/PassiveFoodGeneratorConfigSO.cs) - ScriptableObject cau hinh du lieu.
- **TowerDownwardProjectileFlight.cs** (Units/Buildings/TowerDownwardProjectileFlight.cs) - Tower projectile: parabolic arc from fire point to the aim position (DamageableSensor).

## Units

- **BuildingSO.cs** (Units/BuildingSO.cs) - ScriptableObject cau hinh du lieu.

## Units\Combat

- **CombatAreaTargetQuery.cs** (Units/Combat/CombatAreaTargetQuery.cs) - SRP: Tìm mục tiêu hostiles trong vùng tròn (rampage / sweep).
- **CombatTargetPriorityUtility.cs** (Units/Combat/CombatTargetPriorityUtility.cs) - SRP: Ưu tiên mục tiêu — lính địch trước công trình địch (cùng khoảng cách / trong vùng).
- **UnitAreaRampageController.cs** (Units/Combat/UnitAreaRampageController.cs) - SRP: Phản công vùng khi bị đánh — quét hostile trong bán kính; hết địch thì tắt (AI/planner dùng Attack thường).
- **UnitCounterAttackUtility.cs** (Units/Combat/UnitCounterAttackUtility.cs) - SRP: Kích hoạt phản công vùng khi lính bị địch đánh — không qua lệnh UI/AI.

## Units

- **DamageableSensor.cs** (Units/DamageableSensor.cs) - Type DamageableSensor.

## Units\Formation

- **GroupFormationMoveUtility.cs** (Units/Formation/GroupFormationMoveUtility.cs) - SRP: Áp dụng Move cho nhóm unit theo formation vuông (player multi-select).
- **UnitFormationRole.cs** (Units/Formation/UnitFormationRole.cs) - Vai trò xếp hàng — hàng trước (cận chiến/worker) vs hàng sau (archer).
- **UnitFormationRoleClassifier.cs** (Units/Formation/UnitFormationRoleClassifier.cs) - SRP: Phân loại unit vào hàng trước / hàng sau theo tên UnitSO.
- **UnitSquareFormationPlanner.cs** (Units/Formation/UnitSquareFormationPlanner.cs) - SRP: Tính vị trí đích hình vuông — hàng trước tiến hướng điểm click, archer hàng sau.

## Units

- **Grenadier.cs** (Units/Grenadier.cs) - Type Grenadier.
- **HoldGunIK.cs** (Units/HoldGunIK.cs) - Type HoldGunIK.
- **HomingArrowFlight.cs** (Units/HomingArrowFlight.cs) - Gắn trên Archer. Spawn prefab mũi tên; bay vòng cung hoặc homing ngang tùy cấu hình.
- **IAttacker.cs** (Units/IAttacker.cs) - Interface tấn công — damage, range, target acquisition.
- **IBuildingBuilder.cs** (Units/IBuildingBuilder.cs) - Type IBuildingBuilder.
- **IBuildingPassiveEffect.cs** (Units/IBuildingPassiveEffect.cs) - Passive building behaviours toggled with BaseBuilding enable/disable.
- **IDamageable.cs** (Units/IDamageable.cs) - Interface nhận sát thương — CurrentHealth, MaxHealth, OnHealthUpdated.
- **IMoveable.cs** (Units/IMoveable.cs) - Interface di chuyển — API MoveTo cho NavMesh/formation.
- **IProjectileAttacker.cs** (Units/IProjectileAttacker.cs) - Unit gây damage bằng projectile; Behavior.AttackTargetAction gọi khi AttackConfig.HasProjectileAttacks.
- **ISelectable.cs** (Units/ISelectable.cs) - Interface chọn được — IsSelected, Transform cho box select và UI.
- **ITransportable.cs** (Units/ITransportable.cs) - Type ITransportable.
- **ITransporter.cs** (Units/ITransporter.cs) - Type ITransporter.
- **Owner.cs** (Units/Owner.cs) - Enum phe trong game (Player1, Player2, AI2–AI7, Unowned…) — dùng trên unit, Bus event, fog.
- **SightConfigSO.cs** (Units/SightConfigSO.cs) - ScriptableObject cau hinh du lieu.
- **SupplyCostSO.cs** (Units/SupplyCostSO.cs) - ScriptableObject cau hinh du lieu.
- **TransportConfigSO.cs** (Units/TransportConfigSO.cs) - ScriptableObject cau hinh du lieu.
- **UnitCommands.cs** (Units/UnitCommands.cs) - Type UnitCommands.
- **UnitDeathConfigSO.cs** (Units/UnitDeathConfigSO.cs) - ScriptableObject cau hinh du lieu.
- **UnitDeathController.cs** (Units/UnitDeathController.cs) - API chết từng bước cho Behavior Graph (Cách A): prepare → animator → wait → freeze → hold → sink → destroy (node riêng).
- **UnitSO.cs** (Units/UnitSO.cs) - ScriptableObject cau hinh du lieu.

## Units\Visualization

- **AttackRangeCircleUtility.cs** (Units/Visualization/AttackRangeCircleUtility.cs) - Tạo điểm vòng tròn trên mặt phẳng XZ (tầm đánh trên map).
- **AttackRangeDisplayInstaller.cs** (Units/Visualization/AttackRangeDisplayInstaller.cs) - Gắn UnitAttackRangeDisplay cho unit có tầm đánh (không áp dụng building).
- **UnitAttackRangeDisplay.cs** (Units/Visualization/UnitAttackRangeDisplay.cs) - Hiển thị vòng tròn tầm đánh khi unit được chọn (LineRenderer) và trong Scene view (Gizmos).

## Units

- **WildAnimal.cs** (Units/WildAnimal.cs) - Unit động vật hoang: AI chỉ qua Behavior Graph (blackboard Command + Abort khi Command đổi).
- **Worker.cs** (Units/Worker.cs) - Unit kinh tế: nhận GatherCommand, gán event channel cho BT gather/return.
- **WorkerGatherAssignmentLock.cs** (Units/WorkerGatherAssignmentLock.cs) - Type WorkerGatherAssignmentLock.

## Utilities

- **AnimationConstants.cs** (Utilities/AnimationConstants.cs) - Type AnimationConstants.
- **BuildingKindMatching.cs** (Utilities/BuildingKindMatching.cs) - SRP: So khớp building với prefab archetype (hotkey A/S/D).
- **CivilCentralUtility.cs** (Utilities/CivilCentralUtility.cs) - Identifies the main base (Civil Central) for win/lose and supply-deposit rules.
- **ClosestColliderComparer.cs** (Utilities/ClosestColliderComparer.cs) - Type ClosestColliderComparer.
- **ClosestCommandPostComparer.cs** (Utilities/ClosestCommandPostComparer.cs) - Type ClosestCommandPostComparer.
- **ClosestGameObjectComparer.cs** (Utilities/ClosestGameObjectComparer.cs) - Type ClosestGameObjectComparer.
- **CombatTargetGeometryUtility.cs** (Utilities/CombatTargetGeometryUtility.cs) - Resolves closest surface points on combat targets (colliders, NavMeshObstacle footprint).
- **DamageableSensorAimUtility.cs** (Utilities/DamageableSensorAimUtility.cs) - Resolves world aim points on units (DamageableSensor when present).
- **HostileTargetLocator.cs** (Utilities/HostileTargetLocator.cs) - Finds hostile IDamageable targets in range for defensive buildings.
- **InputSystemKeyboardUtility.cs** (Utilities/InputSystemKeyboardUtility.cs) - SRP: Truy cập bàn phím qua Input System (không dùng UnityEngine.Input / KeyCode runtime).
- **PlacementFieldGridContext.cs** (Utilities/PlacementFieldGridContext.cs) - Type PlacementFieldGridContext.
- **PlacementFieldGridUtility.cs** (Utilities/PlacementFieldGridUtility.cs) - SRP: Ánh xạ tọa độ thế giới → chỉ số ô trên lưới placement field.
- **PlacementFieldSelectionRegistry.cs** (Utilities/PlacementFieldSelectionRegistry.cs) - SRP: AI — lưu địa chỉ đặt nhà đã chọn theo phe và tránh chọn trùng trong cùng ô field (bán kính tối thiểu). Người chơi dùng ghost + Restrictions trên Player.PlayerInput; không gọi registry này.
- **ProjectileArcMath.cs** (Utilities/ProjectileArcMath.cs) - Shared parabolic arc math for unit/building projectiles.
- **StartingUnitSpawnEntry.cs** (Utilities/StartingUnitSpawnEntry.cs) - SRP: Một dòng cấu hình spawn unit khởi đầu (prefab + số lượng).
- **StartingWorkerSpawnLayout.cs** (Utilities/StartingWorkerSpawnLayout.cs) - SRP: Tính vị trí spawn worker khởi đầu quanh điểm base (PvE / MP dùng chung).
- **SupplyDepositApproachUtility.cs** (Utilities/SupplyDepositApproachUtility.cs) - SRP: Điểm tiếp cận động quanh Store/Civil Central — mỗi worker một ô trên vòng quanh footprint.
- **SupplyDepositLocator.cs** (Utilities/SupplyDepositLocator.cs) - Locates completed buildings where workers can deposit gathered supplies.
- **SystemCursorTextureBaker.cs** (Utilities/SystemCursorTextureBaker.cs) - Chuẩn hóa texture để dùng với UnityEngine.Cursor.SetCursor: asset Sprite/compressed thường không đọc được từ CPU và không đúng RGBA32/mip — Unity sẽ báo lỗi.
- **UnitKindMatching.cs** (Utilities/UnitKindMatching.cs) - SRP: So khớu hai unit có cùng archetype (double-click chọn cùng loại, v.v.).

---

_Sinh tự động: `docs/_gen-script-catalog.ps1` — chạy lại khi thêm/sửa script._
