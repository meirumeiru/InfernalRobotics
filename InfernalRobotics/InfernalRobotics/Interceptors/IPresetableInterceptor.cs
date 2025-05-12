using System.Collections.Generic;

using InfernalRobotics_v3.Interfaces;


namespace InfernalRobotics_v3.Interceptors
{
	class IPresetableInterceptor : IPresetable
	{
		private IPresetable p;

		public IPresetableInterceptor(IPresetable presetable)
		{
			p = presetable;
		}

		private bool IsControllable()
		{
			return HighLogic.LoadedSceneIsEditor || (p.HostPart.vessel.CurrentControlLevel > Vessel.ControlLevel.NONE);
		}


		public Part HostPart
		{
			get { return p.HostPart; }
		}

		public void Add(float? position = null)
		{
			if(IsControllable()) p.Add(position);
		}

		public void RemoveAt(int presetIndex)
		{
			if(IsControllable()) p.RemoveAt(presetIndex);
		}
		
		public int Count
		{
			get { return p.Count; }
		}

		public float this[int index]
		{
			get { return p[index]; }
			set { if(IsControllable()) p[index] = value; }
		}

		public void Sort(IComparer<float> sorter = null)
		{
			if(IsControllable()) p.Sort(sorter);
		}

		public void MovePrev(float targetSpeed)
		{
			if(IsControllable()) p.MovePrev(targetSpeed);
		}

		public void MoveNext(float targetSpeed)
		{
			if(IsControllable()) p.MoveNext(targetSpeed);
		}

		public void MoveTo(int presetIndex, float targetSpeed)
		{
			if(IsControllable()) p.MoveTo(presetIndex, targetSpeed);
		}

		public void GetNearestPresets(out int floor, out int ceiling)
		{
			p.GetNearestPresets(out floor, out ceiling);
		}

		////////////////////////////////////////
		// Editor

		public void EditorMovePrev(float targetSpeed)
		{ p.EditorMovePrev(targetSpeed); }

		public void EditorMoveNext(float targetSpeed)
		{ p.EditorMoveNext(targetSpeed); }

		public void EditorMoveTo(int presetIndex, float targetSpeed)
		{ p.EditorMoveTo(presetIndex, targetSpeed); }
	}
}
