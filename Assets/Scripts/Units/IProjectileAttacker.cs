namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Unit gây damage bằng projectile; <see cref="Behavior.AttackTargetAction"/> gọi khi AttackConfig.HasProjectileAttacks.
    /// </summary>
    public interface IProjectileAttacker
    {
        /// <summary>
        /// Mục tiêu: Phóng projectile hướng tới địch đang bị tấn công.
        /// Cách hoạt động: Implementation bay visual (nếu có) rồi TakeDamage trên target.
        /// </summary>
        void LaunchProjectile(IDamageable target);
    }
}
