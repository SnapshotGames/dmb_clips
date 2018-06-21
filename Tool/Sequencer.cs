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

[ExecuteInEditMode]
public partial class Sequencer : MonoBehaviour
{
	[TextArea]
	public string CommandLine;

	// SerializeField so we can Undo
	[SerializeField] 
	private Clip _clip;

	public Display Display = new Display();
	public SWUI UI { get { return Display.UI; } }

	private float _absoluteTime;
	private float _deltaAbsoluteTime;
	private string _clipName;
	private float _lastSaveTime;
	private float _lastChangeTime;
	private int _captureVideoNumFrames;
	private SequencerInspector _inspector;
	private bool _playback;
	private bool _manualScrub;
	private float _timeOnPlayClick;
	private int _numTracks;

	[Header("Properties")]
	public bool SaveOnSaveScene;
	public bool CaptureVideo;
	public bool CaptureVideoSimulate;
	public int CaptureVideoFPS = 30;
	public int CaptureVideoSuperSize = 1;
	public string CaptureVideoDirectory = "";

    public string ClipsParentDirectory = "Assets/dmb_clips/Example/Resources";

	public void SetTargetInspector( SequencerInspector cei ) {
		if ( _inspector != cei ) {
			//Debug.Log( "Acquire custom inspector: " + cei );
			_inspector = cei;
		}
	}

	public bool IsClipChanged()
	{
		return _lastChangeTime > _lastSaveTime;
	}

	private void UNDORecordChange( string change )
	{
		Undo.RecordObject( this, change );
		_lastChangeTime = Time.realtimeSinceStartup;
		// these don't work as expected
		//Undo.RegisterCompleteObjectUndo( this, change );
		//Undo.FlushUndoRecordObjects();
	}

	private void UNDOClear()
	{
		// game object is destroyed some times
		if ( this ) {
			EditorUtility.SetDirty( this );
		}
		Undo.FlushUndoRecordObjects();
		Undo.CollapseUndoOperations(0);
		Undo.ClearAll();
		Debug.Log( "Undo clear." );
	}

	private void SetPersistentClipName( string name )
	{
		_clipName = name;
		PlayerPrefs.SetString( "SequencerLastOpen", name );
	}

	private DateTime _assemblyWriteTime;
	private void TryInit()
	{
		// OnEnable might be called multiple times -- i.e. on undo,
	   	// so initialize only on recompile
		var filePath = Assembly.GetCallingAssembly().Location;
		var lwt = File.GetLastWriteTime( filePath );
		if ( _assemblyWriteTime == lwt ) {
			return;
		}
		Display.Init();
		Reset();
		string lastOpen = PlayerPrefs.GetString( "SequencerLastOpen" );
		Load( lastOpen );
		//if ( LoadClipFromPrefab( lastOpen ) ) {
		//	_clipName = lastOpen;
		//}
		SceneView.onSceneGUIDelegate -= OnScene;
        SceneView.onSceneGUIDelegate += OnScene;
		// this causes our prefab to always be "changed"
		// when something changes in the scene
		//Undo.willFlushUndoRecord -= OnUndoFlush;
		//Undo.willFlushUndoRecord += OnUndoFlush;
		Undo.undoRedoPerformed -= OnUndoRedo;
		Undo.undoRedoPerformed += OnUndoRedo;
		EditorApplication.hierarchyChanged -= OnHierarchyChanged;
		EditorApplication.hierarchyChanged += OnHierarchyChanged;
		EditorApplication.update -= MyUpdate;
		EditorApplication.update += MyUpdate;
		//SerializedObject _so = new UnityEditor.SerializedObject( this );
		//ObjectDef.InitializeDefs();
		_assemblyWriteTime = lwt;
		Debug.Log( "Clip Editor initialized..." );
	}

	private void Reset()
	{
		Debug.Log( "Reset" );
		var clip = new Clip();
		clip.Duration = 6.66f;
		SetClipSortAndReset( clip );
	}

	private void SetClipSortAndReset( Clip clip, string clipName = null )
	{
		_clip = clip;
		if ( clipName == null ) {
			_clipName = "UNNAMED";
		} else {
			SetPersistentClipName( clipName );
		}
		_timeOnPlayClick = 0;
		_numTracks = 0;
		_clip.SortSamplesByTime();
		UpdateAllAnimationInternals();
		Display.Reset();
		var animators = UnityEngine.Object.FindObjectsOfType<Animator>();
		// fixes the "No controller is playing" warning.
		foreach (var a in animators) {
			a.enabled = false;
			a.enabled = true;
		}
		ScrubNormalizedTime( 0 );
		SampleAll();
		UNDOClear();
		transform.position = new Vector3( 999, 999, 999 );
		if ( Application.isPlaying ) {
			var rbs = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
			foreach ( var rb in rbs ) {
				rb.isKinematic = true;
			}
		}
	}

	private void UpdateAnimationInternals(MecanimSample sample)
	{
		MecanimInternals internals;
		if ( AnimGetInternalsFromAnimator( sample.ActorPrefab, sample.State, out internals ) ) {
			sample.Internals = internals;
		}
	}

