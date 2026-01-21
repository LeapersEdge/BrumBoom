using Fusion;
using UnityEngine;

public class DebugDamageKey : NetworkBehaviour
{
    [SerializeField] private float damagePerPress = 10f;

    void Update()
    {
        // samo local player smije triggerat test
        if (!Object.HasInputAuthority) return;

        if (Input.GetKeyDown(KeyCode.K))
        {
            var nh = GetComponent<NetworkHealth>();
            if (nh != null)
                nh.RequestDamage(damagePerPress);
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("steta");
            var nh = GetComponent<NetworkHealth>();
            if (nh != null)
                nh.RequestDamage(-damagePerPress); // (ako želiš heal test) 
            
        
        }
    }
}
