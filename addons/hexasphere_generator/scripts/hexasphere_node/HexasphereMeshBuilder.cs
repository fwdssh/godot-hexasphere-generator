using Godot;
using Godot.Hexasphere;
using System.Collections.Generic;

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

    public (ArrayMesh mesh, List<int[]> tileVertexIndices) BuildNative(NativeHexasphere hexasphere)
    {
        var data = hexasphere.GetBuildData();
        var allPoints = (Vector3[])data["points"];
        var allFaceIndices = (int[])data["face_indices"];
        var ptCounts = (int[])data["point_counts"];
        var fcCounts = (int[])data["face_vertex_counts"];

        int tileCount = hexasphere.GetTileCount();

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        var tileVertexIndices = new List<int[]>(tileCount);
        int globalVertexIndex = 0;
        int ptOffset = 0;
        int faceOffset = 0;

        for (int tileIdx = 0; tileIdx < tileCount; tileIdx++)
        {
            int ptCount = ptCounts[tileIdx];
            int fcCount = fcCounts[tileIdx];

            int[] currentTileVertices = new int[fcCount];
            int localVertexCounter = 0;

            var tileUV = new Vector2(tileIdx, 0f);

            for (int f = 0; f < fcCount / 3; f++)
            {
                int idx0 = allFaceIndices[faceOffset + f * 3] + ptOffset;
                int idx1 = allFaceIndices[faceOffset + f * 3 + 1] + ptOffset;
                int idx2 = allFaceIndices[faceOffset + f * 3 + 2] + ptOffset;

                st.SetUV2(tileUV);
                st.AddVertex(allPoints[idx0]);
                currentTileVertices[localVertexCounter++] = globalVertexIndex++;

                st.SetUV2(tileUV);
                st.AddVertex(allPoints[idx2]);
                currentTileVertices[localVertexCounter++] = globalVertexIndex++;

                st.SetUV2(tileUV);
                st.AddVertex(allPoints[idx1]);
                currentTileVertices[localVertexCounter++] = globalVertexIndex++;
            }

            tileVertexIndices.Add(currentTileVertices);

            ptOffset += ptCount;
            faceOffset += fcCount;
        }

        st.GenerateNormals();
        var mesh = (ArrayMesh)st.Commit();
        return (mesh, tileVertexIndices);
    }
}