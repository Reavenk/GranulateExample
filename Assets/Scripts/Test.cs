// MIT License
// 
// Copyright (c) 2020 Pixel Precision, LLC
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

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sample to test granulation of audio.
/// </summary>
public class Test : MonoBehaviour
{
    /// <summary>
    /// The audio clip recorded.
    /// </summary>
    AudioClip audioClip = null;

    /// <summary>
    /// Binary float array data holding the PCM for the packaged audio sample.
    /// </summary>
    public TextAsset previewData;

    /// <summary>
    /// The audio source to play from. Set in the inspector.
    /// </summary>
    public AudioSource audioSource;

    
    const float MaxScale = 4.0f;        // The highest amount we'll allow for an individual scaling operation
    const float MinScale = 0.25f;        // The smallest amount we'll allow for an individual scaling operation.

    const float MinGrainWidth = 0.1f;   // The lowest amount we'll allow for grain widths (in seconds)
    const float MaxGrainWidth = 0.5f;   // The highest amount we'll allow for grain widths (in seconds)

    const float MinGrainStride = 0.05f;
    const float MaxGrainStride = 0.5f;

    float gain = 1.0f;

    float scaleAmt = 1.5f;
    float grainWidth = 0.2f;
    float grainStride = 0.1f;

    Grainer grainer = new Grainer();

    /// <summary>
    /// Cached recording state - which we can compare with WebMic's value to
    /// see if the recording state changed from the previous frame. This is 
    /// done as a hack because WebMic doesn't have any features for a callback
    /// when the recording stops from passing the max record time.
    /// </summary>
    bool recording = false;

    // Used for preview of how much recording time is left.
    float timeStartedRecording = 0.0f;  

    /// <summary>
    /// Scrollbar value for the UI.
    /// </summary>
    Vector2 uiScroll = Vector2.zero;

    /// <summary>
    /// Microphone wrapper.
    /// </summary>
    public WebMic mic;

    void Start()
    {
        this.mic.SetDefaultRecordingDevice();
    }

    void Update()
    {
#if UNITY_WEBGL
        if(this.mic.RecordingState() == WebMic.State.Recording)
        { 
            float recordLeft = Time.time - this.timeStartedRecording;

            if(recordLeft >= WebMic.MaxRecordTime)
            {
                this.audioClip = this.mic.StopRecording();
                this.recording = false;
            }
        }
#endif
    }

    private void OnGUI()
    {
        this.uiScroll = GUILayout.BeginScrollView(this.uiScroll, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height));
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical(GUILayout.Width(400.0f));
    
