﻿#if MAPMAGIC2 //shouldn't work if MM assembly not compiled

using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

using Den.Tools;
using Den.Tools.GUI;
using MapMagic.Core;  //used once to get tile size
using MapMagic.Products;
using MapMagic.Nodes.MatrixGenerators;


namespace MapMagic.Nodes.GUI
{

	public static class MegaSplatEditor
	{
		[UnityEditor.InitializeOnLoadMethod]
		static void EnlistInMenu ()
		{
			CreateRightClick.generatorTypes.Add(typeof(MegaSplatOutput200));
		}

		[Draw.Editor(typeof(MatrixGenerators.MegaSplatOutput200))]
		public static void DrawMegaSplat (MatrixGenerators.MegaSplatOutput200 gen)
		{ 
			#if !__MEGASPLAT__
			using (Cell.LinePx(60))
				Draw.Helpbox("MicroSplat doesn't seem to be installed, or MicroSplat compatibility is not enabled in settings");
			#endif
			if (GraphWindow.current.mapMagic != null)
			{
				using (Cell.LineStd)
				{
					GeneratorDraw.DrawGlobalVar(ref GraphWindow.current.mapMagic.terrainSettings.material, "Material");

					if (Cell.current.valChanged)
						GraphWindow.current.mapMagic.ApplyTerrainSettings();
				}

				using (Cell.LineStd) 
				{
					#if __MEGASPLAT__
					MegaSplatTextureList texList = GraphWindow.current.mapMagic.globals.megaSplatTexList as MegaSplatTextureList;
					GeneratorDraw.DrawGlobalVar(ref texList, "TexList");
					GraphWindow.current.mapMagic.globals.megaSplatTexList = texList;
					#endif
				}
			
			}

			using (Cell.LinePx(0)) CheckShader(gen);
		}


		[Draw.Editor(typeof(MatrixGenerators.MegaSplatOutput200.MegaSplatLayer))]
		public static void DrawMegaSplatLayer (MatrixGenerators.MegaSplatOutput200.MegaSplatLayer layer, object[] arguments)
		{
			int num = (int)arguments[0];
			MatrixGenerators.MegaSplatOutput200 gen = (MatrixGenerators.MegaSplatOutput200)arguments[1];
			if (layer == null) return;

			#if __MEGASPLAT__
			MegaSplatTextureList textureList = GraphWindow.current.mapMagic?.globals.megaSplatTexList as MegaSplatTextureList;
			#endif

			Cell.EmptyLinePx(3);
			using (Cell.LinePx(28))
			{
				//Cell.current.margins = new Padding(0,0,0,1); //1-pixel more padding from the bottom since layers are 1 pixel overlayed

				if (num!=0) 
					using (Cell.RowPx(0)) GeneratorDraw.DrawInlet(layer, gen);
				else 
					//disconnecting last layer inlet
					if (GraphWindow.current.graph.IsLinked(layer))
						GraphWindow.current.graph.UnlinkInlet(layer);

				Cell.EmptyRowPx(10);

				//icon
				Texture2DArray icon = null;
				int index = -2;

				Material material = null;
				if (GraphWindow.current.mapMagic != null)
					material = GraphWindow.current.mapMagic.terrainSettings.material;

				if (material != null && material.HasProperty("_Diffuse"))
					icon = (Texture2DArray)material?.GetTexture("_Diffuse");

				#if __MEGASPLAT__
				if (textureList != null)
					//icon = textureList.clusters[num].previewTex; //preview textures doesnt seem to be working in recent versions
					index = textureList.clusters[num].indexes[0];
				#endif

				using (Cell.RowPx(28)) 
				{
					if (icon != null && index >= 0) 
						Draw.TextureIcon(icon, index);
				}

				//channel
				Cell.EmptyRowPx(3);
				using (Cell.Row)
				{
					Cell.EmptyLine();
					using (Cell.LineStd)
					{
						Cell.current.fieldWidth = 0.4f;
						#if __MEGASPLAT__
						if (textureList!=null)
							Draw.PopupSelector(ref layer.channelNum, textureList.textureNames);
						else
							Draw.Field(ref layer.channelNum, "Channel");
						#else
						Draw.Field(ref layer.channelNum, "Channel");
						#endif
					}
					Cell.EmptyLine();
				}

				Cell.EmptyRowPx(10);
				using (Cell.RowPx(0)) GeneratorDraw.DrawOutlet(layer);
			}
			Cell.EmptyLinePx(3);
		}


		public static void CheckShader (MegaSplatOutput200 gen)
		{
			if (GraphWindow.current.mapMagic == null) return;

			Material mat = GraphWindow.current.mapMagic.terrainSettings.material;
			if (mat==null || !mat.shader.name.Contains("MegaSplat"))
			{
				using (Cell.LinePx(50))
					using (Cell.Padded(3))
						Draw.Helpbox("The assigned material is not MegaSplat", UnityEditor.MessageType.Error);
			}
		}

		public static void CheckCustomSplatmaps (MegaSplatOutput200 gen)
		{
			if (GraphWindow.current.mapMagic == null) return;

			Material mat = GraphWindow.current.mapMagic.terrainSettings.material;
			if (mat != null && !mat.HasProperty("_CustomControl0"))
			{
				using (Cell.LinePx(60))
					using (Cell.Padded(3))
						Draw.Helpbox("Use Custom Splatmaps is not enabled in the material", UnityEditor.MessageType.Error);
			}
		}
	}
}

#endif