using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DMB
{

// FIXME: migrate to Component instead of MonoBehaviour
public class ClipData : MonoBehaviour
{
	private const int CurrentVersion = 1;

	public int Version;
	public Clip Clip;

	public static string GetPath( string prefabName )
	{
		return "DMBClips/" + prefabName + ".clip";
	}

	public static GameObject Save( string resourcesDir, string prefabName, Clip clip, bool destroy = true )
	{
#if UNITY_EDITOR
		GameObject go = new GameObject();
		ClipData cd = go.AddComponent<ClipData>();
		cd.Version = CurrentVersion;
		cd.Clip = clip;
        string fullPath = resourcesDir + "/" + GetPath( prefabName ) + ".prefab";
		Object prefab = PrefabUtility.CreateEmptyPrefab( fullPath );
        if (prefab) {
            PrefabUtility.ReplacePrefab(go, prefab, ReplacePrefabOptions.ConnectToPrefab);
            if ( destroy ) {
                GameObject.DestroyImmediate( go );
            }
            Debug.Log( "Saved Clip " + fullPath );
        }
		return go;
#else
		return null;
#endif
	}

	public static ClipData LoadData( string prefabName )
	{
		var path = GetPath( prefabName );
		var prefab = UnityEngine.Resources.Load<ClipData>( path );
		if ( prefab != null ) {
			ClipData td = Object.Instantiate( prefab );
			td.name = prefabName;
			return td;
		}
		Debug.Log( "Failed to load '" + path + "'" );
		return null;
	}

	public static bool Load( ClipData prefab, out Clip clip )
	{
		if ( prefab.Clip != null ) {
			clip = prefab.Clip;
			Debug.Log( "Loaded Clip '" + prefab.name + "'" );
		} else {
			clip = null;
			Debug.Log( "'" + prefab.name + "' is not a valid Clip." );
		}
		return clip != null;
	}

	public static bool Load( string prefabName, out Clip clip )
	{
		clip = new Clip();
		ClipData prefab = LoadData( prefabName );
		bool result = false;
		if ( prefab != null ) {
			result = Load( prefab, out clip );
			if ( ! result  ) {
				// TODO: do something
				Debug.Log( "Failed to load clip data '" + prefabName + "'" );
			}
		}
		return result;
	}
}

}
