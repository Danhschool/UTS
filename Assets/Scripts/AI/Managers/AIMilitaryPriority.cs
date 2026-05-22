namespace GameDevTV.RTS.AI
{
    /// <summary>Priority bands — phòng thủ CC trên train Barrack và tấn công.</summary>
    public static class AIMilitaryPriority
    {
        public const int DefendCivilCentral = 960;
        public const int AttackThreatNearCc = 955;
        public const int BuildTowerLine = 565;
        public const int TrainBarrack = 540;
        /// <summary>Gặp CC / đối thủ RTS — gọi toàn quân patrol về formation tấn công.</summary>
        public const int RallyArmyOnContact = 395;
        public const int AttackWaveMove = 380;
        public const int AttackEnemyCivilCentral = 375;
        /// <summary>Quân rảnh — tuần tra vòng quanh base (dưới attack/train).</summary>
        public const int PatrolExpandMap = 250;
    }
}