        this.GUIInnerUI();
    
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.EndScrollView();
    }

    public void GUIInnerUI()
    {
        GUIHeader("STEP1) Record sample audio");

#if !UNITY_WEB || UNITY_EDITOR
        GUILayout.Label("MIC : " + this.mic.recordingDevice);
#endif

        if (this.mic.RecordingState() ==  WebMic.State.NotActive)
        {
            if (GUILayout.Button("Record", GUILayout.Height(50.0f)) == true)
            {
                this.mic.StartRecording();
                
                this.timeStartedRecording = Time.time;
                this.grainer.Clear();
            }
            if(GUILayout.Button("Load Sample") == true)
            {
                // https://stackoverflow.com/a/4636735
                byte [] rb = previewData.bytes;
                float [] floatArray = new float[rb.Length / 4];
                System.Buffer.BlockCopy(rb, 0, floatArray, 0, rb.Length);
        
                this.audioClip = AudioClip.Create("", floatArray.Length, 1, WebMic.FreqRate, false);
                this.audioClip.SetData(floatArray, 0);
            }
        }
        else
        {
            this.recording = true;
        
            GUI.color = Color.red;
        
            string timeLeft = (WebMic.MaxRecordTime - (Time.time - this.timeStartedRecording)).ToString("0.00");
            if (GUILayout.Button($"Stop\n" + timeLeft, GUILayout.Height(50.0f)) == true)
                this.audioClip = this.mic.StopRecording();
                
        
            GUI.color = Color.white;
        
        }
        
        if(this.recording == true)
        { 
            this.audioClip = this.mic.RecordingClip;
            this.recording = false;
        }
        
        if (this.audioClip == null)
        {
            GUILayout.Label("There is not audio to test granulation on. Press the \"Record\" button to record a sample.");
            return;
        }
        
        if (this.mic.RecordingState() != WebMic.State.NotActive)
            return;
        
//#if UNITY_EDITOR
//        if(GUILayout.Button("Save Preview") == true)
//            SaveAudio();
//#endif
    
        GUIHeader("STEP 2) Preview recording & Granulate");
    
        GUILayout.Label("Press Play to preview the recorded sample.");
        if (GUILayout.Button("Play") == true)
        {
            this.audioSource.clip = this.audioClip;
            this.audioSource.Play();
        }
    
        bool wipeGrains = false;
    
        GUILayout.Label("Grain widths (seconds)");
        if (GUISlider(ref this.grainWidth, MinGrainWidth, MaxGrainWidth) == true)
            wipeGrains = true;
    
        GUILayout.Space(10.0f);
        GUILayout.Label("Width Between Grains (stride) (in seconds)");
        if (GUISlider(ref this.grainStride, MinGrainStride, MaxGrainStride) == true)
            wipeGrains = true;
    
        GUILayout.Label("Press Granulate to process the recorded sample into grains.");
        if (GUILayout.Button("Granulate", GUILayout.Height(50.0f)) == true)
        {
            wipeGrains = false;
    
            this.grainer.Granulate(
                this.audioClip,
                this.grainWidth,
                this.grainStride,
                this.grainWidth * 0.45f,
                this.grainWidth * 0.45f);
        }
    
        if (wipeGrains == true)
            this.grainer.Clear();
    
        if (this.grainer.HasGrains() == false)
            return;
    
        GUIHeader("STEP 3) Reconstruct Grains");
    
        GUILayout.Label($"Has {this.grainer.Count} grains.");
    
    
        GUILayout.Label("Time Reconstruction Scale");
        GUISlider(ref this.scaleAmt, MinScale, MaxScale);
        GUILayout.BeginHorizontal();
    
        if (GUILayout.Button("x0.25") == true)
            this.scaleAmt = 0.25f;
    
        if (GUILayout.Button("x0.5") == true)
            this.scaleAmt = 0.5f;
    
        if (GUILayout.Button("x1.0f") == true)
            this.scaleAmt = 1.0f;
    
        if (GUILayout.Button("x1.25") == true)
            this.scaleAmt = 1.25f;
    
        if (GUILayout.Button("x1.5") == true)
            this.scaleAmt = 1.5f;
    
        if (GUILayout.Button("x2") == true)
            this.scaleAmt = 2.0f;
        GUILayout.EndHorizontal();
    
        GUILayout.Space(10.0f);
        GUILayout.Label("Audio Amp");
        GUISlider(ref this.gain, 1.0f, 2.0f);
    
    
        if (GUILayout.Button("Reconstruct", GUILayout.Height(50.0f)) == true)
        {
            this.grainer.ScaleGrainTime(this.scaleAmt, false);
    
            float[] samp = this.grainer.ReconstructGrains( this.gain);
            AudioClip newclip = AudioClip.Create("", samp.Length, 1, WebMic.FreqRate, false);
            newclip.SetData(samp, 0);
    
            this.audioSource.clip = newclip;
            this.audioSource.Play();
        }
    }
    
    public static bool GUISlider(ref float val, float min, float max)
    {
        float orig = val;
    
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
        val = GUILayout.HorizontalSlider(val, min, max, GUILayout.ExpandWidth(true));
        GUI.enabled = false;
        GUILayout.TextField(val.ToString(), GUILayout.Width(100.0f));
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    
        return orig != val;
    }
    
    public static void GUIHeader(string info)
    {
        GUILayout.Space(20.0f);
    
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Space(40.0f);
        GUILayout.BeginHorizontal();
        GUILayout.Space(20.0f);
        GUILayout.Label(info);
        GUILayout.EndHorizontal();
        GUILayout.Space(40.0f);
        GUILayout.EndVertical();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Currently unhooked for the UI, but the function used to save a 
    /// recording while running in the Editor.
    /// 
    /// We do this instead of just saving an AudioClip because Unity
    /// is finicky about reading packaged audio clips for web.
    /// </summary>
    void SaveAudio()
    {
        if (this.audioClip == null)
            return;

        float [] samples = new float[this.audioClip.samples];
        if(this.audioClip.GetData(samples, 0) == false)
            return;

        using (System.IO.FileStream file = System.IO.File.Create("Assets/Sample.bytes"))
        {
            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(file))
            {
                foreach (float value in samples)
                {
                    writer.Write(value);
                }
            }
        }
    }
#endif

}
