using UnityEngine;
using System.Collections;

public class SetRandomColor : MonoBehaviour 
{
	public Color[] colors;

	private Color nextColor = Color.black;
	private MeshRenderer meshRenderer;
	private float lastTime;

	void OnEnable()
	{
		if (meshRenderer == null)
			meshRenderer = GetComponent<MeshRenderer>();
		
		meshRenderer.material.color = colors[Random.Range(0, colors.Length)];
		nextColor = colors[Random.Range(0, colors.Length)];

		lastTime = Time.time;

	}

	// Change color over time;
	void Update()
	{
		if (Time.time - lastTime >= 2f)
		{
			nextColor = colors[Random.Range(0, colors.Length)];

			while (nextColor == meshRenderer.material.color)
			{
				nextColor = colors[Random.Range(0, colors.Length)];
			}
				
			lastTime = Time.time;
		}

		if (meshRenderer != null)
			meshRenderer.material.color = Color.Lerp(GetComponent<MeshRenderer>().material.color, nextColor, 2f * Time.deltaTime); 
	}
}
