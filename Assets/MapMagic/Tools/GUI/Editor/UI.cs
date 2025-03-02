﻿using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Runtime.InteropServices;
using UnityEngine.Profiling;

namespace Den.Tools.GUI
{
	public class UI
	{
		public static UI current;

		public Cell rootCell;
		public List<(Action action, int order)> afterLayouts = new List<(Action,int)>();
		public List<(Action action, int order)> afterDraws = new List<(Action,int)>();
		public bool layout;

		public ScrollZoom scrollZoom = null;
		public StylesCache styles = null;
		public TexturesCache textures = new TexturesCache();
		public CellObjs cellObjs = new CellObjs();
		public Undo undo = null;

		public EditorWindow editorWindow;

		public Rect subWindowRect;   
		public Vector2 viewRectMax = new Vector2(int.MaxValue, int.MaxValue); //window rect in internal coordinates (to optimize cells)
		public Vector2 viewRectMin = new Vector2(-int.MaxValue/2, -int.MaxValue/2); //min is 0 when no scrollzoom, but when scrolled it change it's value
		public Vector2 mousePos; //mouse position in internal coordinates
		public int mouseButton = -1; //shortcut for Event.current.button
		public float ViewRectHeight { get{ return viewRectMax.y-viewRectMin.y; } }

		public bool optimizeEvents = false;
		public bool optimizeElements = false; //skips cell if it has no child cells
		public bool optimizeCells = false; //skips cell if it has child cells. Experimental!

		public bool hardwareMouse = false;

		public bool isInspector = false; //to draw foldout
		public Vector2 scrollBarPos; //for windows with the scrollbar

		public float DpiScaleFactor 
		{get{
			float factor = Screen.width / editorWindow.position.width;
			return ((int)(float)(factor * 4f + 0.5f)) / 4f; //rounding to 0.25f
		}}

		public enum RectSelector { Standard, Padded, Full }


		public static UI ScrolledUI (float maxZoom=1, float minZoom=0.4375f)
		{
			return new UI {
				scrollZoom = new ScrollZoom() { allowScroll=true, allowZoom=true, maxZoom = maxZoom, minZoom = minZoom },
				optimizeEvents = true,
				optimizeElements = true };
		}


		#region Draw

			public void DrawInSubWindow (Action drawAction, int id, Rect rect, bool usePadding=false)
			/// Draws in unity's BeginWindows group
			{
				this.subWindowRect = rect;

				//rect.position = new Vector2(0,0);

				UnityEngine.GUI.WindowFunction drawFn = tmp => Draw(drawAction);

				//hack. GUILayout.Window will not be called when mouse is not in window rect, but we have to release drag somehow
				if (Event.current.rawType == EventType.MouseUp)
				{
						Event.current.mousePosition -= rect.position; //offseting release button mouse position since it counts window offset as 0
						Draw(drawAction);
				}

				else
				{
					//placing 2 rects in _this_ window if GraphGUI was not called
					UnityEditor.EditorGUILayout.GetControlRect(GUILayout.Height(0));
					UnityEditor.EditorGUILayout.GetControlRect(GUILayout.Height(0));
				}

				//window
				GUILayout.Window(id, rect, drawFn, new GUIContent(), GUIStyle.none, 
					GUILayout.MaxHeight(rect.height), GUILayout.MinHeight(rect.height), 
					GUILayout.MaxWidth(rect.width), GUILayout.MinWidth(rect.width) );
			}

			

			public void DrawScrollable (Action action, RectSelector rectSelector=RectSelector.Standard)
			/// Draws in full window, no offsets or scrollbars
			/// usePadding offsets 3 pixels from the window borders
			{
				scrollBarPos = EditorGUILayout.BeginScrollView(scrollBarPos);

				Draw(action, rectSelector:rectSelector);

				EditorGUILayout.EndScrollView();
			}


