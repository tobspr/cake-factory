using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace LeTai.Asset.TranslucentImage
{
/// <summary>
/// Optimized for a small number of entries and the ability to be read in Jobs
/// Since VP matrices are expensive to get, use IndexOf to check if the related Camera is cached before calling Add
/// </summary>
public class VPMatrixCache : IDisposable
{
    public readonly struct Index
    {
        public static readonly Index INVALID = new Index(-1);

        internal readonly int index;

        public Index(int index)
        {
            this.index = index;
        }

        public bool IsValid()
        {
            return index >= 0;
        }
    }

    readonly List<Camera>          cameras = new List<Camera>(2);
    public   NativeList<Matrix4x4> VpMatrices { get; } = new NativeList<Matrix4x4>(2, Allocator.Persistent);

    /// <summary>
    /// Check if the Camera's VP matrix is cached
    /// </summary>
    /// <returns>The index of the camera if found; otherwise, -1.</returns>
    public Index IndexOf(Camera camera)
    {
        return new Index(cameras.IndexOf(camera));
    }

    /// <summary>
    /// Add to the cache. No duplicate check is done. Make sure to call IndexOf first
    /// </summary>
    /// <param name="camera">The camera to get the matrix is from</param>
    /// <returns>The index in the cache where the matrix is stored</returns>
    public Index Add(Camera camera)
    {
        var matrix = camera.projectionMatrix * camera.worldToCameraMatrix;
        return Add(camera, matrix);
    }

    /// <summary>
    /// Add to the cache. No duplicate check is done. Make sure to call IndexOf first
    /// </summary>
    /// <param name="camera">The camera from which the matrix is from</param>
    /// <param name="vpMatrix">camera.projectionMatrix * camera.worldToCameraMatrix</param>
    /// <returns>The index in the cache where the matrix is stored</returns>
    public Index Add(Camera camera, Matrix4x4 vpMatrix)
    {
        var index = cameras.Count;
        cameras.Add(camera);
        VpMatrices.Add(vpMatrix);
        return new Index(index);
    }

    public void Clear()
    {
        cameras.Clear();
        VpMatrices.Clear();
    }

    public void Dispose()
    {
        VpMatrices.Dispose();
    }
}
}
