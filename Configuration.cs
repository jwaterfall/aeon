using Godot;
using System;

public partial class Configuration : Node
{
	public static readonly Vector3 CHUNK_DIMENSION = new(16, 32, 16);
	public static readonly Vector2 TEXTURE_ATLAS_SIZE = new(8, 2);
	public static readonly float MOUSE_SENSITIVITY = 0.3f;
	public static readonly float MOVEMENT_SPEED = 10;
	public static readonly float GRAVITY = 20;
	public static readonly float JUMP_VELOCITY = 10;
	public static readonly float CHUNK_LOAD_RADIUS = 5;
}
