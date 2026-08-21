namespace vaudio_godot_mono_openal_3d;

public static class Conversions
{
    public static List<vaudio.Vector> ConvertMeshToVector3FList(string name, Mesh mesh, out vaudio.Vector minOut, out vaudio.Vector maxOut, VAWorld world)
    {
        List<vaudio.Vector> vertices = [];

        var min = vaudio.Vector.MAX;
        var max = vaudio.Vector.MIN;

        var surfaceCount = mesh.GetSurfaceCount();

        void AddTriangle(Vector3[] normals, int index, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            if (normals != null && index < normals.Length)
            {
                var edge1 = v1 - v0;
                var edge2 = v2 - v0;
                var calculatedNormal = edge1.Cross(edge2).Normalized();
                var meshNormal = normals[index];

                if (calculatedNormal.Dot(meshNormal) < 0)
                {
                    // Flip winding to match mesh normal direction
                    (v1, v2) = (v2, v1);
                }
            }

            // Godot uses CCW winding; VAudio expects CW — swap v1/v2 to convert
            vertices.Add(ToVAudio(v0));
            vertices.Add(ToVAudio(v2));
            vertices.Add(ToVAudio(v1));

            // Update bounds
            min.X = Math.Min(min.X, Math.Min(v0.X, Math.Min(v1.X, v2.X)));
            min.Y = Math.Min(min.Y, Math.Min(v0.Y, Math.Min(v1.Y, v2.Y)));
            min.Z = Math.Min(min.Z, Math.Min(v0.Z, Math.Min(v1.Z, v2.Z)));
            max.X = Math.Max(max.X, Math.Max(v0.X, Math.Max(v1.X, v2.X)));
            max.Y = Math.Max(max.Y, Math.Max(v0.Y, Math.Max(v1.Y, v2.Y)));
            max.Z = Math.Max(max.Z, Math.Max(v0.Z, Math.Max(v1.Z, v2.Z)));
        }

        for (var surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
        {
            var arrays = mesh.SurfaceGetArrays(surfaceIndex);
            if (arrays == null || arrays.Count == 0)
                continue;


            // Get vertex array (index 0 in the arrays)
            var surfaceVertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (surfaceVertices == null || surfaceVertices.Length == 0)
                continue;


            // Get the normal array to check winding consistency
            Vector3[] normals = null;
            var normalsVariant = arrays[(int)Mesh.ArrayType.Normal];

            if (normalsVariant.VariantType != Variant.Type.Nil)
                normals = normalsVariant.AsVector3Array();

            var indicesVariant = arrays[(int)Mesh.ArrayType.Index];

            // No index array - vertices are in triangle order
            if (indicesVariant.VariantType == Variant.Type.Nil)
            {
                for (int i = 0; i < surfaceVertices.Length; i += 3)
                {
                    if (i + 2 >= surfaceVertices.Length)
                        break;

                    var v0 = surfaceVertices[i];
                    var v1 = surfaceVertices[i + 1];
                    var v2 = surfaceVertices[i + 2];

                    AddTriangle(normals, i, v0, v1, v2);
                }
            }
            else
            {
                // Use indices to build triangles
                var indices = indicesVariant.AsInt32Array();

                for (int i = 0; i < indices.Length; i += 3)
                {
                    if (i + 2 >= indices.Length)
                        break;

                    var index0 = indices[i];
                    var index1 = indices[i + 1];
                    var index2 = indices[i + 2];

                    var v0 = surfaceVertices[index0];
                    var v1 = surfaceVertices[index1];
                    var v2 = surfaceVertices[index2];

                    AddTriangle(normals, index0, v0, v1, v2);
                }
            }
        }

        // If no vertices were found, reset to default bounds
        if (vertices.Count == 0)
        {
            min = vaudio.Vector.Zero;
            max = vaudio.Vector.Zero;

            LogWarning($"Mesh {name} will not affect raytracing as it has no vertices");
        }

        minOut = min;
        maxOut = max;

        return vertices;
    }

    public static List<vaudio.Vector> ConvertConcavePolygonToVector3FList(string name, ConcavePolygonShape3D shape, out vaudio.Vector min, out vaudio.Vector max, VAWorld world)
    {
        Vector3[] faces = shape.GetFaces();

        if (faces.Length == 0)
        {
            min = new vaudio.Vector(0, 0, 0);
            max = new vaudio.Vector(0, 0, 0);

            LogWarning($"ConcavePolygonShape3D {name} will not affect raytracing as it has no faces");
            return [];
        }

        List<vaudio.Vector> vertices = [];

        min = vaudio.Vector.MAX;
        max = vaudio.Vector.MIN;

        for (int i = 0; i < faces.Length; i++)
        {
            vaudio.Vector vertex = ToVAudio(faces[i]);

            vertices.Add(vertex);

            // Update bounds
            min.X = Math.Min(min.X, vertex.X);
            min.Y = Math.Min(min.Y, vertex.Y);
            min.Z = Math.Min(min.Z, vertex.Z);

            max.X = Math.Max(max.X, vertex.X);
            max.Y = Math.Max(max.Y, vertex.Y);
            max.Z = Math.Max(max.Z, vertex.Z);
        }

        return vertices;
    }

    public static List<vaudio.Vector> ConvertConvexPolygonToVector3FList(string name, ConvexPolygonShape3D shape, out vaudio.Vector min, out vaudio.Vector max, VAWorld world)
    {
        // Use the debug mesh to triangulate the polygon
        var debugMesh = shape.GetDebugMesh();

        if (debugMesh == null)
        {
            min = new vaudio.Vector(0, 0, 0);
            max = new vaudio.Vector(0, 0, 0);

            LogWarning($"ConvexPolygonShape3D {name} will not affect raytracing as it cannot be triangulated");
            return [];
        }

        return ConvertMeshToVector3FList(name, debugMesh, out min, out max, world);
    }

    public static List<vaudio.Vector> ConvertHeightMapToVector3FList(string name, HeightMapShape3D shape, out vaudio.Vector min, out vaudio.Vector max, VAWorld world)
    {
        int mapWidth = shape.MapWidth;
        int mapDepth = shape.MapDepth;
        float[] mapData = shape.MapData;

        // Skip invalid heightmaps
        if (mapWidth < 2 || mapDepth < 2 || mapData.Length < mapWidth * mapDepth)
        {
            min = new vaudio.Vector(0, 0, 0);
            max = new vaudio.Vector(0, 0, 0);

            LogWarning($"HeightMapShape3D {name} will not affect raytracing as its dimensions are less than 2x2");
            return [];
        }

        List<vaudio.Vector> triangles = [];

        min = vaudio.Vector.MAX;
        max = vaudio.Vector.MIN;

        // HeightMapShape3D nodes are centered at the origin,
        //  spanning from -width / 2 to + width / 2 and -depth / 2 to +depth / 2
        float halfWidth = (mapWidth - 1) / 2.0f;
        float halfDepth = (mapDepth - 1) / 2.0f;

        for (int z = 0; z < mapDepth - 1; z++)
        {
            for (int x = 0; x < mapWidth - 1; x++)
            {
                // Get heights for the 4 corners of this cell
                float h00 = mapData[x + 0 + mapWidth * (z + 0)];
                float h10 = mapData[x + 1 + mapWidth * (z + 0)];
                float h01 = mapData[x + 0 + mapWidth * (z + 1)];
                float h11 = mapData[x + 1 + mapWidth * (z + 1)];

                // Skip invalid heights
                if (IsNaNorInfinity(h00) || IsNaNorInfinity(h10) || IsNaNorInfinity(h01) || IsNaNorInfinity(h11))
                    continue;

                // Calculate world positions (centered at origin)
                vaudio.Vector v00 = new(x - halfWidth, h00, z - halfDepth);
                vaudio.Vector v10 = new(x + 1 - halfWidth, h10, z - halfDepth);
                vaudio.Vector v01 = new(x - halfWidth, h01, z + 1 - halfDepth);
                vaudio.Vector v11 = new(x + 1 - halfWidth, h11, z + 1 - halfDepth);

                // Create two triangles for each quad, with clockwise winding for upward-facing normals
                triangles.Add(v00);
                triangles.Add(v01);
                triangles.Add(v10);
                triangles.Add(v10);
                triangles.Add(v01);
                triangles.Add(v11);

                // Update bounds
                min.X = Math.Min(min.X, Math.Min(v00.X, Math.Min(v10.X, Math.Min(v01.X, v11.X))));
                min.Y = Math.Min(min.Y, Math.Min(v00.Y, Math.Min(v10.Y, Math.Min(v01.Y, v11.Y))));
                min.Z = Math.Min(min.Z, Math.Min(v00.Z, Math.Min(v10.Z, Math.Min(v01.Z, v11.Z))));
                max.X = Math.Max(max.X, Math.Max(v00.X, Math.Max(v10.X, Math.Max(v01.X, v11.X))));
                max.Y = Math.Max(max.Y, Math.Max(v00.Y, Math.Max(v10.Y, Math.Max(v01.Y, v11.Y))));
                max.Z = Math.Max(max.Z, Math.Max(v00.Z, Math.Max(v10.Z, Math.Max(v01.Z, v11.Z))));
            }
        }

        if (triangles.Count == 0)
        {
            min = new vaudio.Vector(0, 0, 0);
            max = new vaudio.Vector(0, 0, 0);
        }

        return triangles;
    }
}
