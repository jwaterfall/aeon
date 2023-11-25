using Godot;
using System;

public partial class Player : CharacterBody3D
{
	private bool Paused = false;
	private float CameraXRotation = 0;

	public override void _Ready()
    {
        if (Configuration.FLYING_ENABLED)
        {
            CollisionShape3D collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
            collisionShape.Disabled = true;
        }

        Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Input(InputEvent @event)
    {
		Node3D Head = GetNode<Node3D>("Head");
		Camera3D Camera = GetNode<Camera3D>("Head/Camera3D");

        if (@event.IsActionPressed("Pause"))
        {
			Paused = !Paused;
			if (Paused)
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
			else
			{
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }

		if (Paused)
		{
			return;
		}

		if (@event is InputEventMouseMotion)
		{
			Head.RotateY(Mathf.DegToRad(-(@event as InputEventMouseMotion).Relative.X * Configuration.MOUSE_SENSITIVITY));

			float DeltaX = (@event as InputEventMouseMotion).Relative.Y * Configuration.MOUSE_SENSITIVITY;
			if (CameraXRotation + DeltaX > -90 && CameraXRotation + DeltaX < 90)
            {
				Camera.RotateX(Mathf.DegToRad(-DeltaX));
				CameraXRotation += DeltaX;
			}
        }
    }

    public override void _PhysicsProcess(double delta)
    {
		Vector3 NewVelocity = Velocity;
        Node3D Head = GetNode<Node3D>("Head");

        if (Paused)
		{
			return;
		}

		if (Configuration.FLYING_ENABLED)
        {
            Vector3 verticalDirection = Vector3.Zero;

            if (Input.IsActionPressed("FlyDown") && !IsOnFloor())
            {
                verticalDirection = Vector3.Down;
            }
            if (Input.IsActionPressed("Jump") && !IsOnCeiling())
            {
                verticalDirection = Vector3.Up;
            }

            if (verticalDirection.Length() != 0)
            {
                NewVelocity.Y = verticalDirection.Y * Configuration.MOVEMENT_SPEED;
            }
            else
            {
                NewVelocity.Y = Mathf.MoveToward(Velocity.Y, 0, Configuration.MOVEMENT_SPEED);
            }
        }
		else
		{
            if (!IsOnFloor())
            {
                NewVelocity.Y -= Configuration.GRAVITY * (float)delta;
            }
            else if (Input.IsActionJustPressed("Jump"))
            {
                NewVelocity.Y = Configuration.JUMP_VELOCITY;
            }
        }

        Basis Basis = Head.GlobalTransform.Basis;
		Vector2 InputDirection = Input.GetVector("Left", "Right", "Forward", "Backward");
		Vector3 Direction = (Basis * new Vector3(InputDirection.X, 0, InputDirection.Y)).Normalized();

		if (Direction.Length() != 0)
		{
            NewVelocity.X = Direction.X * Configuration.MOVEMENT_SPEED;
            NewVelocity.Z = Direction.Z * Configuration.MOVEMENT_SPEED;
        }
		else
		{
            NewVelocity.X = Mathf.MoveToward(Velocity.X, 0, Configuration.MOVEMENT_SPEED);
            NewVelocity.Z = Mathf.MoveToward(Velocity.Z, 0, Configuration.MOVEMENT_SPEED);
        }

		Velocity = NewVelocity;

		MoveAndSlide();
    }
}
