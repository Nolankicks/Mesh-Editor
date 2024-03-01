using Sandbox;

public sealed class CameraInput : Component
{
	public Vector3 Move;
	public Angles EyeAngles;
	protected override void OnUpdate()
	{
		//Get Mouse Input from the player
		EyeAngles += Input.AnalogLook;
		//Clamp the pitch
		EyeAngles.pitch = EyeAngles.pitch.Clamp(-89, 89);
		//Set the rotation of the camera to the EyeAngles
		Transform.Rotation = new Angles(EyeAngles.pitch, EyeAngles.yaw, 0);

		//Move the player based on the EyeAngles
		 Move = new Angles(EyeAngles.pitch, EyeAngles.yaw, 0).ToRotation() * Input.AnalogMove;
		 GameObject.Transform.Position += Move;
	}
}
