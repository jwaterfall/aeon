using Godot;
using System.Collections.Generic;
using System.Linq;

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
        public ShaderMaterial TransparentMaterial { get; private set; } = new();

        public ChunkMeshGenerator(Chunk chunk, ChunkManager chunkManager)
        {
            Material.Shader = GD.Load<Shader>("res://shaders/Standard.gdshader");
            Material.SetShaderParameter("texture_sampler", BlockTextures.Instance.TextureAtlasTexture);

            TransparentMaterial.Shader = GD.Load<Shader>("res://shaders/Transparent.gdshader");
            TransparentMaterial.SetShaderParameter("texture_sampler", BlockTextures.Instance.TextureAtlasTexture);

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

        private bool IsVisible(Vector3I localPosition, Direction faceToCheck, Block sourceBlockType)
        {
            if (
                _chunk.ChunkPosition.Y == 0 && localPosition.Y < 0 ||
                _chunk.ChunkPosition.Y == Configuration.VERTICAL_CHUNKS - 1 && localPosition.Y >= Configuration.CHUNK_DIMENSION.Y
                )
            {
                return true;
            }

            var blockType = _chunkManager.GetBlock(_chunk.GetWorldPosition(localPosition));

            return blockType.Occludes.Contains(faceToCheck) == false || blockType.Transparent && (blockType != sourceBlockType || !blockType.CullsSelf);
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

        private Vector3I[] GetBlockPositionsSorroundingVertex(Vector3I localPosition, Vector3I vertex)
        {
            var sorroundingBlocks = new Vector3I[8]
            {
                localPosition + new Vector3I(0, 0, 0) + vertex,
                localPosition + new Vector3I(0, 0, -1) + vertex,
                localPosition + new Vector3I(-1, 0, 0) + vertex,
                localPosition + new Vector3I(-1, 0, -1) + vertex,
                localPosition + new Vector3I(0, -1, 0) + vertex,
                localPosition + new Vector3I(0, -1, -1) + vertex,
                localPosition + new Vector3I(-1, -1, 0) + vertex,
                localPosition + new Vector3I(-1, -1, -1) + vertex
             };

            return sorroundingBlocks;
        }

        private Color GetVertexColor(Vector3I localPosition, Vector3 vertex)
        {
            // If vertex is not the corner of the block, return the block color
            var blockVertexes = new Vector3I[8]
            {
                new Vector3I(0, 0, 0),
                new Vector3I(0, 0, 1),
                new Vector3I(1, 0, 0),
                new Vector3I(1, 0, 1),
                new Vector3I(0, 1, 0),
                new Vector3I(0, 1, 1),
                new Vector3I(1, 1, 0),
                new Vector3I(1, 1, 1)
            };

            if (!blockVertexes.Any(v => v == vertex))
            {
                return new Color(0, 0, 0);
            }

            var sorroundingBlocks = GetBlockPositionsSorroundingVertex(localPosition, (Vector3I)vertex);
            var lightLevel = new Vector3(0, 0, 0);

            var count = 0;

            foreach (var blockPosition in sorroundingBlocks)
            {
                var light = _chunkManager.GetBlockLightLevel(_chunk.GetWorldPosition(blockPosition));
                var skyLight = _chunkManager.GetSkyLightLevel(_chunk.GetWorldPosition(blockPosition));

                Vector3I combinedLight = new(skyLight, skyLight, skyLight);

                if (combinedLight == Vector3.Zero) continue;

                lightLevel += combinedLight;
                count++;
            }

            lightLevel /= count;

            return new Color(lightLevel.X / 15.0f, lightLevel.Y / 15.0f, lightLevel.Z / 15.0f);
        }

        private void GenerateFace(Face face, Vector3I localPosition, Block blockType)
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

            var aColor = GetVertexColor(localPosition, face.Vertices[0]);
            var bColor = GetVertexColor(localPosition, face.Vertices[1]);
            var cColor = GetVertexColor(localPosition, face.Vertices[2]);

            st.AddTriangleFan(new[] { a, b, c }, new[] { uva, uvb, uvc }, new Color[3] { aColor, bColor, cColor });

            if (blockType.HasCollision)
            {
                _collisionSurfaceTool.AddTriangleFan(new[] { a, b, c }, new[] { uva, uvb, uvc });
            }

            if (face.Vertices.Count == 4)
            {
                var uvd = uvOffset + new Vector2(face.UV[2] * width, face.UV[1] * height);
                var d = face.Vertices[3] + localPosition;
                var dColor = GetVertexColor(localPosition, face.Vertices[3]);

                st.AddTriangleFan(new[] { a, c, d }, new[] { uva, uvc, uvd }, new Color[3] { aColor, cColor, dColor });

                if (blockType.HasCollision)
                {
                    _collisionSurfaceTool.AddTriangleFan(new[] { a, c, d }, new[] { uva, uvc, uvd });
                }
            }
        }
    }
}
