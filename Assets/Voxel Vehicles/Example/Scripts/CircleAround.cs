using UnityEngine;
using System.Collections;

public class CircleAround : MonoBehaviour 
{
	public float radius;
	public float timeMultiplier = 0.25f;
	void Update () 
	{
		transform.position = new Vector3(Mathf.Sin(Time.time * timeMultiplier) * radius, 60f + (Mathf.Cos((Time.time / 2f) * timeMultiplier) * 2f), Mathf.Cos(Time.time * timeMultiplier) * radius);
		transform.LookAt(Vector3.zero);
	}
}
