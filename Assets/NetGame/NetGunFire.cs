using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace NetGame
{
    /// <summary>
    /// Server-authoritative projectile spawning and damage.
    /// Clients send fire input; server simulates projectiles and applies damage.
    /// A lightweight visual projectile is spawned on all clients via RPC.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetGunFire : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform muzzleTransform;

        [Header("Projectile")]
        [SerializeField] private float projectileSpeed = 35f;
        [SerializeField] private float projectileLifetime = 2.5f;
        [SerializeField] private float projectileRadius = 0.1f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Firing")]
        [SerializeField] private float fireRate = 0.1f;

        private float _nextFireTime;

        private struct ServerProjectile
        {
            public Vector3 Position;
            public Vector3 Direction;
            public float TimeLeft;
            public PlayerRef Owner;
        }

        private readonly List<ServerProjectile> _serverProjectiles = new();

        public override void Spawned()
        {
            if (muzzleTransform == null)
                muzzleTransform = transform;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            var health = GetComponent<NetworkHealth>();
            if (health != null && health.IsEliminated)
                return;

            // Consume input from the owning player
            if (Runner.TryGetInputForPlayer(Object.InputAuthority, out CarInput input))
            {
                if (input.Fire && Runner.SimulationTime >= _nextFireTime)
                {
                    _nextFireTime = Runner.SimulationTime + fireRate;
                    SpawnProjectile(input);
                }
            }

            // Update server-side projectiles and apply damage
            for (int i = _serverProjectiles.Count - 1; i >= 0; i--)
            {
                var proj = _serverProjectiles[i];
                Vector3 start = proj.Position;
                Vector3 end = proj.Position + proj.Direction * projectileSpeed * Runner.DeltaTime;

                // Sphere cast for hit detection
                if (Physics.SphereCast(start, projectileRadius, proj.Direction, out RaycastHit hit,
                        Vector3.Distance(start, end), hitMask, QueryTriggerInteraction.Ignore))
                {
                    ApplyDamage(hit, proj.Owner);
                    _serverProjectiles.RemoveAt(i);
                    continue;
                }

                proj.Position = end;
                proj.TimeLeft -= Runner.DeltaTime;

                if (proj.TimeLeft <= 0f)
                {
                    _serverProjectiles.RemoveAt(i);
                }
                else
                {
                    _serverProjectiles[i] = proj;
                }
            }
        }

        private void SpawnProjectile(CarInput input)
        {
            Vector3 dir = new Vector3(input.TurretDir.x, 0f, input.TurretDir.y);
            if (dir.sqrMagnitude < 0.0001f)
                dir = transform.forward;
            dir.Normalize();

            Vector3 spawnPos = muzzleTransform != null ? muzzleTransform.position : transform.position;

            _serverProjectiles.Add(new ServerProjectile
            {
                Position = spawnPos,
                Direction = dir,
                TimeLeft = projectileLifetime,
                Owner = Object.InputAuthority
            });

            RPC_SpawnProjectile(spawnPos, dir);
        }

        private void ApplyDamage(RaycastHit hit, PlayerRef attacker)
        {
            var targetHealth = hit.collider.GetComponentInParent<NetworkHealth>();
            if (targetHealth != null)
            {
                // Ignore self-hit
                if (attacker != default && Runner.TryGetPlayerObject(attacker, out var attackerObj))
                {
                    if (attackerObj == targetHealth.Object)
                        return;
                }
                targetHealth.DealDamage(damage, attacker);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SpawnProjectile(Vector3 startPos, Vector3 direction, RpcInfo info = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Projectile";
            go.transform.position = startPos;
            go.transform.localScale = Vector3.one * 0.2f;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = true;

            var visual = go.AddComponent<ProjectileVisual>();
            visual.Initialize(direction, projectileSpeed, projectileLifetime);
        }
    }
}
