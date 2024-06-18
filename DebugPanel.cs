using Godot;

namespace Aeon
{
    public partial class DebugPanel : PanelContainer
    {
        private readonly float _updateFrequency = 0.5f;
        private float _updateTimer = 0;
        private VBoxContainer _propertyContainer;

        public override void _Ready()
        {
            _propertyContainer = GetNode<VBoxContainer>("MarginContainer/VBoxContainer");
            Visible = false;
        }

        public override void _Process(double delta)
        {
            _updateTimer += (float)delta;

            if (_updateTimer < _updateFrequency)
            {
                return;
            }

            var player = GetNode<Player>("../../");
            var world = GetNode<World>("../../../");
            var blockLookingAt = player.GetBlockLookingAt();

            SetDebugProperty("FPS", Engine.GetFramesPerSecond());
            SetDebugProperty("X", player.Position.X);
            SetDebugProperty("Y", player.Position.Y);
            SetDebugProperty("Z", player.Position.Z);
            SetDebugProperty("Chunk", (player.Position / Configuration.CHUNK_DIMENSION).Floor());
            SetDebugProperty("Looking at", blockLookingAt.HasValue ? $"{blockLookingAt} ({world.GetBlock(blockLookingAt.Value).Name})" : "-");
            SetDebugProperty("Generation time", world.AverageGenerationTime);
            SetDebugProperty("Render time", world.AverageRenderTime);

            _updateTimer = 0;
        }

        public override void _Input(InputEvent @event)
        {
            if (@event.IsActionPressed("DebugMenu"))
            {
                Visible = !Visible;
            }
        }

        public void SetDebugProperty(string name, object value)
        {
            var text = $"{name}: {value ?? "-"}";

            if (_propertyContainer.HasNode(name))
            {
                var node = _propertyContainer.GetNode<Label>(name);
                node.Text = text;
            }
            else
            {
                var label = new Label
                {
                    Name = name,
                    Text = text,
                };

                _propertyContainer.AddChild(label);
            }
        }
    }
}