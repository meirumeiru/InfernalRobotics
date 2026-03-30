using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using InfernalRobotics_v3.Interfaces;
using InfernalRobotics_v3.Module;


namespace InfernalRobotics_v3.Command
{
	public class ServoGroup : IServoGroup
	{
		private Vessel vessel;
		private List<IServo> servos;

		public class Settings
		{
			public string name;
			public string forwardKey;
			public string reverseKey;
			public float groupSpeedFactor;
		};

		public Settings settings;

		private bool bDirty;

		private float totalElectricChargeRequirement;

		public ServoGroup(Vessel v, Settings s)
			: this(s)
		{
			vessel = v;
		}

		public ServoGroup(Settings s)
		{
			servos = new List<IServo>();

			settings = s;

			Expanded = false;
			bDirty = true;

			BuildAid = false;
			IKActive = false;
		}

		public IServoGroup group
		{
			get { return this; }
		}

		public Vessel Vessel
		{
			get { return vessel; }
			set { vessel = value; }
		}

		public string Name 
		{ 
			get { return settings.name; } 
			set { settings.name = value; } 
		}

		public IList<IServo> Servos
		{
			get { return servos; }
		}

		public bool Contains(IServo servo)
		{
			return servos.Contains(servo);
		}

		private void AddControl(IServo servo, int index)
		{
			if(servos.Contains(servo))
				return;

			for(int i = 0; i < servo.HostPart.symmetryCounterparts.Count; i++)
			{
				if(servos.Contains((IServo)servo.HostPart.symmetryCounterparts[i].GetComponent<ModuleIRServo_v3>()))
					return;
			}

			servos.Insert(index < 0 ? servos.Count : index, servo);

			bDirty = true;
		}

		private void RemoveControl(IServo servo)
		{
			if(servos.Remove(servo))
				bDirty = true;
		}

		private void Refresh()
		{
			for(int i = 0; i < servos.Count; i++)
			{
				ModuleIRServo_v3 s = (ModuleIRServo_v3)servos[i].servo;

				s.RemoveGroup(this);
				s.AddGroup(this, i);

				s.SerializeGroupNames();
			}
		}

		public static void AddControl(IServoGroup group, IServo servo, int index, bool updateGroup = true)
		{
			((ServoGroup)group).AddControl(servo, index);

			if(updateGroup)
				((ServoGroup)group).Refresh();
		}

		public static void RemoveControl(IServoGroup group, IServo servo, bool updateServo, bool updateGroup = true)
		{
			if(updateServo)
			{
				((ModuleIRServo_v3)servo.servo).RemoveGroup(group);
				((ModuleIRServo_v3)servo.servo).SerializeGroupNames();
			}

			((ServoGroup)group).RemoveControl(servo);

			if(updateGroup)
				((ServoGroup)group).Refresh();
		}

		public static void MoveServo(IServoGroup from, IServoGroup to, IServo servo, int index, bool updateGroups = true)
		{
			((ModuleIRServo_v3)servo.servo).RemoveGroup(from.group);

			((ServoGroup)from.group).RemoveControl(servo);
			((ServoGroup)to.group).AddControl(servo, index);

			if(updateGroups)
			{
				((ServoGroup)from.group).Refresh();
				((ServoGroup)to.group).Refresh();
			}
		}

		public static void UpdateGroup(IServoGroup group)
		{
			((ServoGroup)group).Refresh();
		}

		////////////////////////////////////////
		// Status

		private int iMovingDirection = 0;

		public int MovingDirection
		{
			get { return iMovingDirection; }
		}

		////////////////////////////////////////
		// Settings

		public bool Expanded { get; set; }

		public bool AdvancedMode { get; set; }

		public float GroupSpeedFactor
		{
			get { return settings.groupSpeedFactor; }
			set
			{
				settings.groupSpeedFactor = value;
			}
		}

		public string ForwardKey
		{
			get { return settings.forwardKey; }
			set
			{
				settings.forwardKey = value;
			}
		}

		public string ReverseKey
		{
			get { return settings.reverseKey; }
			set
			{
				settings.reverseKey = value;
			}
		}

		////////////////////////////////////////
		// Input

		private bool KeyPressed(string key)
		{
			return (key != "" && vessel == FlightGlobals.ActiveVessel
					&& InputLockManager.IsUnlocked(ControlTypes.LINEAR)
					&& Input.GetKey(key));
		}

		private bool KeyUnPressed(string key)
		{
			return (key != "" && vessel == FlightGlobals.ActiveVessel
					&& InputLockManager.IsUnlocked(ControlTypes.LINEAR)
					&& Input.GetKeyUp(key));
		}

		public void CheckInputs()
		{
			if(KeyPressed(settings.forwardKey))
				MoveRight();
			else if(KeyPressed(settings.reverseKey))
				MoveLeft();
			else if(KeyUnPressed(settings.forwardKey) || KeyUnPressed(settings.reverseKey))
				Stop();
		}

		////////////////////////////////////////
		// Characteristics

		public float TotalElectricChargeRequirement
		{
			get
			{
				if(bDirty) Freshen();
				return totalElectricChargeRequirement;
			}
		}

		public void MoveLeft()
		{
			iMovingDirection = -1;

			foreach(var servo in servos)
				servo.MoveLeft(servo.DefaultSpeed * GroupSpeedFactor);
		}

		public void MoveCenter()
		{
			iMovingDirection = 0;

			foreach(var servo in servos)
				servo.MoveCenter(servo.DefaultSpeed * GroupSpeedFactor);
		}

		public void MoveRight()
		{
			iMovingDirection = 1;

			foreach(var servo in servos)
				servo.MoveRight(servo.DefaultSpeed * GroupSpeedFactor);
		}

		public void MovePrevPreset()
		{
			foreach(var servo in servos)
				servo.Presets.MovePrev(servo.DefaultSpeed * GroupSpeedFactor);
		}

		public void MoveNextPreset()
		{
			foreach(var servo in servos)
				servo.Presets.MoveNext(servo.DefaultSpeed * GroupSpeedFactor);
		}

		public void Stop()
		{
			iMovingDirection = 0;

			foreach(var servo in servos)
				servo.Stop();
		}

		private void Freshen()
		{
			totalElectricChargeRequirement = servos.Where(s => s.IsFreeMoving == false).Sum (s => s.ElectricChargeRequired);

			bDirty = false;
		}

		////////////////////////////////////////
		// Editor

		public void EditorMoveLeft()
		{
			foreach(var servo in servos)
				servo.EditorMoveLeft(servo.DefaultSpeed * GroupSpeedFactor);
		}

		public void EditorMoveCenter()
		{
			foreach(var servo in servos)
				servo.EditorMoveCenter(servo.DefaultSpeed * GroupSpeedFactor);
		}

		public void EditorMoveRight()
		{
			foreach(var servo in servos)
				servo.EditorMoveRight(servo.DefaultSpeed * GroupSpeedFactor);
		}

		public void EditorMovePrevPreset()
		{
			foreach(var servo in servos)
				servo.Presets.EditorMovePrev(servo.DefaultSpeed * GroupSpeedFactor);
		}

		public void EditorMoveNextPreset()
		{
			foreach(var servo in servos)
				servo.Presets.EditorMovePrev(servo.DefaultSpeed * GroupSpeedFactor);
		}

		////////////////////////////////////////
		// BuildAid

		public bool BuildAid { get; set; }

		////////////////////////////////////////
		// IK

		public bool IKActive { get; set; }
	}
}