	private void UpdateAllAnimationInternals()
	{
		Debug.Log( "Updating animation internals..." );
		foreach ( var sample in _clip.MecanimSamples ) {
			UpdateAnimationInternals( sample );
		}
	}

	private void SaveClipToPrefab( string clipName )
	{
		_lastSaveTime = Time.realtimeSinceStartup;
		UpdateAllAnimationInternals();
		// can't destroy game objects in validate callbacks
		ClipData.Save( ClipsParentDirectory, clipName, _clip, destroy: false );
	}

    void OnScene( SceneView sceneView ) 
	{
		if ( DrawWorldEdits() ) {
			SampleAll();
		}
		bool play;
		bool scrub;
		DrawControls( _playback, out play, out scrub );
		if ( scrub ) {
			// the actual sampling is always done on Update so LateUpdate-s work on scrub
			play = false;
			_manualScrub = true;
			ScrubNormalizedTime( Mathf.Clamp( Display.MouseXToNormTime(), 0, 1 ) );
		}
		if ( play != _playback ) {
			_playback = play;
			_timeOnPlayClick = GetTime() - _absoluteTime;
		}
		// FIXME: messes up the ppgen commands
		// ClipData can be created in validate callback (save command)
		// and game objects cannot be destroyed in the callback
		// so destroy any garbage here
		var td = GameObject.FindObjectOfType<ClipData>();
		if ( td && td.gameObject ) {
			GameObject.DestroyImmediate( td.gameObject );
		}
    }

	private void CheckForClipDragAndDrop()
	{
		// FIXME: messes up the ppgen_ commands
		//var prefab = GameObject.FindObjectOfType<ClipData>();
		//if ( prefab != null && prefab.gameObject != null ) {
		//	string clipName = CleanClipName( prefab.name );
		//	Load( clipName );
		//}
	}

	void OnEnable()
	{
		TryInit();
	}

	void OnDestroy()
	{
		SceneView.onSceneGUIDelegate -= OnScene;
		EditorApplication.update -= MyUpdate;
		//Undo.willFlushUndoRecord -= OnUndoFlush;
		Undo.undoRedoPerformed -= OnUndoRedo;
	}

	// in milliseconds
	private int GetCaptureVideoFrameDuration()
	{
		return 1000 / Mathf.Max( CaptureVideoFPS, 1 );
	}

	private float GetTime()
	{
		float result;
		if ( CaptureVideo || CaptureVideoSimulate ) {
			result = _captureVideoNumFrames * GetCaptureVideoFrameDuration() / 1000.0f;
		} else {
			result = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
		}
		return result;
	}

	private void MyUpdate()
	{
		if ( _playback ) {
			bool capture = CaptureVideo || CaptureVideoSimulate;
			Time.captureFramerate = capture ? CaptureVideoFPS : 0;
			if ( _clip.Duration > 0 ) {
				if ( CaptureVideo ) {
					int totalTime = _captureVideoNumFrames *  GetCaptureVideoFrameDuration();
					int timeOnClick = ( int )( _timeOnPlayClick * 1000.0f );
					int timePassedSincePlayClick = totalTime - timeOnClick;
					int duration = ( int )( _clip.Duration * 1000.0f );
					int numFrames = timePassedSincePlayClick / GetCaptureVideoFrameDuration(); 
					int totalFrames = duration / GetCaptureVideoFrameDuration(); 
					if ( timePassedSincePlayClick < duration ) {
						ScrubAbsoluteTime( timePassedSincePlayClick / 1000.0f );
						string path = CaptureVideoDirectory + "/frame" + ( numFrames % totalFrames ).ToString( "D5" ) + ".png";
						ScreenCapture.CaptureScreenshot( path, superSize: Mathf.Clamp( CaptureVideoSuperSize, 1, 4 ) );
						Debug.Log( "Captured: " + path );
					} else {
						CaptureVideo = false;
					}
				} else {
					float timePassedSincePlayClick = GetTime() - _timeOnPlayClick;
					float t = Mathf.Repeat( timePassedSincePlayClick, _clip.Duration );
					ScrubAbsoluteTime( t );
				}
			} else {
				ScrubAbsoluteTime( 0 );
			}
			SampleAll();
			if ( capture ) {
				_captureVideoNumFrames++;
			}
		} else if ( _manualScrub ) {
			SampleAll();
			//SampleParticles();
			_manualScrub = false;
		}
	}

	private void OnHierarchyChanged()
	{
		// in play mode hierarchy changes all the time
		if ( Application.isEditor ) {
			CheckForClipDragAndDrop();
		}
	}

	private void OnUndoRedo()
	{
		SampleAll();
	}

