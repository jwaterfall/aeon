using Godot;
using System;

public partial class Configuration : Node
{
	public static readonly Vector3I CHUNK_DIMENSION = new(16, 16, 16);
	public static readonly Vector2I TEXTURE_ATLAS_SIZE = new(8, 2);
	public static readonly float MOUSE_SENSITIVITY = 0.3f;
	public static readonly float MOVEMENT_SPEED = 5;
	public static readonly float GRAVITY = 20;
	public static readonly float JUMP_VELOCITY = 6.25f;
	public static readonly float CHUNK_UNLOAD_TIME = 10;
	public static readonly bool FLYING_ENABLED = false;
	public static readonly int CHUNK_LOAD_RADIUS = 10;
    public static readonly int VERTICAL_CHUNK_LOAD_RADIUS = 5;
}
