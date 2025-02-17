using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using InfernalRobotics_v3.Interfaces;
using InfernalRobotics_v3.Interceptors;
using InfernalRobotics_v3.Servo;
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

		public static void MoveServo(IServoGroup from, IServoGroup to, int index, IServo servo)
		{
			((ServoGroup)from.group).RemoveControl(servo);
			((ServoGroup)to.group).AddControl(servo, index);
		}

		private static void EditorAddServo(IServo servo)
		{
			if(!Instance)
				return;
			
			if(Instance.servosState == null)
				Instance.servosState = new Dictionary<IServo, IServoState>();

			Instance.servosState.Add(servo, new IServoState());

			if(Instance.ServoGroups == null)
				Instance.ServoGroups = new List<IServoGroup>();

			if(!string.IsNullOrEmpty(servo.GroupName))
			{
				List<string> groups = new List<string>(servo.GroupName.Split('|'));

				foreach(ServoGroup cg in Instance.ServoGroups)
				{
					if(groups.Contains(cg.Name))
					{
						cg.AddControl(servo, -1); // FEHLER, Index noch irgendwie behalten...
						groups.Remove(cg.Name);
					}
				}

				while(groups.Count > 0)
				{
					Instance.ServoGroups.Add(new ServoGroup(servo, groups[0]));
					groups.RemoveAt(0);
				}
			}

			Logger.Log("[ServoController] AddServo finished successfully", Logger.Level.Debug);

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

			if(!string.IsNullOrEmpty(servo.GroupName))
			{
				string GroupName = servo.GroupName;

				List<string> groups = new List<string>(servo.GroupName.Split('|'));

				for(int i = 0; i < Instance.ServoGroups.Count; i++)
				{
					if(groups.Contains(Instance.ServoGroups[i].Name))
					{
						((ServoGroup)Instance.ServoGroups[i].group).RemoveControl(servo);
					}
				}

				servo.GroupName = GroupName; // restore (RemoveControl removes the GroupNames from this value, but that's not what we want when detaching the servo)
			}

			if(Gui.WindowManager.Instance)
				Gui.WindowManager.Instance.Invalidate();

			if(Gui.IRBuildAid.IRBuildAidManager.Instance)
				Gui.IRBuildAid.IRBuildAidManager.Instance.HideServoRange(servo);

			Logger.Log("[ServoController] AddServo finished successfully", Logger.Level.Debug);
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

			var groupServos = new Dictionary<string, List<IServo>>();

			foreach(Part p in EditorLogic.fetch.ship.Parts)
			{
				foreach(var servo in p.ToServos())
				{
					servosState.Add(servo, new IServoState());

					if(!string.IsNullOrEmpty(servo.GroupName))
					{
						List<string> groups = new List<string>(servo.GroupName.Split('|'));

						foreach(string group in groups)
						{
							List<IServo> servos;
							if(!groupServos.TryGetValue(group, out servos))
							{
								servos = new List<IServo>();
								groupServos.Add(group, servos);
							}

							servos.Add(servo);
						}
					}
				}
			}

			foreach(var kv in groupServos)
			{
				ServoGroup g = new ServoGroup((string)kv.Key);
				ServoGroups.Add(g);

				foreach(IServo s in kv.Value)
					g.AddControl(s, -1);
			}

			if(ServoGroups.Count == 0)
				ServoGroups = null;

			if((_IKServoGroup != null) && !ServoGroups.Contains(_IKServoGroup))
				_IKServoGroup = null;

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

			Logger.Log ("OnEditorStarted called", Logger.Level.Debug);
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
			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate(); // would most likely also work with just one WaitForFixedUpdate but it doesn't hurt to wait longer

			bRebuildingServoGroupsFlight = false;

			Dictionary<IServo, IServoState> oldServoState = (Instance.servosState != null) ? Instance.servosState : new Dictionary<IServo, IServoState>();
			servosState = new Dictionary<IServo, IServoState>();

			List<IServoGroup> oldServoGroups = (ServoGroups != null) ? ServoGroups : new List<IServoGroup>();
			ServoGroups = new List<IServoGroup>();

			for(int i = 0; i < FlightGlobals.Vessels.Count; i++)
			{
				var vessel = FlightGlobals.Vessels[i];

				if(!vessel.loaded)
					continue;

				var groupServos = new Dictionary<string, List<IServo>>();

				foreach(var servo in vessel.ToServos())
				{
					IServoState state;
					if(oldServoState.TryGetValue(servo, out state))
						servosState.Add(servo, state);
					else
						servosState.Add(servo, new IServoState());

					if(!string.IsNullOrEmpty(servo.GroupName))
					{
						List<string> groups = new List<string>(servo.GroupName.Split('|'));

						foreach(string group in groups)
						{
							List<IServo> servos;
							if(!groupServos.TryGetValue(group, out servos))
							{
								servos = new List<IServo>();
								groupServos.Add(group, servos);
							}

							servos.Add(servo);
						}
					}
				}

				// re-use existing groups

				for(int j = 0; j < oldServoGroups.Count; j++)
				{
//					if(oldServoGroups[j].Vessel == vessel)
// FEHLER, das hier ignorieren wir, weil es nicht wirklich passt -> am Anfang ist vessel == null für Gruppen, weil die nicht gesetzt sind im Part...
					{
						ServoGroup g = (ServoGroup)oldServoGroups[j];

						List<IServo> servosNew;
						if(groupServos.TryGetValue(g.Name, out servosNew))
						{
// FEHLER, Murks-Bugfix? mal sehen wegen Identifikation später dann...
g.Vessel = vessel;

							HashSet<IServo> servosNotToAdd = new HashSet<IServo>();
							HashSet<IServo> servosToRemove = new HashSet<IServo>();

							foreach(IServo s in g.Servos)
							{
								if(servosNew.Contains(s))
									servosNotToAdd.Add(s);
								else
									servosToRemove.Add(s);
							}

							foreach(IServo s in servosToRemove)
								g.RemoveControl(s);

							foreach(IServo s in servosNew)
							{
								if(!servosNotToAdd.Contains(s))
									g.AddControl(s, -1);
							}

							ServoGroups.Add(g);

							groupServos.Remove(g.Name);
						}

						oldServoGroups.RemoveAt(j--);
					}
				}

				// add new groups

				foreach(var kv in groupServos)
				{
					ServoGroup g = new ServoGroup(vessel, (string)kv.Key);
					ServoGroups.Add(g);

					foreach(IServo s in kv.Value)
						g.AddControl(s, -1);
				}
			}

			if(ServoGroups.Count == 0)
				ServoGroups = null;

			if((_IKServoGroup != null) && !ServoGroups.Contains(_IKServoGroup))
				_IKServoGroup = null;

			if(Gui.WindowManager.Instance != null)
				Gui.WindowManager.Instance.Invalidate();
		}

		private void OnEditorPartAttach(Part part)
		{
			foreach(var p in part.GetChildServos())
				EditorAddServo(p);

			Logger.Log("[ServoController] OnPartAttach finished successfully", Logger.Level.Debug);
		}

		private void OnEditorPartRemove(Part part)
		{
			foreach(var p in part.GetChildServos())
				EditorRemoveServo(p);

			Logger.Log("[ServoController] OnPartRemove finished successfully", Logger.Level.Debug);
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
			Logger.Log(string.Format("[ServoController] vessel {0}", v.name));

			RebuildServoGroupsFlight();

			Logger.Log("[ServoController] OnVesselChange finished successfully", Logger.Level.Debug);
		}

		private void OnVesselWasModified(Vessel v)
		{
			RebuildServoGroupsFlight();
		}

		private void OnVesselLoaded(Vessel v)
		{
			Logger.Log("[ServoController] OnVesselLoaded, v=" + v.GetName(), Logger.Level.Debug);
			RebuildServoGroupsFlight();
		}

		private void OnVesselUnloaded(Vessel v)
		{
			Logger.Log("[ServoController] OnVesselUnloaded, v=" + v.GetName(), Logger.Level.Debug);
			RebuildServoGroupsFlight();
		}

		private void Awake()
		{
			Logger.Log("[ServoController] awake, AddonName = " + this.AddonName);

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

			Logger.Log("[ServoController] awake finished successfully, AddonName = " + this.AddonName, Logger.Level.Debug);
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
			Logger.Log("[ServoController] destroy", Logger.Level.Debug);

			GameEvents.onVesselChange.Remove(OnVesselChange);
			GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
			GameEvents.onVesselLoaded.Remove(OnVesselLoaded);
			GameEvents.onVesselDestroy.Remove(OnVesselUnloaded);
			GameEvents.onVesselGoOnRails.Remove(OnVesselUnloaded);

			GameEvents.onEditorStarted.Remove(OnEditorStarted);
			GameEvents.onEditorPartEvent.Remove(OnEditorPartEvent);
			GameEvents.onEditorUndo.Remove(OnEditorUnOrRedo);
			GameEvents.onEditorRedo.Remove(OnEditorUnOrRedo);

			Logger.Log("[ServoController] OnDestroy finished successfully", Logger.Level.Debug);
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

			IServoInterceptor interceptor = new IServoInterceptor(servo);

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

			IServoGroupInterceptor interceptor = new IServoGroupInterceptor(group);

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

			if(Controller.Instance.ServoGroups == null)
				Controller.Instance.ServoGroups = new List<IServoGroup>();

			int Count = int.Parse(config.GetValue("Groups"));

			for(int i = 0; i < Count; i++)
			{
				ConfigNode groupNode = config.GetNode("Group" + i);

				string name = groupNode.GetValue("Name");

// FEHLER, später das Zeug anders identifizieren, weil es so Kollisionen geben kann, wenn mehrere Schiffe den gleichen Namen verwenden -> und das kommt dann echt nicht gut...
				int j = 0;
				while((j < Controller.Instance.ServoGroups.Count)
				   && (Controller.Instance.ServoGroups[j].Name.CompareTo(name) != 0))
					++j;

				if(j < Controller.Instance.ServoGroups.Count)
					continue; // already found

				ServoGroup g;

				if((p != null) && (p.vessel != null))
					g = new ServoGroup(p.vessel, groupNode.GetValue("Name"));
				else
					g = new ServoGroup(groupNode.GetValue("Name"));

				string forwardKey = groupNode.GetValue("ForwardKey");
				if(forwardKey != null)
					g.ForwardKey = forwardKey;
				string reverseKey = groupNode.GetValue("ReverseKey");
				if(reverseKey != null)
					g.ReverseKey = reverseKey;
				g.GroupSpeedFactor = float.Parse(groupNode.GetValue("GroupSpeedFactor"));

				Controller.Instance.ServoGroups.Add(g);
			}
		}
	}
}