	private void SampleCameraPaths( int numTracks )
	{
		float prevTime = _absoluteTime - _deltaAbsoluteTime;
		Vector3 camLookat = ClipUtil.ScrubCamLookats( _clip, numTracks, prevTime, _absoluteTime, fallbackToMainCam: true );
		Vector3 camPos = ClipUtil.ScrubCamFollowPath( _clip, numTracks, prevTime, _absoluteTime, camLookat, fallbackToMainCam: true );
		Display.UpdateCameraLine( camLookat, camPos );
	}

	private void SampleFollowPath( FollowPathSample fps )
	{
		FollowPathEdit.SampleAtTime( fps.ActorPrefab, fps, _absoluteTime );
	}

	private void SampleFollowPaths()
	{
		foreach( var fps in _clip.FollowPathSamples ) {
			SampleFollowPath(fps);
		}
	}

	private void UpdateParticleSystem( ParticleSystem s, float timeScale = 1 ) {
		s.Play();
		s.Pause();
		s.Simulate( _deltaAbsoluteTime * timeScale, withChildren: true, restart: false );
	}

	private bool FindParticleSampleBySystem( ParticleSystem s, out ParticleSample result )
	{
		foreach( var fps in _clip.ParticleSamples ) {
			if ( s.name == fps.ActorPrefab ) {
				result = fps;
				return true;
			}
		}
		//Debug.Log( "Can't find particle sample for '" + s.name + "'" );
		result = null;
		return false;
	}

	private void ScrubParticleSample( ParticleSample ps, ParticleSystem system )
	{
		float lt = ClipUtil.LocalTime( _absoluteTime, ps.StartTime, ps.Duration );
		if ( lt < 0 ) {
			system.Stop();
			system.Clear();
			system.time = 0;
		} else if ( lt > 1 ) {
			system.Stop();
			system.Clear();
			system.time = system.main.duration;
		} else if ( Mathf.Abs( _deltaAbsoluteTime ) > 0.2f ) {
			system.Play();
			system.Pause();
			system.Simulate( lt * system.main.duration, withChildren: true, restart: true );
		} else {
			// FIXME: only stretched time particles?
			UpdateParticleSystem( system, timeScale: system.main.duration / ps.Duration );
		}
	}

	private void SampleParticles()
	{
		ParticleSystem [] systems = GameObject.FindObjectsOfType<ParticleSystem>();
		foreach ( var s in systems ) {
			ParticleSample ps;
			if ( FindParticleSampleBySystem( s, out ps ) ) {
				ScrubParticleSample( ps, s );
			}
		}
	}

	private void SampleMovements()
	{
		foreach( var m in _clip.MovementSamples ) {
			GameObject go = GameObject.Find( m.ActorPrefab );
			if ( go != null ) {
				float lt = ClipUtil.LocalTime( _absoluteTime, m.StartTime, m.Duration );
				if ( lt >= 0 && lt <= 1 ) {
					go.transform.position = Vector3.Lerp( m.Start.Position, m.End.Position, lt );
					if ( m.Start.Facing.sqrMagnitude > 0.0001f 
						&& m.End.Facing.sqrMagnitude > 0.0001f ) {
						go.transform.forward = Vector3.Slerp( m.Start.Facing, m.End.Facing, lt );
					}
				}
			}
		}
	}

	private void SampleAll()
	{
		int numTracks = _clip.GetNumberOfTracks();
		// camera sampling updates editor specific state
		// skip it in the general scrubber
		SampleCameraPaths( numTracks );
		// FIXME: exposed scrubber doesnt sample paths nor particles
		ClipUtil.SimpleScrub( _clip, numTracks, _absoluteTime - _deltaAbsoluteTime, _absoluteTime, skipCamera: true );
		SampleFollowPaths();
		SampleParticles();
		//_deltaAbsoluteTime = 0;
	}

	private void ScrubAbsoluteTime( float t )
	{
		_deltaAbsoluteTime = t - _absoluteTime;
		_absoluteTime = t;
		//Debug.Log ( "scrubbing: " + Time.realtimeSinceStartup );
	}

	private void ScrubNormalizedTime( float t )
	{
		ScrubAbsoluteTime( t * _clip.Duration );
	}

	private static float atof( string s )
	{
		float res;
		if ( float.TryParse(s, out res) ) {
			return res;
		}
		return 0;
	}

	private void Load( string name )
	{
		Debug.Log( "Load: Attempt at loading " + name );
		Load_f( new string [] { "", name } );
	}

	private static string CleanClipName( string name )
	{
		// FIXME: no more timelines?
		string result = name.Replace( ".timeline", "" );
		result = result.Replace( ".clip", "" );
		return result;
	}

	private static bool FindFirstClipData( string [] argv, out ClipData match )
	{
		match = null;
		UnityEngine.Object[] tlds = Resources.FindObjectsOfTypeAll( typeof( ClipData ) ); 
		foreach( var tld in tlds ) {
			ClipData cd = tld as ClipData;
			int contains = 1;
			for( int i = 1; i < argv.Length; i++ ) {
				float d;
				if ( float.TryParse( argv[i], out d ) 
						|| cd.name.ToUpper().Contains( argv[i].ToUpper() ) ) {
					contains++;
				}
			}
			if ( contains == argv.Length ) {
				match = cd;
				return true;
			}
		}
		return false;
	}

