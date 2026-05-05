namespace InfernalRobotics_v3.Servo
{
	class DummyLimiter : Interfaces.ILimiter
	{
		public bool SetCommand(ref float p_TargetPosition, ref float p_TargetSpeed, ref float p_Acceleration)
		{
			return false;
		}
	}
}
