namespace InfernalRobotics_v3.Interfaces
{
	public interface IIKModule
	{
		void Reset();
		
		bool SelectActiveGroup(Interfaces.IServoGroup g);

		void SetLimiter(Interfaces.IServoGroup g, bool active);
		bool GetLimiter(Interfaces.IServoGroup g);

		void SetDirectMode(Interfaces.IServoGroup g, bool active);
		bool GetDirectMode(Interfaces.IServoGroup g);

		void Relax(Interfaces.IServoGroup g, int factor);

		void SelectEndEffector(Interfaces.IServoGroup g);

		void SetShowPosition(Interfaces.IServoGroup g, bool show);
		bool GetShowPosition(Interfaces.IServoGroup g);

		void SelectTarget(Interfaces.IServoGroup g);

		void SetTarget(Interfaces.IServoGroup g, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation);
		void GetTarget(Interfaces.IServoGroup g, out UnityEngine.Vector3 position, out UnityEngine.Quaternion rotation);

		void Action1(Interfaces.IServoGroup g);
		void Action2(Interfaces.IServoGroup g);
	}
}
