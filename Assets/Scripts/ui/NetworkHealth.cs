using Fusion;
using NetGame;
using UnityEngine;

public class NetworkHealth : NetworkBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int startLives = 3;
    [SerializeField] private float spawnGhostDuration = 2f;
    [SerializeField] private float ghostAlpha = 0.35f;
    [SerializeField] private Material ghostMaterial;

    [Networked] public float Health { get; private set; }
    [Networked] public int Lives { get; private set; }
    [Networked] public int Kills { get; private set; }
    [Networked] public NetworkBool IsEliminated { get; private set; }
    [Networked] public NetworkBool IsGhost { get; private set; }
    [Networked] private float GhostUntil { get; set; }
    [Networked] public NetworkString<_32> PlayerName { get; private set; }

    public float MaxHealth => maxHealth;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Health = maxHealth;
            Lives = startLives;
            IsEliminated = false;
            StartGhost(spawnGhostDuration);
        }

        CacheComponents();
        ApplyEliminatedState();
        if (IsGhost)
            ApplyGhostState();

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

        if (!IsEliminated && _lastGhost != IsGhost)
        {
            ApplyGhostState();
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
        if (IsGhost) return;

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

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (!IsGhost)
            return;

        UpdateCollisionIgnores();

        if (Runner.SimulationTime < GhostUntil)
            return;

        if (IsOverlappingOtherPlayers())
        {
            GhostUntil = Runner.SimulationTime + spawnGhostDuration;
        }
        else
        {
            IsGhost = false;
        }
    }

    private Renderer[] _renderers;
    private Collider[] _colliders;
    private Rigidbody _rb;
    private bool _cached;
    private bool _spectatorTargetSet;
    private bool _wasKinematic;
    private bool _lastEliminated;
    private bool _lastGhost;
    private MaterialSnapshot[] _materialSnapshots;
    private Material[][] _originalSharedMaterials;
    private Material _ghostMaterialInstance;

    private void CacheComponents()
    {
        if (_cached) return;
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        _rb = GetComponent<Rigidbody>();
        _wasKinematic = _rb != null && _rb.isKinematic;
        _cached = true;
        CacheOriginalMaterials();
        CacheMaterialSnapshots();
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

    private void ApplyGhostState()
    {
        CacheComponents();
        _lastGhost = IsGhost;

        if (IsGhost)
        {
            SetGhostMaterials(true);
            UpdateCollisionIgnores();
        }
        else
        {
            SetGhostMaterials(false);
            UpdateCollisionIgnores();
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

        if (Object.HasStateAuthority)
        {
            StartGhost(spawnGhostDuration);
            ApplyGhostState();
        }
    }

    private void StartGhost(float duration)
    {
        IsGhost = true;
        GhostUntil = Runner.SimulationTime + duration;
    }

    private bool IsOverlappingOtherPlayers()
    {
        if (_colliders == null || _colliders.Length == 0)
            return false;

        if (!TryGetOwnBounds(out var ownBounds))
            return false;

        foreach (var nh in FindObjectsOfType<NetworkHealth>())
        {
            if (nh == null || nh == this)
                continue;

            if (nh.IsEliminated)
                continue;

            var otherCols = nh.GetComponentsInChildren<Collider>(includeInactive: true);
            foreach (var otherCol in otherCols)
            {
                if (otherCol == null || otherCol.enabled == false || otherCol.isTrigger)
                    continue;

                if (ownBounds.Intersects(otherCol.bounds))
                    return true;
            }
        }

        return false;
    }

    private bool TryGetOwnBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;

        foreach (var col in _colliders)
        {
            if (col == null || col.enabled == false || col.isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return hasBounds;
    }

    private void UpdateCollisionIgnores()
    {
        if (_colliders == null || _colliders.Length == 0)
            return;

        foreach (var other in FindObjectsOfType<NetworkHealth>())
        {
            if (other == null || other == this)
                continue;

            bool ignore = IsGhost || other.IsGhost;
            var otherCols = other.GetComponentsInChildren<Collider>(includeInactive: true);

            foreach (var ownCol in _colliders)
            {
                if (ownCol == null || ownCol.isTrigger)
                    continue;

                foreach (var otherCol in otherCols)
                {
                    if (otherCol == null || otherCol.isTrigger)
                        continue;

                    Physics.IgnoreCollision(ownCol, otherCol, ignore);
                }
            }
        }
    }

    private void CacheMaterialSnapshots()
    {
        if (_materialSnapshots != null)
            return;

        var snapshots = new System.Collections.Generic.List<MaterialSnapshot>();
        foreach (var r in _renderers)
        {
            if (r == null)
                continue;

            var mats = r.materials;
            foreach (var mat in mats)
            {
                if (mat == null)
                    continue;

                snapshots.Add(new MaterialSnapshot(mat));
            }
        }

        _materialSnapshots = snapshots.ToArray();
    }

    private void SetGhostMaterials(bool ghost)
    {
        if (ghostMaterial != null)
        {
            ApplyGhostMaterialSwap(ghost);
            return;
        }

        if (_materialSnapshots == null || _materialSnapshots.Length == 0)
            return;

        foreach (var snap in _materialSnapshots)
        {
            if (snap.Material == null || !snap.Material.HasProperty("_Color"))
                continue;

            if (ghost)
            {
                var c = snap.OriginalColor;
                c.a = ghostAlpha;
                SetMaterialTransparent(snap.Material);
                snap.Material.color = c;
            }
            else
            {
                SetMaterialOpaque(snap);
            }
        }
    }

    private static void SetMaterialTransparent(Material mat)
    {
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void SetMaterialOpaque(MaterialSnapshot snap)
    {
        var mat = snap.Material;
        if (mat == null)
            return;

        mat.SetOverrideTag("RenderType", snap.RenderType);
        mat.SetInt("_SrcBlend", snap.SrcBlend);
        mat.SetInt("_DstBlend", snap.DstBlend);
        mat.SetInt("_ZWrite", snap.ZWrite);

        SetKeyword(mat, "_ALPHATEST_ON", snap.AlphaTest);
        SetKeyword(mat, "_ALPHABLEND_ON", snap.AlphaBlend);
        SetKeyword(mat, "_ALPHAPREMULTIPLY_ON", snap.AlphaPremultiply);

        mat.renderQueue = snap.RenderQueue;
        if (mat.HasProperty("_Color"))
            mat.color = snap.OriginalColor;
    }

    private static void SetKeyword(Material mat, string keyword, bool enabled)
    {
        if (enabled) mat.EnableKeyword(keyword);
        else mat.DisableKeyword(keyword);
    }

    private void CacheOriginalMaterials()
    {
        if (_originalSharedMaterials != null)
            return;

        if (_renderers == null || _renderers.Length == 0)
        {
            _originalSharedMaterials = System.Array.Empty<Material[]>();
            return;
        }

        _originalSharedMaterials = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            _originalSharedMaterials[i] = r != null ? r.sharedMaterials : System.Array.Empty<Material>();
        }
    }

    private void ApplyGhostMaterialSwap(bool ghost)
    {
        if (_renderers == null || _renderers.Length == 0)
            return;

        var ghostMat = GetGhostMaterialInstance();
        if (ghostMat == null)
            return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null)
                continue;

            if (ghost)
            {
                int count = _originalSharedMaterials != null && i < _originalSharedMaterials.Length
                    ? _originalSharedMaterials[i].Length
                    : r.sharedMaterials.Length;

                if (count <= 0)
                    continue;

                var ghostArray = new Material[count];
                for (int m = 0; m < count; m++)
                    ghostArray[m] = ghostMat;

                r.sharedMaterials = ghostArray;
            }
            else if (_originalSharedMaterials != null && i < _originalSharedMaterials.Length)
            {
                r.sharedMaterials = _originalSharedMaterials[i];
            }
        }
    }

    private Material GetGhostMaterialInstance()
    {
        if (ghostMaterial == null)
            return null;

        if (_ghostMaterialInstance != null)
            return _ghostMaterialInstance;

        _ghostMaterialInstance = new Material(ghostMaterial);
        if (_ghostMaterialInstance.HasProperty("_Color"))
        {
            var c = _ghostMaterialInstance.color;
            c.a = ghostAlpha;
            _ghostMaterialInstance.color = c;
        }

        return _ghostMaterialInstance;
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

    private readonly struct MaterialSnapshot
    {
        public readonly Material Material;
        public readonly Color OriginalColor;
        public readonly int SrcBlend;
        public readonly int DstBlend;
        public readonly int ZWrite;
        public readonly string RenderType;
        public readonly int RenderQueue;
        public readonly bool AlphaTest;
        public readonly bool AlphaBlend;
        public readonly bool AlphaPremultiply;

        public MaterialSnapshot(Material mat)
        {
            Material = mat;
            OriginalColor = mat.HasProperty("_Color") ? mat.color : Color.white;
            SrcBlend = mat.HasProperty("_SrcBlend") ? mat.GetInt("_SrcBlend") : (int)UnityEngine.Rendering.BlendMode.One;
            DstBlend = mat.HasProperty("_DstBlend") ? mat.GetInt("_DstBlend") : (int)UnityEngine.Rendering.BlendMode.Zero;
            ZWrite = mat.HasProperty("_ZWrite") ? mat.GetInt("_ZWrite") : 1;
            RenderType = mat.GetTag("RenderType", false, string.Empty);
            RenderQueue = mat.renderQueue;
            AlphaTest = mat.IsKeywordEnabled("_ALPHATEST_ON");
            AlphaBlend = mat.IsKeywordEnabled("_ALPHABLEND_ON");
            AlphaPremultiply = mat.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON");
        }
    }
}
