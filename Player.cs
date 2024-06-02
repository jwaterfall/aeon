using Godot;

namespace Aeon
{
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
            Vector3 newVelocity = Velocity;
            Node3D head = GetNode<Node3D>("Head");
            float speed = Configuration.FLYING_ENABLED ? Configuration.FLYING_SPEED : Configuration.MOVEMENT_SPEED;

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
                    newVelocity.Y = verticalDirection.Y * speed;
                }
                else
                {
                    newVelocity.Y = Mathf.MoveToward(Velocity.Y, 0, speed);
                }
            }
            else
            {
                if (!IsOnFloor())
                {
                    newVelocity.Y -= Configuration.GRAVITY * (float)delta;
                }
                else if (Input.IsActionJustPressed("Jump"))
                {
                    newVelocity.Y = Configuration.JUMP_VELOCITY;
                }
            }

            Basis Basis = head.GlobalTransform.Basis;
            Vector2 InputDirection = Input.GetVector("Left", "Right", "Forward", "Backward");
            Vector3 Direction = (Basis * new Vector3(InputDirection.X, 0, InputDirection.Y)).Normalized();

            if (Direction.Length() != 0)
            {
                newVelocity.X = Direction.X * speed;
                newVelocity.Z = Direction.Z * speed;
            }
            else
            {
                newVelocity.X = Mathf.MoveToward(Velocity.X, 0, speed);
                newVelocity.Z = Mathf.MoveToward(Velocity.Z, 0, speed);
            }

            Velocity = newVelocity;

            MoveAndSlide();

            RayCast3D ray = GetNode<RayCast3D>("Head/Camera3D/RayCast3D");
            Node3D blockOutline = GetNode<Node3D>("BlockOutline");

            if (ray.IsColliding())
            {
                var normal = ray.GetCollisionNormal();
                var position = ray.GetCollisionPoint() - normal * 0.5f;
                var worldPosition = (Vector3I)(position.Floor());

                blockOutline.GlobalPosition = worldPosition + (Vector3.One / 2);
                blockOutline.GlobalRotation = Vector3.Zero;
                blockOutline.Visible = true;

                var customSignals = GetNode<CustomSignals>("/root/CustomSignals");

                if (Input.IsActionJustPressed("Break"))
                {
                    customSignals.EmitSignal(nameof(CustomSignals.BreakBlock), worldPosition);
                }
                else if (Input.IsActionJustPressed("Place"))
                {
                    customSignals.EmitSignal(nameof(CustomSignals.PlaceBlock), worldPosition + normal, "stone_slope");
                }
            }
            else
            {
                blockOutline.Visible = false;
            }
        }
    }
}

