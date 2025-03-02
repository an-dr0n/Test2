﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection;
using UnityEngine;
using UnityEditor;

using Den.Tools;
using Den.Tools.Matrices;
using Den.Tools.Matrices.Window;
using Den.Tools.Splines;
using Den.Tools.Tasks;
using Den.Tools.GUI;

using MapMagic.Core;
using MapMagic.Products;
using MapMagic.Nodes;
using MapMagic.Terrains;

using MapMagic.Nodes.GUI;

namespace MapMagic.Previews
{
	public enum PreviewStage { Blank, Generating, Ready }

	public interface IPreview
	{
		void SetObject (IOutlet<object> outlet, TileData data);
		void Clear ();
		
		PreviewStage Stage { get; }

		void DrawInGraph ();

		BaseMatrixWindow Window { get; set; }
		BaseMatrixWindow CreateWindow ();

		Terrain Terrain { get; }
		void ToTerrain (Terrain terrain);
		void ClearTerrain ();
	}

	public interface IPreviewType<T> where T: class { }


	public static class PreviewManager
	{
		private static Dictionary<IOutlet<object>,IPreview> all = new Dictionary<IOutlet<object>,IPreview>(); //TODO: make weak reference
		//public static ConditionalWeakTable<IOutlet,Preview> all = new ConditionalWeakTable<IOutlet,Preview>();
		
		public  static MatrixPreview heightOutputPreview = new MatrixPreview();


		public static IPreview GetPreview (IOutlet<object> outlet)
		{
			if (all.TryGetValue(outlet, out IPreview preview)) return preview;
			else return null;
		}


		public static IPreview CreatePreview (IOutlet<object> outlet)
		{
			IPreview preview = null; 
			if (outlet is IOutlet<MatrixWorld>) preview = new MatrixPreview();
			else if (outlet is IOutlet<TransitionsList>) preview = new ObjectsPreview();
			else if (outlet is IOutlet<SplineSys>) preview = new SplinePreview();
			else return null;

			if (all.ContainsKey(outlet)) all[outlet] = preview;
			else all.Add(outlet, preview);

			return preview;
		}


		public static void SetObject (IOutlet<object> outlet, TileData data)
		{
			if (all.TryGetValue(outlet, out IPreview preview))
				preview.SetObject(outlet, data);
		}


		public static void SetObjectsAll (TileData data)
		{
			foreach (var kvp in all)
			{
				IOutlet<object> outlet = kvp.Key;
				IPreview preview = kvp.Value;

				preview.SetObject(outlet, data);
			}
		}

		public static void Clear (IOutlet<object> outlet)
		{
			if (all.TryGetValue(outlet, out IPreview preview))
				preview.Clear();
		}

		public static void ClearAll ()
		{
			foreach (IPreview preview in all.Values)
				preview.Clear();
		}



		public static void RemoveAllFromTerrain ()
		{
			foreach (IPreview preview in all.Values)
				preview.ClearTerrain();
		}

		public static void ChangeTerrain (Terrain newTerrain)
		{
			foreach (IPreview preview in all.Values)
				if (preview.Terrain != null)
				{
					preview.ClearTerrain();
					preview.ToTerrain(newTerrain);
				}
		}


		public static BaseMatrixWindow GetCreateWindow (IPreview preview)
		/// Tries to find opened window by preview and updates it, and opens new window if non is found
		{
			IOutlet<object> outlet = PreviewManager.GetOutlet(preview);

			Guid previewGuid = outlet.Gen.Guid;

			BaseMatrixWindow[] windows = Resources.FindObjectsOfTypeAll<BaseMatrixWindow>();

			BaseMatrixWindow window = null;
			for (int i=0; i<windows.Length; i++)
			{
				if (windows[i] is IPreviewWindow serGuid  &&  serGuid.SerializedGenGuid == previewGuid) 
					{ window = windows[i]; break; }
			}

			if (window == null)
			{
				window = preview.CreateWindow();
				if (window is IPreviewWindow serGuid) serGuid.SerializedGenGuid = previewGuid;
				window.ShowTab();
			}

			window.Show();

			return window;
		}


		public static IPreview Get (Guid genGuid)
		{
			foreach (var kvp in all)
				if (kvp.Key.Gen.Guid == genGuid)
					return kvp.Value;
			return null;
		}

		public static IOutlet<object> GetOutlet (IPreview preview)
		{
			foreach (var kvp in all)
				if (kvp.Value == preview)
					return kvp.Key;
			return null;
		}


		[RuntimeInitializeOnLoadMethod, UnityEditor.InitializeOnLoadMethod] 
		static void LoadPreviews ()
		/// Loads all of the previews for current MapMagic object in scene
		/// And finds proper windows to all of the previews
		{
			MapMagicObject mapMagic = GameObject.FindObjectOfType<MapMagicObject>();
			if (mapMagic == null  ||  mapMagic.graph==null) return;
			foreach (Generator gen in mapMagic.graph.generators)
				if (gen.guiPreview  &&  gen is IOutlet<object> outlet)
					CreatePreview(outlet);

			BaseMatrixWindow[] windows = Resources.FindObjectsOfTypeAll<BaseMatrixWindow>();
			foreach (BaseMatrixWindow window in windows)
			{
				if (!(window is IPreviewWindow previewWindow)) continue;

				Guid guid = previewWindow.SerializedGenGuid;

				foreach (var kvp in all)  
					if (kvp.Key.Gen.Guid == guid) 
					{
						kvp.Value.Window = window;
						previewWindow.Preview = kvp.Value;
					}
			}
		}


		[RuntimeInitializeOnLoadMethod, UnityEditor.InitializeOnLoadMethod] 
		static void Subscribe ()
		{
			/// Assigning height preview on height change
			Graph.OnBeforeOutputFinalize += (type, data, applyData, stop) =>
			{
				if (data.isPreview && typeof(Nodes.MatrixGenerators.HeightOutput200).IsAssignableFrom(type))
					heightOutputPreview.SetObject(data.heights, data.area);
			};

			/// Starting to generate a preview on node change
			/// Note this is called in thread
			Graph.OnAfterNodeGenerated += (gen, data) =>
			{
				if (data.isPreview  &&  gen is IOutlet<object> outlet)
					SetObject(outlet, data);
			};

			/// When node cleared - disabling n/a mark
			Graph.OnBeforeNodeCleared += (gen, data) =>
			{
				if (!data.isPreview) return;

				if (gen is Nodes.MatrixGenerators.HeightOutput200)
					heightOutputPreview?.Clear();

				if (gen is IOutlet<object> outlet)
					Clear(outlet);
			};

			/// Forces all preview to re-generate on selecting new preview tile
			TerrainTile.OnPreviewAssigned += (data) =>
			{
				if (data != null && !data.isPreview)
				{
					heightOutputPreview.SetObject(data.heights, data.area);
					SetObjectsAll(data);
				}

				ChangeTerrain(GraphWindow.current.mapMagic.PreviewTile.ActiveTerrain);
			};
		}
	}

	public interface IPreviewWindow
	{
		Guid SerializedGenGuid { get; set; }  //to keep references of windows to preview on code re-compile
		IPreview Preview { get; set; }
	}
}
