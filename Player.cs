using Godot;
using System;

namespace Aeon
{
    public partial class Player : CharacterBody3D
    {
        private bool _paused = false;
        private float _cameraXRotation = 0;
        private Nullable<Vector3I> _blockLookingAt;

        public override void _Ready()
        {
            if (Configuration.FLYING_ENABLED)
            {
                var collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
                collisionShape.Disabled = true;
            }

            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        public override void _Input(InputEvent @event)
        {
            var head = GetNode<Node3D>("Head");
            var camera = GetNode<Camera3D>("Head/Camera3D");

            if (@event.IsActionPressed("Pause"))
            {
                _paused = !_paused;
                if (_paused)
                {
                    Input.MouseMode = Input.MouseModeEnum.Visible;
                }
                else
                {
                    Input.MouseMode = Input.MouseModeEnum.Captured;
                }
            }

            if (_paused)
            {
                return;
            }

            if (@event is InputEventMouseMotion)
            {
                head.RotateY(Mathf.DegToRad(-(@event as InputEventMouseMotion).Relative.X * Configuration.MOUSE_SENSITIVITY));

                var deltaX = (@event as InputEventMouseMotion).Relative.Y * Configuration.MOUSE_SENSITIVITY;
                if (_cameraXRotation + deltaX > -90 && _cameraXRotation + deltaX < 90)
                {
                    camera.RotateX(Mathf.DegToRad(-deltaX));
                    _cameraXRotation += deltaX;
                }
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            var newVelocity = Velocity;

            if (_paused)
            {
                return;
            }

            var head = GetNode<Node3D>("Head");
            var speed = Configuration.FLYING_ENABLED ? Configuration.FLYING_SPEED : Configuration.MOVEMENT_SPEED;

            if (Configuration.FLYING_ENABLED)
            {
                var verticalDirection = Vector3.Zero;

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

            var basis = head.GlobalTransform.Basis;
            var inputDirection = Input.GetVector("Left", "Right", "Forward", "Backward");
            var direction = (basis * new Vector3(inputDirection.X, 0, inputDirection.Y)).Normalized();

            if (direction.Length() != 0)
            {
                newVelocity.X = direction.X * speed;
                newVelocity.Z = direction.Z * speed;
            }
            else
            {
                newVelocity.X = Mathf.MoveToward(Velocity.X, 0, speed);
                newVelocity.Z = Mathf.MoveToward(Velocity.Z, 0, speed);
            }

            Velocity = newVelocity;

            MoveAndSlide();

            var ray = GetNode<RayCast3D>("Head/Camera3D/RayCast3D");
            var blockOutline = GetNode<Node3D>("BlockOutline");

            if (ray.IsColliding())
            {
                var normal = ray.GetCollisionNormal();
                var position = ray.GetCollisionPoint() - normal * 0.5f;
                var worldPosition = (Vector3I)(position.Floor());

                _blockLookingAt = worldPosition;

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
                    var choices = new string[] { "red_glowstone", "green_glowstone", "blue_glowstone" };
                    var random = new System.Random();
                    var choice = choices[random.Next(choices.Length)];
                    customSignals.EmitSignal(nameof(CustomSignals.PlaceBlock), worldPosition + normal, choice);
                }
            }
            else
            {
                blockOutline.Visible = false;
                _blockLookingAt = null;
            }
        }

        public Nullable<Vector3I> GetBlockLookingAt()
        {
            return _blockLookingAt;
        }
    }
}

