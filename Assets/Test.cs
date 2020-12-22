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
    /// A representation of a moment in the audio that we've gathered the
    /// samples for and extracted.
    /// </summary>
    public class Grain
    {
        /// <summary>
        /// The cached original start of where it was taken from the auidio.
        /// </summary>
        public float origStart;

        /// <summary>
        /// The time, in seconds, on when it should start adding content to the stream.
        /// </summary>
        public float start;

        /// <summary>
        /// The amount of time it should add content to the stream for. This is tied to
        /// the Grain.samples variable - because we don't have audio past this.
        /// </summary>
        public float width;

        /// <summary>
        /// The amount of time to ramp up when adding content to the stream. This should 
        /// not be greater than Grain.width - Grain.outEdge.
        /// </summary>
        public float inEdge;

        /// <summary>
        /// The amount of time to ramp down when adding content to the stream. This should
        /// not be greater than Grain.width - Grain.inEdge.
        /// </summary>
        public float outEdge;

        /// <summary>
        /// The PCM samples for the grain.
        /// </summary>
        public float [] samples;
    }

    /// <summary>
    /// The audio clip recorded.
    /// </summary>
    public AudioClip audioClip = null;

    /// <summary>
    /// The processed audio when the recorded audio is granulated.
    /// </summary>
    public AudioClip granulatedClip = null;

    /// <summary>
    /// The audio source to play from. Set in the inspector.
    /// </summary>
    public AudioSource audioSource;

    /// <summary>
    /// The name of the recording device recorded from. Cached and displayed for debugging.
    /// </summary>
    public string recordingDevice;

    /// <summary>
    /// The grains extracted from the recorded audio clips.
    /// </summary>
    List<Grain> grains = null;

    /// <summary>
    /// The record and playback rate. Kept as a constant to keep things simple.
    /// </summary>
    const int FreqRate = 44100;

    const float MaxScale = 4.0f;        // The highest amount we'll allow for an individual scaling operation
    const float MinScale = 0.25f;        // The smallest amount we'll allow for an individual scaling operation.

    const float MinGrainWidth = 0.1f;   // The lowest amount we'll allow for grain widths (in seconds)
    const float MaxGrainWidth = 0.5f;   // The highest amount we'll allow for grain widths (in seconds)

    const float MinGrainStride = 0.05f;
    const float MaxGrainStride = 0.5f;

    float gain = 1.0f;

    /// <summary>
    /// Maximum number of seconds we'll allow for recording.
    /// </summary>
    const int MaxRecordTime = 5;

    float scaleAmt = 1.5f;
    float grainWidth = 0.2f;
    float grainStride = 0.1f;

    // Used for preview of how much recording time is left.
    float timeStartedRecording = 0.0f;  

    public TextAsset previewData;

    Vector2 uiScroll = Vector2.zero;

    void Start()
    {
        if(Microphone.devices.Length > 0)
            this.recordingDevice = Microphone.devices[0];
    }

    // void Update()
    // {}

    


    /// <summary>
    /// Granulate the audio data in the recorded audio.
    /// </summary>
    /// <param name="grainLen">The length of a grain.</param>
    /// <param name="skipLen">The distance (in seconds) between grains.</param>
    /// <param name="inlen">The ramp-up time for each grain.</param>
    /// <param name="outlen">The ramp-down time for each grain.</param>
    /// <returns>The ordered list of extracted grains.</returns>
    public static List<Grain> Granulate(AudioClip clip, float grainLen, float skipLen, float inlen, float outlen)
    {
        List<Grain> ret = new List<Grain>();

        if(clip == null)
            return ret;

        // Sanity fix
        grainLen = Mathf.Max(grainLen, inlen + outlen); 

        int sampleCt = clip.samples;
        float [] samps = new float[sampleCt];
        clip.GetData(samps, 0);

        float loc = 0.0f;

        int grainSamples = (int)(clip.frequency * grainLen);
        while (loc < clip.length)
        {
            Grain g = new Grain();
            g.inEdge = inlen;
            g.outEdge = outlen;
            g.start = loc;
            g.origStart = loc;
            g.width = grainLen;

            int grainStart = (int)(clip.frequency * loc);
            int grainEnd = grainStart + grainSamples;
            
            // Detect the last sample we'll copy over. It's either when we can't
            // record any more, or when we go past the last sample we're granulating.
            int sampleCpy = Mathf.Min(grainEnd, sampleCt) - grainStart;

            g.samples = new float[grainSamples];

            int i = 0;
            for(; i <  sampleCpy; ++i)
                g.samples[i] = samps[i + grainStart];

            // Contingency for the last grain - it's probably going to go past the
            // end of the samples. We could have modified the length to end exactly,
            // but we're just going to fill it with zeros.
            for(; i < grainSamples; ++i)
                g.samples[i] = 0;

            ret.Add(g);
            loc += skipLen;
        }

        return ret;
    }

    /// <summary>
    /// Given a list of grains, scale the start time of the entire list
    /// by a constant value.
    /// </summary>
    /// <param name="grains">The grains to start the start times.</param>
    /// <param name="scale">The amount to scale the start times.</param>
    /// <param name="compound">If true, scale by current grain time, else scale by the original time.</param>
    public static void ScaleGrainTime(List<Grain> grains, float scale, bool compound)
    {
        for(int i = 0; i < grains.Count; ++i)
        {
            if(compound == true)
                grains[i].start *= scale;
            else
                grains[i].start = grains[i].origStart * scale;
        }
    }

    /// <summary>
    /// Given a list of grains, reconstruct them into the PCM for an audio stream.
    /// </summary>
    /// <remarks>The function assumes the list is ordered - or for the very least
    /// that the last grain has the farthest time.</remarks>
    /// <param name="grains">The grains to reconstruct.</param>
    /// <returns>The reconstructed audio from the grains.</returns>
    public static float[] ReconstructGrains(List<Grain> grains, float gain)
    {
        Grain lastGrain = grains[grains.Count - 1];
        int retSampleCt = (int)(lastGrain.start * FreqRate) + lastGrain.samples.Length;

        // Doing a weighted average over time, so for each moment in time.
        float [] ret = new float[retSampleCt];  // accumulated Sample_Value * Weight
        float [] wt = new float[retSampleCt];   // accumulated Weight
        for (int i = 0; i < retSampleCt; ++i)
        {
            ret[i] = 0;
            wt[i] = 0;
        }

        foreach (Grain g in grains)
        {
            int start = (int)(g.start * FreqRate);      // Sample index to start accumulating data into.
            int end = g.samples.Length;                                 // The number of samples to write.
            int endup = (int)(g.inEdge * FreqRate);     // The number of samples for the ramp-up
            int endhigh = (int)(g.outEdge * FreqRate);  // The number of samples before ramping down.

            int i = 0;  // The current grain sample we're writing.

            // Write samples ramping up - just a linear increase in weight.
            for (i = 0; i < endhigh; ++i)
            {
                float lam = ((float)i / (float)endhigh);
                ret[start + i] += lam * g.samples[i];
                wt[start + i] += lam;
            }

            // Write samples non-ramping - just a weight of 1.0
            for (i = endup; i < endhigh; ++i)
            {
                ret[start + i] += g.samples[i];
                wt[start+ i] += 1.0f;
            }

            // Write samples ramping down - just a linear decrease in weight.
            float sub = 1.0f / ((float)end - (float)endhigh);
            float downWt = 1.0f;
            for (i = endhigh; i < end; ++i)
            {
                ret[start + i] += g.samples[i] * downWt;
                wt[start+i] += downWt;
                downWt -= sub;
            }
        }

        // Divide each sample by the weight to turn the samples into a weighted average.
        for (int i = 0; i < ret.Length; ++i)
        {
            if(wt[i] == 0.0f)
                ret[i] = 0.0f;
            else
                ret[i] = ret[i] / wt[i] * gain;
        }

        return ret;
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

        GUILayout.Label("MIC : " + this.recordingDevice);

        if (Microphone.IsRecording(this.recordingDevice) == false)
        {
            if (GUILayout.Button("Record", GUILayout.Height(50.0f)) == true)
            {
                this.audioClip = Microphone.Start(this.recordingDevice, false, MaxRecordTime, FreqRate);
                this.timeStartedRecording = Time.time;
                this.granulatedClip = null;
                this.grains = null;
            }
            if(GUILayout.Button("Load Sample") == true)
            {
                // https://stackoverflow.com/a/4636735
                byte [] rb = previewData.bytes;
                float [] floatArray = new float[rb.Length / 4];
                System.Buffer.BlockCopy(rb, 0, floatArray, 0, rb.Length);

                this.audioClip = AudioClip.Create("", floatArray.Length, 1, FreqRate, false);
                this.audioClip.SetData(floatArray, 0);
                this.granulatedClip = null;
                this.grains = null;
            }
        }
        else
        {
            GUI.color = Color.red;

            string timeLeft = (MaxRecordTime - (Time.time - this.timeStartedRecording)).ToString("0.00");
            if (GUILayout.Button($"Stop\n" + timeLeft, GUILayout.Height(50.0f)) == true)
                Microphone.End(this.recordingDevice);

            GUI.color = Color.white;

        }

        if (this.audioClip == null)
        {
            GUILayout.Label("There is not audio to test granulation on. Press the \"Record\" button to record a sample.");
            return;
        }

        if (Microphone.IsRecording(this.recordingDevice) == true)
            return;

#if UNITY_EDITOR
        if(GUILayout.Button("Save Preview") == true)
            SaveAudio();
#endif

        GUIHeader("STEP 2) Preview recording & Grainulate");

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

            this.grains =
                Granulate(
                    this.audioClip,
                    this.grainWidth,
                    this.grainStride,
                    this.grainWidth * 0.45f,
                    this.grainWidth * 0.45f);
        }

        if (wipeGrains == true)
            this.grains = null;

        if (this.grains == null)
            return;

        GUIHeader("STEP 3) Reconstruct Grains");

        GUILayout.Label($"Has {this.grains.Count} grains.");


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
            ScaleGrainTime(this.grains, this.scaleAmt, false);

            float[] samp = ReconstructGrains(this.grains, this.gain);
            AudioClip newclip = AudioClip.Create("", samp.Length, 1, FreqRate, false);
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
