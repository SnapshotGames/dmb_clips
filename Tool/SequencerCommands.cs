#if UNITY_EDITOR

using System;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DMB
{

public partial class Sequencer : MonoBehaviour
{
	private Dictionary<string,Action<string[]>> _commands;
	private string _prevCommandLine;

	public bool TryCommandLineChange()
	{
		if ( CommandLine != null && CommandLine.Length > 0 ) {
			int lastIdx = CommandLine.Length - 1;
			char last = CommandLine[lastIdx];
			if ( last == '\n' ) {
				if ( CommandLine[0] == '@' ) {
					CommandLine = _prevCommandLine;
				} else {
					ExecuteCommandLine( CommandLine );
					_prevCommandLine = CommandLine.Trim();
					CommandLine = "";
				}
				return true;
			}
		}
		return false;
	}

	private void Load_f( string [] argv )
	{
		_clip.ClearAllLists( clearDuration: true );
		ClipData match = Import_f( argv );
		if ( ! match ) {
			Resources.LoadAll( "DMBClips/", typeof( ClipData ) );
			match = Import_f( argv );
		}
		if ( match ) {
			string clipName = CleanClipName( match.name );
			_clip.Duration = match.Clip.Duration;
			SetClipSortAndReset( _clip, clipName );
			_lastSaveTime = Time.realtimeSinceStartup;
		}
	}

	private ClipData Import_f( string [] argv ) 
	{
		if ( argv.Length < 2 ) {
			Debug.Log( "Import_f: No name pattern supplied." );
			return null;
		}
		ClipData matchingCD;
		if ( FindFirstClipData( argv, out matchingCD ) ) {
			_clip.ImportAfterLastTrack( matchingCD.Clip );
			Debug.Log( "Import_f: Importing match '" + matchingCD.name + "'." );
			return matchingCD;
		}
		Debug.Log( "Import_f: Can't find matching clip." );
		return null;
	}

	private void Trim_f() 
	{
		Sample first;
		if ( _clip.FindFirstSample( s => true, out first ) ) {
			_clip.ForEachSample( s => {
				if ( s.StartTime < first.StartTime ) {
					first = s;
				}
			} );
		}
		_clip.Shift( -first.StartTime );
		_clip.Duration = _clip.CalculateEnd();
	}

	private void Save_f( string [] argv )
	{
		string name = argv.Length <= 1 ? _clipName : argv[1];
		SaveClipToPrefab( name );
		SetPersistentClipName( name );
		Debug.Log( "Save_f: Saved '" + name + "'." );
	}

	private void ReplaceActor_f( string [] argv )
	{
		if ( argv.Length >= 2 ) {
			if ( argv.Length == 2 ) {
				_clip.ForEachSample( s => {
					s.ActorPrefab = argv[1];
				} );
				Debug.Log( "Replaced all actors in samples with " + argv[1] );
			} else {
				Debug.Log( "Replacing MATCHING actors in samples..." );
				_clip.ForEachSample( s => {
					if ( s.ActorPrefab == argv[1] ) {
						Debug.Log( "Replacing " + s.ActorPrefab + " with " + argv[2] );
						s.ActorPrefab = argv[2];
					}
				} );
			}
		}
	}

	private void PrintAnimatorStates_f( string [] argv ) 
	{
		if ( argv.Length < 2 ) {
			Debug.Log( "Specify actor in scene." );
		} else {
			Animator a;
			if ( ClipUtil.GetAnimator( argv[1], out a ) ) {
				string s = "";
				foreach ( var c in a.runtimeAnimatorController.animationClips ) {
					var path = AssetDatabase.GetAssetPath( c );
					s += Path.GetFileName( path ) + ": " + c.name + "\n";
				}
				Debug.Log( s );
			}
		}
	}

	//private void Merge_f( string [] argv ) 
	//{
	//	ClipData match;
	//	if ( FindFirstClipData( argv, out match ) ) {
	//		Clip mergedClip = new Clip();
	//		mergedClip.ImportAfterLastTrack( _clip );
	//		Clip overrides = match.Clip;
	//		mergedClip.MergeList<MecanimSample>( overrides, silent: false );
	//		mergedClip.MergeList<MovementSample>( overrides, silent: false );
	//		mergedClip.MergeList<ParticleSample>( overrides, silent: false );
	//		mergedClip.Duration = mergedClip.CalculateEnd();
	//		SetClipSortAndReset( mergedClip, _clipName );
	//	}
	//}

	partial void InitExtensionCommands();

	private void InitCommands()
	{
		_commands = new Dictionary<string,Action<string[]>>() {
			{ "NEW", argv => Reset() },
			{ "RESET", argv => Reset() },
			{ "CLEAR", argv => _clip.ClearAllLists() },
			{ "IMPORT", argv => Import_f( argv ) },
			{ "LOAD", argv => Load_f( argv ) },
			//{ "MERGE", argv => Merge_f( argv ) }, 
			{ "PRINT_ANIMATOR_STATES", argv => PrintAnimatorStates_f( argv ) },
			{ "REPLACE_ACTOR", argv => ReplaceActor_f( argv ) },
			{ "REVERT", argv => Load( _clipName ) },
			{ "SAVE", argv => Save_f( argv ) },
			{ "SHIFT", argv => { if ( argv.Length > 1 ) _clip.Shift( atof( argv[1] ) ); } },
			{ "TRIM", argv => Trim_f() },
		};
		InitExtensionCommands();
	}

	private void ExecuteCommandLine( string command )
	{
		if ( _commands == null ) {
			InitCommands();
		}
		string pattern = @"[ \t\n]+";
		string[] argv = Regex.Split(command, pattern).Where((s) => !string.IsNullOrEmpty(s)).ToArray();
		if ( argv.Length == 0 ) {
			return;
		}
		string cmdName = argv[0].ToUpper();
		Action<string[]> act;
		if ( _commands.TryGetValue( cmdName, out act ) ) {
			UNDORecordChange( cmdName );
			act( argv );
			Debug.Log( "Executed '" + command + "'" );
		} else {
			Debug.Log( "No command called '" + argv[0] + "'" );
		}
	}
}

}

#endif
