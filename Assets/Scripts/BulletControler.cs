using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletControler : MonoBehaviour
{
    public Vector3 velocity;
    public uint damage = 0;
    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.AddForce(velocity, ForceMode.VelocityChange);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            CarHealth healthScript = collision.gameObject.GetComponent<CarHealth>();
            healthScript.Deal_Damage(damage);
        }
    }
}