	[MenuItem("File/Save DMBSequencer Clip %w")]
	private static void SaveMenuItem()
	{
		Debug.Log( "SaveMenuItem invoked..." );
		var seq = UnityEngine.Object.FindObjectOfType<DMB.Sequencer>();
		if ( seq != null ) {
			seq.SaveCurrentClip();
		}
	}

	public void SaveCurrentClip()
	{
		SaveClipToPrefab( _clipName );
	}

	private void SetMovementFromAnimation( MovementSample ms, MecanimSample mcn )
	{
		UNDORecordChange( "Set Distance From Animation" );
		ms.End.Position = ms.Start.Position + mcn.Internals.Offset;
		Debug.Log( "Set movement from animation. Animation Offset: " + mcn.Internals.Offset );
	}

	private void RestoreMecanimDuration( Sample sample )
	{
		UNDORecordChange( "Restore Duration" );
		var mcn = sample as MecanimSample;
		mcn.Duration = Mathf.Max( 0.123456789f, mcn.Internals.Duration );
	}

	private void ChangeState( MecanimSample sample, string newState )
	{
		sample.State = newState;
		UpdateAnimationInternals( sample );
		sample.Duration = Mathf.Max( 0.123456789f, sample.Internals.Duration );
	}

	private bool GetFirstSampleAbove( int track, float time, out Sample sample ) 
	{
		sample = null;
		if ( track == 0 ) {
			return false;
		}
		return _clip.FindFirstSample( s => 
										s.Track < track 
											&& s.StartTime <= time 
											&& s.StartTime + s.Duration >= time,
									out sample );
	}

	private bool GetFirstSampleBelow( int track, float time, out Sample sample ) 
	{
		sample = null;
		return _clip.FindFirstSample( s => 
										s.Track > track 
											&& s.StartTime <= time 
											&& time <= s.StartTime + s.Duration, 
									out sample );
	}

	private void AlignToSample( Sample sample, Sample alignTo )
	{
		UNDORecordChange( "Align Samples" );
		sample.StartTime = alignTo.StartTime;
		sample.Duration = alignTo.Duration;
	}

	private void AddCamKey( CameraPathSample cps, float timeInClip )
	{
		UNDORecordChange( "Add Key" );
		Vector3 pos;
		int idx;
	   	ClipUtil.ScrubCameraSample( cps, timeInClip, out pos, out idx );
		cps.Keys.Insert( idx + 1, new CameraPathKey {
			TimeNorm = ( timeInClip - cps.StartTime ) / cps.Duration,
			Node = new Vector3[] {
				pos - new Vector3( 0, 0, 1 ),
				pos,
				pos + new Vector3( 0, 0, 1 ),
			},
		} );
	}

	private void RemoveKey( KeyedSample ks, int k )
	{
		UNDORecordChange( "Remove Key" );
		ks.RemoveKey( k );
	}

	private void AddFPKey( FollowPathSample fps, float timeInClip )
	{
		UNDORecordChange( "Add Key" );
		float keyTime = ( timeInClip - fps.StartTime ) / fps.Duration;
		Vector3 pos;
		int i;
		FollowPathEdit.GetPointOnPath( fps.Keys, keyTime, out pos, out i );
		fps.Keys.Insert( i + 1, new FollowPathKey {
			TimeNorm = keyTime,
			Position = pos,
		} );
	}

	private void GeneratePathKey( MecanimSample ms, FollowPathSample fps, Vector3 origin )
	{
		fps.Keys.Add( new FollowPathKey {
			Position = origin + ms.Internals.Offset,
			TimeNorm = ( ms.EndTime - fps.StartTime ) / fps.Duration,
		} );
	}

	private void GeneratePathFromAnims( FollowPathSample fps )
	{
		UNDORecordChange( "Generate Path" );
		MecanimSample first = _clip.MecanimSamples[0];
		MecanimSample last = _clip.MecanimSamples[_clip.MecanimSamples.Count - 1];
		int animsTrack = first.Track;
		fps.StartTime = first.StartTime;
		fps.Duration = last.EndTime - first.StartTime;
		fps.Keys.Clear();
		Vector3 prev = Vector3.zero;
		fps.Keys.Add( new FollowPathKey {
			Position = Vector3.zero,
			TimeNorm = 0,
		} );
		foreach ( var ms in _clip.MecanimSamples ) {
			if ( ms.Track == animsTrack ) {
				float numLoops = ms.Looped ? ms.Duration / ms.OneShotDuration : 1;
				Vector3 pos = prev + ms.Internals.Offset * numLoops;
				fps.Keys.Add( new FollowPathKey {
					Position = pos,
					TimeNorm = ( ms.EndTime - fps.StartTime ) / fps.Duration,
				} );
				prev = pos;
			}
		}
	}

