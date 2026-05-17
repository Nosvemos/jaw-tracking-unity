using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace JawTracking.FileAccess
{
    public sealed class RuntimeStlMeshLoader : IStlMeshLoader
    {
        public StlImportResult LoadMesh(byte[] stlBytes, string meshName)
        {
            if (stlBytes == null || stlBytes.Length < 15)
            {
                return StlImportResult.Failure("STL dosyası geçersiz veya çok küçük.");
            }

            try
            {
                return LooksLikeBinaryStl(stlBytes)
                    ? LoadBinary(stlBytes, meshName)
                    : LoadAscii(stlBytes, meshName);
            }
            catch (Exception ex)
            {
                return StlImportResult.Failure($"STL yüklenemedi: {ex.Message}");
            }
        }

        private static bool LooksLikeBinaryStl(byte[] bytes)
        {
            if (bytes.Length < 84)
            {
                return false;
            }

            uint triangleCount = BitConverter.ToUInt32(bytes, 80);
            long expectedLength = 84L + triangleCount * 50L;
            if (expectedLength == bytes.Length)
            {
                return true;
            }

            string header = Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length));
            return !header.Equals("solid", StringComparison.OrdinalIgnoreCase);
        }

        private static StlImportResult LoadBinary(byte[] bytes, string meshName)
        {
            uint triangleCount = BitConverter.ToUInt32(bytes, 80);
            long expectedLength = 84L + triangleCount * 50L;
            if (expectedLength > bytes.Length)
            {
                return StlImportResult.Failure("Binary STL dosyası eksik veya bozuk görünüyor.");
            }

            var vertices = new List<Vector3>(checked((int)triangleCount * 3));
            var normals = new List<Vector3>(checked((int)triangleCount * 3));
            var triangles = new List<int>(checked((int)triangleCount * 3));

            int offset = 84;
            for (int i = 0; i < triangleCount; i++)
            {
                Vector3 normal = ReadVector3(bytes, offset);
                offset += 12;

                for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++)
                {
                    vertices.Add(ReadVector3(bytes, offset));
                    normals.Add(normal);
                    triangles.Add(vertices.Count - 1);
                    offset += 12;
                }

                offset += 2;
            }

            return BuildMesh(meshName, vertices, triangles, normals, (int)triangleCount);
        }

        private static StlImportResult LoadAscii(byte[] bytes, string meshName)
        {
            string text = Encoding.UTF8.GetString(bytes);
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (!line.StartsWith("vertex ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 4)
                {
                    continue;
                }

                vertices.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
                triangles.Add(vertices.Count - 1);
            }

            if (vertices.Count < 3 || vertices.Count % 3 != 0)
            {
                return StlImportResult.Failure("ASCII STL içinde geçerli üçgen verisi bulunamadı.");
            }

            return BuildMesh(meshName, vertices, triangles, null, vertices.Count / 3);
        }

        private static StlImportResult BuildMesh(
            string meshName,
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector3> normals,
            int triangleCount)
        {
            var mesh = new Mesh
            {
                name = string.IsNullOrWhiteSpace(meshName) ? "Runtime STL Mesh" : meshName,
                indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            if (normals != null && normals.Count == vertices.Count)
            {
                mesh.SetNormals(normals);
            }
            else
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            return StlImportResult.Ok(mesh, triangleCount);
        }

        private static Vector3 ReadVector3(byte[] bytes, int offset)
        {
            return new Vector3(
                BitConverter.ToSingle(bytes, offset),
                BitConverter.ToSingle(bytes, offset + 4),
                BitConverter.ToSingle(bytes, offset + 8));
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
