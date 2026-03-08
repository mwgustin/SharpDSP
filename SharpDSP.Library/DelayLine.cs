using System.Buffers;
using System.Reflection.Metadata;
using Microsoft.VisualBasic;

namespace SharpDSP.Library;

public class DelayLine
{
    private float[] buffer;
    private int writeIndex = 0;
    private int bufferSize; 
    private int wrapMask; // Power-of-two wrap mask 

    //Parameters 
    private float b0, target_b0, step_b0; //current delay
    private float f0, target_f0, step_f0; //feedback
    private float m0, target_m0, step_m0; //mix

    private float lastWetSample = 0;


    private int samplesRemaining = 0;
    private const float smoothingTimeSeconds = 0.02f;
    private const float samplesToTarget = smoothingTimeSeconds * Constants.SampleRate;

    public DelayLine(int minDelayInSamples)
    {
        // find the next power-of-two for the buffersize
        bufferSize = 1;
        while(bufferSize < minDelayInSamples) bufferSize <<= 1;

        wrapMask = bufferSize - 1;

        buffer = new float[bufferSize];
    }


    public void Process(float[] buffer)
    {
        //hot path
        for(int i = 0; i < buffer.Length; i++)
        {
            if(samplesRemaining > 0)
            {
                b0 += step_b0;
                f0 += step_f0;
                m0 += step_m0;
                samplesRemaining--;

                // ensure we hit the exact target at the end of the transition
                if(samplesRemaining == 0)
                {
                    b0 = target_b0; 
                    f0 = target_f0;
                    m0 = target_m0;
                }
            }

            float input = buffer[i];
            
            // 1. Write current input (plus any feedback) into the buffer
            // We use a temporary variable for feedback to avoid a 'now' dependency
            float feedbackSignal = lastWetSample * f0;
            //soft clip the feedback to avoid infinite blowup and add some character
            feedbackSignal = MathF.Tanh(feedbackSignal);

            this.buffer[writeIndex] = input + feedbackSignal;

            // 2. Read delayed sample (wet)
            float wet = ReadSample(b0);
            lastWetSample = wet; // store for feedback in the next sample

            // 3. Advance the write index with bitwise wrapping
            writeIndex = (writeIndex + 1) & wrapMask;

            // 4. Mix dry and wet signals for output
            buffer[i] = (input * (1.0f - m0)) + (wet * m0);
        }
    }


    public float ReadSample(float delaySamples)
    {
        // Subtract delay from current write position
        float readPos = (float)writeIndex - delaySamples;

        // Wrap readPos if it goes behind index 0
        // (Adding bufferSize to a negative number wraps it correctly)
        // while (readPos < 0) readPos += bufferSize;

        // More efficient wrapping using bitwise AND with wrapMask (bufferSize - 1)
        int iPos = (int)MathF.Floor(readPos);

        // 3. Get the two integer indices for interpolation
        int indexA = (int)iPos & wrapMask; // Integer part (floor) with wrapping
        int indexB = (indexA + 1) & wrapMask; // Next index with wrapping

        // 4. Calculate the fractional remainder (the "t" in Lerp)
        float fraction = readPos - iPos;

        // 5. Linear Interpolation + return
        // buffer[indexA] = 0.5
        // buffer[indexB] = 0.7
        // fraction = 0.5 (halfway between)
        // return 0.5 + 0.5 * (0.7 - 0.5)
        // = 0.5 + 0.5 * 0.2
        // = 0.5 + 0.1
        // = 0.6 (halfway (0.5 fraction) between 0.5 buffer[indexA] and 0.7 buffer[indexB])
        return buffer[indexA] + fraction * (buffer[indexB] - buffer[indexA]);
    }

    public void UpdateParameters(float delayInSeconds, float feedback, float mix)
    {
        int delayInSamples = SecondsToSamples(delayInSeconds);
        target_b0 = Math.Clamp(delayInSamples, 0, bufferSize - 1);
        step_b0 = (target_b0 - b0) / samplesToTarget;

        target_f0 = Math.Clamp(feedback, 0, 1.2f); // allow a little extra for some self-oscillation if desired
        step_f0 = (target_f0 - f0) / samplesToTarget;

        target_m0 = Math.Clamp(mix, 0, 1);
        step_m0 = (target_m0 - m0) / samplesToTarget;

        samplesRemaining = (int)samplesToTarget;
    }

    private int SecondsToSamples(float seconds)
    {
        int samples = (int)(seconds * Constants.SampleRate);
        return samples;
    }
}