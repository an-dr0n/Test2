﻿// Operations with "maps"
// Note that functions are not inlined in editor, so keeping all method unfolded

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Tests")]

namespace Den.Tools.Matrices
{
	[Serializable, StructLayout (LayoutKind.Sequential)] //to pass to native
	public class Matrix // : Matrix2D<float>
	{
		public CoordRect rect; //never assign it's size manually, use ChangeRect
		public int count;
		public float[] arr;

		public const bool native = true;

		#region Constructors

			public Matrix () { arr = new float[0]; rect = new CoordRect(0,0,0,0); count=0;  } //for serializer

			public Matrix (int offsetX, int offsetZ, int sizeX, int sizeZ, float[] array=null)
			{
				rect = new CoordRect(offsetX, offsetZ, sizeX, sizeZ);
				count = rect.Count;
				DefineArray(array);
			}

			public Matrix (CoordRect rect, float[] array=null)
			{
				this.rect = rect;
				count = rect.Count;
				DefineArray(array);
			}

			public Matrix (Coord offset, Coord size, float[] array=null)
			{
				rect = new CoordRect(offset, size);
				count = rect.Count;
				DefineArray(array);
			}

			public Matrix (Matrix src, float[] array=null)
			{
				rect = src.rect;
				count = rect.Count;

				DefineArray(array);
				Array.Copy(src.arr, arr, arr.Length);
			}

			public Matrix (Texture2D texture, int channel=-1)
			{
				rect = new CoordRect(0,0, texture.width, texture.height);
				count = rect.Count;
				arr = new float[count];

				Color[] colors = texture.GetPixels();
				ImportColors(colors, texture.width, texture.height, channel);
			}

			protected void DefineArray (float[] array=null)
			{
				int matrixCount = rect.size.x*rect.size.z;
				if (array != null)
				{
					if (array.Length < matrixCount) 
						throw new Exception("Array length: " + array.Length + " is lower then matrix capacity: " + matrixCount);
					else arr = array;
				}
				else arr = new float[matrixCount];
			}

		#endregion


		#region Get/Set

			public float this[int x, int z] 
			{
				get { return arr[(z-rect.offset.z)*rect.size.x + x - rect.offset.x]; } //rect fn duplicated to increase performance
				set { arr[(z-rect.offset.z)*rect.size.x + x - rect.offset.x] = value; }
			}


			public float this[Coord c] 
			{
				get { return arr[(c.z-rect.offset.z)*rect.size.x + c.x - rect.offset.x]; }
				set { arr[(c.z-rect.offset.z)*rect.size.x + c.x - rect.offset.x] = value; }
			}


			public float GetInterpolated (float fx, float fz)
			{
				int ix = (int)fx; if (fx<0) ix--; if (ix==rect.offset.x+rect.size.x) ix--;
				int iz = (int)fz; if (fz<0) iz--; if (iz==rect.offset.z+rect.size.z) iz--;

				float xPercent = fx-ix; 
				float zPercent = fz-iz;

				if (ix<rect.offset.x) ix = rect.offset.x;
				if (iz<rect.offset.z) iz = rect.offset.z;
				int ix1 = ix+1;  if (ix1>=rect.offset.x+rect.size.x) ix1 = rect.offset.x+rect.size.x-1;
				int iz1 = iz+1;  if (iz1>=rect.offset.z+rect.size.z) iz1 = rect.offset.z+rect.size.z-1;

				float val1 = this[ix,iz];
				float val2 = this[ix1,iz];
				float val3 = val1*(1-xPercent) + val2*xPercent;

				float val4 = this[ix,iz1];
				float val5 = this[ix1,iz1];
				float val6 = val4*(1-xPercent) + val5*xPercent;
			
				return val3*(1-zPercent) + val6*zPercent;
			}


			public float GetRelative (float rx, float rz)
			/// Where rx and rz are 0-1 in percent relative to matrix rect
			{
				float fx = rx*rect.size.x + rect.offset.x;
				float fz = rz*rect.size.z + rect.offset.z;

				return GetInterpolated(fx,fz);
			}


			public float GetRelative (float rx, float rz, CoordRect customRect)
			/// Same GetRelative, but 0-1 range is relative to custom rect (for example, active zone)
			{
				float fx = rx*customRect.size.x + customRect.offset.x;
				float fz = rz*customRect.size.z + customRect.offset.z;

				return GetInterpolated(fx,fz);
			}


			public float GetRelativeWithRotation (float sx, float sz, Vector2D rotationDirection, Vector2D rotationPivot, CoordRect customRect)
			/// Same GetRelative, but 0-1 range is relative to custom rect (for example, active zone)
			/// rotationPivot range is 0-1 too
			{
				float cx = sx - rotationPivot.x;  float cz = sz - rotationPivot.z;

				Vector2D vx = rotationDirection * cx;
				Vector2D vz = new Vector2D(-rotationDirection.z, rotationDirection.x) * cz;
				Vector2D v = vx+vz;

				v.x += rotationPivot.x;  v.z += rotationPivot.z;

				if (v.x < 0 || v.x > 1) return 0;
				if (v.z < 0 || v.z > 1) return 0;

				float fx = v.x*customRect.size.x + customRect.offset.x;
				float fz = v.z*customRect.size.z + customRect.offset.z;

				return GetInterpolated(fx,fz);
			}

		#endregion


		#region Import

			public void ImportTexture (Texture2D tex, int channel=-1) { ImportTexture(tex, rect.offset, channel); }
			//texOffset == matrix.rect.offset by default, so it will read texture from 0,0
			//when matrix bigger - reads whole texture as matrix part
			//when texture bigger - reads only the matrix part form it

			public void ImportTexture (Texture2D tex, Coord texOffset, int channel=-1)
			/// Imports texture using color arrays.
			/// If channel -1 reads avg of 3 colors
			{
				Coord texSize = new Coord(tex.width, tex.height);
				CoordRect intersection = CoordRect.Intersected(rect, new CoordRect(texOffset,texSize)); //to get array smaller than the whole texture

				Color[] colors = tex.GetPixels(intersection.offset.x-texOffset.x, intersection.offset.z-texOffset.z, intersection.size.x, intersection.size.z);
				ImportColors(colors, intersection.offset, intersection.size, channel);
			}