	private void DrawSampleRBMenu( Sample sample, float mouseTime )
	{
		var m = GetSampleRBMenu( sample, mouseTime );
		m.Add( new SWUI.PopupMenuItem( MenuDelimiter, () => {} ) );
		m.AddRange( GetTrackRBMenu( sample.Track, mouseTime ) );
		UI.CreatePopupMenu( m );
	}

	private List<SWUI.PopupMenuItem>  GetSampleRBMenu( Sample sample, float mouseTime )
	{
		var items = new List<SWUI.PopupMenuItem>();
		Action<string,Action> add = (s,a) => items.Add( new SWUI.PopupMenuItem( s, a ) );
		Sample sampleAbove, sampleBelow;
		bool hasSampleAbove = GetFirstSampleAbove( sample.Track, 
													sample.StartTime + sample.Duration / 2, out sampleAbove );
		bool hasSampleBelow = GetFirstSampleBelow( sample.Track, 
													sample.StartTime + sample.Duration / 2, out sampleBelow );
		if ( sample is MecanimSample ) {
			add( "Toggle Looped", () => ToggleLooped( sample ) );
			add( "Restore Duration", () => RestoreMecanimDuration( sample ) );
			//add( "Split", () => RestoreMecanimDuration( sample ) );
			add( MenuDelimiter, () => {} );
		} else if ( sample is CameraPathSample ) {
			add( "Add Key", () => AddCamKey( sample as CameraPathSample, mouseTime ) );
			add( MenuDelimiter, () => {} );
		} else if ( sample is FollowPathSample ) {
			add( "Add Key", () => AddFPKey( sample as FollowPathSample, mouseTime ) );
			if ( _clip.MecanimSamples.Count > 0 ) {
				add( "Generate From Anims", () => GeneratePathFromAnims( sample as FollowPathSample ) );
			}
			add( MenuDelimiter, () => {} );
		} else if ( sample is MovementSample ) {
			MovementSample ms = sample as MovementSample;
			if ( sampleAbove is MecanimSample ) {
				MecanimSample mcn = sampleAbove as MecanimSample;
				if ( mcn.ActorPrefab == ms.ActorPrefab ) {
					add( "Set Distance From Animation Above", () => SetMovementFromAnimation( ms, mcn ) );
				}
			}
			add( MenuDelimiter, () => {} );
		}
		//if ( sample.Track > 0 ) {
		//	add( "Move Up Track", () => { UNDORecordChange( "Move Track" ); sample.Track--; } );
		//}
		//add( "Move Down Track", () => { UNDORecordChange( "Move Track" ); sample.Track++; } );
		if ( hasSampleAbove ) {
			add( "Align To Above", () => AlignToSample( sample, sampleAbove ) );
		}
		if ( hasSampleBelow ) {
			add( "Align To Below", () => AlignToSample( sample, sampleBelow ) );
		}
		add( "Remove Sample", () => RemoveSample( sample ) );
		add( "Clone Sample At Cursor", () => CloneSampleAtCursor( sample ) );
		return items;
	}

	const string MenuDelimiter = "...";

	partial void AddTrackExtensionRBMenu( int track, float startTime, 
														List<SWUI.PopupMenuItem> list );

	private List<SWUI.PopupMenuItem> GetTrackRBMenu( int track, float startTime )
	{
		List<SWUI.PopupMenuItem> result = new List<SWUI.PopupMenuItem>() {
			new SWUI.PopupMenuItem( "Add Camera Path", () => CreateCameraPath( startTime, track ) ),
			new SWUI.PopupMenuItem( "Add Camera Lookat", () => CreateCameraLookat( startTime, track ) ),
			new SWUI.PopupMenuItem( "Add Animation", () => CreateMecanim( startTime, track ) ),
			new SWUI.PopupMenuItem( "Add Follow Path", () => CreateFollowPath( startTime, track ) ),
			new SWUI.PopupMenuItem( "Add Particle", () => CreateParticle( startTime, track ) ),
			new SWUI.PopupMenuItem( "Add Movement", () => CreateMovement( startTime, track ) ),
		};
		AddTrackExtensionRBMenu( track, startTime, result );
		result.AddRange( new List<SWUI.PopupMenuItem>() {
			new SWUI.PopupMenuItem( MenuDelimiter, () => {} ),
			new SWUI.PopupMenuItem( "Add Track Below", () => CreateTracks( track, 1 ) ),
			new SWUI.PopupMenuItem( "Remove Track", () => RemoveTrack( track ) ),
		} );
		return result;
	}

	private void DrawTrackRBMenu( int track, float startTime )
	{
		UI.CreatePopupMenu( GetTrackRBMenu( track, startTime ) );
	}

