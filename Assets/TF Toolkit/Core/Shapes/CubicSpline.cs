using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TF_Toolkit
{
    public static class CubicSpline
    {
        public static void GenerateCylinderGeometry(Mesh mesh, Vector2[] cubicPoints, float height, float radialSegments = 32)
        {
            radialSegments = Mathf.Floor(radialSegments);
            float heightSegments = cubicPoints.Length - 1;

            // buffers
            List<int> indices = new List<int>();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            // helper variables
            int index = 0;
            List<List<int>> indexArray = new List<List<int>>();
            float halfHeight = height * 0.5f;

            float radiusTop = cubicPoints[cubicPoints.Length - 1].y;
            float radiusBottom = cubicPoints[0].y;

            // generate geometry
            #region Middle Part
            // generate vertices, normals and uvs
            for (int y = 0; y <= heightSegments; y++)
            {
                float v = y / heightSegments;

                // calculate the radius of the current row
                List<int> rowIndices = new List<int>();

                float cubicHeight = cubicPoints[y].x;
                float radius = cubicPoints[y].y; //TODO

                float slope;
                if (y == 0)
                {
                    slope = (cubicPoints[y + 1].y - radius) / (cubicHeight - cubicPoints[y + 1].x);
                }
                else if (y == heightSegments)
                {
                    slope = (radius - cubicPoints[y - 1].y) / (cubicPoints[y - 1].x - cubicHeight);
                }
                else
                {
                    float slopeBefore = (radius - cubicPoints[y - 1].y) / (cubicHeight - cubicPoints[y - 1].x);
                    float slopeAfter = (cubicPoints[y + 1].y - radius) / (cubicPoints[y + 1].x - cubicHeight);
                    slope = (slopeBefore + slopeAfter) * -0.5f;
                }

                for (int x = 0; x <= radialSegments; x++)
                {
                    float u = x / radialSegments;

                    float theta = u * Mathf.PI * 2;
                    float sinTheta = Mathf.Sin(theta);
                    float cosTheta = Mathf.Cos(theta);

                    // vertex
                    vertices.Add(new Vector3(
                        radius * sinTheta,
                        cubicHeight,
                        radius * cosTheta
                    ));

                    // normal
                    Vector3 normal = new Vector3(
                        sinTheta,
                        slope,
                        cosTheta
                    );
                    normal.Normalize();
                    normals.Add(normal);

                    // uv
                    uvs.Add(new Vector2(u, 1 - v));

                    // save index of vertex in respective row
                    rowIndices.Add(index++);
                }

                indexArray.Add(rowIndices);
            }

            // generate indices
            for (int x = 0; x < radialSegments; x++)
            {
                for (int y = 0; y < heightSegments; y++)
                {
                    // we use the index array to access the correct indices
                    int a = indexArray[y][x];
                    int b = indexArray[y + 1][x];
                    int c = indexArray[y + 1][x + 1];
                    int d = indexArray[y][x + 1];

                    // faces
                    indices.Add(b);
                    indices.Add(a);
                    indices.Add(d);

                    indices.Add(c);
                    indices.Add(b);
                    indices.Add(d);
                }
            }
            #endregion
         

            generateCap(true);
            generateCap(false);

            // build geometry
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = indices.ToArray();

            void generateCap(bool top)
            {
                // save the index of the first center vertex
                int centerIndexStart = index;

                float radius = top ? radiusTop : radiusBottom;
                int sign = top ? 1 : -1;

                // first we generate the center vertex data of the cap.
                // because the geometry needs one set of uvs per face,
                // we must generate a center vertex per face/segment
                Vector3 normal = Vector3.up * sign;
                Vector2 topUV = Vector2.one * 0.5f;
                Vector3 topVertex = Vector3.up * halfHeight * sign;
                for (int x = 1; x <= radialSegments; x++)
                {
                    vertices.Add(topVertex);
                    normals.Add(normal);
                    uvs.Add(topUV);
                    index++;
                }

                // save the index of the last center vertex
                int centerIndexEnd = index;

                // now we generate the surrounding vertices, normals and uvs

                for (int x = 0; x <= radialSegments; x++)
                {
                    float u = x / radialSegments;
                    float theta = u * Mathf.PI * 2;

                    float cosTheta = Mathf.Cos(theta);
                    float sinTheta = Mathf.Sin(theta);

                    // vertex
                    vertices.Add(new Vector3(
                        radius * sinTheta,
                        halfHeight * sign,
                        radius * cosTheta
                    ));

                    // normal
                    normals.Add(normal);

                    // uv
                    uvs.Add(new Vector2(
                        (cosTheta * 0.5f) + 0.5f,
                        (sinTheta * 0.5f * sign) + 0.5f
                    ));

                    // increase index
                    index++;
                }

                // generate indices
                for (int x = 0; x < radialSegments; x++)
                {
                    int i = centerIndexEnd + x;

                    if (top)
                    {
                        // face top
                        indices.Add(i);
                        indices.Add(i + 1);
                    }
                    else
                    {
                        // face bottom
                        indices.Add(i + 1);
                        indices.Add(i);
                    }
                    indices.Add(centerIndexStart + x);
                }
            }
        }

        /// <summary>
        /// Generate a smooth (interpolated) curve that follows the path of the given X/Y points
        /// </summary>
        public static Vector2[] InterpolateXY(Vector2[] points, int count)
        {
            float[] xs = new float[points.Length];
            float[] ys = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                xs[i] = points[i].x;
                ys[i] = points[i].y;
            }

            if (xs is null || ys is null || xs.Length != ys.Length)
                throw new ArgumentException($"{nameof(xs)} and {nameof(ys)} must have same length");

            int inputPointCount = xs.Length;
            float[] inputDistances = new float[inputPointCount];
            for (int i = 1; i < inputPointCount; i++)
            {
                float dx = xs[i] - xs[i - 1];
                float dy = ys[i] - ys[i - 1];
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                inputDistances[i] = inputDistances[i - 1] + distance;
            }

            float meanDistance = inputDistances.Last() / (count - 1);
            float[] evenDistances = Enumerable.Range(0, count).Select(x => x * meanDistance).ToArray();
            float[] xsOut = Interpolate(inputDistances, xs, evenDistances);
            float[] ysOut = Interpolate(inputDistances, ys, evenDistances);

            Vector2[] outPoints = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                outPoints[i] = new Vector2(xsOut[i], ysOut[i]);
            }

            return outPoints;
        }
        private static float[] Interpolate(float[] xOrig, float[] yOrig, float[] xInterp)
        {
            (float[] a, float[] b) = FitMatrix(xOrig, yOrig);

            float[] yInterp = new float[xInterp.Length];
            for (int i = 0; i < yInterp.Length; i++)
            {
                int j;
                for (j = 0; j < xOrig.Length - 2; j++)
                    if (xInterp[i] <= xOrig[j + 1])
                        break;

                float dx = xOrig[j + 1] - xOrig[j];
                float t = (xInterp[i] - xOrig[j]) / dx;
                float y = (1 - t) * yOrig[j] + t * yOrig[j + 1] +
                    t * (1 - t) * (a[j] * (1 - t) + b[j] * t);
                yInterp[i] = y;
            }

            return yInterp;
        }
        private static (float[] a, float[] b) FitMatrix(float[] x, float[] y)
        {
            int n = x.Length;
            float[] a = new float[n - 1];
            float[] b = new float[n - 1];
            float[] r = new float[n];
            float[] A = new float[n];
            float[] B = new float[n];
            float[] C = new float[n];

            float dx1, dx2, dy1, dy2;

            dx1 = x[1] - x[0];
            C[0] = 1.0f / dx1;
            B[0] = 2.0f * C[0];
            r[0] = 3 * (y[1] - y[0]) / (dx1 * dx1);

            for (int i = 1; i < n - 1; i++)
            {
                dx1 = x[i] - x[i - 1];
                dx2 = x[i + 1] - x[i];
                A[i] = 1.0f / dx1;
                C[i] = 1.0f / dx2;
                B[i] = 2.0f * (A[i] + C[i]);
                dy1 = y[i] - y[i - 1];
                dy2 = y[i + 1] - y[i];
                r[i] = 3 * (dy1 / (dx1 * dx1) + dy2 / (dx2 * dx2));
            }

            dx1 = x[n - 1] - x[n - 2];
            dy1 = y[n - 1] - y[n - 2];
            A[n - 1] = 1.0f / dx1;
            B[n - 1] = 2.0f * A[n - 1];
            r[n - 1] = 3 * (dy1 / (dx1 * dx1));

            float[] cPrime = new float[n];
            cPrime[0] = C[0] / B[0];
            for (int i = 1; i < n; i++)
                cPrime[i] = C[i] / (B[i] - cPrime[i - 1] * A[i]);

            float[] dPrime = new float[n];
            dPrime[0] = r[0] / B[0];
            for (int i = 1; i < n; i++)
                dPrime[i] = (r[i] - dPrime[i - 1] * A[i]) / (B[i] - cPrime[i - 1] * A[i]);

            float[] k = new float[n];
            k[n - 1] = dPrime[n - 1];
            for (int i = n - 2; i >= 0; i--)
                k[i] = dPrime[i] - cPrime[i] * k[i + 1];

            for (int i = 1; i < n; i++)
            {
                dx1 = x[i] - x[i - 1];
                dy1 = y[i] - y[i - 1];
                a[i - 1] = k[i - 1] * dx1 - dy1;
                b[i - 1] = -k[i] * dx1 + dy1;
            }

            return (a, b);
        }
    }
}
