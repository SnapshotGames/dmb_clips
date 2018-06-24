#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace DMB
{

[Serializable]
public class Display
{
    public Texture2D PlayTexture;
    public Texture2D PauseTexture;
    public Texture2D RampTexture;
    public Texture2D NubTexture;

    public float UnselectedSplineWidth = 2;
    public float SelectedSplineWidth = 5;
    public Texture2D SplineTexture;

    public SWUI UI = new SWUI();

    private const float TrackXMargin = 4;
    private const float TrackYMargin = 4;
    private const float TrackHeight = 18;
    private const float SampleYMargin = 1;

    private EditCameraPath _ecp = new EditCameraPath();
    private EditFollowPath _efp = new EditFollowPath();

    public float TrackWidth { get { return UI.Window.width - TrackXMargin - TrackYMargin; } }

    public void Init()
    {
        UI.Init();
    }

    public void DeselectAll()
    {
        _ecp.Deselect();
        _efp.Deselect();
    }

    public void Reset()
    {
        DeselectAll();
    }

    public bool CheckDeleteKey( Clip clip, out KeyedSample modifiedSample, out int keyToRemove )
    {
        if ( _ecp.IsSelected() && _ecp.IsSelectedNode() ) {
            var cps = clip.CamPathSamples[_ecp.GetSelectedPath()];
            if ( cps.Keys.Count > 2 ) {
                modifiedSample = cps;
                keyToRemove = cps.KeyFromPoint( _ecp.GetSelectedPoint() );
                return true;
            }
        }
        // FIXME: do it, finaly merge common functionality 
        //if ( _efp.IsSelected() && _efp.IsSelectedNode() ) {
        //  var fps = clip.FollowPathSamples[_efp.GetSelectedPath()];
        //  if ( fps.Keys.Count > 2 ) {
        //      modifiedSample = fps;
        //      keyToRemove = fps.KeyFromPoint( _ecp.GetSelectedPoint() );
        //      return true;
        //  }
        //}
        keyToRemove = -1;
        modifiedSample = null;
        return false;
    }

    public void DrawClipInfo( Clip clip, string clipName, bool changed )
    {
        string data = clipName + " " + clip.Duration.ToString( "F1" ) + "s" 
            + ( changed ? "  MODIFIED! Press Ctrl + W to save." : "" );
        UI.DrawText( data, 0, 2, UI.Window.width, 16, shadow: true, 
                    color: changed ? new Color( 1, 0.7f, 0 ) : Color.white );
    }

    private float TracksHeight( int numTracks )
    {
        return numTracks * TrackHeight;
    }

    private float TrackY( int track, int numTracks )
    {
        return UI.Window.height - ( numTracks - track ) * TrackHeight - TrackYMargin;
    }

    public float MouseXToNormTime()
    {
        return ( Event.current.mousePosition.x - TrackXMargin ) / TrackWidth;
    }

    public float MouseXToTime( float totalTime )
    {
        return MouseXToNormTime() * totalTime;
    }

    private static void GetNormalizedTimes( Sample sample, float totalTime, 
                                            out float start, out float duration )
    {
        start = sample.StartTime / totalTime;
        duration = sample.Duration / totalTime;
    }

    private void DrawTimeString( float normTime, int numTracks, float totalTime )
    {
        float time = normTime * totalTime;
        float w = 100;
        float h = 16;
        float x = TrackXMargin + normTime * TrackWidth;
        UI.DrawText( time.ToString( "F2" ), x - w / 2, TrackY( 0, numTracks ) - h, w, h, 
                                shadow: true, color: Color.white );
    }

    private void DrawTimePositionBar( float normTime, int numTracks, float totalTime, bool drawTime = true )
    {
        float x = TrackXMargin + normTime * ( TrackWidth - 1 );
        float tracksHeight = TracksHeight( numTracks );
        float y = UI.Window.height - tracksHeight - TrackYMargin;
        UI.DrawFillBox( x - 0.5f, y, 1, tracksHeight, Color.white );
        if ( drawTime ) {
            DrawTimeString( normTime, numTracks, totalTime );
        }
    }

    private SWUI.Result DrawSampleEnd( float x, float y, float width, float height, Color color ) {
        SWUI.Result res;
        Color cact = color;
        Color cinact = new Color( 0, 0, 0, 0 );
        res = UI.InteractBoxSimple( x + ( width < 0 ? width : 0 ), y, Mathf.Abs( width ), height,
                                    colorInactiveBgr: cinact, colorActiveBgr: cact );
        if ( res == SWUI.Result.HoverEnter || res == SWUI.Result.Drag || res == SWUI.Result.LBDown ) {
            EditorGUIUtility.AddCursorRect( new Rect( 0, 0, UI.Window.width, UI.Window.height ), 
                                            MouseCursor.ResizeHorizontal );
        }
        return res;
    }

    private void GetSampleSizeInPixels( Sample sample, int numTracks, float totalTime, 
                                            out Vector2 pos, out Vector2 size )
    {
        float start;
        float duration;
        GetNormalizedTimes( sample, totalTime, out start, out duration );
        float tw = TrackWidth;
        float x = TrackXMargin + start * tw;
        float trackY = TrackY( sample.Track, numTracks );
        float y = trackY + SampleYMargin;
        float width = duration * tw;
        float height = TrackHeight - SampleYMargin * 2;
        pos.x = x;
        pos.y = y;
        size.x = width;
        size.y = height;
    }

    private string GetLabelString( Sample sample )
    {
        string result = "ERROR";
        string actorPart = GetLabelPart( sample.ActorPrefab );
        if ( sample is CameraPathSample ) {
            var cs = sample as CameraPathSample;
            result = actorPart + ( cs.IsLookatPath ? " Lookat" : " Path" );
        } else if ( sample is MecanimSample ) {
            var ma = sample as MecanimSample;
            string statePart = GetLabelPart( ma.State );
            result = statePart;
            if ( ma.Looped ) {
                result += " [L]";
            }
        } else {
            result = actorPart;
        }
        return result;
    }

    public enum DrawSampleEndsResult
    {
        None,
        Drag,
        StoppedDragging,
    }

    public DrawSampleEndsResult DrawSampleEnds( Clip clip, int numTracks, float totalTime,
                                            out Sample modifiedSample, 
                                            out float modifiedStartTime, out float modifiedDuration )
    {
        var result = DrawSampleEndsResult.None;
        float cStart = 0, cDuration = 0;
        Sample cSample = null;
        clip.ForEachSample( sample => { 
            float endsWidth = 6;
            float start;
            float duration;
            GetNormalizedTimes( sample, totalTime, out start, out duration );
            Vector2 pos, size;
            GetSampleSizeInPixels( sample, numTracks, totalTime, out pos, out size );
            SWUI.Result uiResStart = DrawSampleEnd( pos.x, pos.y, endsWidth, size.y, sample.Color );
            if ( uiResStart == SWUI.Result.Drag ) {
                float end = start + duration;
                start = Mathf.Clamp( MouseXToNormTime(), 0, end - 0.005f );
                cStart = start * totalTime;
                cDuration = ( end - start ) * totalTime;
                cSample = sample;
                UI.Pop( () => {
                    DrawTimePositionBar( start, numTracks, totalTime );
                    return false;
                } );
                result = DrawSampleEndsResult.Drag;
            }
            SWUI.Result uiResEnd = DrawSampleEnd( pos.x + size.x, pos.y, -endsWidth, size.y, sample.Color );
            if ( uiResEnd == SWUI.Result.Drag ) {
                float end = Mathf.Clamp( MouseXToNormTime(), start + 0.005f, 1 );
                cStart = sample.StartTime;
                cDuration = end * totalTime - cStart;
                cSample = sample;
                UI.Pop( () => {
                    DrawTimePositionBar( end, numTracks, totalTime );
                    return false;
                } );
                result = DrawSampleEndsResult.Drag;
            }
            if ( uiResStart == SWUI.Result.LBUp || uiResEnd == SWUI.Result.LBUp ) {
                cStart = sample.StartTime;
                cDuration = sample.Duration;
                cSample = sample;
                result = DrawSampleEndsResult.StoppedDragging;
            }
            UI.DrawFillBox( pos.x - 1, pos.y, 1, size.y, Color.black );
            UI.DrawFillBox( pos.x + size.x - 1, pos.y, 1, size.y, Color.black );
        } );
        modifiedSample = cSample;
        modifiedStartTime = cStart;
        modifiedDuration = cDuration;
        return result;
    }

    public enum DrawSampleResult
    {
        None,
        DragSample,
        DragSampleGroup,
        DragKey,
        ShowContextMenu,
        IsDragAndDrop,
        // select path splines and other world objects
        StoppedDragging,
        Select,
    }

    public float GetHorzShift( float totalTime )
    {
        return UI.MouseDelta().x * totalTime / TrackWidth;
    }

    public void DragSample( int numTracks, float totalTime, Sample sample,
                            out float modifiedStartTime, out int modifiedTrack )  
    {
        modifiedTrack = sample.Track;
        modifiedStartTime = Mathf.Clamp( sample.StartTime + GetHorzShift( totalTime ), 0, 
                                            totalTime - 0.1f );
        float halfth = TrackHeight / 2;
        float trackY = TrackY( sample.Track, numTracks );
        float offy = Event.current.mousePosition.y - ( trackY + halfth );
        if ( Mathf.Abs( offy ) > halfth ) {
            modifiedTrack = Mathf.Clamp( sample.Track + ( offy > 0 ? 1 : -1 ), 0, numTracks - 2 );
        }
    }

    public DrawSampleResult DrawSample( int numTracks, float totalTime, 
                                        Sample sample, string name, Color color, 
                                        out UnityEngine.Object dragAndDrop,
                                        float fadeIn = 0, float fadeOut = 0 )
    {
        DrawSampleResult result = DrawSampleResult.None;
        float start;
        float duration;
        GetNormalizedTimes( sample, totalTime, out start, out duration );
        float tw = TrackWidth;
        Vector2 pos, size;
        GetSampleSizeInPixels( sample, numTracks, totalTime, out pos, out size );
        bool hover;
        Vector2 drag;
        float tint = 0.9f;
        float tintABgr = 0.7f;
        float tintBgr = 0.5f;
        string tagText = sample.Tag != "__NOT_SET__" ? ( " " + sample.Tag ) : "";
        SWUI.Result uiResult = UI.InteractBox( pos.x + fadeIn * tw, pos.y, 
                size.x - ( fadeIn + fadeOut ) * tw, size.y, 
                out hover, out drag, out dragAndDrop,
                text: name + " " + sample.Duration.ToString( "F2" ) + tagText,
                colorInactive: new Color( color.r * tint, color.g * tint, color.b * tint ), 
                colorActive: color,
                colorInactiveBgr: new Color( color.r * tintBgr, color.g * tintBgr, color.b * tintBgr ), 
                colorActiveBgr: new Color( color.r * tintABgr, color.g * tintABgr, color.b * tintABgr ) );
        if ( uiResult == SWUI.Result.DragAndDropExit ) {
            result = DrawSampleResult.IsDragAndDrop;
        } else if ( uiResult == SWUI.Result.RBUp ) {
            result = DrawSampleResult.ShowContextMenu;
        } 
        // LBUp messes up because of sorting?, use LBDown instead
        else if ( uiResult == SWUI.Result.LBDown ) {
            result = DrawSampleResult.Select;
        // Should be on LBUp, otherwise the display stops receiving ui events...
        // FIXME: look at this when on LBDown
        } else if ( uiResult == SWUI.Result.LBUp ) {
            result = DrawSampleResult.StoppedDragging;
        } else {
            Vector2 mouseDelta = UI.MouseDelta();
            if ( uiResult == SWUI.Result.Drag && mouseDelta.sqrMagnitude > 0.0001f ) {
                if ( Event.current.shift ) {
                    result = DrawSampleResult.DragSampleGroup;
                } else {
                    result = DrawSampleResult.DragSample;
                }
            }
        }
        Color rampColor = color * ( hover ? tintABgr : tintBgr ) * 1.25f;
        rampColor.a = 0.5f;
        if ( fadeIn > 0.0001f ) {
            UI.DrawTexture( pos.x, pos.y, fadeIn * tw, size.y, RampTexture, color: rampColor );
        }
        if ( fadeOut > 0.0001f ) {
            UI.DrawTexture( pos.x + size.x - fadeOut * tw, pos.y, -fadeOut * tw, size.y, 
                            RampTexture, color: rampColor );
        }
        return result;
    }

    public enum DrawTrackResult 
    {
        None,
        Scrub,
        ShowContextMenu,
        IsDragAndDrop,
        ShowInInspector,
    }

    public DrawTrackResult DrawTrack( int track, int numTracks, float totalTime, 
                                    out UnityEngine.Object dragAndDrop )
    {
        float x = TrackXMargin;
        float y = TrackY( track, numTracks ); 
        float gray = ( track & 1 ) == 0 ? 1 : 0.9f;
        Color cinact = new Color( UI.ColorButtonInactive.r * gray, 
                                UI.ColorButtonInactive.g * gray, 
                                UI.ColorButtonInactive.b * gray );
        bool hover;
        Vector2 drag;
        SWUI.Result uiResult = UI.InteractBox( x, y, TrackWidth, TrackHeight, 
                                            out hover, out drag, out dragAndDrop,
                                                    colorInactiveBgr: cinact );
        DrawTrackResult result = DrawTrackResult.None;
        if ( uiResult == SWUI.Result.Drag ) {
            result = DrawTrackResult.Scrub;
        } else if ( uiResult == SWUI.Result.LBUp ) {
            result = DrawTrackResult.ShowInInspector;
        } else if ( uiResult == SWUI.Result.DragAndDropExit && ( dragAndDrop is AnimationClip ) ) {
            result = DrawTrackResult.IsDragAndDrop;
        } else if ( uiResult == SWUI.Result.RBUp ) {
            result = DrawTrackResult.ShowContextMenu;
        }
        return result;
    }

    public bool DrawPlayButton( int numTracks, bool playback )
    {
        float x = TrackYMargin;
        float h = TrackHeight * 1.5f;
        float y = TrackY( 0, numTracks ) - h - TrackYMargin;
        Texture2D tex = playback ? PauseTexture : PlayTexture;
        float w = 60 - TrackYMargin * 2;
        return UI.Button( tex, x, y, w, h );
    }

    public bool DrawNub( float normTime, int numTracks, float totalTime, 
                            bool draggingOnTrack, bool playback )
    {
        DrawTimePositionBar( normTime, numTracks, totalTime, drawTime: false );
        float x = TrackXMargin + normTime * TrackWidth;
        float y = UI.Window.height - TrackHeight - TrackYMargin + 3;
        bool hover;
        bool dragNub = UI.InteractBoxSimple( x - NubTexture.width / 2.0f - 1, y, 
                    NubTexture.width, -NubTexture.height, out hover,
                    texture: NubTexture, skipBackground: true ) == SWUI.Result.Drag;
        if ( draggingOnTrack || hover || dragNub || playback ) {
            DrawTimeString( normTime, numTracks, totalTime );
        }
        float dragAmount = Mathf.Abs( UI.MouseDelta().x );
        return draggingOnTrack || ( dragNub && dragAmount > 0 );
    }

    public void UpdateCameraLine( Vector3 camPos, Vector3 camLookat )
    {
        _ecp.SetLookatSegment( camPos, camLookat );
    }

    public bool DrawCameraPaths( Clip clip, out CameraPathSample modifiedSample, out Vector3 [] modifiedPoints ) 
    {
        _ecp.DrawLookatLine();
        modifiedPoints = null;
        modifiedSample = null;
        for ( int i = 0; i < clip.CamPathSamples.Count; i++ ) {
            var cps = clip.CamPathSamples[i];
            Vector3 [] points = cps.GetPoints();
            if ( _ecp.DrawPath( points, cps.Color, i, SelectedSplineWidth, UnselectedSplineWidth, 
                                    SplineTexture ) ) {
                modifiedPoints = points;
                modifiedSample = cps;
            }
        }
        return modifiedSample != null;
    }

    public bool DrawFollowPaths( Clip clip, float absoluteTime, out FollowPathSample modifiedSample, 
                                    out Vector3 [] modifiedPoints ) 
    {
        modifiedPoints = null;
        modifiedSample = null;
        for ( int i = 0; i < clip.FollowPathSamples.Count; i++ ) {
            var fps = clip.FollowPathSamples[i];
            Vector3 [] points = fps.GetPositions();
            int originSegment;
            Vector3 origin, chasePoint;
            EditFollowPath.SampleAtTime( fps, absoluteTime, out originSegment, out origin, out chasePoint );
            if ( _efp.DrawPath( points, origin, chasePoint, fps.Color, i, UI.IsAltHoldDown(), 
                        SelectedSplineWidth, UnselectedSplineWidth, SplineTexture ) ) {
                modifiedPoints = points;
                modifiedSample = fps;
            }
        }
        return modifiedSample != null;
    }

    private bool DrawKey( int numTracks, float totalTime, Sample sample, Color color, 
                                float keyTime, out float newKeyTime )
    {
        bool result = false;
        newKeyTime = keyTime;
        Vector2 pos, size;
        GetSampleSizeInPixels( sample, numTracks, totalTime, out pos, out size );
        if ( keyTime > 0 && keyTime < 1 ) {
            float w = 6;
            float x = pos.x + keyTime * size.x - w / 2;
            float y = pos.y;
            float h = size.y;
            SWUI.Result res = UI.InteractBoxSimple( x, y, w, h,
                                                    colorInactiveBgr: new Color( 0, 0, 0, 0), 
                                                    colorActiveBgr: color );
            if ( res == SWUI.Result.HoverEnter 
                    || res == SWUI.Result.Drag 
                    || res == SWUI.Result.LBDown ) {
                EditorGUIUtility.AddCursorRect( new Rect( 0, 0, UI.Window.width, UI.Window.height ), 
                                                MouseCursor.ResizeHorizontal );
                if ( res == SWUI.Result.Drag ) {
                    float kt = Mathf.Clamp( ( Event.current.mousePosition.x - pos.x ) / size.x, 0.01f, 0.99f );
                    UI.Pop( () => {
                        DrawTimePositionBar( ( sample.StartTime + kt * sample.Duration ) / totalTime, 
                                                numTracks, totalTime );
                        return false;
                    } );
                    newKeyTime = kt;
                    result = true;
                }
            }
        }
        return result;
    }   

    private void DrawUnderline( Vector2 pos, Vector3 size, float [] moments ) 
    {
        if ( moments.Length < 2 ) {
            return;
        }
        for ( int i = 0; i < moments.Length - 1; i++ ) {
            float mi = moments[i];
            float dist = ( moments[i + 1] - mi ) * size.x;
            float start = mi * size.x;
            float nubSize = 4;
            UI.DrawFillBox( pos.x + start + nubSize / 2, pos.y + size.y - 3, 
                            dist - nubSize, 3, new Color( 0, 0, 0, 0.7f ) );
        }
    }

    private bool DrawKeyTimes( int numTracks, float totalTime, Sample sample, Color c, float [] times, 
                                out int modifiedKey, out float modifiedKeyTime )
    {
        modifiedKey = -1;
        modifiedKeyTime = 0;
        for ( int i = 0; i < times.Length; i++ ) {
            float newTime;
            if ( DrawKey( numTracks, totalTime, sample, c, times[i], out newTime ) ) {
                modifiedKey = i;
                modifiedKeyTime = newTime;
            }
        }
        return modifiedKey >= 0;
    }

    private DrawSampleResult DrawKeyedSample( int numTracks, float totalTime, Sample sample, 
                                                string name, Color color, float [] keyTimes, 
                                                out int modifiedTrack, out float modifiedStartTime,
                                                out int modifiedKey, out float modifiedKeyTime,
                                                float fadeIn = 0, float fadeOut = 0 )
    {
        modifiedTrack = sample.Track;
        modifiedStartTime = sample.StartTime;
        modifiedKey = 0;
        modifiedKeyTime = keyTimes[0];
        UnityEngine.Object dragAndDrop;
        DrawSampleResult res = DrawSample( numTracks, totalTime, sample, name, color, out dragAndDrop, 
                                            fadeIn, fadeOut );
        if ( DrawKeyTimes( numTracks, totalTime, sample, color, keyTimes, 
                            out modifiedKey, out modifiedKeyTime ) ) {
            res = DrawSampleResult.DragKey;
        } else if ( res == DrawSampleResult.DragSample ) {
            DragSample( numTracks, totalTime, sample, out modifiedStartTime, out modifiedTrack );
        }
        Vector2 pos, size;
        GetSampleSizeInPixels( sample, numTracks, totalTime, out pos, out size );
        DrawUnderline( pos, size, keyTimes );
        return res;
    }

    private static string GetLabelPart( string label )
    {
        if ( label.Length < 8 ) {
            return label;
        }
        string newString = "";
        for ( int i = 0; i < 8 + 3; i++ ) {
            newString += i < 8 ? label[i] : '.';
        }
        return newString;
    }

    private bool CanCrossfade( Sample sample, Sample other ) 
    {
        return other != null && ( sample is MecanimSample ) && sample.ActorPrefab == other.ActorPrefab;
    }
    
    public DrawSampleResult DrawSamples( Clip clip, int numTracks, float totalTime,
                                        out Sample modifiedSample, 
                                        out int modifiedTrack, out float modifiedStartTime,
                                        out int modifiedKey, out float modifiedKeyTime )
    {
        int cTrack = -1;
        float cStartTime = 0;
        Sample cSample = null;
        int cKey = -1;
        float cKeyTime = 0;
        DrawSampleResult result = DrawSampleResult.None;
        clip.ForEachSample( sample => {
            float fadeIn, fadeOut;
            Sample prev, next;
            clip.GetSamplesOverlaps( sample, out prev, out next, out fadeIn, out fadeOut );
            fadeIn = CanCrossfade( sample, prev ) ? fadeIn / totalTime : 0;
            fadeOut = CanCrossfade( sample, next ) ? fadeOut / totalTime : 0;
            if ( ! CanCrossfade( sample, next ) ) {
                fadeOut = 0;
            }
            string name = GetLabelString( sample );
            int mtr = 0, mk = 0;
            float mst = 0, mkt = 0;
            DrawSampleResult res;
            if ( sample is KeyedSample ) {
                float [] keyTimes = ( sample as KeyedSample ).GetKeyTimes();
                res = DrawKeyedSample( numTracks, totalTime, sample, name, sample.Color, keyTimes, 
                                        out mtr, out mst, out mk, out mkt, fadeIn, fadeOut );
            } else {
                UnityEngine.Object dragAndDrop;
                res = DrawSample( numTracks, totalTime, sample, name, sample.Color, out dragAndDrop, 
                                    fadeIn, fadeOut );
                if ( res == DrawSampleResult.DragSample ) {
                    DragSample( numTracks, totalTime, sample, out mst, out mtr );
                }
                if ( sample is MecanimSample ) {
                    var ma = sample as MecanimSample;
                    if ( ma.Looped && ma.Duration > 0 && ma.OneShotDuration > 0 ) {
                        var moments = new List<float>();
                        float step = ma.OneShotDuration / ma.Duration;
                        for ( float moment = 0; moment < 1; moment += step ) {
                            moments.Add( moment );
                        }
                        moments.Add( 1 );
                        Vector2 pos, size;
                        GetSampleSizeInPixels( sample, numTracks, totalTime, out pos, out size );
                        DrawUnderline( pos, size, moments.ToArray() );
                    }
                }
            }
            if ( res != DrawSampleResult.None ) {
                if ( res == DrawSampleResult.StoppedDragging ) {
                    if ( sample is FollowPathSample ) {
                        var fp = sample as FollowPathSample;
                        _ecp.Deselect();
                        _efp.SelectPath( -1, clip.FollowPathSamples.IndexOf( fp ) );
                    } else {
                        var cp = sample as CameraPathSample;
                        _efp.Deselect();
                        _ecp.SelectPath( -1, clip.CamPathSamples.IndexOf( cp ) );
                    }
                } 
                cTrack = mtr;
                cStartTime = mst;
                cKey = mk;
                cKeyTime = mkt;
                cSample = sample;
                result = res;
            }
        } );
        modifiedTrack = cTrack;
        modifiedStartTime = cStartTime;
        modifiedKey = cKey;
        modifiedKeyTime = cKeyTime;
        modifiedSample = cSample;
        return result;
    }
}

}

#endif
