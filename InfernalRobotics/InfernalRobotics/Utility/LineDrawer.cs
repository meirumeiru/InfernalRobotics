using UnityEngine;

#if DEBUG

namespace InfernalRobotics_v3.Utility
{
	public struct LineDrawer
	{
		private LineRenderer lineRenderer;
		private float lineSize;

		public LineDrawer(Transform parent = null, float lineSize = 0.02f)
		{
			GameObject lineObj = new GameObject("LineObj");
			lineRenderer = lineObj.AddComponent<LineRenderer>();

			lineRenderer.transform.parent = parent;

			lineRenderer.transform.localPosition = Vector3.zero;
			lineRenderer.transform.localRotation = Quaternion.identity;

			lineRenderer.useWorldSpace = false;

			// particles / additive
			lineRenderer.material = new Material(Shader.Find("Hidden/Internal-Colored"));

			this.lineSize = lineSize;

			lineRenderer.enabled = false;
		}

		public void Destroy()
		{
			if(lineRenderer != null)
				UnityEngine.Object.Destroy(lineRenderer.gameObject);
		}

		// draws the line through the provided vertices
		public void Draw(Vector3 start, Vector3 end, Color color)
		{
			if(lineRenderer == null)
				return;

			lineRenderer.startColor = color; lineRenderer.endColor = color;
			lineRenderer.startWidth = lineSize; lineRenderer.endWidth = lineSize;

			// set line count which is 2
			lineRenderer.SetPosition(0, lineRenderer.transform.InverseTransformPoint(start));
			lineRenderer.SetPosition(1, lineRenderer.transform.InverseTransformPoint(end));

			lineRenderer.enabled = true;
		}

		// hides the line
		public void Hide()
		{
			lineRenderer.enabled = false;
		}
	}

	public class MultiLineDrawer
	{
		public LineDrawer[] al;

		public void Create(Transform t, int count = 13)
		{
			al = new LineDrawer[count];

			for(int i = 0; i < al.Length; i++)
				al[i] = new LineDrawer(t);
		}

		public void Destroy()
		{
			for(int i = 0; i < al.Length; i++)
				al[i].Destroy();

			al = null;
		}

		// draws the line through the provided vertices
		public void Draw(int idx, int color, Vector3 start, Vector3 end)
		{
			Color clr;

			switch(color)
			{
			case 0: clr = Color.red; break;
			case 1: clr = Color.green; break;
			case 2: clr = Color.yellow; break;
			case 3: clr = Color.magenta; break;
			case 4: clr = Color.blue; break;
			case 5: clr = Color.white; break;
			case 6: clr = new Color(255.0f / 255.0f, 128.0f / 255.0f, 0.0f / 255.0f); break;
			case 7: clr = new Color(0.0f / 255.0f, 128.0f / 255.0f, 0.0f / 255.0f); break;
			case 8: clr = new Color(0.0f / 255.0f, 255.0f / 255.0f, 255.0f / 255.0f); break;
			case 9: clr = new Color(128.0f / 255.0f, 0.0f / 255.0f, 255.0f / 255.0f); break;
			case 10: clr = new Color(255.0f / 255.0f, 0.0f / 255.0f, 128.0f / 255.0f); break;
			case 11: clr = new Color(0.0f / 255.0f, 0.0f / 255.0f, 128.0f / 255.0f); break;
			case 12: clr = new Color(128.0f / 255.0f, 0.0f / 255.0f, 128.0f / 255.0f); break;
			default: clr = Color.black; break;
			}

			clr.a = 0.7f;
			al[idx].Draw(start, end, clr);
		}

		// hides the line
		public void Hide(int idx)
		{
			al[idx].Hide();
		}

		// hides all lines
		public void Hide()
		{
			for(int i = 0; i < al.Length; i++)
				al[i].Hide();
		}
	}
}

#endif
