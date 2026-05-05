using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using InfernalRobotics_v3.Interfaces;
using InfernalRobotics_v3.Interceptors;
using InfernalRobotics_v3.Module;
using InfernalRobotics_v3.Utility;


namespace InfernalRobotics_v3.Command
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class ControllerFlight : Controller
	{
		public override string AddonName { get { return this.name; } }
	}

	[KSPAddon(KSPAddon.Startup.EditorAny, false)]
	public class ControllerEditor : Controller
	{
		public override string AddonName { get { return this.name; } }
	}

	public class Controller : MonoBehaviour
	{
		public virtual String AddonName { get; set; }

		protected static Controller ControllerInstance;

		public static IIKModule _IKModule;
		public static IServoGroup _IKServoGroup;

		public static List<ServoGroup.Settings> ServoGroupSettings; // needs to be static to keep the settings available when reverting to VAB/SPH, because we don't get a new OnLoad in this case
		public List<IServoGroup> ServoGroups;

		private class IServoState { public bool bIsBuildAidOn = false; }
		private Dictionary<IServo, IServoState> servosState;

		private int loadedVesselCounter = 0;

		public static Controller Instance { get { return ControllerInstance; } }

		public static void RegisterIKModule(IIKModule IKModule)
		{
			_IKModule = IKModule;

			if(ControllerInstance != null)
				Gui.WindowManager.Instance.UpdateIKButtons();
		}

		public static bool APIReady { get { return ControllerInstance != null && ControllerInstance.servosState != null && ControllerInstance.servosState.Count > 0; } }

		private static void EditorAddServo(IServo servo)
		{
			if(!Instance)
				return;
			
			if(Instance.servosState == null)
				Instance.servosState = new Dictionary<IServo, IServoState>();

			Instance.servosState.Add(servo, new IServoState());

			if(Instance.ServoGroups == null)
				Instance.ServoGroups = new List<IServoGroup>();

			ModuleIRServo_v3 s = (ModuleIRServo_v3)servo;

			if(!string.IsNullOrEmpty(s.groupName))
			{
				s.GroupPositions.Clear();

				List<string> groups = new List<string>(s.groupName.Split('|'));

				for(int i = 0; i < groups.Count; i++)
				{
					int j = groups[i].IndexOf(';');
					if(j >= 0) groups[i] = groups[i].Substring(0, j);
				}

				foreach(ServoGroup g in Instance.ServoGroups)
				{
					if(groups.Contains(g.Name))
					{
						ServoGroup.AddControl(g, servo, -1);

						groups.Remove(g.Name);
					}
				}

				while(groups.Count > 0)
				{
					ServoGroup.Settings settings = new ServoGroup.Settings();
					settings.name = groups[0];
					settings.forwardKey = "";
					settings.reverseKey = "";
					settings.groupSpeedFactor = 1;

					ServoGroup g = new ServoGroup(settings);
					Instance.ServoGroups.Add(g);

					ServoGroup.AddControl(g, servo, 0);

					groups.RemoveAt(0);
				}
			}

			if(Gui.WindowManager.Instance != null)
				Gui.WindowManager.Instance.Invalidate();
		}

		private static void EditorRemoveServo(IServo servo)
		{
			if(!Instance)
				return;

			Instance.servosState.Remove(servo);
			Instance._ServoToServoInterceptor.Remove(servo);

			if(Instance.ServoGroups == null)
				return;

			for(int i = 0; i < Instance.ServoGroups.Count; i++)
			{
				ServoGroup g = (ServoGroup)Instance.ServoGroups[i].group;

				if(g.Contains(servo))
					ServoGroup.RemoveControl(g, servo, false);
			}

			((ModuleIRServo_v3)servo).GroupPositions.Clear();

			if(Gui.WindowManager.Instance)
				Gui.WindowManager.Instance.Invalidate();

			if(Gui.IRBuildAid.IRBuildAidManager.Instance)
				Gui.IRBuildAid.IRBuildAidManager.Instance.HideServoRange(servo);
		}

		internal ServoGroup.Settings FindOrCreateServoGroupSettings(string name)
		{
			if(ServoGroupSettings == null)
				ServoGroupSettings = new List<ServoGroup.Settings>();

			foreach(ServoGroup.Settings h in ServoGroupSettings)
			{
				if(h.name.CompareTo(name) == 0)
					return h;
			}

			ServoGroup.Settings n = new ServoGroup.Settings();
			n.name = name;
			n.forwardKey = "";
			n.reverseKey = "";
			n.groupSpeedFactor = 1;

			return n;
		}

		internal void RefreshServoGroupSettings()
		{
			ServoGroupSettings = new List<ServoGroup.Settings>();

			foreach(ServoGroup g in ServoGroups)
			{
				if(!ServoGroupSettings.Contains(g.settings))
					ServoGroupSettings.Add(g.settings);
				// OPTION: an other idea would be to create a copy and use the copy in the group -> this case is possible, if multiple ships use the same group
// FEHLER, diese Option nochmal prüfen
			}
		}

		internal struct ServoWithIndex
		{ public IServo servo; public int index; };

		internal int CompareServoWithIndex(ServoWithIndex left, ServoWithIndex right)
		{
			if(left.index < right.index) return -1;
			if(left.index > right.index) return 1;
			return 0;
		}

		// internal (not private) because we need to call it from "ModuleIRServo_v3.RemoveFromSymmetry2"
		internal void RebuildServoGroupsEditor()
		{
			ServoGroups = null;
			servosState = null;

			if(EditorLogic.fetch.ship == null)
				return;

			ServoGroups = new List<IServoGroup>();
			servosState = new Dictionary<IServo, IServoState>();

			var groupServos = new Dictionary<string, List<ServoWithIndex>>();

			foreach(Part p in EditorLogic.fetch.ship.Parts)
			{
				foreach(var servo in p.ToServos())
				{
					servosState.Add(servo, new IServoState());

					ModuleIRServo_v3 s = (ModuleIRServo_v3)servo;

					if(!string.IsNullOrEmpty(s.groupName))
					{
						List<string> groups = new List<string>(s.groupName.Split('|'));

						foreach(string group in groups)
						{
							string[] gi = group.Split(';');

							List<ServoWithIndex> servos;
							if(!groupServos.TryGetValue(gi[0], out servos))
							{
								servos = new List<ServoWithIndex>();
								groupServos.Add(gi[0], servos);
							}

							ServoWithIndex si = new ServoWithIndex();
							si.servo = servo; si.index = (gi.Length > 1) ? int.Parse(gi[1]) : -1;

							servos.Add(si);
						}
					}
				}
			}

			foreach(var kv in groupServos)
			{
				ServoGroup g = new ServoGroup(FindOrCreateServoGroupSettings((string)kv.Key));
				ServoGroups.Add(g);

				kv.Value.Sort(CompareServoWithIndex);

				foreach(ServoWithIndex si in kv.Value)
					ServoGroup.AddControl(g, si.servo, -1, false);

				ServoGroup.UpdateGroup(g);
			}

			RefreshServoGroupSettings();

			if(ServoGroups.Count == 0)
				ServoGroups = null;

			if(Gui.WindowManager.Instance != null)
				Gui.WindowManager.Instance.Invalidate();
		}
	   
		private void OnEditorStarted()
		{
			RebuildServoGroupsEditor();

			if(Gui.WindowManager.Instance != null)
				Gui.WindowManager.Instance.Invalidate();

			if(Gui.IRBuildAid.IRBuildAidManager.Instance)
				Gui.IRBuildAid.IRBuildAidManager.Reset();
		}

		// internal (not private) because we need to call it from "ModuleIRServo_v3.RemoveFromSymmetry2"
		private bool bRebuildingServoGroupsFlight = false;

		internal void RebuildServoGroupsFlight()
		{
			if(!bRebuildingServoGroupsFlight)
			{
				bRebuildingServoGroupsFlight = true;
				StartCoroutine(_RebuildServoGroupsFlight());
			}
		}

		internal IEnumerator _RebuildServoGroupsFlight()
		{
			int w = 8;
			while(--w > 0)
				yield return new WaitForFixedUpdate();

			bRebuildingServoGroupsFlight = false;

			Dictionary<IServo, IServoState> oldServoState = (Instance.servosState != null) ? Instance.servosState : new Dictionary<IServo, IServoState>();
			servosState = new Dictionary<IServo, IServoState>();

			List<IServoGroup> oldServoGroups = (ServoGroups != null) ? ServoGroups : new List<IServoGroup>();
			ServoGroups = new List<IServoGroup>();

			bool bNewGroups = false;

			IServoGroup ikGroup = null;

			var vessel = FlightGlobals.ActiveVessel;

			var groupServos = new Dictionary<string, List<ServoWithIndex>>();

			foreach(var servo in vessel.ToServos())
			{
				IServoState state;
				if(oldServoState.TryGetValue(servo, out state))
					servosState.Add(servo, state);
				else
					servosState.Add(servo, new IServoState());

				ModuleIRServo_v3 s = (ModuleIRServo_v3)servo;

				if(!string.IsNullOrEmpty(s.groupName))
				{
					List<string> groups = new List<string>(s.groupName.Split('|'));

					foreach(string group in groups)
					{
						string[] gi = group.Split(';');

						List<ServoWithIndex> servos;
						if(!groupServos.TryGetValue(gi[0], out servos))
						{
							servos = new List<ServoWithIndex>();
							groupServos.Add(gi[0], servos);
						}

						ServoWithIndex si = new ServoWithIndex();
						si.servo = servo; si.index = (gi.Length > 1) ? int.Parse(gi[1]) : -1;

						servos.Add(si);
					}
				}
			}

			foreach(var kv in groupServos)
			{
				kv.Value.Sort(CompareServoWithIndex);

				ServoGroup g = null;

				// find existing group in old groups
				for(int j = 0; (j < oldServoGroups.Count) && (g == null); j++)
				{
					ServoGroup o = (ServoGroup)oldServoGroups[j];

					if(o.Name != (string)kv.Key)
						continue;

					if(o.Servos.Count != kv.Value.Count)
						continue;

					int k = 0;
					while((k < o.Servos.Count) && (o.Servos[k].HostPart.flightID == kv.Value[k].servo.HostPart.flightID))
						++k;

					if(k < o.Servos.Count)
						continue;

					oldServoGroups.RemoveAt(j);
					g = o;
				}

				if(g != null)
				{
					g.Vessel = g.Servos[0].HostPart.vessel; // update vessel in group

					ServoGroups.Add(g);

					if(g.IKActive)
						ikGroup = g;
				}
				else
				{
					bNewGroups = true;

					g = new ServoGroup(vessel, FindOrCreateServoGroupSettings((string)kv.Key));
					ServoGroups.Add(g);

					foreach(ServoWithIndex si in kv.Value)
						ServoGroup.AddControl(g, si.servo, -1, false);

					ServoGroup.UpdateGroup(g);
				}
			}

			if(bNewGroups || (oldServoGroups.Count > 0))
			{
				RefreshServoGroupSettings();

				if(ServoGroups.Count == 0)
					ServoGroups = null;

				if(_IKModule != null)
				{
					_IKModule.Reset();

					if(ikGroup != null)
					{
						if(_IKServoGroup != null)
						{
							Vector3 p; Quaternion q;
							_IKModule.GetTarget(_IKServoGroup, out p, out q);

							_IKModule.SelectActiveGroup(null);
							_IKModule.SelectActiveGroup(ikGroup);
							_IKServoGroup = ikGroup;

							_IKModule.SetTarget(_IKServoGroup, p, q);
						}
						else
						{
							_IKModule.SelectActiveGroup(null);
							_IKModule.SelectActiveGroup(ikGroup);
							_IKServoGroup = ikGroup;
						}
					}
					else
					{
						_IKModule.SelectActiveGroup(null);
						_IKServoGroup = null;
					}
				}

				if(Gui.WindowManager.Instance != null)
					Gui.WindowManager.Instance.Invalidate();
			}
			else
			{
				if((_IKModule != null) && (ikGroup != null))
				{
					if(_IKServoGroup != null)
					{
						Vector3 p; Quaternion q;
						_IKModule.GetTarget(_IKServoGroup, out p, out q);

						_IKModule.SelectActiveGroup(null);
						_IKModule.SelectActiveGroup(ikGroup);
						_IKServoGroup = ikGroup;

						_IKModule.SetTarget(_IKServoGroup, p, q);
					}
					else
					{
						_IKModule.SelectActiveGroup(null);
						_IKModule.SelectActiveGroup(ikGroup);
						_IKServoGroup = ikGroup;
					}
				}
			}
		}

		private void OnEditorPartAttach(Part part)
		{
			foreach(var p in part.GetChildServos())
				EditorAddServo(p);
		}

		private void OnEditorPartRemove(Part part)
		{
			foreach(var p in part.GetChildServos())
				EditorRemoveServo(p);
		}

		public void OnEditorPartEvent(ConstructionEventType evt, Part part)
		{
			switch(evt)
			{
			case ConstructionEventType.PartAttached:
				OnEditorPartAttach(part);
				break;

			case ConstructionEventType.PartSymmetryDeleted:
				OnEditorPartRemove(part); // we have to handle this event, because we don't get a "PartDetached" if a symmetric part is detached instead of the original one
				break;

			case ConstructionEventType.PartDetached:
				OnEditorPartRemove(part);
				break;
			}
		}

		private void OnEditorUnOrRedo(ShipConstruct ship)
		{
			if(!Instance)
				return;

			if(Instance.ServoGroups == null)
				return;

			List<ModuleIRServo_v3> allServos = new List<ModuleIRServo_v3>();

			foreach(Part p in ship.parts)
			{
				ModuleIRServo_v3 servo = p.GetComponent<ModuleIRServo_v3>();

				if(servo != null)
					allServos.Add(servo);
			}

			HashSet<ModuleIRServo_v3> servosNotToAdd = new HashSet<ModuleIRServo_v3>();
			HashSet<ModuleIRServo_v3> servosToRemove = new HashSet<ModuleIRServo_v3>();

			for(int i = 0; i < Instance.ServoGroups.Count; i++)
			{
				for(int j = 0; j < Instance.ServoGroups[i].Servos.Count; j++)
				{
					ModuleIRServo_v3 servo = (ModuleIRServo_v3)Instance.ServoGroups[i].Servos[j].servo;

					if(allServos.Contains(servo))
						servosNotToAdd.Add(servo);
					else
						servosToRemove.Add(servo);
				}
			}

			foreach(ModuleIRServo_v3 servo in servosToRemove)
				EditorRemoveServo(servo);

			foreach(ModuleIRServo_v3 servo in allServos)
			{
				if(!servosNotToAdd.Contains(servo))
					EditorAddServo(servo);
			}
		}

		private void OnVesselChange(Vessel v)
		{
			RebuildServoGroupsFlight();
		}

		private void OnVesselWasModified(Vessel v)
		{
			RebuildServoGroupsFlight();
		}

		private void OnVesselLoaded(Vessel v)
		{
			RebuildServoGroupsFlight();
		}

		private void OnVesselUnloaded(Vessel v)
		{
			RebuildServoGroupsFlight();
		}

		private void Awake()
		{
			GameScenes scene = HighLogic.LoadedScene;

			if(HighLogic.LoadedSceneIsFlight)
			{
				GameEvents.onVesselChange.Add(OnVesselChange);
				GameEvents.onVesselWasModified.Add(OnVesselWasModified);
				GameEvents.onVesselLoaded.Add(OnVesselLoaded);
				GameEvents.onVesselDestroy.Add(OnVesselUnloaded);
				GameEvents.onVesselGoOnRails.Add(OnVesselUnloaded);

				ControllerInstance = this;
			}
			else if(HighLogic.LoadedSceneIsEditor)
			{
				GameEvents.onEditorStarted.Add(OnEditorStarted);
				GameEvents.onEditorPartEvent.Add(OnEditorPartEvent);
				GameEvents.onEditorUndo.Add(OnEditorUnOrRedo);
				GameEvents.onEditorRedo.Add(OnEditorUnOrRedo);

				ControllerInstance = this;
			}
			else
			{
				ControllerInstance = null;
			}
		}

		private void FixedUpdate()
		{
			if(HighLogic.LoadedSceneIsFlight)
			{
				// because OnVesselDestroy and OnVesselGoOnRails seem to only work for active vessel I had to build this stupid workaround
				if(FlightGlobals.Vessels.Count(v => v.loaded) != loadedVesselCounter)
				{
					RebuildServoGroupsFlight();
					loadedVesselCounter = FlightGlobals.Vessels.Count(v => v.loaded);
				}

				if(ServoGroups != null)
				{
					for(int i = 0; i < ServoGroups.Count; i++)
						((ServoGroup)ServoGroups[i]).CheckInputs();
				}
			}
		}

		private void OnDestroy()
		{
			GameEvents.onVesselChange.Remove(OnVesselChange);
			GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
			GameEvents.onVesselLoaded.Remove(OnVesselLoaded);
			GameEvents.onVesselDestroy.Remove(OnVesselUnloaded);
			GameEvents.onVesselGoOnRails.Remove(OnVesselUnloaded);

			GameEvents.onEditorStarted.Remove(OnEditorStarted);
			GameEvents.onEditorPartEvent.Remove(OnEditorPartEvent);
			GameEvents.onEditorUndo.Remove(OnEditorUnOrRedo);
			GameEvents.onEditorRedo.Remove(OnEditorUnOrRedo);
		}

		private static Part GetPartUnderCursor()
		{
			Ray ray;
			if(HighLogic.LoadedSceneIsFlight)
				ray = FlightCamera.fetch.mainCamera.ScreenPointToRay(Input.mousePosition);
			else
				ray = Camera.main.ScreenPointToRay(Input.mousePosition);

			RaycastHit hit;
			if(Physics.Raycast(ray, out hit, 1000, 557059))
				return hit.transform.gameObject.GetComponent<Part>();
			else
				return null;
		}

		////////////////////////////////////////
		// Interceptors

		private Dictionary<IServo, IServo> _ServoToServoInterceptor = new Dictionary<IServo, IServo>();

		public IServo GetInterceptor(IServo servo)
		{
			// check if this is already an interceptor
			if(servo.servo != servo)
				return servo;

			foreach(var pair in _ServoToServoInterceptor)
			{
				if(pair.Key == servo)
					return pair.Value;
			}

			ServoInterceptor interceptor = new ServoInterceptor(servo);

			_ServoToServoInterceptor.Add(servo, interceptor);

			return interceptor;
		}

		private Dictionary<IServoGroup, IServoGroup> _ServoGroupToServoGroupInterceptor = new Dictionary<IServoGroup, IServoGroup>();

		public IServoGroup GetInterceptor(IServoGroup group)
		{
			// check if this is already an interceptor
			if(group.group != group)
				return group;

			foreach(var pair in _ServoGroupToServoGroupInterceptor)
			{
				if(pair.Key == group)
					return pair.Value;
			}

			ServoGroupInterceptor interceptor = new ServoGroupInterceptor(group);

			_ServoGroupToServoGroupInterceptor.Add(group, interceptor);

			return interceptor;
		}

		////////////////////////////////////////
		// BuildAid

		public bool ServoBuildAid(IServo s)
		{
			return servosState[s.servo].bIsBuildAidOn;
		}

		public void ServoBuildAid(IServo s, bool v)
		{
			servosState[s.servo].bIsBuildAidOn = v;
		}

		////////////////////////////////////////
		// Relax

		public void Relax(IServoGroup g, int factor)
		{
			if((_IKModule != null) && (_IKServoGroup == g))
				_IKModule.Relax(g, factor);
			else
				StartCoroutine(RelaxGroup(g, factor));
		}

		private IEnumerator RelaxGroup(IServoGroup g, int factor)
		{
			foreach(IServo s in g.Servos)
				s.SetRelaxMode(1f);

			int i = factor;

			while(i-- > 0)
			{
				foreach(IServo s in g.Servos)
					s.RelaxStep();

				yield return new WaitForFixedUpdate();
			}

			foreach(IServo s in g.Servos)
				s.ResetRelaxMode();
		}
	}

	public class ModuleIRController
	{
		public static void OnSave(ConfigNode config, Part p)
		{
			if((Controller.Instance == null) || (Controller.Instance.ServoGroups == null))
				return;

			IServo s = p.GetComponent<ModuleIRServo_v3>();

			config = config.AddNode("IRControllerData");

			int Count = 0;

			for(int i = 0; i < Controller.Instance.ServoGroups.Count; i++)
			{
				if(((ServoGroup)Controller.Instance.ServoGroups[i]).Contains(s))
				{
					ConfigNode groupNode = config.AddNode("Group" + Count);

					groupNode.AddValue("Name", Controller.Instance.ServoGroups[i].Name);
					if(Controller.Instance.ServoGroups[i].ForwardKey.Length > 0)
						groupNode.AddValue("ForwardKey", Controller.Instance.ServoGroups[i].ForwardKey);
					if(Controller.Instance.ServoGroups[i].ReverseKey.Length > 0)
						groupNode.AddValue("ReverseKey", Controller.Instance.ServoGroups[i].ReverseKey);
					groupNode.AddValue("GroupSpeedFactor", Controller.Instance.ServoGroups[i].GroupSpeedFactor);

					++Count;
				}
			}

			config.AddValue("Groups", Count);
		}

		public static void OnLoad(ConfigNode config, Part p)
		{
			config = config.GetNode("IRControllerData");

			if(config == null)
				return;

			if(Controller.ServoGroupSettings == null)
				Controller.ServoGroupSettings = new List<ServoGroup.Settings>();

			int Count = int.Parse(config.GetValue("Groups"));

			for(int i = 0; i < Count; i++)
			{
				ConfigNode groupNode = config.GetNode("Group" + i);

				string name = groupNode.GetValue("Name");

				int j = 0;
				while((j < Controller.ServoGroupSettings.Count)
				   && (Controller.ServoGroupSettings[j].name.CompareTo(name) != 0))
					++j;

				if(j < Controller.ServoGroupSettings.Count)
					Controller.ServoGroupSettings.RemoveAt(j);

				ServoGroup.Settings h = new ServoGroup.Settings();

				h.name = groupNode.GetValue("Name");
				string forwardKey = groupNode.GetValue("ForwardKey");
				h.forwardKey = (forwardKey != null) ? forwardKey : "";
				string reverseKey = groupNode.GetValue("ReverseKey");
				h.reverseKey = (reverseKey != null) ? reverseKey : "";
				h.groupSpeedFactor = float.Parse(groupNode.GetValue("GroupSpeedFactor"));

				Controller.ServoGroupSettings.Add(h);
			}
		}
	}
}
