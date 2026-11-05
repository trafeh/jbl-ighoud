using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LandscapeGrassData", menuName = "Environment/Landscape Grass Data")]
public class LandscapeGrassData : ScriptableObject
{
    [Serializable]
    public struct GrassInstance
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    [Serializable]
    public class GrassChunk
    {
        public Bounds bounds;
        public GrassInstance[] instances;

        [NonSerialized]
        private List<Matrix4x4[]> matrixBatches;

        public List<Matrix4x4[]> MatrixBatches
        {
            get
            {
                if (matrixBatches == null)
                {
                    PrepareMatrices();
                }
                return matrixBatches;
            }
        }

        public void PrepareMatrices()
        {
            matrixBatches = new List<Matrix4x4[]>();
            if (instances == null || instances.Length == 0) return;

            int total = instances.Length;
            int offset = 0;
            while (offset < total)
            {
                int count = Mathf.Min(1023, total - offset);
                var batch = new Matrix4x4[count];
                for (int i = 0; i < count; i++)
                {
                    var inst = instances[offset + i];
                    batch[i] = Matrix4x4.TRS(inst.position, inst.rotation, inst.scale);
                }
                matrixBatches.Add(batch);
                offset += count;
            }
        }

        public void InvalidateMatrices()
        {
            matrixBatches = null;
        }

        public void RebuildBounds()
        {
            if (instances == null || instances.Length == 0)
            {
                bounds = new Bounds(Vector3.zero, Vector3.zero);
                InvalidateMatrices();
                return;
            }

            Vector3 bMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 bMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < instances.Length; i++)
            {
                Vector3 p = instances[i].position;
                bMin = Vector3.Min(bMin, p - Vector3.one * 0.5f);
                bMax = Vector3.Max(bMax, p + Vector3.one * 1.5f);
            }
            bounds = new Bounds((bMin + bMax) * 0.5f, bMax - bMin);
            PrepareMatrices();
        }

        public void AddInstances(List<GrassInstance> toAdd)
        {
            if (toAdd == null || toAdd.Count == 0) return;
            int oldLen = instances != null ? instances.Length : 0;
            var newArr = new GrassInstance[oldLen + toAdd.Count];
            if (oldLen > 0) Array.Copy(instances, newArr, oldLen);
            for (int i = 0; i < toAdd.Count; i++)
            {
                newArr[oldLen + i] = toAdd[i];
            }
            instances = newArr;
            RebuildBounds();
        }

        public int RemoveInRadius(Vector3 center, float radius)
        {
            if (instances == null || instances.Length == 0) return 0;
            float sqrR = radius * radius;
            var kept = new List<GrassInstance>(instances.Length);
            int removed = 0;
            for (int i = 0; i < instances.Length; i++)
            {
                Vector3 p = instances[i].position;
                float dx = p.x - center.x;
                float dz = p.z - center.z;
                float dy = p.y - center.y;
                if ((dx * dx + dz * dz) <= sqrR && Mathf.Abs(dy) <= radius * 2f)
                {
                    removed++;
                }
                else
                {
                    kept.Add(instances[i]);
                }
            }

            if (removed > 0)
            {
                instances = kept.ToArray();
                RebuildBounds();
            }
            return removed;
        }
    }

    public List<GrassChunk> chunks = new List<GrassChunk>();
    public int totalInstances = 0;
    public float bakedSpacing = 2.0f;
    public string bakedTimestamp = "";

    public void InvalidateAllMatrices()
    {
        if (chunks == null) return;
        foreach (var c in chunks)
        {
            c?.InvalidateMatrices();
        }
    }

    public bool HasInstanceNear(Vector3 pos, float minDistance)
    {
        float sqrMin = minDistance * minDistance;
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            if (chunk == null || chunk.instances == null) continue;
            if (chunk.bounds.SqrDistance(pos) > sqrMin) continue;

            for (int j = 0; j < chunk.instances.Length; j++)
            {
                Vector3 p = chunk.instances[j].position;
                float dx = p.x - pos.x;
                float dz = p.z - pos.z;
                if (dx * dx + dz * dz < sqrMin && Mathf.Abs(p.y - pos.y) < 2f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public GrassChunk FindOrCreateChunk(Vector3 pos, float chunkSize = 40f, float minX = -335f, float minZ = -335f)
    {
        int col = Mathf.FloorToInt((pos.x - minX) / chunkSize);
        int row = Mathf.FloorToInt((pos.z - minZ) / chunkSize);
        Vector3 cellCenter = new Vector3(minX + (col + 0.5f) * chunkSize, pos.y, minZ + (row + 0.5f) * chunkSize);

        for (int i = 0; i < chunks.Count; i++)
        {
            var c = chunks[i];
            if (c != null)
            {
                float dx = Mathf.Abs(c.bounds.center.x - cellCenter.x);
                float dz = Mathf.Abs(c.bounds.center.z - cellCenter.z);
                if (dx < chunkSize * 0.55f && dz < chunkSize * 0.55f)
                {
                    return c;
                }
            }
        }

        var newChunk = new GrassChunk();
        newChunk.bounds = new Bounds(pos, Vector3.one * 2f);
        newChunk.instances = new GrassInstance[0];
        chunks.Add(newChunk);
        return newChunk;
    }

    public int EraseInstancesInRadius(Vector3 center, float radius)
    {
        int totalRemoved = 0;
        float sqrR = radius * radius;
        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            var c = chunks[i];
            if (c == null) continue;
            if (c.bounds.SqrDistance(center) > sqrR) continue;

            int rem = c.RemoveInRadius(center, radius);
            totalRemoved += rem;
            if (c.instances == null || c.instances.Length == 0)
            {
                chunks.RemoveAt(i);
            }
        }
        totalInstances = Mathf.Max(0, totalInstances - totalRemoved);
        return totalRemoved;
    }

    public void RecalculateTotalInstances()
    {
        int count = 0;
        if (chunks != null)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i] != null && chunks[i].instances != null)
                    count += chunks[i].instances.Length;
            }
        }
        totalInstances = count;
    }
}
