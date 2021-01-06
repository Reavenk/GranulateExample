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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grainer
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
        public float[] samples;
    }

    /// <summary>
    /// The grains extracted from the recorded audio clips.
    /// </summary>
    List<Grain> grains = new List<Grain>();

    /// <summary>
    /// 
    /// </summary>
    public int Count
    { 
        get
        { 
            if(this.grains == null) // Sanity check
                return 0;
            return this.grains.Count;
        }
    }

    /// <summary>
    /// Granulate the audio data in the recorded audio.
    /// </summary>
    /// <param name="grainLen">The length of a grain.</param>
    /// <param name="skipLen">The distance (in seconds) between grains.</param>
    /// <param name="inlen">The ramp-up time for each grain.</param>
    /// <param name="outlen">The ramp-down time for each grain.</param>
    /// <returns>The ordered list of extracted grains.</returns>
    public static List<Grain> GranulateFromClip(AudioClip clip, float grainLen, float skipLen, float inlen, float outlen)
    {
        List<Grain> ret = new List<Grain>();

        if (clip == null)
            return ret;

        // Sanity fix
        grainLen = Mathf.Max(grainLen, inlen + outlen);

        int sampleCt = clip.samples;
        float[] samps = new float[sampleCt];
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
            for (; i < sampleCpy; ++i)
                g.samples[i] = samps[i + grainStart];

            // Contingency for the last grain - it's probably going to go past the
            // end of the samples. We could have modified the length to end exactly,
            // but we're just going to fill it with zeros.
            for (; i < grainSamples; ++i)
                g.samples[i] = 0;

            ret.Add(g);
            loc += skipLen;
        }

        return ret;
    }

    public int Granulate(AudioClip clip, float grainLen, float skipLen, float inlen, float outlen)
    { 
        this.grains = GranulateFromClip(clip, grainLen, skipLen, inlen, outlen);
        return this.grains.Count;
    }

    /// <summary>
    /// Given a list of grains, scale the start time of the entire list
    /// by a constant value.
    /// </summary>
    /// <param name="grains">The grains to start the start times.</param>
    /// <param name="scale">The amount to scale the start times.</param>
    /// <param name="compound">If true, scale by current grain time, else scale by the original time.</param>
    public void ScaleGrainTime(float scale, bool compound)
    {
        for (int i = 0; i < this.grains.Count; ++i)
        {
            if (compound == true)
                this.grains[i].start *= scale;
            else
                this.grains[i].start = this.grains[i].origStart * scale;
        }
    }

    /// <summary>
    /// Given a list of grains, reconstruct them into the PCM for an audio stream.
    /// </summary>
    /// <remarks>The function assumes the list is ordered - or for the very least
    /// that the last grain has the farthest time.</remarks>
    /// <param name="grains">The grains to reconstruct.</param>
    /// <returns>The reconstructed audio from the grains.</returns>
    public float[] ReconstructGrains(float gain)
    {
        Grain lastGrain = this.grains[this.grains.Count - 1];
        int retSampleCt = (int)(lastGrain.start * WebMic.FreqRate) + lastGrain.samples.Length;

        // Doing a weighted average over time, so for each moment in time.
        float[] ret = new float[retSampleCt];  // accumulated Sample_Value * Weight
        float[] wt = new float[retSampleCt];   // accumulated Weight
        for (int i = 0; i < retSampleCt; ++i)
        {
            ret[i] = 0;
            wt[i] = 0;
        }

        foreach (Grain g in this.grains)
        {
            int start = (int)(g.start * WebMic.FreqRate);      // Sample index to start accumulating data into.
            int end = g.samples.Length;                                 // The number of samples to write.
            int endup = (int)(g.inEdge * WebMic.FreqRate);     // The number of samples for the ramp-up
            int endhigh = (int)(g.outEdge * WebMic.FreqRate);  // The number of samples before ramping down.

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
                wt[start + i] += 1.0f;
            }

            // Write samples ramping down - just a linear decrease in weight.
            float sub = 1.0f / ((float)end - (float)endhigh);
            float downWt = 1.0f;
            for (i = endhigh; i < end; ++i)
            {
                ret[start + i] += g.samples[i] * downWt;
                wt[start + i] += downWt;
                downWt -= sub;
            }
        }

        // Divide each sample by the weight to turn the samples into a weighted average.
        for (int i = 0; i < ret.Length; ++i)
        {
            if (wt[i] == 0.0f)
                ret[i] = 0.0f;
            else
                ret[i] = ret[i] / wt[i] * gain;
        }

        return ret;
    }

    /// <summary>
    /// Clear the recorded grains.
    /// </summary>
    public void Clear()
    { 
        this.grains.Clear();
    }

    /// <summary>
    /// Check if there are any grains.
    /// </summary>
    /// <returns>True, if there are any grains. Else, false.</returns>
    public bool HasGrains()
    { 
        return
            this.grains != null && // Sanity check
            this.grains.Count > 0;
    }
}
