namespace SharpDSP.Library;


public class FlangerEffect
{
    private DelayLine delayLine;
    private LFO lfo;

    private SmoothedParameter depth;
    private SmoothedParameter feedback;
    private SmoothedParameter rate;
    private SmoothedParameter mix;

    private const float smoothingTimeSeconds = 0.02f; // 20ms smoothing time for parameter changes
    private const float centerDelay = 0.003f * Constants.SampleRate; // 3ms center delay for flanger

    public FlangerEffect(float initialDepth, float initialRateInHz)
    {
        int maxDelayInSamples = MathUtils.SecondsToSamples(0.02f); // max 20ms buffer
        delayLine = new DelayLine(maxDelayInSamples);
        lfo = new LFO(initialRateInHz);

        depth = new SmoothedParameter(initialDepth * Constants.SampleRate, smoothingTimeSeconds);
        feedback = new SmoothedParameter(0.5f, smoothingTimeSeconds); // default 50% feedback
        mix = new SmoothedParameter(0.5f, smoothingTimeSeconds); // default 50% wet/dry mix
        rate = new SmoothedParameter(initialRateInHz, smoothingTimeSeconds);
    }

    public void Process(float[] buffer)
    {
        for(int i = 0; i < buffer.Length; i++)
        {
            float curDepth = depth.Next();
            float curFeedback = feedback.Next();
            float curMix = mix.Next();
            float curRate = rate.Next();

            //update LFO frequency for modulation
            lfo.UpdateFrequency(curRate);

            //calc modulation
            float lfoValue = lfo.Process(); // -1 to 1

            //final delay = center + (lfoValue * depth)
            float currentDelay = centerDelay + (lfoValue * curDepth);

            buffer[i] = delayLine.ProcessSampleManual(buffer[i], currentDelay, curFeedback, curMix);
        }
    }

    public void UpdateParameters(float newDepth, float newRateInHz, float newFeedback, float newMix)
    {
        depth.SetTarget(newDepth * Constants.SampleRate);
        rate.SetTarget(newRateInHz);
        feedback.SetTarget(newFeedback);
        mix.SetTarget(newMix);
    }
}