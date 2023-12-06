using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using YamlDotNet.Serialization;

public class TextureAtlasLoader:FileLoader
{
    public Vector2I textureAtlasSize;
    public StandardMaterial3D textureAtlasMaterial = new();
    private Dictionary<string, Image> textures = new();
    private Dictionary<string, Vector2I> textureAtlasOffsets = new();
    private int textureSize;

    public TextureAtlasLoader(int textureSize, string directory, string extension = ".png") : base(directory, extension)
    {
        this.textureSize = textureSize;
    }

    public new Dictionary<string, Vector2I> Load()
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

        textureAtlasSize = CalculateAtlasSize(textures.Count);
        Image atlasImage = Image.Create(textureAtlasSize.X * textureSize, textureAtlasSize.Y * textureSize, false, Image.Format.Rgb8);

        int currentBlockIndex = 0;
        foreach (var name in textures.Keys)
        {
            var image = textures[name];

            var offset = new Vector2I(currentBlockIndex % textureAtlasSize.X, currentBlockIndex / textureAtlasSize.X);
            textureAtlasOffsets.Add(name, offset);

            Rect2I sourceRect = new Rect2I(Vector2I.Zero, image.GetSize());
            Vector2I destRect = new Vector2I(offset.X * Configuration.TEXTURE_SIZE, offset.Y * Configuration.TEXTURE_SIZE);

            atlasImage.BlitRect(image, sourceRect, destRect);

            currentBlockIndex++;
        }

        var texture = ImageTexture.CreateFromImage(atlasImage);

        AtlasTexture textureAtlasTexture = new();
        textureAtlasTexture.Atlas = texture;

        textureAtlasMaterial.AlbedoTexture = textureAtlasTexture;
        textureAtlasMaterial.TextureFilter = StandardMaterial3D.TextureFilterEnum.Nearest;

        loaded = true;

        return textureAtlasOffsets;
    }

    protected override void LoadFile(string name)
    {
        Image image = new();
        image.Load($"{directory}/{name}{extension}");
        GD.Print(name, image.GetFormat());
        textures.Add(name, image);
    }

    public static Vector2I CalculateAtlasSize(int count)
    {
        var sqrt = Mathf.Sqrt(count);
        var rounded = Mathf.CeilToInt(sqrt);

        return new Vector2I(rounded, rounded);
    }
}