			public void ImportTextureRaw (Texture2D tex, int channel=0) { ImportTextureRaw(tex, rect.offset, channel); }
			public void ImportTextureRaw (Texture2D tex, Coord texOffset, int channel=0)
			/// Uses raw texture bytes instead of color arrays. Faster, but requires specific non-compressed format
			{
				Coord texSize = new Coord(tex.width, tex.height);
				
				TextureFormat format = tex.format;
				if (format!=TextureFormat.RGBA32 && format!=TextureFormat.ARGB32 && format!=TextureFormat.RGB24 && format!=TextureFormat.R8 && format!=TextureFormat.R16)
					throw new Exception("Matrix export: raw texture format is not supported");

				byte[] bytes = tex.GetRawTextureData();

				switch(format)
				{
						case TextureFormat.RGBA32: ImportRawBytes(bytes, texOffset, texSize, channel, 4); break;
						case TextureFormat.ARGB32: channel++; if (channel == 5) channel = 0; ImportRawBytes(bytes, texOffset, texSize, channel, 4); break;
						case TextureFormat.RGB24: ImportRawBytes(bytes, texOffset, texSize, channel, 3); break;
						case TextureFormat.R8: ImportRawBytes(bytes, texOffset, texSize, 0, 1); break;
						case TextureFormat.R16: ImportRaw16(bytes, texOffset, texSize); break;
				}
			}


			public void ImportColors (Color[] colors, int width, int height, int channel=-1) { ImportColors(colors, rect.offset, new Coord(width,height), channel); }
			public void ImportColors (Color[] colors, Coord colorsSize, int channel=-1) { ImportColors(colors, rect.offset, colorsSize, channel); }
			public void ImportColors (Color[] colors, Coord colorsOffset, Coord colorsSize,  int channel=-1)
			{
				if (colors.Length != colorsSize.x*colorsSize.z)
					throw new Exception("Array count does not match texture dimensions");

				CoordRect intersection = CoordRect.Intersected(rect, new CoordRect(colorsOffset, colorsSize));
				Coord min = intersection.Min; Coord max = intersection.Max;
				
				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int colorsPos = (z-colorsOffset.z)*colorsSize.x + x - colorsOffset.x;

						float val;
						switch (channel)
						{
							case 0: val = colors[colorsPos].r; break;
							case 1: val = colors[colorsPos].g; break;
							case 2: val = colors[colorsPos].b; break;
							case 3: val = colors[colorsPos].a; break;
							default: val = (colors[colorsPos].r + colors[colorsPos].g + colors[colorsPos].b)/3; break;
						}

						arr[matrixPos] = val;
					}
			}


			public void ImportRawBytes (byte[] bytes, int width, int height, int start, int step) { ImportRawBytes(bytes, rect.offset, new Coord(width,height), start, step); }
			public void ImportRawBytes (byte[] bytes, Coord bytesSize, int start, int step) { ImportRawBytes(bytes, rect.offset, bytesSize, start, step); }
			public void ImportRawBytes (byte[] bytes, Coord bytesOffset, Coord bytesSize, int start, int step)
			{
				if (bytes.Length != bytesSize.x*bytesSize.z*step &&  
					(bytes.Length < bytesSize.x*bytesSize.z*step*1.3f || bytes.Length > bytesSize.x*bytesSize.z*step*1.3666f)) //in case of mipmap information
						throw new Exception("Array count does not match texture dimensions");

				#if MM_NATIVE
				MatrixNativeExtensions.ImportRawBytes(this, bytes, bytes.Length, bytesOffset, bytesSize, start, step);
				#else

				CoordRect intersection = CoordRect.Intersected(rect, new CoordRect(bytesOffset, bytesSize));
				Coord min = intersection.Min; Coord max = intersection.Max;

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int bytesPos = (z-bytesOffset.z)*bytesSize.x + x - bytesOffset.x;
						bytesPos = bytesPos * step + start;

						float val = bytes[bytesPos] / 255f;  //matrix has the range 0-1 _inclusive_, it could be 1, so using 255
						arr[matrixPos] = val;
					}

