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
    // private float b0, target_b0, step_b0; //current delay
    // private float f0, target_f0, step_f0; //feedback
    // private float m0, target_m0, step_m0; //mix

    private SmoothedParameter delay;
    private SmoothedParameter feedback;
    private SmoothedParameter mix;

    private float lastWetSample = 0;


    private int samplesRemaining = 0;
    private const float smoothingTimeSeconds = 0.02f;
    private const float samplesToTarget = smoothingTimeSeconds * Constants.SampleRate;

    public DelayLine(float maxDelayInSeconds)
    {
        int maxDelayInSamples = SecondsToSamples(maxDelayInSeconds);
        // find the next power-of-two for the buffersize
        bufferSize = 1;
        while(bufferSize < maxDelayInSamples) bufferSize <<= 1;

        wrapMask = bufferSize - 1;

        buffer = new float[bufferSize];

        // Initialize parameters
        delay = new SmoothedParameter(maxDelayInSamples, smoothingTimeSeconds);
        feedback = new SmoothedParameter(0.5f, smoothingTimeSeconds);
        mix = new SmoothedParameter(0.5f, smoothingTimeSeconds);
    }


    public void Process(float[] buffer)
    {
        //hot path
        for(int i = 0; i < buffer.Length; i++)
        {
            // Get current parameter values with smoothing
            float curDelay = delay.Next();
            float curFeedback = feedback.Next();
            float curMix = mix.Next();

            float input = buffer[i];
            
            // 1. Write current input (plus any feedback) into the buffer
            // We use a temporary variable for feedback to avoid a 'now' dependency
            float feedbackSignal = lastWetSample * curFeedback;
            //soft clip the feedback to avoid infinite blowup and add some character
            feedbackSignal = MathF.Tanh(feedbackSignal);

            this.buffer[writeIndex] = input + feedbackSignal;

            // 2. Read delayed sample (wet)
            float wet = ReadSample(curDelay);
            lastWetSample = wet; // store for feedback in the next sample

            // 3. Advance the write index with bitwise wrapping
            writeIndex = (writeIndex + 1) & wrapMask;

            // 4. Mix dry and wet signals for output
            buffer[i] = (input * (1.0f - curMix)) + (wet * curMix);
        }
    }

    public float ProcessSampleManual(float input, float delaySamples, float feedback, float mix)
    {
        // 1. Write current input (plus any feedback) into the buffer
        float feedbackSignal = MathF.Tanh(lastWetSample * feedback);
        this.buffer[writeIndex] = input + feedbackSignal;
        
        // 2. Read delayed sample (wet)
        float wet = ReadSample(delaySamples);
        lastWetSample = wet;

        // 3. Advance the write index with bitwise wrapping
        writeIndex = (writeIndex + 1) & wrapMask;

        // 4. Mix dry and wet signals for output
        return (input * (1.0f - mix)) + (wet * mix);
    }


    public float ReadSample(float delaySamples)
    {
        // Subtract delay from current write position
        float readPos = (float)writeIndex - delaySamples;

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

    public void UpdateParameters(float newDelayInSeconds, float newFeedback, float newMix)
    {
        int delayInSamples = SecondsToSamples(newDelayInSeconds);
        delay.SetTarget(delayInSamples);
        feedback.SetTarget(newFeedback);
        mix.SetTarget(newMix);
    }

    private int SecondsToSamples(float seconds)
    {
        int samples = (int)(seconds * Constants.SampleRate);
        return samples;
    }
}