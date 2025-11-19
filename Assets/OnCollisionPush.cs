using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnCollisionPush : MonoBehaviour
{
     Rigidbody m_Rigidbody;
    public float sila = 20;
    public ForceMode vrsta_Sile=ForceMode.VelocityChange;
    bool collisionHappened = false;
    
    // Start is called before the first frame update
    void Start()
    {
        m_Rigidbody = this.GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
    }
  

    void OnCollisionEnter(Collision collision)
    {
            m_Rigidbody.AddForce(transform.forward * sila, vrsta_Sile);
       

    }


}