				#endif
			}
			


			public void ImportRaw16 (byte[] bytes, int width, int height) { ImportRaw16(bytes, rect.offset, new Coord(width,height)); }
			public void ImportRaw16 (byte[] bytes, Coord texSize) { ImportRaw16(bytes, rect.offset, texSize); }
			public void ImportRaw16 (byte[] bytes, Coord texOffset, Coord texSize)
			{
				if (texSize.x*texSize.z*2 > bytes.Length) //extra bytes could be mipmaps
					throw new Exception("Array count does not match texture dimensions");

				#if MM_NATIVE
				MatrixNativeExtensions.ImportRaw16(this, bytes, bytes.Length, texOffset, texSize);
				#else

				CoordRect intersection = CoordRect.Intersected(rect, new CoordRect(texOffset, texSize));
				Coord min = intersection.Min; Coord max = intersection.Max;

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int bytesPos = (z-texOffset.z)*texSize.x + x - texOffset.x;
						bytesPos *= 2; 

						float val = (bytes[bytesPos+1]*255f + bytes[bytesPos]) / 65025f;
						arr[matrixPos] = val;
					}

				#endif
			}


			public void ImportRawFloat (byte[] bytes, int width, int height, float mult=1) { ImportRawFloat(bytes, new Coord(width,height), rect.offset, mult); }
			public void ImportRawFloat (byte[] bytes, Coord texSize, float mult=1) { ImportRawFloat(bytes, texSize, rect.offset, mult); }
			public void ImportRawFloat (byte[] bytes, Coord texOffset, Coord texSize, float mult=1)
			{
				int numPixels = texSize.x*texSize.z;
				if (numPixels*4 > bytes.Length) //extra bytes could be mipmaps
					throw new Exception("Array count does not match texture dimensions");

				#if MM_NATIVE
				MatrixNativeExtensions.ImportRawFloat(this, bytes, bytes.Length, texOffset, texSize, mult);
				#else

				CoordRect intersection = CoordRect.Intersected(rect, new CoordRect(texOffset, texSize));
				Coord min = intersection.Min; Coord max = intersection.Max;

				FloatToBytes converter = new FloatToBytes();

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int bytesPos = (z-texOffset.z)*texSize.x + x - texOffset.x;
						bytesPos *= 4; 

						converter.b0 = bytes[bytesPos];
						converter.b1 = bytes[bytesPos+1];
						converter.b2 = bytes[bytesPos+2];
						converter.b3 = bytes[bytesPos+3];

						arr[matrixPos] = converter.f * mult;
					}

				#endif
			}


			public void ImportHeights (float[,] heights) { ImportHeights(heights, rect.offset); }
			public void ImportHeights (float[,] heights, Coord heightsOffset)
			{
				Coord heightsSize = new Coord(heights.GetLength(1), heights.GetLength(0));  //x and z swapped
				CoordRect heightsRect = new CoordRect(heightsOffset, heightsSize);
				
				CoordRect intersection = CoordRect.Intersected(rect, heightsRect);
				Coord min = intersection.Min; Coord max = intersection.Max;

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int heightsPosZ = x - heightsRect.offset.x;
						int heightsPosX = z - heightsRect.offset.z;

						arr[matrixPos] = heights[heightsPosX, heightsPosZ];
					}
			}

			public void ImportHeightStrips (float[][,] heights) { ImportHeightStrips(heights, rect.offset); }
			public void ImportHeightStrips (float[][,] heights, Coord heightsOffset)
			{
				//TODO: offset doesnt work
				int offset = 0;
				for (int s=0; s<heights.Length; s++)
				{
					ImportHeights(heights[s], new Coord(0,offset));
					offset+=heights[s].GetLength(0);
				}
			}

			public void ImportSplats (float[,,] splats, int channel) { ImportSplats(splats, rect.offset, channel); }
			public void ImportSplats (float[,,] splats, Coord splatsOffset, int channel)
			{
				Coord splatsSize = new Coord(splats.GetLength(1), splats.GetLength(0));  //x and z swapped
				CoordRect splatsRect = new CoordRect(splatsOffset, splatsSize);
				
				CoordRect intersection = CoordRect.Intersected(rect, splatsRect);
				Coord min = intersection.Min; Coord max = intersection.Max;

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int heightsPosZ = x - splatsRect.offset.x;
						int heightsPosX = z - splatsRect.offset.z;

						arr[matrixPos] = splats[heightsPosX, heightsPosZ, channel];
					}
			}


			public void ImportData (TerrainData data, int channel=-1) { ImportData (data, rect.offset, channel=-1); }
			public void ImportData (TerrainData data, Coord dataOffset, int channel=-1)
			/// Partial terrain data (loading only the part intersecting with matrix). Do not work in thread!
			/// If channel is -1 getting height
			{
				int resolution = channel==-1 ?  data.heightmapResolution : data.alphamapResolution;

				Coord dataSize = new Coord(resolution, resolution);
				CoordRect dataIntersection = CoordRect.Intersected(rect, new CoordRect(dataOffset, dataSize));
				if (dataIntersection.size.x==0 || dataIntersection.size.z==0) return;

				if (channel == -1)
				{
					float[,] heights = data.GetHeights(dataIntersection.offset.x-dataOffset.x, dataIntersection.offset.z-dataOffset.z, dataIntersection.size.x, dataIntersection.size.z);
					ImportHeights(heights, dataIntersection.offset);
				}

				else
				{
					float[,,] splats = data.GetAlphamaps(dataIntersection.offset.x-dataOffset.x, dataIntersection.offset.z-dataOffset.z, dataIntersection.size.x, dataIntersection.size.z);
					ImportSplats(splats, dataIntersection.offset, channel);
				}
			}

		#endregion

			
		#region Export

			public void ExportTexture (Texture2D tex, int channel=-1) { ExportTexture(tex, rect.offset, channel); }
			//texOffset == matrix.rect.offset by default, so it will write texture from matrix's start
			//when matrix bigger - writes whole texture, disregards unnecessary data
			//when texture bigger - writes only the matrix part to it

			public void ExportTexture (Texture2D tex, Coord texOffset, int channel=-1)
			{
				Coord texSize = new Coord(tex.width, tex.height);

				CoordRect intersection = CoordRect.Intersected(rect, new CoordRect(texOffset,texSize)); //to get array smaller than the whole texture

				Color[] colors;
				if (channel < 0) //will re-create all colors anyways
					colors = new Color[intersection.size.x * intersection.size.z];
				else
					colors = tex.GetPixels(intersection.offset.x-texOffset.x, intersection.offset.z-texOffset.z, intersection.size.x, intersection.size.z);

				ExportColors(colors, intersection.offset, intersection.size, channel);
				tex.SetPixels(intersection.offset.x-texOffset.x, intersection.offset.z-texOffset.z, intersection.size.x, intersection.size.z, colors);

				tex.Apply();
			}


			public void ExportTextureRaw (Texture2D tex)
			/// Will overwrite all data (not partial)
			/// More of a snippet since could be mostly done in thread
			{
				if (tex.width!=rect.size.x || tex.height!=rect.size.z)
					throw new Exception("Matrix export: matrix size and texture resolution mismatch (tex:" + tex.width +"*" + tex.height + " matrix:" + rect.size.x + "*" + rect.size.z + ")");

				byte[] bytes;

				switch (tex.format)
				{
					case TextureFormat.RGBA32:
						bytes = new byte[rect.Count*4];
						ExportRawBytes(bytes, rect.offset, rect.size, 0, 4);
						ExportRawBytes(bytes, rect.offset, rect.size, 1, 4);
						ExportRawBytes(bytes, rect.offset, rect.size, 2, 4);
						break;

					case TextureFormat.ARGB32:
						bytes = new byte[rect.Count*4];
						ExportRawBytes(bytes, rect.offset, rect.size, 1, 4);
						ExportRawBytes(bytes, rect.offset, rect.size, 2, 4);
						ExportRawBytes(bytes, rect.offset, rect.size, 3, 4);
						break;

					case TextureFormat.RGB24:
						bytes = new byte[rect.Count*3];
						ExportRawBytes(bytes, rect.offset, rect.size, 0, 3);
						ExportRawBytes(bytes, rect.offset, rect.size, 1, 3);
						ExportRawBytes(bytes, rect.offset, rect.size, 2, 3);
						break;

					case TextureFormat.R8:
						bytes = new byte[rect.Count];
						ExportRawBytes(bytes, rect.offset, rect.size, 0, 1);
						break;

					case TextureFormat.R16:
						bytes = new byte[rect.Count*2];
						ExportRaw16(bytes, rect.offset, rect.size);
						break;

					case TextureFormat.RFloat:
						bytes = new byte[rect.Count*4];
						ExportRawFloat(bytes, rect.offset, rect.size);
						break;

					default: 
						throw new Exception("Matrix export: raw texture format is not supported (" + tex.format + ")");
				}

				tex.LoadRawTextureData(bytes);
				tex.Apply();
			}


			public void ExportTextureRaw (Texture2D tex, Coord texOffset, int channel=-1)
			{
				TextureFormat format = tex.format;
				if (format!=TextureFormat.RGBA32 && format!=TextureFormat.ARGB32 && format!=TextureFormat.RGB24 && format!=TextureFormat.R8 && format!=TextureFormat.R16 && format!=TextureFormat.RFloat)
					throw new Exception("Matrix export: raw texture format is not supported");

				Coord texSize = new Coord(tex.width, tex.height);

				byte[] bytes = tex.GetRawTextureData();  //to use part of the texture and to apply only one channel

				switch(format)
				{
						case TextureFormat.RGBA32: ExportRawBytes(bytes, texOffset, texSize, channel, 4); break;
						case TextureFormat.ARGB32: channel++; if (channel == 5) channel = 0; ExportRawBytes(bytes, texOffset, texSize, channel, 4); break;
						case TextureFormat.RGB24: ExportRawBytes(bytes, texOffset, texSize, channel, 3); break;
						case TextureFormat.R8: ExportRawBytes(bytes, texOffset, texSize, 0, 1); break;
						case TextureFormat.R16: ExportRaw16(bytes, texOffset, texSize); break;
						case TextureFormat.RFloat: ExportRawFloat(bytes, texOffset, texSize); break;
				}

				tex.LoadRawTextureData(bytes);
				tex.Apply();
			}


			public void ExportColors (Color[] colors, int width, int height, int channel=-1) { ExportColors(colors, rect.offset, new Coord(width,height), channel); }
			public void ExportColors (Color[] colors, Coord colorsSize, int channel=-1) { ExportColors(colors, rect.offset, colorsSize, channel); }
			public void ExportColors (Color[] colors, Coord colorsOffset, Coord colorsSize,  int channel=-1)
			{
				if (colors.Length != colorsSize.x*colorsSize.z)
					throw new Exception("Array count does not match texture dimensions");

				CoordRect intersection = CoordRect.Intersected(rect, new CoordRect(colorsOffset, colorsSize));
				Coord min = intersection.Min; Coord max = intersection.Max;
				
				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int colorsPos = (z-colorsOffset.z)*colorsSize.x + x - colorsOffset.x;

						float val = arr[matrixPos];
						//if (float.IsNaN(val))  colors[colorsPos] = new Color(0,0,1,0);
						if (val > 1) colors[colorsPos] = new Color(0,1,0,0);
						else if (val < 0) colors[colorsPos] = new Color(1,0,0,0);
						else switch (channel)
						{
							case 0: colors[colorsPos].r = val; break;
							case 1: colors[colorsPos].g = val; break;
							case 2: colors[colorsPos].b = val; break;
							case 3: colors[colorsPos].a = val; break;
							default: colors[colorsPos].r = val; colors[colorsPos].g =val; colors[colorsPos].b = val; colors[colorsPos].a = val; break;
						}
					}
			}


			public void ExportRawBytes (byte[] bytes, int width, int height, int start, int step) { ExportRawBytes(bytes, new Coord(width,height), rect.offset, start, step); }
			public void ExportRawBytes (byte[] bytes, Coord bytesSize, int start, int step) { ExportRawBytes(bytes, bytesSize, rect.offset, start, step); }
			public void ExportRawBytes (byte[] bytes, Coord bytesOffset, Coord bytesSize, int start, int step)
			{
				if (bytes.Length != bytesSize.x*bytesSize.z*step &&  
					(bytes.Length < bytesSize.x*bytesSize.z*step*1.3f || bytes.Length > bytesSize.x*bytesSize.z*step*1.3666f)) //in case of mipmap information
						throw new Exception("Array count does not match texture dimensions");

				#if MM_NATIVE
				MatrixNativeExtensions.ExportRawBytes(this, bytes, bytes.Length, bytesOffset, bytesSize, start, step);
				#else

				CoordRect intersection = CoordRect.Intersected(rect, new CoordRect(bytesOffset, bytesSize));
				Coord min = intersection.Min; Coord max = intersection.Max;

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int bytesPos = (z-bytesOffset.z)*bytesSize.x + x - bytesOffset.x;
						bytesPos = bytesPos * step + start;

						float val = arr[matrixPos];
						bytes[bytesPos] = (byte)(val * 255f); //matrix has the range 0-1 _inclusive_, it could be 1
					}

				#endif
			}


			public void ExportRaw16 (byte[] bytes, int width, int height) { ExportRaw16(bytes, new Coord(width,height), rect.offset); }
			public void ExportRaw16 (byte[] bytes, Coord texSize) { ExportRaw16(bytes, texSize, rect.offset); }
			public void ExportRaw16 (byte[] bytes, Coord texOffset, Coord texSize)
			{
				if (texSize.x*texSize.z*2 != bytes.Length)
					throw new Exception("Array count does not match texture dimensions");

				#if MM_NATIVE
				MatrixNativeExtensions.ExportRaw16(this, bytes, bytes.Length, texOffset, texSize);
				#else

				CoordRect intersection = CoordRect.Intersected(rect, new CoordRect(texOffset, texSize));
				Coord min = intersection.Min; Coord max = intersection.Max;

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int bytesPos = (z-texOffset.z)*texSize.x + x - texOffset.x;
						bytesPos *= 2; 

						float val = arr[matrixPos]; //this[x+regionRect.offset.x, z+regionRect.offset.z];

						int intVal = (int)(val*65536);
						if (intVal>=65536) intVal = 65535;
						bytes[bytesPos] = (byte)(intVal & 0xFF);
						bytes[bytesPos+1] = (byte)(intVal>>8);
					}

				#endif
			}


			public void ExportRawFloat (byte[] bytes, int width, int height, float mult=1) { ExportRawFloat(bytes, new Coord(width,height), rect.offset, mult); }
			public void ExportRawFloat (byte[] bytes, Coord texSize, float mult=1) { ExportRawFloat(bytes, texSize, rect.offset, mult); }
			public void ExportRawFloat (byte[] bytes, Coord texOffset, Coord texSize, float mult=1)
			{
				int numPixels = texSize.x*texSize.z;
				if (numPixels*4 != bytes.Length)
					throw new Exception("Array count does not match texture dimensions");

				#if MM_NATIVE 
				MatrixNativeExtensions.ExportRawFloat(this, bytes, bytes.Length, texOffset, texSize, mult);
				#else

				CoordRect intersection = CoordRect.Intersected(rect, new CoordRect(texOffset, texSize));
				Coord min = intersection.Min; Coord max = intersection.Max;

				FloatToBytes converter = new FloatToBytes();

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int bytesPos = (z-texOffset.z)*texSize.x + x - texOffset.x;
						bytesPos *= 4; 

						if (bytesPos>=bytes.Length || bytesPos<0) Debug.Log("Test");

						converter.f = arr[matrixPos] * mult;
						bytes[bytesPos] = converter.b0;
						bytes[bytesPos+1] = converter.b1;
						bytes[bytesPos+2] = converter.b2;
						bytes[bytesPos+3] = converter.b3;
					}

				#endif
			}

			[StructLayout(LayoutKind.Explicit)]
			public class FloatToBytes
			{
				[FieldOffset(0)] public float f;
				[FieldOffset(0)] public byte b0;
				[FieldOffset(1)] public byte b1;
				[FieldOffset(2)] public byte b2;
				[FieldOffset(3)] public byte b3;
			}


			public void ExportHeights (float[,] heights) { ExportHeights(heights, rect.offset); }
			public void ExportHeights (float[,] heights, Coord heightsOffset)
			{
				Coord heightsSize = new Coord(heights.GetLength(1), heights.GetLength(0));  //x and z swapped
				CoordRect heightsRect = new CoordRect(heightsOffset, heightsSize);
				
				CoordRect intersection = CoordRect.Intersected(rect, heightsRect);
				Coord min = intersection.Min; Coord max = intersection.Max;

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int heightsPosX = x - heightsRect.offset.x;
						int heightsPosZ = z - heightsRect.offset.z;

						float val = arr[matrixPos];
						heights[heightsPosZ, heightsPosX] = val;
					}
			}

			public void ExportHeightStrips (float[][,] heights) { ExportHeightStrips(heights, rect.offset); }
			public void ExportHeightStrips (float[][,] heights, Coord heightsOffset)
			{
				//TODO: offset doesnt work
				int offset = 0;
				for (int s=0; s<heights.Length; s++)
				{
					ExportHeights(heights[s], new Coord(0,offset));
					offset+=heights[s].GetLength(0);
				}
			}

			public void ExportSplats (float[,,] splats, int channel) { ExportSplats(splats, rect.offset, channel); }
			public void ExportSplats (float[,,] splats, Coord splatsOffset, int channel)
			{
				Coord splatsSize = new Coord(splats.GetLength(1), splats.GetLength(0));  //x and z swapped
				CoordRect splatsRect = new CoordRect(splatsOffset, splatsSize);
				
				CoordRect intersection = CoordRect.Intersected(rect, splatsRect);
				Coord min = intersection.Min; Coord max = intersection.Max;

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int matrixPos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;
						int heightsPosZ = x - splatsRect.offset.x;
						int heightsPosX = z - splatsRect.offset.z;

						float val = arr[matrixPos];
						splats[heightsPosX, heightsPosZ, channel] = val;
					}
			}


			//partial terrain data (loading only the part intersecting with matrix). Do not work in thread!
			public void ExportTerrainData (TerrainData data) { ExportTerrainData (data, rect.offset, -1); }
			public void ExportTerrainData (TerrainData data, int channel) { ExportTerrainData (data, rect.offset, channel); }
			public void ExportTerrainData (TerrainData data, Coord dataOffset, int channel) //if channel is -1 getting height
			{
				int resolution = channel==-1 ?  data.heightmapResolution : data.alphamapResolution;

				Coord dataSize = new Coord(resolution, resolution);
				CoordRect dataIntersection = CoordRect.Intersected(rect, new CoordRect(dataOffset, dataSize));
				if (dataIntersection.size.x==0 || dataIntersection.size.z==0) return;

				if (channel == -1)
				{
					float[,] heights = new float[dataIntersection.size.z, dataIntersection.size.x];  //x and z swapped
					ExportHeights(heights, dataIntersection.offset);
					data.SetHeights(dataIntersection.offset.x-dataOffset.x, dataIntersection.offset.z-dataOffset.z, heights);  //while get/set has the right order
				}

				else
				{
					float[,,] splats = data.GetAlphamaps(dataIntersection.offset.x-dataOffset.x, dataIntersection.offset.z-dataOffset.z, dataIntersection.size.x, dataIntersection.size.z);
					ExportSplats(splats, dataIntersection.offset, channel);
					data.SetAlphamaps(dataIntersection.offset.x-dataOffset.x, dataIntersection.offset.z-dataOffset.z, splats);
				}
			}

		#endregion


		#region Arithmetic

			#if !MM_NATIVE

			public void Fill (float val) 
			{ 
				for (int i=0; i<count; i++) 
					arr[i] = val; 
			}

			public void Fill (Matrix m) 
			{ 
				for (int i=0; i<count; i++) 
					arr[i] = m.arr[i]; 
			}

			public void Fill (float val, float opacity)
			{
				for (int i=0; i<count; i++)
					arr[i] = arr[i]*(1-opacity) + val*opacity;
			}

			public void Mix (Matrix m, float opacity=1) 
			{ 
				float invOpacity = 1-opacity;
				for (int i=0; i<count; i++) 
					arr[i] = m.arr[i]*opacity + arr[i]*invOpacity; 
			}

			public void Mix (Matrix m, Matrix mask) 
			{ 
				for (int i=0; i<count; i++) 
					arr[i] = arr[i]*(1-mask.arr[i]) + m.arr[i]*mask.arr[i]; 
			}

			public void Mix (Matrix m, Matrix mask, float opacity) 
			{ 
				float invOpacity = 1-opacity;
				for (int i=0; i<count; i++) 
					arr[i] = arr[i]*invOpacity*(1-mask.arr[i]) + m.arr[i]*opacity*mask.arr[i]; 
			}

			public void Mix (Matrix m, Matrix mask, float maskMin, float maskMax, bool maskInvert, bool fallof, float opacity) 
			{ 
				for (int i=0; i<count; i++) 
				{
					float percent = mask.arr[i];
					
					percent = (percent-maskMin)/(maskMax-maskMin);
					if (percent<0) percent = 0; if (percent>1) percent = 1;
					//if (fallof) percent = 3*percent*percent - 2*percent*percent*percent;
					percent *= opacity;
					if (maskInvert) percent = 1-percent;

					arr[i] = arr[i]*(1-percent) + m.arr[i]*percent; 
				}
			}

			public void InvMix (Matrix m, Matrix invMask, float opacity=1) 
			//using inverted mask: mask1 will leave original value, mask0 will use m
			{ 
				float invOpacity = 1-opacity;
				for (int i=0; i<count; i++) 
					arr[i] = arr[i]*invOpacity*invMask.arr[i] + m.arr[i]*opacity*(1-invMask.arr[i]); 
			}

			public void Add (Matrix add, float opacity=1) 
			{ 
				for (int i=0; i<count; i++) 
					arr[i] += add.arr[i] * opacity; 
			}

			public void Add (Matrix add, Matrix mask, float opcaity=1) 
			{ 
				for (int i=0; i<count; i++) 
					arr[i] += add.arr[i] * mask.arr[i] * opcaity; 
			}

			public void Add (float add) 
			{
				for (int i=0; i<count; i++) 
					arr[i] += add; 
			}

			public void Blend (Matrix matrix, Matrix mask, Matrix add, Matrix addMask) 
			{ 
				for (int i=0; i<count; i++) 
				{
					float sum = mask.arr[i] + addMask.arr[i];
					 arr[i] = sum != 0 ?
						(matrix.arr[i]*mask.arr[i] + add.arr[i]*addMask.arr[i]) / sum :
						(matrix.arr[i] + add.arr[i])/2;
				}
			}

			public void Max (Matrix matrix, Matrix mask, Matrix add, Matrix addMask) 
			{ 
				for (int i=0; i<count; i++) 
					 arr[i] = mask.arr[i] > addMask.arr[i]  ?  matrix.arr[i]  :  add.arr[i];
			}

			public void Step (float mid=0.5f) 
			{
				for (int i=0; i<count; i++) 
					arr[i] = arr[i]>mid ? 1 : 0;
			}

			public void Subtract (Matrix m, float opacity=1) 
			{ 
				for (int i=0; i<count; i++) 
					arr[i] -= m.arr[i] * opacity; 
			}

			public void InvSubtract (Matrix m, float opacity=1) 
			/// subtracting this matrix from m
			{ 
				for (int i=0; i<count; i++) 
					arr[i] = m.arr[i]*opacity - arr[i]; 
			}

			public void Multiply (Matrix m, float opacity=1) 
			{
				float invOpacity = 1-opacity;
				for (int i=0; i<count; i++) 
					arr[i] *= m.arr[i]*opacity + invOpacity; 
			}

			public void Multiply (float m) 
			{ 
				for (int i=0; i<count; i++) 
					arr[i] *= m; 
			}

			public void Contrast (float m)
			/// Leaving 0.5 values untouched, and increasing/shrinking 1-0 range
			{
				for (int i=0; i<count; i++) 
				{
					float val = arr[i] - 0.5f;
					val *= m;
					arr[i] = val + 0.5f;
				}
			}

			public void Divide (Matrix m, float opacity=1) 
			{ 
				float invOpacity = 1-opacity;
				for (int i=0; i<count; i++) 
					arr[i] *= opacity/m.arr[i] + invOpacity; 
			}

			public void Difference (Matrix m, float opacity=1) 
			{ 
				for (int i=0; i<count; i++) 
				{
					float val = arr[i] - m.arr[i]*opacity;
					if (val < 0) val = -val;
					arr[i] = val;
				}
			}

			public void Overlay (Matrix m, float opacity=1)
			{
				for (int i=0; i<count; i++) 
				{
					float a = arr[i];
					float b = m.arr[i];

					b = b*opacity + (0.5f - opacity/2); //enhancing contrast via levels

					if (a > 0.5f) b = 1 - 2*(1-a)*(1-b);
					else b = 2*a*b;
					
					arr[i] = b;// b*opacity + a*(1-opacity); //the same
				}
			}

			public void HardLight (Matrix m, float opacity=1)
			/// Same as overlay but estimating b>0.5
			{
				for (int i=0; i<count; i++) 
				{
					float a = arr[i];
					float b = m.arr[i];

					if (b > 0.5f) b = 1 - 2*(1-a)*(1-b);
					else b = 2*a*b; 

					arr[i] = b*opacity + a*(1-opacity);
				}
			}

			public void SoftLight (Matrix m, float opacity=1)
			{
				for (int i=0; i<count; i++) 
				{
					float a = arr[i];
					float b = m.arr[i];
					b = (1-2*b)*a*a + 2*b*a;
					arr[i] = b*opacity + a*(1-opacity);
				}
			}

			public void Max (Matrix m, float opacity=1) 
			{ 
				for (int i=0; i<count; i++) 
				{
					float val = m.arr[i]>arr[i] ? m.arr[i] : arr[i];
					arr[i] = val*opacity + arr[i]*(1-opacity);
				}
			}

			public void Min (Matrix m, float opacity=1) 
			{ 
				for (int i=0; i<count; i++) 
				{
					float val = m.arr[i]<arr[i] ? m.arr[i] : arr[i];
					arr[i] = val*opacity + arr[i]*(1-opacity);
				}
			}

			public void Invert() 
			{ 
				for (int i=0; i<count; i++) 
					arr[i] = -arr[i]; 
			}

			public void InvertOne() 
			{ 
				for (int i=0; i<count; i++) 
					arr[i] = 1-arr[i]; 
			}

			public void SelectRange (float minFrom, float minTo, float maxFrom, float maxTo)
			/// Fill all values within min1-max0 with 1, while min0-1 and max0-1 are filled with blended
			{
				for (int i=0; i<count; i++)
				{
					float src = arr[i];
					float dst;
				
					if (src<minFrom || src>maxTo) dst = 0;
					else if (src>minTo && src<maxFrom) dst = 1;
					else
					{
						float minVal = (src-minFrom)/(minTo-minFrom);
						float maxVal = 1-(src-maxFrom)/(maxTo-maxFrom);
						dst = minVal>maxVal? maxVal : minVal;
						if (dst<0) dst=0; if (dst>1) dst=1;
					}

					arr[i] = dst;
				}
			}

			public void ChangeRange (float fromMin, float fromMax, float toMin, float toMax)
			/// Used to convert matrix from -1 1 range to 0 1 or vice versa
			{ 
				float fromRange = fromMax - fromMin;
				float toRange = toMax - toMin;

				for (int i=0; i<count; i++) 
				{
					float val = (arr[i]-fromMin) / fromRange;  //converting to 0 1
					arr[i] = val*toRange + toMin;  //converting to to
				}
			}

			public void Clamp01 ()
			{ 
				for (int i=0; i<count; i++) 
				{
					float val = arr[i];
					if (val > 1) arr[i] = 1;
					else if (val < 0) arr[i] = 0;
				}
			}

			public float MaxValue () 
			{ 
				float max=float.MinValue; 
				for (int i=0; i<count; i++) 
				{
					float val = arr[i];
					if (val > max) max = val;
				}
				return max; 
			}

			public float MinValue () 
			{ 
				float min=float.MaxValue; 
				for (int i=0; i<count; i++) 
				{
					float val = arr[i];
					if (val < min) min = val;
				}
				return min; 
			}

			public float Average ()
			{
				float avg = 0;
				for (int i=0; i<count; i++)
					avg += arr[i] / count;
				return avg;
			}

			public virtual bool IsEmpty () 
			/// Better than MinValue since it can quit if matrix is not empty
			{ 
				for (int i=0; i<count; i++) 
					if (arr[i] > 0.0001f) return false; 
				return true; 
			}

			public virtual bool IsEmpty (float delta) 
			{ 
				for (int i=0; i<count; i++) 
					if (arr[i] > delta) return false; 
				return true; 
			}

			public void BlackWhite (float mid)
			/// Sets all values bigger than mid to white (1), and those lower to black (0)
			{
				for (int i=0; i<count; i++) 
				{
					float val = arr[i];
					if (val > mid) arr[i] = 1;
					else arr[i] = 0;
				}
			}

			public void BrighnesContrast (float brightness, float contrast)
			{
				for (int i=0; i<count; i++)
				{
					float val = arr[i];
					
					val = ((val-0.5f)*contrast) + 0.5f;  //contrast
					val += brightness/2 * (contrast<1 ? 1 : contrast); //brightness  //this way brightness works in range -1 to 1 both for contrast <1 and >1
					//val += brightness/2 * (1+contrast);  //alt brightness  

					if (val<0) val = 0; if (val>1) val=1;
					arr[i] = val;
				}
			}

			public void Terrace (float[] terraces, float steepness)
			{
				float intensity = Mathf.Sqrt(steepness);

				for (int i=0; i<count; i++)
				{
					float val = arr[i];
					if (val > 0.9999f) continue;	//do nothing with values that are out of range

					int terrNum = 0;		
					for (int t=0; t<terraces.Length-1; t++)
					{
						if (terraces[terrNum+1] > val || terrNum+1 == terraces.Length) break;
						terrNum++;
					}

					//kinda curve evaluation
					float delta = terraces[terrNum+1] - terraces[terrNum];
					float relativePos = (val - terraces[terrNum]) / delta;

					float percent = 3*relativePos*relativePos - 2*relativePos*relativePos*relativePos;

					percent = (percent-0.5f)*2;
					bool minus = percent<0; percent = Mathf.Abs(percent);

					percent = Mathf.Pow(percent,1f-steepness);

					if (minus) percent = -percent;
					percent = percent/2 + 0.5f;

					float dstVal = terraces[terrNum]*(1-percent) + terraces[terrNum+1]*percent;
					arr[i] = dstVal*intensity + val*(1-intensity);
				}
			}

			public void Levels (float inMin, float inMax, float gamma, float outMin, float outMax)
			{
				float inDelta = inMax - inMin;
				float outDelta = outMax - outMin;

				for (int i=0; i<count; i++)
				{
					float val = arr[i];

					//preliminary clamping
					if (val < inMin) { arr[i] = outMin; continue; }
					if (val > inMax) { arr[i] = outMax; continue; }

					//input
					if (inDelta != 0)
						val = (val-inMin) / inDelta;
					else
						val = inMin;

					//gamma
					if (gamma>1.00001f || gamma<0.9999f)  // gamma != 1
					{
						if (gamma<1) val = Mathf.Pow(val, gamma);
						else val = Mathf.Pow(val, 1/(2-gamma));
					}

					//output
					if (outDelta != 0)
						val = outMin + val * outDelta;
					else
						val = outMin;

					arr[i] = val;
				}
			}

			public void UniformCurve (float[] lut)
			/// Applies curve that got curve lut with uniformly placed times
			/// A copy of curve's EvaluateLuted
			{
				float step = 1f / (lut.Length-1);

				for (int i=0; i<count; i++)
				{
					float val = arr[i];

					int prevNum = (int)(val/step);
					int nextNum = prevNum+1;

					if (prevNum<0) prevNum = 0; if (prevNum>=lut.Length) prevNum=lut.Length-1;
					if (nextNum<0) nextNum = 0; if (nextNum>=lut.Length) nextNum=lut.Length-1;

					float prevX = prevNum * step;
					float percent = (val-prevX) / step;

					float prevY = lut[prevNum];
					float nextY = lut[nextNum];

					arr[i] = prevY*(1-percent) + nextY*percent;
				}
			}

			#endif

			static public void ReadMatrix (Matrix src, Matrix dst, CoordRect.TileMode tileMode = CoordRect.TileMode.Clamp)
			/// Fills dst with values that are at the same position in src
			/// if coordinate is out of src bounds - reading using specified tiling
			{
				Coord tmp = new Coord(0,0);
				Coord min = dst.rect.Min; Coord max = dst.rect.Max;
				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						tmp.x = x; tmp.z=z;
						src.rect.Tile(ref tmp, tileMode);
						dst[x,z] = src[tmp];
					}
			}

			static public void BlendLayers (Matrix[] matrices, float[] opacity=null) 
			/// Changes splatmaps in photoshop layered style so their summary value does not exceed 1
			{
				CoordRect? rect = matrices.Any()?.rect;
				if (rect == null) return;

				int rectCount = rect.Value.Count;
				for (int pos=0; pos<rectCount; pos++)
				{
					float left = 1;
					for (int i=matrices.Length-1; i>=0; i--) //layer 0 is background, layer Length-1 is the top one
					{
						if (matrices[i] == null) continue;
						
						float val = matrices[i].arr[pos];

						if (opacity != null) val *= opacity[i];

						val = val * left;
						matrices[i].arr[pos] = val;
						left -= val;

						if (left < 0) break;
					}
				}
			}


			static public void NormalizeLayers (Matrix[] matrices, bool allowBelowOne=false) 
			/// Just changes splatmaps so their summary value is always 1 (or never more than 1 if allowBelowOne disabled)
			{
				CoordRect? rect = matrices.Any()?.rect;
				if (rect == null) return;

				int rectCount = rect.Value.Count;
				for (int pos=0; pos<rectCount; pos++)
				{
					float sum = 0;
					for (int i=0; i<matrices.Length; i++) sum += matrices[i].arr[pos];
					if (sum > 1f || !allowBelowOne) for (int i=0; i<matrices.Length; i++) matrices[i].arr[pos] /= sum;
				}
			}


			static public void NormalizeLayers (Matrix[] matrices, Matrix[] masks) 
			/// Just changes splatmaps so their summary value always 1
			{
				CoordRect? rect = matrices.Any()?.rect;
				if (rect == null) return;

				int rectCount = rect.Value.Count;
				for (int pos=0; pos<rectCount; pos++)
				{
					for (int i=0; i<matrices.Length; i++) matrices[i].arr[pos] *= masks[i].arr[pos];

					float sum = 0;
					for (int i=0; i<matrices.Length; i++) sum += matrices[i].arr[pos];
					if (sum > 1f) for (int i=0; i<matrices.Length; i++) matrices[i].arr[pos] /= sum;
				}
			}


			public void BlendStamped (Matrix src, Matrix stamp, float centerX, float centerZ, float radius, float transition, bool smoothFallof=true)
			/// Blends two matrices in a smooth circular way using radius and hardness
			/// Fill all values outside radius+transition with SRC, inside radius with STAMP. In most cases src is this.
			/// All values are in pixels and using matrix offset
			/// Center does not need to be the real center, it's just used to calculate fallof
			/// Hardness is the percent (0-1) of the stamp that has 100% fallof
			{
				CoordRect intersection = CoordRect.Intersected(rect, stamp.rect);
				Coord min = intersection.Min; Coord max = intersection.Max;

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						float dist = Mathf.Sqrt((x-centerX)*(x-centerX) + (z-centerZ)*(z-centerZ));

						int pos = (z-rect.offset.z)*rect.size.x + x - rect.offset.x;

						int stampPos = (z-stamp.rect.offset.z)*stamp.rect.size.x + x - stamp.rect.offset.x;
						if (dist < radius) { arr[pos] = stamp.arr[stampPos]; continue; }

						int srcPos = (z-src.rect.offset.z)*src.rect.size.x + x - src.rect.offset.x;
						//if (dist > radius+transition) { arr[pos] = src.arr[srcPos]; continue; }

						float fallof;
						if (transition == 0)
							fallof = dist>radius ? 0 : 1;
						else
						{
							fallof = 1 - (dist-radius) / transition;
							if (fallof>1) fallof = 1; if (fallof<0) fallof = 0;
							if (smoothFallof) fallof = 3*fallof*fallof - 2*fallof*fallof*fallof;
						}

						arr[pos] = src.arr[pos]*(1-fallof) + stamp.arr[stampPos]*fallof;
					}
			}


		#endregion


		#region Histogram

			public float[] Histogram (int resolution, float max=1, bool normalize=true)
			/// Evaluates all pixels in matrix and returns an array of pixels count by their value. 
			{
				float[] quants = new float[resolution];

				for (int i=0; i<count; i++)
				{
					float val = arr[i];
					if (val<0 || val>max) continue; //out of histogram range

					float percent = val / max;
					int num = (int)(percent*resolution);
					if (num==resolution) num--; //this could happen when val==max

					quants[num]++;
				}

				if (normalize)
				{
					float maxQuant = 0;

					for (int i=0; i<resolution; i++)
						if (quants[i] > maxQuant) maxQuant = quants[i];

					for (int i=0; i<resolution; i++)
						quants[i] /= maxQuant;
				}

				return quants;
			}


			public byte[] HistogramBytes (int resolution, float max=1, bool normalize=true)
			/// Just as Histogram, but returns bytes array ready to convert to texture
			{
				float[] quants = Histogram(resolution, max, normalize);
				byte[] bytes = new byte[quants.Length];

				for (int i=0; i<quants.Length; i++)
					bytes[i] = (byte)(quants[i]*255);

				return bytes;
			}


			public static byte[] HistogramToTextureBytes (float[] quants, int height, byte empty=0, byte top=255, byte filled=128)
			/// Converts an array from Histogram to texture bytes
			{
				int width = quants.Length;
				byte[] bytes = new byte[width * height];

				for (int x=0; x<width; x++)
				{
					int max = (int)(quants[x] * height);
					if (max==height) max--;

					for (int z=0; z<height; z++)
					{
						byte val = empty;
						if (z==max) val=top;
						else if (z<max) val=filled;

						bytes[z*width + x] = val; 
					}
				}

				return bytes;
			}

			public static Texture2D HistogramToTextureR8 (float[] quants, int height, byte empty=0, byte top=255, byte filled=128)
			/// Converts an array from Histogram to texture (format R8)
			{
				byte[] bytes = HistogramToTextureBytes(quants, height, empty, top, filled);
				
				Texture2D tex = new Texture2D(quants.Length, height, TextureFormat.R8, false, linear:true);
				tex.LoadRawTextureData(bytes);
				tex.Apply(updateMipmaps:false);

				return tex;
			}

			public byte[] HistogramTextureBytes (int width, int height, byte empty=0, byte top=255, byte filled=128)
			{
				float[] quants = Histogram(width);
				byte[] bytes = HistogramToTextureBytes(quants, height, empty, top, filled);
				return bytes;
			}

		#endregion


		#region Editor Events

			public static Action<Matrix,string> onPreview;  //manually called to announce MatrixWindow and other editor stuff. String is window name/id

			public void ToWindow (string name, bool useCopy=false, bool mainThread=false) 
			{ 
				Matrix matrix = useCopy ? new Matrix(this) : this;

				if (!mainThread)
					onPreview?.Invoke(matrix,name);
				else
					Den.Tools.Tasks.CoroutineManager.Enqueue(() => onPreview?.Invoke(matrix,name) ); 
			}

		#endregion


		#region Other

			public void Line (Vector2 start, Vector2 end, float valStart=1, float valEnd=1, bool antialised=false, bool paddedOnePixel=false, bool endInclusive=false)
			/// Strokes the line from start (inclusive) to end (non-inclusive), gradientally filling it with valStart to valEnd. Antialiased adds 2 pixels to line width.
			/// PaddedOnePixel works similarly to AA, but fills border pixels with full value (to create main tex for the mask)
			{
				int numSteps = Mathf.Max(
					Mathf.Abs( Mathf.FloorToInt(end.x) - Mathf.FloorToInt(start.x) ),  //NOT (int)(end-start)!
					Mathf.Abs( Mathf.FloorToInt(end.y) - Mathf.FloorToInt(start.y) )  );

				float stepX = (end.x-start.x) / numSteps;
				float stepZ = (end.y-start.y) / numSteps;

				bool isVertical = Mathf.Abs(stepZ) > Mathf.Abs(stepX); //for antializasing

				int ei = endInclusive ? 1 : 0;
				for (int s=0; s<numSteps+ei; s++)
				{
					float fx = start.x + stepX*s - rect.offset.x;  //neglecting matrix rect, should be from 0 to matrix.rect.size
					float fz = start.y + stepZ*s - rect.offset.z;

					int ix = (int)(float)fx;  //if (fx<0) ix--;  
					int iz = (int)(float)fz;  //if (fz<0) iz--;  

					if (ix<0 || ix>rect.size.x-1 ||
						iz<0 || iz>rect.size.z-1 )
							continue;

					int pos = iz*rect.size.x + ix;

					float valPercent = 1f*s/numSteps;
					float val = valStart*(1-valPercent) + valEnd*valPercent;

					arr[pos] = val;

					if (antialised)
					{
						if (!isVertical)
						{
							if (iz<1 || iz>rect.size.z-2) continue;
							float p = fz-iz;
							arr[pos-rect.size.x] = arr[pos-rect.size.x]*p + val*(1-p); //blending commented out since it create semas while Stroke
							arr[pos+rect.size.x] = arr[pos+rect.size.x]*(1-p) + val*p;
						}
						else
						{
							if (ix<1 || ix>rect.size.x-2) continue;
							float p = fx-ix;
							arr[pos-1] = arr[pos-1]*p + val*(1-p);
							arr[pos+1] = arr[pos+1]*(1-p) + val*p;
						}
					}

					if (paddedOnePixel)
					{
						if (!isVertical)
						{
							if (iz<1 || iz>rect.size.z-2) continue;
							arr[pos-rect.size.x] = val;
							arr[pos+rect.size.x] = val;
						}
						else
						{
							if (ix<1 || ix>rect.size.x-2) continue;
							arr[pos-1] = val;
							arr[pos+1] = val;
						}
					}
				}
			}


			public void Read (Matrix src, CoordRect rect)
			{
				CoordRect intersection = CoordRect.Intersected(rect, this.rect);
				intersection = CoordRect.Intersected(src.rect, this.rect);

				Coord min = intersection.Min; Coord max = intersection.Max;

				for (int x=min.x; x<max.x; x++)
					for (int z=min.z; z<max.z; z++)
					{
						int thisPos = (z-this.rect.offset.z)*this.rect.size.x + x - this.rect.offset.x;
						int srcPos = (z-src.rect.offset.z)*src.rect.size.x + x - src.rect.offset.x;

						arr[thisPos] = src.arr[srcPos];
					}
			}

			public void Read (Matrix src) { Read(src, rect); }

		#endregion
	}
}