#if UNITY_EDITOR
namespace GameDevTV.RTS.Editor.Gameplay
{
    /// <summary>
    /// SRP: Đường dẫn cố định cho prefab lõi map và scene tham chiếu ổn định.
    /// </summary>
    internal static class GameplayMapScenePaths
    {
        public const string ReferenceStableScenePath =
            "Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Game.unity";

        public const string CorePrefabPath = "Assets/Prefab/Gameplay/GameplayMapCore.prefab";

        public const string CivilCentralPrefabPath =
            "Assets/Prefab/Buildings/civil_central/civil_central.prefab";

        public const string WorkerMpPrefabPath = "Assets/Prefab/Unit/Worker 1.prefab";

        public const string WorkerPvAiPrefabPath = "Assets/Prefab/Unit/Worker.prefab";

        public const string FogP2PrefabPath = "Assets/Prefab/Fog of War P2.prefab";

        public const string FogP1PrefabPath = "Assets/Prefab/Fog of War 1.prefab";

        public const string RuntimeUiHudPrefabPath = "Assets/Prefab/UI/Runtime UI UGUI.prefab";

        public const string CoreRootName = "GameplayMapCore";
    }
}
#endif
