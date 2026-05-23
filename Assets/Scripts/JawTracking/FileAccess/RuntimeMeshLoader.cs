using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace JawTracking.FileAccess
{
    public sealed class RuntimeMeshLoader : IMeshLoader
    {
        public ModelImportResult LoadMesh(byte[] fileBytes, string meshName)
        {
            if (fileBytes == null || fileBytes.Length < 15)
            {
                return ModelImportResult.Failure("Model dosyası geçersiz veya çok küçük.");
            }

            try
            {
                if (LooksLikePly(fileBytes))
                {
                    return LoadPly(fileBytes, meshName);
                }

                return LooksLikeBinaryStl(fileBytes)
                    ? LoadBinary(fileBytes, meshName)
                    : LoadAscii(fileBytes, meshName);
            }
            catch (Exception ex)
            {
                return ModelImportResult.Failure($"Model yüklenemedi: {ex.Message}");
            }
        }

        private static bool LooksLikePly(byte[] bytes)
        {
            if (bytes.Length < 4) return false;
            return bytes[0] == 'p' && bytes[1] == 'l' && bytes[2] == 'y' && bytes[3] == '\n';
        }

        private static ModelImportResult LoadPly(byte[] bytes, string meshName)
        {
            try
            {
                int headerEndIndex = -1;
                int vertexCount = 0;
                int faceCount = 0;
                bool isBinary = false;
                
                int vertexPropertiesSize = 0;
                int redOffset = -1;
                int greenOffset = -1;
                int blueOffset = -1;
                int alphaOffset = -1;

                string headerStr = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 2048));
                string[] lines = headerStr.Split(new[] { '\n' });
                
                bool inVertexElement = false;

                foreach (string lineRaw in lines)
                {
                    string line = lineRaw.TrimEnd('\r');
                    if (line.StartsWith("format binary")) isBinary = true;
                    else if (line.StartsWith("element vertex")) 
                    {
                        vertexCount = int.Parse(line.Split(' ')[2]);
                        inVertexElement = true;
                    }
                    else if (line.StartsWith("element face")) 
                    {
                        faceCount = int.Parse(line.Split(' ')[2]);
                        inVertexElement = false;
                    }
                    else if (inVertexElement && line.StartsWith("property"))
                    {
                        string[] parts = line.Split(' ');
                        if (parts.Length >= 3)
                        {
                            string propName = parts[2];
                            int size = 0;
                            
                            if (line.Contains(" float ") || line.Contains(" float32 ") || line.Contains(" int ") || line.Contains(" int32 ")) size = 4;
                            else if (line.Contains(" uchar ") || line.Contains(" uint8 ") || line.Contains(" char ") || line.Contains(" int8 ")) size = 1;
                            else if (line.Contains(" double ") || line.Contains(" float64 ")) size = 8;
                            else if (line.Contains(" short ") || line.Contains(" ushort ") || line.Contains(" int16 ") || line.Contains(" uint16 ")) size = 2;
                            
                            if (propName == "red" || propName == "r") redOffset = vertexPropertiesSize;
                            else if (propName == "green" || propName == "g") greenOffset = vertexPropertiesSize;
                            else if (propName == "blue" || propName == "b") blueOffset = vertexPropertiesSize;
                            else if (propName == "alpha" || propName == "a") alphaOffset = vertexPropertiesSize;

                            vertexPropertiesSize += size;
                        }
                    }
                    else if (line == "end_header")
                    {
                        headerEndIndex = headerStr.IndexOf("end_header") + 11;
                        break;
                    }
                }

                if (headerEndIndex == -1 || vertexCount == 0 || faceCount == 0 || !isBinary)
                {
                    return ModelImportResult.Failure("Desteklenmeyen PLY formatı. (Sadece Binary PLY)");
                }

                if (vertexPropertiesSize < 12)
                {
                    return ModelImportResult.Failure("PLY dosyasında geçerli XYZ koordinatları bulunamadı.");
                }

                var vertices = new List<Vector3>(vertexCount);
                var colors = new List<Color32>(vertexCount);
                var triangles = new List<int>(faceCount * 3);

                bool hasColors = redOffset != -1 && greenOffset != -1 && blueOffset != -1;

                int offset = headerEndIndex;
                for (int i = 0; i < vertexCount; i++)
                {
                    float x = BitConverter.ToSingle(bytes, offset);
                    float y = BitConverter.ToSingle(bytes, offset + 4);
                    float z = BitConverter.ToSingle(bytes, offset + 8);
                    
                    if (hasColors)
                    {
                        byte r = bytes[offset + redOffset];
                        byte g = bytes[offset + greenOffset];
                        byte b = bytes[offset + blueOffset];
                        byte a = alphaOffset != -1 ? bytes[offset + alphaOffset] : (byte)255;
                        colors.Add(new Color32(r, g, b, a));
                    }

                    offset += vertexPropertiesSize;
                    vertices.Add(new Vector3(x, y, z));
                }

                for (int i = 0; i < faceCount; i++)
                {
                    if (offset >= bytes.Length) break;

                    byte count = bytes[offset];
                    offset += 1;
                    
                    if (count == 3)
                    {
                        int v1 = BitConverter.ToInt32(bytes, offset);
                        int v2 = BitConverter.ToInt32(bytes, offset + 4);
                        int v3 = BitConverter.ToInt32(bytes, offset + 8);
                        
                        triangles.Add(v1);
                        triangles.Add(v2);
                        triangles.Add(v3);
                    }
                    
                    offset += count * 4;
                }

                Mesh mesh = new Mesh { name = meshName };
                if (vertices.Count > 65000)
                {
                    mesh.indexFormat = IndexFormat.UInt32;
                }
                mesh.SetVertices(vertices);
                if (hasColors)
                {
                    mesh.SetColors(colors);
                }
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                return ModelImportResult.Ok(mesh, triangles.Count / 3);
            }
            catch (Exception ex)
            {
                return ModelImportResult.Failure("PLY okuma hatası: " + ex.Message);
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

        private static ModelImportResult LoadBinary(byte[] bytes, string meshName)
        {
            uint triangleCount = BitConverter.ToUInt32(bytes, 80);
            long expectedLength = 84L + triangleCount * 50L;
            if (expectedLength > bytes.Length)
            {
                return ModelImportResult.Failure("Binary STL dosyası eksik veya bozuk görünüyor.");
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

        private static ModelImportResult LoadAscii(byte[] bytes, string meshName)
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
                return ModelImportResult.Failure("ASCII STL içinde geçerli üçgen verisi bulunamadı.");
            }

            return BuildMesh(meshName, vertices, triangles, null, vertices.Count / 3);
        }

        private static ModelImportResult BuildMesh(
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
            return ModelImportResult.Ok(mesh, triangleCount);
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
