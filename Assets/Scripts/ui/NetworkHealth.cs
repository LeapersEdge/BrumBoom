using Fusion;
using UnityEngine;

public class NetworkHealth : NetworkBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int startLives = 3;

    [Networked] public float Health { get; private set; }
    [Networked] public int Lives { get; private set; }

    public float MaxHealth => maxHealth;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Health = maxHealth;
            Lives = startLives;
        }
    }

    public void DealDamage(float amount)
    {
        if (!Object.HasStateAuthority) return;

        Health = Mathf.Max(0, Health - amount);

        if (Health <= 0)
        {
            Lives = Mathf.Max(0, Lives - 1);

            if (Lives > 0)
                Health = maxHealth;
            else
                Runner.Despawn(Object);
        }
    }

    public void RequestDamage(float amount)
    {
        RPC_RequestDamage(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDamage(float amount, RpcInfo info = default)
    {
        DealDamage(amount);
    }
}