			public void Draw (Action drawAction, RectSelector rectSelector=RectSelector.Standard, bool offsetAfterDraw=true)
			/// If calling two Draw instances in one window one should have offsetAfterDraw enabled (otherwise will not mouseUp)
			{
				Profiler.BeginSample("DrawUI");

				UI.current = this;

				afterLayouts.Add((drawAction, 0));
				afterDraws.Add((drawAction, 0));

				editorWindow = GetActiveWindow();

				//finding rect
				UnityEditor.EditorGUI.indentLevel = 0;
				Rect rect;
				switch (rectSelector)
				{
					case RectSelector.Standard: rect = GUILayoutUtility.GetRect(new GUIContent(), GUIStyle.none); break;
					case RectSelector.Padded: rect = GUILayoutUtility.GetRect(new GUIContent(),  EditorStyles.helpBox); break;
					case RectSelector.Full: rect = new Rect(0,0,Screen.width, Screen.height); break;
					default: rect = new Rect(0,0,0,0); break;
				}
				//Rect rect = GUILayoutUtility.GetRect(new GUIContent(), usePadding? EditorStyles.helpBox : GUIStyle.none);
				//Rect rect = GUILayoutUtility.GetLastRect();

				//repaint on mouse up - for buttons, drag, all that stuff
				//if (Event.current.rawType == EventType.MouseUp  &&  editorWindow != null)
				//	editorWindow.Repaint();

				//scroll/zoom
				if (scrollZoom != null)
				{
					scrollZoom.Scroll();
					scrollZoom.Zoom();
				}
				

				//styles
				if (styles == null) 
					styles = new StylesCache();
				styles.CheckInit();
				if (scrollZoom != null) 
					styles.Resize(scrollZoom.zoom);
			
				//mouse button
				if (Event.current.type == EventType.MouseDown)
					mouseButton = Event.current.button;
				else
					mouseButton = -1;

				//mouse pos
				#if UNITY_EDITOR_WIN
				if (hardwareMouse)
				{
					GetCursorPos(out Vector2Int intPos);
					mousePos = intPos - editorWindow.position.position;
				}
				else 
				#endif
					mousePos = Event.current.mousePosition;

				//internal rect
				if (scrollZoom != null)
				{
					viewRectMin = scrollZoom.ToInternal( new Vector2(0,0) ) - Vector2.one;
					viewRectMax = scrollZoom.ToInternal( new Vector2(Screen.width, Screen.height) ) + Vector2.one;
					mousePos = scrollZoom.ToInternal(mousePos);
				}
				else
				{
					viewRectMin = new Vector2(0, 0);
					viewRectMax = new Vector2(Screen.width, Screen.height);
				}

				//preparing shaders
				Shader.SetGlobalVector("_ScreenRect", new Vector4(rect.x, rect.y, Screen.width, Screen.height) );
				Shader.SetGlobalVector("_ScreenParams", new Vector4(Screen.width, Screen.height, 1f/Screen.width, 1f/Screen.height) );
				Shader.SetGlobalVector("_InternalRect", new Vector4(viewRectMin.x, viewRectMin.y, viewRectMax.x-viewRectMin.x, viewRectMax.y-viewRectMin.y) );

				//clearing active cell stack in case previous gui was failed to finish (or color picker clicked)
				if (Cell.activeStack.Count != 0)
				{
					Cell.activeStack.Clear();
					//Debug.Log("Trying to start UI with non-empty active stack");  
				}
			
			
				//drawing
				if (!optimizeEvents || !SkipEvent())
				//using (Timer.Start("Draw GUI"))
				{
					layout = true;
					//using (Timer.Start("Draw pre-layout"))
						using (Cell.Root(ref rootCell, rect))
						{
							for (int i=0; i<afterLayouts.Count; i++) //count could be increased while iterating
								afterLayouts[i].action();
						}

					//using (Timer.Start("CalculateMinContentsSize"))
						rootCell.CalculateMinContentsSize();

					//using (Timer.Start("CalculateRootRects"))
						rootCell.CalculateRootRects();

					layout = false;
					//using (Timer.Start("Draw final"))
						using (Cell.Root(ref rootCell, rect))
						{
							for (int i=0; i<afterDraws.Count; i++) //count could be increased while iterating
								afterDraws[i].action();
						}

					UI.current = null;
				}

				DragDrop.ResetTempObjs();

				//resetting afterdraw actions
				afterLayouts.Clear();
				afterDraws.Clear();

				cellObjs.Clear();

				//setting inspector/window rect
				if (offsetAfterDraw)
				{
					float inspectorHeight = rootCell!=null ? (float)rootCell.finalSize.y : 0;
					inspectorHeight -= 20; //Unity leaves empty space for some reason
					Rect wholeRect = UnityEditor.EditorGUILayout.GetControlRect(GUILayout.Height(inspectorHeight));

					UnityEngine.GUI.Button(wholeRect, "", GUIStyle.none); 
					//drawing any control on all the field, otherwise OnMouseUp won't be called when mouse left the window
					//TODO: whole rect doesnt cover all for some reason
				}

				Profiler.EndSample();
			}

			public void DrawAfter (Action action, int layer=1)
			{
				if (layout)
				{
					afterLayouts.Add( (action,layer) );
					afterLayouts.Sort( (a,b) => a.order - b.order );
				}
				else
				{
					
					afterDraws.Add( (action,layer) );
					afterDraws.Sort( (a,b) => a.order - b.order );
				}

				//Debug.Log("Layouts add " + afterLayouts.Count);
				//Debug.Log("Draws add " + afterDraws.Count);
			}

			public void ClearDrawAfter ()
			{
				if (layout)
					afterLayouts.Clear();
				else
					afterDraws.Clear();
			}

		#endregion


		#region Helpers

