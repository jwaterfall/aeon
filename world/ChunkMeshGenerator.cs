using Godot;
using System.Collections.Generic;

namespace Aeon
{
    public class ChunkMeshGenerator
    {
        private static readonly Dictionary<Direction, Vector3I> FaceDirections = new()
        {
            { Direction.Up, Vector3I.Up },
            { Direction.Down, Vector3I.Down },
            { Direction.North, Vector3I.Left },
            { Direction.South, Vector3I.Right },
            { Direction.West, Vector3I.Forward },
            { Direction.East, Vector3I.Back }
        };

        private static readonly Dictionary<Direction, Direction> InverseDirections = new()
        {
            { Direction.Up, Direction.Down },
            { Direction.Down, Direction.Up },
            { Direction.North, Direction.South },
            { Direction.South, Direction.North },
            { Direction.West, Direction.East },
            { Direction.East, Direction.West }
        };

        private SurfaceTool _surfaceTool;
        private SurfaceTool _transparentSurfaceTool;
        private SurfaceTool _collisionSurfaceTool;
        private Chunk _chunk;
        private ChunkManager _chunkManager;

        public ArrayMesh Mesh { get; private set; }
        public ArrayMesh TransparentMesh { get; private set; }
        public Shape3D CollisionShape { get; private set; }
        public ShaderMaterial Material { get; private set; } = new();
        public StandardMaterial3D TransparentMaterial { get; private set; } = new();

        public ChunkMeshGenerator(Chunk chunk, ChunkManager chunkManager)
        {
            Material.Shader = GD.Load<Shader>("res://shaders/Lighting.gdshader");
            Material.SetShaderParameter("chunk_position", chunk.ChunkPosition);
            Material.SetShaderParameter("chunk_dimensions", chunk.Dimensions);
            Material.SetShaderParameter("texture_sampler", BlockTextures.Instance.TextureAtlasTexture);

            TransparentMaterial.AlbedoTexture = BlockTextures.Instance.TextureAtlasTexture;
            TransparentMaterial.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            TransparentMaterial.Transparency = BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass;
            
            _chunk = chunk;
            _chunkManager = chunkManager;
        }

        private void InitializeSurfaceTools()
        {
            _surfaceTool = new SurfaceTool();
            _surfaceTool.Begin(Godot.Mesh.PrimitiveType.Triangles);
            _surfaceTool.SetSmoothGroup(uint.MaxValue);

            _transparentSurfaceTool = new SurfaceTool();
            _transparentSurfaceTool.Begin(Godot.Mesh.PrimitiveType.Triangles);
            _transparentSurfaceTool.SetSmoothGroup(uint.MaxValue);

            _collisionSurfaceTool = new SurfaceTool();
            _collisionSurfaceTool.Begin(Godot.Mesh.PrimitiveType.Triangles);
            _collisionSurfaceTool.SetSmoothGroup(uint.MaxValue);
        }

        private void CommitSurfaceTools()
        {
            _surfaceTool.GenerateNormals(false);
            Mesh = _surfaceTool.Commit();

            _transparentSurfaceTool.GenerateNormals(false);
            TransparentMesh = _transparentSurfaceTool.Commit();

            _collisionSurfaceTool.GenerateNormals(false);
            CollisionShape = _collisionSurfaceTool.Commit().CreateTrimeshShape();

            _surfaceTool.Dispose();
            _transparentSurfaceTool.Dispose();
            _collisionSurfaceTool.Dispose();

            _surfaceTool = null;
            _transparentSurfaceTool = null;
            _collisionSurfaceTool = null;
        }

        private bool IsVisible(Vector3I localPosition, Direction faceToCheck, BlockType sourceBlockType)
        {
            var blockType = !_chunk.IsInChunk(localPosition)
                ? _chunkManager.GetBlock(_chunk.GetWorldPosition(localPosition))
                : _chunk.GetBlock(localPosition);

            return blockType.Occludes.Contains(faceToCheck) == false || (blockType.Transparent && (blockType != sourceBlockType || !blockType.CullsSelf));
        }

        public void Generate()
        {
            InitializeSurfaceTools();

            for (int x = 0; x < _chunk.Dimensions.X; x++)
            {
                for (int y = 0; y < _chunk.Dimensions.Y; y++)
                {
                    for (int z = 0; z < _chunk.Dimensions.Z; z++)
                    {
                        GenerateBlock(new Vector3I(x, y, z));
                    }
                }
            }

            CommitSurfaceTools();
        }

        private void GenerateBlock(Vector3I localPosition)
        {
            var blockType = _chunk.GetBlock(localPosition);

            if (blockType.Name == "air") return;

            foreach (var face in blockType.Faces)
            {
                if (face.OccludedBy.HasValue)
                {
                    var directionOfBlockToCheck = FaceDirections[face.OccludedBy.Value];
                    var faceToCheck = InverseDirections[face.OccludedBy.Value];

                    if (!IsVisible(localPosition + directionOfBlockToCheck, faceToCheck, blockType))
                    {
                        continue;
                    }
                }

                GenerateFace(face, localPosition, blockType);
            }
        }

        private Vector3 CalculateFaceNormal(List<Vector3> vertices)
        {
            var v1 = vertices[1] - vertices[0];
            var v2 = vertices[2] - vertices[0];
            return v2.Cross(v1).Normalized();
        }

        private void GenerateFace(Face face, Vector3I localPosition, BlockType blockType)
        {
            var uvOffset = face.TextureAtlasOffset / BlockTextures.Instance.size;
            var height = 1.0f / BlockTextures.Instance.size.Y;
            var width = 1.0f / BlockTextures.Instance.size.X;

            var uva = uvOffset + new Vector2(face.UV[0] * width, face.UV[1] * height);
            var uvb = uvOffset + new Vector2(face.UV[0] * width, face.UV[3] * height);
            var uvc = uvOffset + new Vector2(face.UV[2] * width, face.UV[3] * height);

            var st = blockType.Transparent ? _transparentSurfaceTool : _surfaceTool;

            var a = face.Vertices[0] + localPosition;
            var b = face.Vertices[1] + localPosition;
            var c = face.Vertices[2] + localPosition;

            var normal = CalculateFaceNormal(face.Vertices);

            var neighboringBlockPosition = localPosition + (Vector3I)normal.Floor();
            var lightLevel = _chunkManager.GetLightLevel(_chunk.GetWorldPosition(neighboringBlockPosition));
            var color = new Color(lightLevel.X / 15.0f, lightLevel.Y / 15.0f, lightLevel.Z / 15.0f);

            st.AddTriangleFan(new[] { a, b, c }, new[] { uva, uvb, uvc }, new Color[3] { color, color, color });

            if (blockType.HasCollision)
            {
                _collisionSurfaceTool.AddTriangleFan(new[] { a, b, c }, new[] { uva, uvb, uvc });
            }

            if (face.Vertices.Count == 4)
            {
                var uvd = uvOffset + new Vector2(face.UV[2] * width, face.UV[1] * height);
                var d = face.Vertices[3] + localPosition;

                st.AddTriangleFan(new[] { a, c, d }, new[] { uva, uvc, uvd }, new Color[3] { color, color, color }); 

                if (blockType.HasCollision)
                {
                    _collisionSurfaceTool.AddTriangleFan(new[] { a, c, d }, new[] { uva, uvc, uvd });
                }
            }
        }
    }
}
