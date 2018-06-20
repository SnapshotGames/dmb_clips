#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace DMB
{

[CustomEditor(typeof(Sequencer))]
public class SequencerInspector : Editor 
{
	public override void OnInspectorGUI() 
	{
		var ce = ( Sequencer )target;
		( ( Sequencer )target ).SetTargetInspector(this);
        DrawDefaultInspector();
		if (ce.TryCommandLineChange()) {
			EditorGUIUtility.editingTextField = false;
			//string name = GUI.GetNameOfFocusedControl();
		}
    }
}

}

#endif
