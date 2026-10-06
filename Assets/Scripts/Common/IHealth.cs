using System;

public interface IHealth
{
    float CurrentHealth { get; }
    float MaxHealth { get; }

    event Action<float> OnDamaged;
    event Action OnDeath;
}