	private bool DrawTracks( int numTracks, float totalTime )
   	{
		bool drag = false;
		for ( int i = 0; i < numTracks; i++ ) {
			UnityEngine.Object dragAndDrop;
			Display.DrawTrackResult res = Display.DrawTrack( i, numTracks, totalTime, out dragAndDrop );
			if ( res != Display.DrawTrackResult.None ) {
				if ( res == Display.DrawTrackResult.Scrub ) {
					if ( i == numTracks - 1 ) {
						drag = true;
					}
				} else if ( res == Display.DrawTrackResult.ShowInInspector ) {
					Selection.activeGameObject = gameObject;
				} else if ( res == Display.DrawTrackResult.IsDragAndDrop ) {
					AnimationClip c = dragAndDrop as AnimationClip;
					// has undo
					MecanimSample ms = CreateMecanim( Display.MouseXToTime( totalTime ), i );
					ChangeState( ms, c.name );
					Debug.Log( "Drag and Drop new clip: " + c.name );
				} else if ( res == Display.DrawTrackResult.ShowContextMenu ) {
					DrawTrackRBMenu( i, Display.MouseXToTime( totalTime ) );
				}
			}
		}
		return drag;
	}

	private void DrawSamples( int numTracks, float totalTime )
	{
		float startTime;
		int track;
		float newKeyTime;
		int key;
		Sample s;
		Display.DrawSampleResult dsr = Display.DrawSamples( _clip, numTracks, totalTime, 
															out s, out track, out startTime,
															out key, out newKeyTime );
		if ( dsr != Display.DrawSampleResult.None ) {
			if ( dsr == Display.DrawSampleResult.Select ) {
				SelectInInspector( s );
			} else if ( dsr == Display.DrawSampleResult.StoppedDragging ) {
				_clip.SortSamplesByTime();
				SelectInInspector( s );
			} else if ( dsr == Display.DrawSampleResult.ShowContextMenu ) {
				DrawSampleRBMenu( s, Display.MouseXToTime( totalTime ) );
			} else {
				if ( dsr == Display.DrawSampleResult.DragKey ) {
					SelectInInspector( s, "Keys", key );
				}
				UNDORecordChange( "Modified Sample" );
				if ( dsr == Display.DrawSampleResult.DragSample ) {
					s.Track = track;
					s.StartTime = startTime;
				} else if ( dsr == Display.DrawSampleResult.DragSampleGroup ) {
					_clip.ShiftSamplesRightOfCursor( s.StartTime, s.Track, 
													Display.GetHorzShift( totalTime ), 
													stretchPrev: false );
				} else if ( dsr == Display.DrawSampleResult.DragKey ) {
					( s as KeyedSample ).SetKeyTime( key, newKeyTime );
				}
			}
		}

		Sample modifiedSample;
		float modifiedStartTime;
		float modifiedDuration;
		Display.DrawSampleEndsResult result = Display.DrawSampleEnds( _clip, numTracks, totalTime, 
								out modifiedSample, out modifiedStartTime, out modifiedDuration );
		if ( result == Display.DrawSampleEndsResult.Drag ) {
			UNDORecordChange( "Dragged Sample Ends" );
			float clamp = 0.1f;
			if ( UI.IsCtlHoldDown() && modifiedSample is KeyedSample ) {
				KeyedSample ks = modifiedSample as KeyedSample;
				float [] times = ks.GetKeyTimes();
				if ( times.Length > 2 ) {
					float clipTime = ks.StartTime + times[1] * ks.Duration;
					modifiedStartTime = Mathf.Min( clipTime, modifiedStartTime );
					modifiedDuration = Mathf.Max( times[times.Length - 2] * ks.Duration + clamp, 
													modifiedDuration );
					float ratio = ks.Duration / modifiedDuration;
					for ( int i = 1; i < times.Length - 1; i++ ) {
						ks.SetKeyTime( i, times[i] * ratio );
					}
				}
			} else {
				modifiedDuration = Mathf.Max( modifiedDuration, clamp );
			}
			modifiedSample.StartTime = modifiedStartTime;
			modifiedSample.Duration = modifiedDuration;
		} else if ( result == Display.DrawSampleEndsResult.StoppedDragging ) {
			_clip.SortSamplesByTime();
			SelectInInspector( modifiedSample );
		}
	}

	private void DrawControls( bool inPlayback, out bool outPlayback, out bool shouldScrub )
	{
		// FIXME: no begins/ends of ui outside of display code?
		UI.Begin();
		outPlayback = inPlayback;
		Display.DrawClipInfo( _clip, _clipName, IsClipChanged() );
		int numTracksInClip = _clip.GetNumberOfTracks();
		if ( numTracksInClip > _numTracks ) {
			_numTracks = numTracksInClip;
			Debug.Log( "Num Tracks changed: " + _numTracks );
		}
		int numTracksToDraw = _numTracks + 1;
		if ( Display.DrawPlayButton( numTracksToDraw, inPlayback ) || UI.IsEnterPressed() ) {
			outPlayback = ! inPlayback;
		}
		float totalTime = Mathf.Max( _clip.Duration, 0.001f );
		bool draggingOnTracks = DrawTracks( numTracksToDraw, totalTime );
		DrawSamples( numTracksToDraw, totalTime );
		float normalizedTime = _absoluteTime / totalTime;
		shouldScrub = Display.DrawNub( normalizedTime, numTracksToDraw, totalTime, draggingOnTracks, 
										inPlayback );
		// FIXME: move on Display.End
		if ( UI.ClickedOutsideOfUI() ) {
			Display.DeselectAll();
		}
		if ( UI.IsDeletePressed() ) {
			KeyedSample ks;
			int k;
			if ( Display.CheckDeleteKey( _clip, out ks, out k ) ) {
				RemoveKey( ks, k );
				UI.ConsumeEvent();
			} else {
				// FIXME: follow path 
				var go = Selection.activeGameObject;
				if ( go && ! go.GetComponent<Sequencer>() ) {
					Undo.DestroyObjectImmediate( go );
				}
				UI.ConsumeEvent();
			}
		}
		UI.End( inPlayback );
	}

