﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using KSP.IO;
using UnityEngine;

using InfernalRobotics_v3.Interfaces;

namespace InfernalRobotics_v3.Gui
{
	class IPresetableInterceptor : IPresetable
	{
		private IPresetable p;
		private Vessel v;

		public IPresetableInterceptor(IPresetable presetable, Vessel vessel)
		{
			p = presetable;
			v = vessel;
		}

		private bool IsControllable()
		{
			return HighLogic.LoadedSceneIsEditor || (v.CurrentControlLevel >= Vessel.ControlLevel.PARTIAL_MANNED);
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

		public void MovePrev()
		{
			if(!HighLogic.LoadedSceneIsEditor)
			{ if(IsControllable()) p.MovePrev(); }
			else
				p.EditorMovePrev();
		}

		public void MoveNext()
		{
			if(!HighLogic.LoadedSceneIsEditor)
			{ if(IsControllable()) p.MoveNext(); }
			else
				p.EditorMoveNext();
		}

		public void MoveTo(int presetIndex)
		{
			if(!HighLogic.LoadedSceneIsEditor)
			{ if(IsControllable()) p.MoveTo(presetIndex); }
			else
				p.EditorMoveTo(presetIndex);
		}

		public void GetNearestPresets(out int floor, out int ceiling)
		{
			p.GetNearestPresets(out floor, out ceiling);
		}

		////////////////////////////////////////
		// Editor

		public void EditorMovePrev()
		{
			p.EditorMovePrev();
		}

		public void EditorMoveNext()
		{
			p.EditorMoveNext();
		}

		public void EditorMoveTo(int presetIndex)
		{
			p.EditorMoveTo(presetIndex);
		}
	}
}
