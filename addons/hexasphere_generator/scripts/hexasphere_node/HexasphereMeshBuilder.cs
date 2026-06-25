using Godot;
using Godot.Hexasphere;
using System.Collections.Generic;
using System.Diagnostics;

public class HexasphereMeshBuilder
{
    public (ArrayMesh mesh, List<int[]> tileVertexIndices) Build(Hexasphere hexasphere)
    {
        var tiles = hexasphere.Tiles;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        var tileVertexIndices = new List<int[]>(tiles.Count);
        int globalVertexIndex = 0;

        for (int tileIdx = 0; tileIdx < tiles.Count; tileIdx++)
        {
            var tile       = tiles[tileIdx];
            int facesCount = tile.Faces.Count;
            int pointsCount = tile.Points.Count;

            int[] currentTileVertices = new int[facesCount * 3];
            int   localVertexCounter  = 0;

            var godotVertices = new Godot.Vector3[pointsCount];
            for (int i = 0; i < pointsCount; i++)
            {
                var p = tile.Points[i];
                godotVertices[i] = new Godot.Vector3(
                    (float)p.Position.X,
                    (float)p.Position.Y,
                    (float)p.Position.Z);
            }

            var pointIndexMap = new Dictionary<int, int>(pointsCount);
            for (int i = 0; i < pointsCount; i++)
                pointIndexMap[tile.Points[i].ID] = i;

            // Индекс тайла кодируем в UV2.x — float, но значения целые
            var tileUV = new Vector2(tileIdx, 0f);

            for (int f = 0; f < facesCount; f++)
            {
                var face = tile.Faces[f];
                int idx0 = pointIndexMap[face.Points[0].ID];
                int idx1 = pointIndexMap[face.Points[1].ID];
                int idx2 = pointIndexMap[face.Points[2].ID];

                st.SetUV2(tileUV);
                st.AddVertex(godotVertices[idx0]);
                currentTileVertices[localVertexCounter++] = globalVertexIndex++;

                st.SetUV2(tileUV);
                st.AddVertex(godotVertices[idx2]);
                currentTileVertices[localVertexCounter++] = globalVertexIndex++;

                st.SetUV2(tileUV);
                st.AddVertex(godotVertices[idx1]);
                currentTileVertices[localVertexCounter++] = globalVertexIndex++;
            }

            tileVertexIndices.Add(currentTileVertices);
        }

        st.GenerateNormals();
        var mesh = (ArrayMesh)st.Commit();
        return (mesh, tileVertexIndices);
    }
}