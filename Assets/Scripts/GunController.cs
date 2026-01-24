using UnityEngine;

public class GunController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform gunTransform;
    [SerializeField] private Transform muzzleTransform;

    private Fusion.NetworkObject _netObj;

    [Header("Aiming")]
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float yawOffsetDegrees = 0f; // set 180 if model faces backward

    [Header("Hitscan")]
    [SerializeField] private float fireRate = 0.1f;
    [SerializeField] private float range = 200f;
    [SerializeField] private float damage = 1f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Debug Impact Orb")]
    [SerializeField] private bool spawnDebugOrb = true;
    [SerializeField] private float debugOrbRadius = 0.08f;
    [SerializeField] private float debugOrbLifetime = 2f;
    [SerializeField] private float debugOrbSurfaceOffset = 0.01f;

    [Header("Optional FX")]
    [SerializeField] private GameObject impactPrefab;
    [SerializeField] private bool debugDraw = true;

    private float nextFireTime;
    private string localPlayerLayerName = "LocalPlayer";

    private void Awake()
    {
        _netObj = GetComponentInParent<Fusion.NetworkObject>();

        // Ensure hitMask excludes LocalPlayer layer
        int localLayer = LayerMask.NameToLayer(localPlayerLayerName);
        if (localLayer >= 0)
        {
            hitMask &= ~(1 << localLayer);
        }
    }

    private void Start()
    {
        TryResolveCamera();

        // IMPORTANT in multiplayer: only run this on the local player instance.
        int localLayer = LayerMask.NameToLayer(localPlayerLayerName);
        if (localLayer < 0) return;

        var cols = playerTransform.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
            c.gameObject.layer = localLayer;
    }

    private void Update()
    {
        bool isLocal = _netObj == null || _netObj.HasInputAuthority;
        if (!isLocal) return;

        if (cameraTransform == null || gameplayCamera == null)
        {
            TryResolveCamera();
            if (cameraTransform == null || gameplayCamera == null)
                return;
        }

        // Rotate turret toward camera forward (yaw only, local space friendly)
        Vector3 viewDir = cameraTransform.forward;
        viewDir.y = 0f;
        if (viewDir.sqrMagnitude < 0.0001f)
            viewDir = playerTransform.forward; // fallback
        viewDir.Normalize();

        // Apply yaw offset to compensate for model facing the opposite direction
        Quaternion targetWorld = Quaternion.LookRotation(viewDir, Vector3.up) * Quaternion.Euler(0f, yawOffsetDegrees, 0f);
        Quaternion targetLocal = gunTransform.parent != null
            ? Quaternion.Inverse(gunTransform.parent.rotation) * targetWorld
            : targetWorld;

        gunTransform.localRotation = Quaternion.Slerp(
            gunTransform.localRotation,
            targetLocal,
            rotationSpeed * Time.deltaTime);

        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
            FireHitscan();
    }

    private void FireHitscan()
    {


        nextFireTime = Time.time + fireRate;
        if (muzzleTransform == null) muzzleTransform = gunTransform;

        // 1) Ray from camera to find what the crosshair is pointing at
        Ray camRay = gameplayCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint = cameraTransform.position + cameraTransform.forward * range;

        if (Physics.Raycast(camRay, out RaycastHit camHit, range, hitMask, QueryTriggerInteraction.Ignore))
            targetPoint = camHit.point;

        // 2) Ray from muzzle to the targetPoint
        Vector3 dir = (targetPoint - muzzleTransform.position).normalized;
        float dist = Vector3.Distance(muzzleTransform.position, targetPoint);

        if (Physics.Raycast(muzzleTransform.position, dir, out RaycastHit hit, dist, hitMask, QueryTriggerInteraction.Ignore))
        {
            // Damage
            Health hp = hit.collider.GetComponentInParent<Health>();
            if (hp != null) hp.ApplyDamage(damage);

            // Visual debug orb (red sphere)
            if (spawnDebugOrb) SpawnDebugOrb(hit.point, hit.normal);

            // Optional impact prefab
            if (impactPrefab != null)
                Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));

            if (debugDraw) Debug.DrawLine(muzzleTransform.position, hit.point, Color.red, 0.05f);
        }
        else
        {
            if (debugDraw) Debug.DrawLine(muzzleTransform.position, muzzleTransform.position + dir * dist, Color.yellow, 0.05f);
        }
    }

    private void SpawnDebugOrb(Vector3 hitPoint, Vector3 hitNormal)
    {
        // Slight offset so it doesn't z-fight inside the surface
        Vector3 pos = hitPoint + hitNormal * debugOrbSurfaceOffset;

        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "DebugImpactOrb";
        orb.transform.position = pos;
        orb.transform.localScale = Vector3.one * (debugOrbRadius * 2f);

        // Make it red using a runtime material instance
        var r = orb.GetComponent<Renderer>();
        if (r != null)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = Color.red;
            r.material = mat;
        }

        // No collider needed for a debug marker
        var col = orb.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Destroy(orb, debugOrbLifetime);
    }

    private void TryResolveCamera()
    {
        if (gameplayCamera == null)
        {
            // First try camera on this prefab (active or inactive)
            gameplayCamera = GetComponentInParent<Camera>(includeInactive: true);
            if (gameplayCamera == null)
                gameplayCamera = GetComponentInChildren<Camera>(includeInactive: true);
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;
        }

        if (gameplayCamera != null && cameraTransform == null)
            cameraTransform = gameplayCamera.transform;
    }
}
