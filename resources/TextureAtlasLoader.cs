using Godot;
using System.Collections.Generic;
using System.IO;

namespace Aeon
{
    public class TextureAtlasLoader
    {
        protected string directory;
        protected string extension;
        public bool loaded = false;
        public Vector2I size;
        public StandardMaterial3D material = new();
        public StandardMaterial3D transparentMaterial = new();
        private Dictionary<string, Image> textures = new();
        private Dictionary<string, Vector2I> offsets = new();
        private readonly int textureSize;

        public TextureAtlasLoader(int textureSize, string directory, string extension = ".png")
        {
            this.textureSize = textureSize;
            this.directory = directory;
            this.extension = extension;
        }

        public Dictionary<string, Vector2I> Load()
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                if (Path.GetExtension(file) != extension)
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(file);
                LoadFile(name);
            }

            size = CalculateAtlasSize(textures.Count);
            Image atlasImage = Image.Create(size.X * textureSize, size.Y * textureSize, false, Image.Format.Rgba8);

            int currentBlockIndex = 0;
            foreach (var name in textures.Keys)
            {
                var image = textures[name];

                var offset = new Vector2I(currentBlockIndex % size.X, currentBlockIndex / size.X);
                offsets.Add(name, offset);

                Rect2I sourceRect = new(Vector2I.Zero, image.GetSize());
                Vector2I destRect = new(offset.X * Configuration.TEXTURE_SIZE, offset.Y * Configuration.TEXTURE_SIZE);

                atlasImage.BlitRect(image, sourceRect, destRect);

                currentBlockIndex++;
            }

            var texture = ImageTexture.CreateFromImage(atlasImage);

            AtlasTexture textureAtlasTexture = new()
            {
                Atlas = texture
            };

            material.AlbedoTexture = textureAtlasTexture;
            material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;

            transparentMaterial.AlbedoTexture = textureAtlasTexture;
            transparentMaterial.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            transparentMaterial.Transparency = BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass;

            loaded = true;

            return offsets;
        }

        protected void LoadFile(string name)
        {
            Image image = new();
            image.Load($"{directory}/{name}{extension}");
            image.Convert(Image.Format.Rgba8);
            textures.Add(name, image);
        }

        public static Vector2I CalculateAtlasSize(int count)
        {
            var sqrt = Mathf.Sqrt(count);
            var rounded = Mathf.CeilToInt(sqrt);

            return new Vector2I(rounded, rounded);
        }
    }
}