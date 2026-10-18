namespace GameJam.Combat
{
    /// <summary>
    /// Contract for the future player health component. That component owns health,
    /// damage rules and replication; the avatar only reads and forwards damage.
    /// </summary>
    public interface IPlayerHealth : ITakeDamage
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }
    }
}
