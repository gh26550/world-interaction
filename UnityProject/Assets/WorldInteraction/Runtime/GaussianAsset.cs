using System;
using System.IO;
using UnityEngine;

namespace WorldInteraction
{
    [Serializable] public struct Gaussian
    {
        public Vector3 center, scale;
        public Quaternion rotation;
        public Color color;
    }
    public static class GaussianAsset
    {
        public const int MaxCount = 200000;
        public static Gaussian[] Read(byte[] bytes)
        {
            using var reader=new BinaryReader(new MemoryStream(bytes));
            if(new string(reader.ReadChars(4))!="WIG1") throw new InvalidDataException("Expected WIG1 Gaussian data");
            int count=reader.ReadInt32();
            if(count<1 || count>MaxCount || bytes.Length!=8L+count*56L) throw new InvalidDataException("Invalid Gaussian count/length");
            var result=new Gaussian[count];
            for(int i=0;i<count;i++)
            {
                float[] f=new float[14];
                for(int j=0;j<14;j++) { f[j]=reader.ReadSingle(); if(float.IsNaN(f[j])||float.IsInfinity(f[j]))throw new InvalidDataException("Nonfinite Gaussian"); }
                if(f[3]<=0 || f[4]<=0 || f[5]<=0 || f[13]<0 || f[13]>1)throw new InvalidDataException("Invalid scale/opacity");
                var q=new Quaternion(f[6],f[7],f[8],f[9]);
                if(Quaternion.Dot(q,q)<1e-8f)throw new InvalidDataException("Zero quaternion");
                result[i]=new Gaussian {center=new(f[0],f[1],f[2]),scale=new(f[3],f[4],f[5]),rotation=Quaternion.Normalize(q),color=new(f[10],f[11],f[12],f[13])};
            }
            return result;
        }
    }
}
