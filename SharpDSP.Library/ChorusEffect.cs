namespace SharpDSP.Library;

// composition of delay line and LFO to create a chorus effect
public class ChorusEffect
{
    private DelayLine delayLine;
    private LFO lfo;

    // chorus parameters
    private SmoothedParameter depth;
    private SmoothedParameter rate;
    private SmoothedParameter mix;

    private const float smoothingTimeSeconds = 0.02f; // 20ms smoothing time for parameter changes
    private const float samplesToTarget = smoothingTimeSeconds * Constants.SampleRate;
    private const float centerDelay = 0.02f * Constants.SampleRate; // 20ms center delay

    
    public ChorusEffect(float maxDelayInSeconds, float initialDepthInSeconds, float initialRateInHz)
    {
        int maxDelayInSamples = (int)(maxDelayInSeconds * Constants.SampleRate);
        delayLine = new DelayLine(maxDelayInSamples);
        lfo = new LFO(initialRateInHz);

        // Initialize parameters
        depth = new SmoothedParameter(initialDepthInSeconds * Constants.SampleRate, smoothingTimeSeconds);
        rate = new SmoothedParameter(initialRateInHz, smoothingTimeSeconds);
        mix = new SmoothedParameter(0.5f, smoothingTimeSeconds); // default to 50% wet/dry mix
    }

    public void Process(float[] buffer)
    {
        for(int i = 0; i < buffer.Length; i++)
        {
            // Get current parameter values with smoothing
            float curDepth = depth.Next();
            float curRate = rate.Next();
            float curMix = mix.Next();
            
            // Update LFO frequency for modulation
            lfo.UpdateFrequency(curRate);
            // calc modulation
            float lfoValue = lfo.Process(); // -1 to 1

            // final delay = center + (lfoValue * depth)
            float currentDelay = centerDelay + (lfoValue * curDepth);

            // process sample
            buffer[i] = delayLine.ProcessSampleManual(buffer[i], currentDelay, 0.0f, curMix);
        }
    }

    public void UpdateParameters(float newDepthInSeconds, float newRateInHz, float newMix)
    {
        depth.SetTarget(newDepthInSeconds * Constants.SampleRate);
        rate.SetTarget(newRateInHz);
        mix.SetTarget(newMix);
    }
    
}