using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class WebMic : MonoBehaviour
{
    public enum State
    { 
        Booting,
        NotActive,
        Recording
    }

    [DllImport("__Internal")]
    public static extern void Hello();

    [DllImport("__Internal")]
    public static extern void HelloString(string str);

    [DllImport("__Internal")]
    public static extern void Recording_Start();

    [DllImport("__Internal")]
    public static extern void Recording_Stop();

    [DllImport("__Internal")]
    public static extern bool Recording_IsRecording();

    /// <summary>
    /// The record and playback rate. Kept as a constant to keep things simple.
    /// </summary>
    public const int FreqRate = 44100;

    AudioClip recordingClip = null;

    /// <summary>
    /// The name of the recording device recorded from. Cached and displayed for debugging.
    /// </summary>
    public string recordingDevice;

    /// <summary>
    /// Maximum number of seconds we'll allow for recording.
    /// </summary>
    public const int MaxRecordTime = 5;

    private void Start()
    {
        //Recording_Init();
    }

    public AudioClip GetAudioClip()
    {
        return this.recordingClip;
    }

#if !UNITY_WEBGL || UNITY_EDITOR
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
        this.recordingClip = Microphone.Start(this.recordingDevice, false, MaxRecordTime, FreqRate);
        return this.recordingClip != null;
    }

    public AudioClip StopRecording()
    {
        Microphone.End(this.recordingDevice);
        return this.recordingClip;
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
    private List<string> binaryStreams = new List<string>();
    State recordingState = State.NotActive;

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

        this.recordingClip = null;
        return true;
    }

    public AudioClip StopRecording()
    {
        NotifyRecordingChange((int)State.NotActive);
        Recording_Stop();
        return this.recordingClip;
    }

    public State RecordingState()
    {
        return this.recordingState;
    }

    public bool ClearRecording()
    {
        Debug.Log("Clearing recording");

        if (this.binaryStreams.Count == 0)
            return false;

        this.binaryStreams.Clear();

        return true;
    }

    public float [] GetData(bool clear = true)
    { 
        Debug.Log("GetData");
        List<float[]> datas = new List<float[]>();

        int fCt = 0;
        foreach(string str in this.binaryStreams)
        {
            byte[] rb = System.Convert.FromBase64String(str);
            float[] rf = new float[rb.Length / 4];

            System.Buffer.BlockCopy(rb, 0, rf, 0, rb.Length);
            datas.Add(rf);
            fCt += rf.Length;
        }

        float [] ret = new float[fCt];


        int write = 0;
        foreach(float [] rf in datas )
        { 
            int byteCt = rf.Length * 4;
            System.Buffer.BlockCopy(rf, 0, ret, write, byteCt);
            write += byteCt;
        }

        Debug.Log($"Called GetData() with {this.binaryStreams.Count} streams to merge");

        if(clear == true)
            ClearRecording();


        return ret;
    }

    /// <summary>

    /// </summary>
    /// <param name="newRS"></param>
    /// <remarks>Called with SendMessage outside web management stuff.<remarks>
    public void NotifyRecordingChange(int newRS)
    { 
        Debug.Log("notified recording change " + ((State)newRS).ToString());

        if((int)this.recordingState == newRS)
            return;

        State oldState = this.recordingState;
        this.recordingState = (State)newRS;

        if(oldState == State.Recording)
            this.recordingClip = this.FlushDataIntoClip();

    }

    public void ReceiveB64FloatPCMChunk(string val)
    {
        // Reject excess.
        if(this.recordingState == State.NotActive)
            return;

        this.binaryStreams.Add(val);
    }

    AudioClip FlushDataIntoClip()
    {
        Debug.Log("called FlushData");

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