	public bool DrawWorldEdits() {
		Vector3 [] points;
		CameraPathSample cps;
		bool isEditing = Display.DrawCameraPaths( _clip, out cps, out points );
		if ( isEditing ) {
			UNDORecordChange( "Modified Camera Points" );
			cps.SetPoints( points );
		}
		FollowPathSample fps;
		if ( Display.DrawFollowPaths( _clip, out fps, out points ) ) {
			UNDORecordChange( "Modified Follow Path Points" );
			fps.UpdatePositions( points );
			isEditing = true;
		}
		return isEditing;
	}

	// FIXME: some of these below can become commands

	private void ToggleLooped( Sample sample )
	{
		UNDORecordChange( "Toggle Looped" );
		var mcn = sample as MecanimSample;
		mcn.Looped = ! mcn.Looped;
	}

	private void RemoveSample( Sample sample )
	{
		UNDORecordChange( "Remove Sample" );
		_clip.RemoveSample( sample );
		_clip.SortSamplesByTime();
		_numTracks = _clip.GetNumberOfTracks();
		Debug.Log( "Removed sample." );
	}

	private void CloneSampleAtCursor( Sample sample )
	{
		UNDORecordChange( "Clone Sample" );
		Sample clone = _clip.CloneSample( sample );
		if ( clone != null ) {
			clone.StartTime = _absoluteTime;
			_clip.SortSamplesByTime();
			Debug.Log( "Cloned sample at " + _absoluteTime );
		} else {
			Debug.Log( "Failed to clone sample. Missing in any list." );
		}
	}

	private MecanimSample CreateMecanim( float startTime, int track )
	{
		UNDORecordChange( "Create Mecanim Sample" );
		var ms = new MecanimSample {
			StartTime = startTime,
			Track = track,
			Duration = 0.3f,
		};
		DetectAndAssignActor( ms, track );
		_clip.MecanimSamples.Add( ms );
		_clip.SortSamplesByTime();
		UpdateAnimationInternals( ms );
		Debug.Log( "Created mecanim sample." );
		return ms;
	}

	private CameraPathSample CreateCamSample( float startTime, int track )
	{
		var origin = Camera.main.transform.position;
		var p0 = origin;
		var p1 = origin + new Vector3( 0, 0, 3 );
		string actor = CameraPathEdit.FallbackCamera.name;
		if ( _clip.CamPathSamples.Count > 0 ) {
			actor = _clip.CamPathSamples[0].ActorPrefab;
		}
		var cps = new CameraPathSample {
			ActorPrefab = actor,
			Track = track,
			StartTime = startTime,
			Duration = 0.3f,
			Keys = new List<CameraPathKey> {
				new CameraPathKey {
					Node = new Vector3[] {
						p0 - new Vector3( 0, 0, 1 ),
						p0,
						p0 + new Vector3( 0, 0, 1 ),
					},
					TimeNorm = 0,
				},
				new CameraPathKey {
					Node = new Vector3[] {
						p1 - new Vector3( 0, 0, 1 ),
						p1,
						p1 + new Vector3( 0, 0, 1 ),
					},
					TimeNorm = 1,
				},
			}
		};
		_clip.CamPathSamples.Add( cps );
		_clip.SortSamplesByTime();
		return cps;
	}

	private void CreateCameraLookat( float startTime, int track )
	{
		UNDORecordChange( "Create Camera Lookat Sample" );
		var cps = CreateCamSample( startTime, track );
		cps.IsLookatPath = true;
		Debug.Log( "Created camera lookat sample." );
	}

	private void CreateCameraPath( float startTime, int track )
	{
		UNDORecordChange( "Create Camera Path Sample" );
		var cps = CreateCamSample( startTime, track );
		cps.IsLookatPath = false;
		Debug.Log( "Created camera path sample." );
	}

	private void CreateFollowPath( float startTime, int track )
	{
		UNDORecordChange( "Create Follow Path Sample" );
		var fps = new FollowPathSample {
			Track = track,
			StartTime = startTime,
			Duration = 0.3f,
			Keys = new List<FollowPathKey> {
				new FollowPathKey {
				},
				new FollowPathKey {
					Position = new Vector3( 0, 0, 3 ),
					TimeNorm = 0.5f,
				},
				new FollowPathKey {
					Position = new Vector3( 0, 0, 6 ),
					TimeNorm = 1.0f,
				},
			},
		};
		_clip.FollowPathSamples.Add( fps );
		DetectAndAssignActor( fps, track );
		_clip.SortSamplesByTime();
		Debug.Log( "Created follow path sample." );
	}

