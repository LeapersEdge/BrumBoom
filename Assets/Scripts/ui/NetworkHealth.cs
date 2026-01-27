using Fusion;
using NetGame;
using UnityEngine;

public class NetworkHealth : NetworkBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int startLives = 3;

    [Networked] public float Health { get; private set; }
    [Networked] public int Lives { get; private set; }
    [Networked] public int Kills { get; private set; }
    [Networked] public NetworkBool IsEliminated { get; private set; }
    [Networked] public NetworkString<_32> PlayerName { get; private set; }

    public float MaxHealth => maxHealth;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Health = maxHealth;
            Lives = startLives;
            IsEliminated = false;
        }

        CacheComponents();
        ApplyEliminatedState();

        if (GetComponent<PlayerWorldUI>() == null)
            gameObject.AddComponent<PlayerWorldUI>();

        if (Object.HasInputAuthority)
        {
            var name = StartMenuUI.GetLocalPlayerName();
            RPC_SetPlayerName(name);
        }
    }

    public override void Render()
    {
        if (_lastEliminated != IsEliminated)
        {
            ApplyEliminatedState();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetPlayerName(string name, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        PlayerName = SanitizeName(name);
    }

    public void DealDamage(float amount)
    {
        DealDamage(amount, default);
    }

    public void DealDamage(float amount, PlayerRef attacker)
    {
        if (!Object.HasStateAuthority) return;
        if (IsEliminated) return;

        Health = Mathf.Max(0, Health - amount);

        if (Health <= 0)
        {
            Lives = Mathf.Max(0, Lives - 1);

            if (Lives > 0)
            {
                Health = maxHealth;
                Respawn();
            }
            else
            {
                TryAddKill(attacker);
                IsEliminated = true;
                Health = 0f;
                ApplyEliminatedState();
            }
        }
    }

    public void RequestDamage(float amount)
    {
        RPC_RequestDamage(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDamage(float amount, RpcInfo info = default)
    {
        DealDamage(amount, info.Source);
    }

    private void TryAddKill(PlayerRef attacker)
    {
        if (attacker == default)
            return;

        if (Runner.TryGetPlayerObject(attacker, out var attackerObj))
        {
            var attackerHealth = attackerObj.GetComponent<NetworkHealth>();
            if (attackerHealth != null)
            {
                attackerHealth.Kills += 1;
            }
        }
    }

    private Renderer[] _renderers;
    private Collider[] _colliders;
    private Rigidbody _rb;
    private bool _cached;
    private bool _spectatorTargetSet;
    private bool _wasKinematic;
    private bool _lastEliminated;

    private void CacheComponents()
    {
        if (_cached) return;
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        _rb = GetComponent<Rigidbody>();
        _wasKinematic = _rb != null && _rb.isKinematic;
        _cached = true;
    }

    private void ApplyEliminatedState()
    {
        CacheComponents();
        _lastEliminated = IsEliminated;

        if (IsEliminated)
        {
            if (_rb != null)
            {
                if (_rb.isKinematic == false)
                {
                    _rb.velocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
                _rb.isKinematic = true;
            }

            if (_colliders != null)
            {
                foreach (var col in _colliders)
                {
                    if (col != null) col.enabled = false;
                }
            }

            if (_renderers != null)
            {
                foreach (var r in _renderers)
                {
                    if (r != null) r.enabled = false;
                }
            }

            TrySetSpectatorTarget();
        }
        else
        {
            if (_rb != null)
                _rb.isKinematic = _wasKinematic;

            if (_colliders != null)
            {
                foreach (var col in _colliders)
                {
                    if (col != null) col.enabled = true;
                }
            }

            if (_renderers != null)
            {
                foreach (var r in _renderers)
                {
                    if (r != null) r.enabled = true;
                }
            }

            RestoreLocalCameraTarget();
        }
    }

    private void TrySetSpectatorTarget()
    {
        if (_spectatorTargetSet) return;
        if (!Object.HasInputAuthority) return;

        var camLook = FindObjectOfType<NetCameraLook>();
        if (camLook == null) return;

        foreach (var nh in FindObjectsOfType<NetworkHealth>())
        {
            if (nh != null && nh != this && !nh.IsEliminated)
            {
                camLook.SetTarget(nh.transform);
                _spectatorTargetSet = true;
                return;
            }
        }
    }

    private void RestoreLocalCameraTarget()
    {
        if (!_spectatorTargetSet) return;
        if (!Object.HasInputAuthority) return;

        var camLook = FindObjectOfType<NetCameraLook>();
        if (camLook == null) return;

        camLook.SetTarget(transform);
        _spectatorTargetSet = false;
    }

    private void Respawn()
    {
        if (GameBootstrap.Instance != null && GameBootstrap.Instance.TryGetSpawn(out var pos, out var rot))
        {
            transform.SetPositionAndRotation(pos, rot);
        }
        else
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Player";

        value = value.Trim();
        if (value.Length > 24)
            value = value.Substring(0, 24);
        return value;
    }
}
