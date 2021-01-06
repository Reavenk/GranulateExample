// MIT License
// 
// Copyright (c) 2021 Pixel Precision, LLC
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A class to wrap the functionality of the microphone to hack in microphone 
/// abilities for WebGL.
/// 
/// It does not handle streaming, and for sanity sake for the WebGL platform,
/// it only records WebMic.MaxRecordTime seconds of audio.
/// 
/// For simplicity, the audio is hard coded to 44100 and we only care about mono audio.
/// </summary>
/// <remarks>It's expected to be added to a root GameObject named "Managers"</remarks>
/// <remarks>The recording works differently than Unity's base implementation, 
/// in that the AudioClip isn't available until after the recording is finished.</remarks>
/// <remarks>To use properly for the browser, make sure MicRoutines.js included on
/// the webpage. This will have to be done on the webpage's HTML source outside of Unity.</remarks>
public class WebMic : MonoBehaviour
{
    /// <summary>
    /// The state of recording. For sanity/portability sake, anything using the WebMic should be
    /// coded to pretend that all 3 states of plausible for whatever platform it's running on -
    /// i.e., don't hardcode anything platform-specific that WebMic is in charge of wrapping
    /// logic for.
    /// </summary>
    public enum State
    { 
        /// <summary>
        /// The microphone is being prepared for use, but is not ready yet.
        /// </summary>
        Booting,

        /// <summary>
        /// Not recording.
        /// </summary>
        NotActive,

        /// <summary>
        /// Currently recording.
        /// </summary>
        Recording
    }

    /// <summary>
    /// Called to start web recording functionality.
    /// </summary>
    [DllImport("__Internal")]
    public static extern void Recording_Start();

    /// <summary>
    /// Called to stop web recording.
    /// </summary>
    [DllImport("__Internal")]
    public static extern void Recording_Stop();

    [DllImport("__Internal")]
    public static extern bool Recording_UpdatePointer(float [] idx);

    /// <summary>
    /// The record and playback rate. Kept as a constant to keep things simple.
    /// </summary>
    public const int FreqRate = 44100;

    /// <summary>
    /// When a recording is finished, it's cached for whoever want to pluck it out.
    /// Only the very last recording is held.
    /// </summary>
    AudioClip _recordingClip = null;
    public AudioClip RecordingClip 
    {
        get=>this._recordingClip; 
        private set{this._recordingClip = value; } 
    }

    /// <summary>
    /// The name of the recording device recorded from. Cached and displayed for debugging.
    /// </summary>
    public string recordingDevice;

    /// <summary>
    /// Maximum number of seconds we'll allow for recording.
    /// </summary>
    public const int MaxRecordTime = 5;

    // private void Start()
    // {}

    // The functionality for when we're testing outside of a web browser.
    // To developer for the web browser, temporarily switch to #if false
#if !UNITY_WEBGL || UNITY_EDITOR
//#if false
    public bool SetDefaultRecordingDevice()
    {
        if (Microphone.devices.Length > 0)
        {
            this.recordingDevice = Microphone.devices[0];
            return true;
        }
        return false;
    }

    public bool StartRecording()
    {
        this.RecordingClip = Microphone.Start(this.recordingDevice, false, MaxRecordTime, FreqRate);
        return this.RecordingClip != null;
    }

    public AudioClip StopRecording()
    {
        Microphone.End(this.recordingDevice);
        return this.RecordingClip;
    }

    public State RecordingState()
    {
        return
            Microphone.IsRecording(this.recordingDevice) ?
                State.Recording :
                State.NotActive;
    }

    public bool ClearRecording()
    {
        return true;
    }

#else

    const int BufferSize = 2048;
    public struct FloatArray
    { 
        public float [] buffer;
        public int written;
    }

    private List<FloatArray> binaryStreams = new List<FloatArray>();
    State recordingState = State.NotActive;

    FloatArray currentBuffer;

    public void Awake()
    {
        this.currentBuffer = new FloatArray();
        this.currentBuffer.buffer = new float[BufferSize];
        Recording_UpdatePointer(this.currentBuffer.buffer);
    }

    public void LogWrittenBuffer(int written)
    { 
        if(this.recordingState != State.Recording)
            return;

        this.currentBuffer.written = written;
        this.binaryStreams.Add(this.currentBuffer);

        this.currentBuffer = new FloatArray();
        this.currentBuffer.buffer = new float[BufferSize];
        Recording_UpdatePointer(this.currentBuffer.buffer);
    }

    public bool SetDefaultRecordingDevice()
    {
        return false;
    }

    public bool StartRecording()
    {
        if(this.recordingState != State.NotActive)
            return false;

        this.recordingState = State.Booting;

        Recording_Start();

        this.RecordingClip = null;
        return true;
    }

    public AudioClip StopRecording()
    {
        Recording_Stop();
        return this.RecordingClip;
    }

    public State RecordingState()
    {
        return this.recordingState;
    }

    public bool ClearRecording()
    {
        if (this.binaryStreams.Count == 0)
            return false;

        this.binaryStreams.Clear();

        return true;
    }

    public float [] GetData(bool clear = true)
    { 
        int fCt = 0;
        foreach(FloatArray fa in this.binaryStreams)
            fCt += fa.written;

        float [] ret = new float[fCt];


        int write = 0;
        foreach(FloatArray fa in this.binaryStreams)
        { 
            System.Buffer.BlockCopy(fa.buffer, 0, ret, write * 4, fa.written * 4);
            write += fa.written;
        }

        if (clear == true)
            ClearRecording();

        return ret;
    }

    /// <summary>
    // Called from JavaScript to notify the app that the microphone recording
    // state has changed.
    /// </summary>
    /// <param name="newRS">The new recording state. The int value of a State enum.</param>
    /// <remarks>Called with SendMessage outside web management stuff.<remarks>
    public void NotifyRecordingChange(int newRS)
    { 
        if((int)this.recordingState == newRS)
            return;

        State oldState = this.recordingState;
        this.recordingState = (State)newRS;

        if(oldState == State.Recording)
            this.RecordingClip = this.FlushDataIntoClip();
    }

    AudioClip FlushDataIntoClip()
    {
        float[] pcm = this.GetData();
        if (pcm != null && pcm.Length > 0)
        {
            AudioClip ac = AudioClip.Create("", pcm.Length, 1, FreqRate, false);
            ac.SetData(pcm, 0);
            return ac;
        }
        return null;
    }

#endif
}
