#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DMB
{

public class CameraPathEdit
{
	private int _selectedPath;
	private int _selectedIndex;
	private Vector3 _camLookat;
	private Vector3 _camPos;
	public static Camera FallbackCamera { get {
		if ( Camera.main == null ) {
			GameObject go = new GameObject();
			go.AddComponent<Camera>();
			go.tag = "MainCamera";
			go.name = "Main Camera";
		}
		return Camera.main;
	} }

	private void DrawCurve( Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, float alpha, bool drawHandles, float thickness = 2, Texture2D tex = null )
	{
		if ( drawHandles ) {
			Handles.color = new Color( Color.gray.r, Color.gray.g, Color.gray.b, alpha );
			//Handles.color = new Color( 1, 1, 0, alpha );
			Handles.DrawAAPolyLine( tex, thickness, new Vector3 [] { p0, p1 } ); 
			Handles.DrawAAPolyLine( tex, thickness, new Vector3 [] { p2, p3 } ); 
		}
		Color c = SWUI.CCol( new Color( color.r, color.g, color.b, alpha ) );
		Handles.DrawBezier( p0, p3, p1, p2, c, tex, thickness);
	}	

	private void DrawDeselectedPath( Vector3 [] path, Color color, int pathId, float thickness ) 
	{
		for ( int i = 0; i < path.Length - 3; i += 3 ) {
			DrawCurve( path[i + 0], path[i + 1], path[i + 2], path[i + 3], 
					color, 0.3f, false, thickness );
		}
		for ( int i = 0; i < path.Length; i += 3 ) {
			Vector3 selPt = path[i];
			Vector3 newPt;
			if ( SWUI.LinePoint( selPt, false, false, out newPt ) ) {
				SelectPath( i, pathId );
			}
		}
	}

	private static bool IsTangentHandle( int index ) {
		return index % 3 != 0;
	}

	private bool DrawSelectedPath( Vector3 [] path, Color color, bool moveWholeSpline,
									float thickness = 2, Texture2D tex = null ) 
	{
		bool pathChanged = false;
		for ( int i = 0; i < path.Length - 3; i += 3 ) {
			DrawCurve( path[i + 0], path[i + 1], path[i + 2], path[i + 3], 
					color, 1, true, thickness, tex );
		}
		for ( int i = 0; i < path.Length; i++ ) {
			Vector3 selPt = path[i];
			EditorGUI.BeginChangeCheck();
			Vector3 newPt;
			if ( SWUI.LinePoint( selPt, true, moveWholeSpline, out newPt, 
						selectedIndex: i == _selectedIndex, isTangent: IsTangentHandle( i ) ) ) {
				_selectedIndex = i;
			}
			if ( EditorGUI.EndChangeCheck() ) {
				if ( moveWholeSpline ) {
					Vector3 d = newPt - selPt;
					for ( int j = 0; j < path.Length; j++ ) {
						path[j] += d;
					}
				} else {
					if ( i % 3 == 0 ) {
						Vector3 dPrev = i == 0 ? Vector3.zero : path[i - 1] - selPt;
						Vector3 dNext = i == path.Length - 1 ? Vector3.zero : path[i + 1] - selPt;
						path[Mathf.Max( i - 1, 0 )] = newPt + dPrev;
						path[Mathf.Min( i + 1, path.Length - 1 )] = newPt + dNext;
					} else {
						int controlPoint;
						int mirrorPoint;
						if ( i % 3 == 1 ) {
							controlPoint = i - 1;
							mirrorPoint = i - 2;
						} else {
							controlPoint = i + 1;
							mirrorPoint = i + 2;
						}
						if ( mirrorPoint >= 0 && mirrorPoint < path.Length ) {
							Vector3 cpv = path[controlPoint];
							path[mirrorPoint] = cpv + cpv - newPt;
						}
					}
					path[i] = newPt;
				}
				pathChanged = true;
			}
			if ( moveWholeSpline ) {
				break;
			}
		}
		return pathChanged;
	}

	public bool DrawPath( Vector3 [] path, Color color, int pathId, bool moveWholeSpline, 
						float selectedWidth = 2, 
						float unselectedWidth = 5, 
						Texture2D tex = null ) 

	{
		if ( _selectedPath == pathId ) {
			return DrawSelectedPath( path, color, moveWholeSpline, selectedWidth, tex );
		}
		DrawDeselectedPath( path, color, pathId, unselectedWidth );
		return false;
	}

	public void SelectPath( int pointIndex, int pathId )
	{
		_selectedIndex = pointIndex;
		_selectedPath = pathId;
	}

	public void Deselect()
	{
		_selectedPath = -1;
		_selectedIndex = -1;
	}

	public int GetSelectedPath()
	{
		return _selectedPath;
	}

	public int GetSelectedPoint()
	{
		return _selectedIndex;
	}

	public bool IsSelected()
	{
		return _selectedPath >= 0;
	}

	public bool IsSelectedNode()
	{
		return _selectedIndex >= 0;
	}

	public void SetLookatSegment( Vector3 pos, Vector3 lookat )
	{
		_camPos = pos;
		_camLookat = lookat;
	}

	public void DrawLookatLine()
	{
		Handles.color = Color.gray;
		Handles.DrawDottedLine( _camPos, _camLookat, 2 );
	}
}

}

#endif