			public static bool SkipEvent ()
			/// Should this event be skipped?
			{
				bool skipEvent = false;

				if (Event.current.type == EventType.Layout  ||  Event.current.type == EventType.Used) skipEvent = true; //skip all layouts
				if (Event.current.type == EventType.MouseDrag) //skip all mouse drags (except when dragging text selection cursor in field)
				{
					if (!UnityEditor.EditorGUIUtility.editingTextField) skipEvent = true;
					if (UnityEngine.GUI.GetNameOfFocusedControl() == "Temp") skipEvent = true; 
				}
				if (Event.current.rawType == EventType.MouseUp) skipEvent = false;

				return skipEvent;
			}


			public bool IsInWindow ()
			/// Finding if cell within a window by it's rect
			{
				Cell cell = Cell.current;

				float borders = 1;

				//Vector2 cellRectPos = cell.worldPosition;
				//Vector2 cellRectSize = cell.finalSize;

				float minX = cell.worldPosition.x;
				float maxX = cell.worldPosition.x + cell.finalSize.x;

				float minY = cell.worldPosition.y;
				float maxY = cell.worldPosition.y + cell.finalSize.y;

				if (maxX < viewRectMin.x - borders||
					maxY < viewRectMin.y - borders ||
					minX > viewRectMax.x + borders ||
					minY > viewRectMax.y + borders)
						return false;

				return true;
			}


			public bool IsInWindow (float minX, float maxX, float minY, float maxY)
			/// Finding if cell within a window by it's rect
			{
				Cell cell = Cell.current;

				float borders = 1;

				if (maxX < viewRectMin.x - borders||
					maxY < viewRectMin.y - borders ||
					minX > viewRectMax.x + borders ||
					minY > viewRectMax.y + borders)
						return false;

				return true;
			}


			public static void RemoveFocusOnControl ()
			/// GUI.FocusControl(null) is not reliable, so creating a temporary control and focusing on it
			{
				//UnityEngine.GUI.SetNextControlName("Temp");
				//UnityEditor.EditorGUI.FloatField(new Rect(-10,-10,0,0), 0);
				//UnityEngine.GUI.FocusControl("Temp");

				UnityEngine.GUI.FocusControl(null);
			}


			public static void RepaintAllWindows ()
			/// Usually called on undo
			{
				UnityEditor.EditorWindow[] windows = Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>();
				foreach (UnityEditor.EditorWindow win in windows)
					win.Repaint();
			}


			public void MarkChanged (bool completeUndo=false)
			/// Writes undo and cell change. Should be called BEFORE actual change since writes undo
			{
				//write undo and dirty (got to know undo object to set it dirty)
				undo?.Record(completeUndo);

				//writing changed state in all active cells
				for (int i=Cell.activeStack.Count-1; i>=0; i--)
				{
					if (!Cell.activeStack[i].trackChange) break; //root cell should not recieve value change if non-tracked cell changed
					Cell.activeStack[i].valChanged = true;
				}

			}


			private static string[] GetPopupNames<T> (T[] objs, Func<T,string> nameFn, string none=null, string[] names=null)
			/// Generates names array for popups. Use 'none' to place it before other variants. Use 'names' to re-use array.
			{
				int arrLength = objs.Length;
				if (none != null) arrLength++;

				if (names == null || names.Length != arrLength)
					names = new string[arrLength];

				int c = 0;
				for (int i=0; i<arrLength; i++)
				{
					if (i==0 && none!=null) { names[0] = none; continue; }
					names[i] = nameFn(objs[c]);
					c++;
				}

				return names;
			}


			public static Texture2D GetBlankTex ()
			{
				Texture2D tex = new Texture2D(4,4);
				Color[] colors = tex.GetPixels();
				for (int i=0; i<colors.Length; i++) colors[i] = new Color(0,0,0,1);
				tex.SetPixels(colors);
				tex.Apply(true, true);
				return tex;
			}


			[DllImport("user32.dll")]
			public static extern bool GetCursorPos(out Vector2Int lpPoint);

			[DllImport("user32.dll")]
			public static extern bool SetCursorPos(int x, int y);


			public static EditorWindow GetActiveWindow ()
			{
				//HostView hostView = GUIView.current as HostView;
				//return hostView.actualView;

				Type guiViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
				PropertyInfo currentGuiViewProp = guiViewType.GetProperty("current", BindingFlags.Static | BindingFlags.Public);
				object currentGuiView = currentGuiViewProp.GetValue(guiViewType, null);
				if (currentGuiView == null) return null;

				Type hostViewType = currentGuiView.GetType(); //could be DockArea, which also has a actualView property
				//Type hostViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.HostView");
				//if (currentGuiView.GetType() != hostViewType) return null;
				PropertyInfo actualViewProp = hostViewType.GetProperty("actualView", BindingFlags.Instance | BindingFlags.NonPublic);
				object activeView = actualViewProp.GetValue(currentGuiView);

				return activeView as EditorWindow;
			}

		#endregion
	}
}