	private void CreateMovement( float startTime, int track )
	{
		UNDORecordChange( "Create Movement Sample" );
		_clip.MovementSamples.Add( new MovementSample {
			Track = track,
			StartTime = startTime,
			Duration = 0.3f,
		} );
		_clip.SortSamplesByTime();
		Debug.Log( "Created Movement Sample." );
	}

	private void CreateParticle( float startTime, int track )
	{
		UNDORecordChange( "Create Particle Sample" );
		_clip.ParticleSamples.Add( new ParticleSample {
			Track = track,
			StartTime = startTime,
			Duration = 0.3f,
		} );
		_clip.SortSamplesByTime();
		Debug.Log( "Created Particle Sample." );
	}

	private void DetectAndAssignActor( Sample ms, int track ) 
	{
		Sample match;
		MecanimSample sampleOnSameTrack;
		if ( _clip.GetFirstSampleByTrack<MecanimSample>( track, out sampleOnSameTrack ) ) {
			ms.ActorPrefab = sampleOnSameTrack.ActorPrefab;
		} else if ( _clip.MecanimSamples.Count > 0 ) {
			ms.ActorPrefab = _clip.MecanimSamples[0].ActorPrefab;
		} else if ( _clip.FindFirstSample( s => s.ActorPrefab != "UNKNOWN", out match ) ) {
			ms.ActorPrefab = match.ActorPrefab;
		} else {
			Animator [] anims = UnityEngine.Object.FindObjectsOfType<Animator>();
			Debug.Log( "Detect animators: " + anims.Length );
			foreach( var a in anims ) {
				if ( a.GetComponentInChildren<Renderer>() ) {
					Debug.Log( "Found renderer: " + a.gameObject.name );
					ms.ActorPrefab = a.gameObject.name;
					break;
				}
			}
		}
		Debug.Log( "Assigned actor: '" + ms.ActorPrefab + "'." );
	}

	private void SelectInInspector( Sample sample, string childPropName = null, 
									int indexInChildProp = -1 )
	{
		int index;
		int listCount;
		string propName;
		if ( _clip.GetSampleListInfo( sample, out index, out listCount, out propName ) ) {
			Selection.activeGameObject = gameObject;
			if ( _inspector == null ) {
				//Debug.Log( "inspector is null" );
				return;
			}
			SerializedObject so = _inspector.serializedObject;
			SerializedProperty clipProp = so.FindProperty("_clip");
			clipProp.isExpanded = true;
			SerializedProperty prop = clipProp;
			// fold all
			if (prop.Next(true)) {
				do {
					prop.isExpanded = false;
				} while (prop.Next(true));
			}
			SerializedProperty parentProp = so.FindProperty( "_clip." + propName );
			parentProp.isExpanded = true;
			prop = parentProp.GetArrayElementAtIndex( index );
			prop.isExpanded = true;
			if ( childPropName != null ) {
				var childProp = prop.FindPropertyRelative( childPropName );
				if ( childProp != null ) {
					childProp.isExpanded = true;
					if ( indexInChildProp >= 0 ) {
						var elem = childProp.GetArrayElementAtIndex( indexInChildProp );
						if ( elem != null ) {
							elem.isExpanded = true;
						} else {
							Debug.Log( "No index " + indexInChildProp + " in '" + childProp + "'." );
						}
					}
				} else {
					Debug.Log( "No child property named '" + childPropName + "' in " + propName );
				}
			}
			_inspector.Repaint();
		} else {
			Debug.Log( "Can't find sample in clip: " + sample );
		}
	}

	private void CreateTracks( int currentTrack, int numTracks )
	{
		UNDORecordChange( "Create Tracks" );
		_clip.ForEachSample( s => {
			if ( s.Track > currentTrack ) {
				s.Track += numTracks;
			}
		} );
	}

	private void RemoveTrack( int track )
	{
		UNDORecordChange( "Remove Track" );
		_clip.RemoveSamples( s => s.Track == track );
		_clip.ForEachSample( s => {
			if ( s.Track > track ) {
				s.Track--;
			}
		} );
		_numTracks--;
	}
}

public class MySaver : UnityEditor.AssetModificationProcessor
{
    static string[] OnWillSaveAssets(string[] paths)
    {
        foreach (string path in paths) {
			if ( path.EndsWith( ".unity" ) ) {
				var seq = UnityEngine.Object.FindObjectOfType<DMB.Sequencer>();
				if ( seq != null && seq.SaveOnSaveScene && seq.IsClipChanged() ) {
					Debug.Log( "OnWillSaveAssets" );
					seq.SaveCurrentClip();
				}
				break;
			}
		}
        return paths;
    }
}

}

#endif
