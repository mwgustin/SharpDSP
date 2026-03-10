namespace SharpDSP.Library;

public class PingPongDelay
{
    private DelayLine leftDelay;
    private DelayLine rightDelay;

    private SmoothedParameter delayTime;
    private SmoothedParameter feedback;
    private SmoothedParameter mix;

    // The "Memory" of the previous sample's output for cross-feedback
    private float lastLeftOutput = 0f;
    private float lastRightOutput = 0f;

    public PingPongDelay(float maxDelaySeconds)
    {
        int samples = MathUtils.SecondsToSamples(maxDelaySeconds);
        leftDelay = new DelayLine(samples);
        rightDelay = new DelayLine(samples);

        // Standard 20ms smoothing
        delayTime = new SmoothedParameter(0.5f * Constants.SampleRate, 0.02f); 
        feedback = new SmoothedParameter(0.5f, 0.02f);
        mix = new SmoothedParameter(0.5f, 0.02f);
    }

    public void Process(float[] leftBuffer, float[] rightBuffer)
    {
        for (int i = 0; i < leftBuffer.Length; i++)
        {
            float curDelay = delayTime.Next();
            float curFeedback = feedback.Next();
            float curMix = mix.Next();

            // 1. Sum Input to Mono (Standard for Ping-Pong)
            float monoInput = (leftBuffer[i] + rightBuffer[i]) * 0.5f;

            // 2. THE FIGURE-8 FEEDBACK LOGIC
            // Left gets: Mono Input + (Previous Right Output * Feedback)
            float inputL = monoInput + (lastRightOutput * curFeedback);
            
            // Right gets: ONLY (Previous Left Output * Feedback)
            // Note: We don't add monoInput here! If we did, both sides would 
            // trigger at the same time.
            float inputR = (lastLeftOutput * curFeedback);

            // 3. Process individual lines (Set internal feedback to 0)
            // We set mix to 1.0 because we want the raw 'wet' signal to mix ourselves
            float wetL = leftDelay.ProcessSampleManual(inputL, curDelay, 0, 1.0f);
            float wetR = rightDelay.ProcessSampleManual(inputR, curDelay, 0, 1.0f);

            // 4. Update memory for the next sample
            lastLeftOutput = wetL;
            lastRightOutput = wetR;

            // 5. Final Output Mix
            leftBuffer[i] = (leftBuffer[i] * (1.0f - curMix)) + (wetL * curMix);
            rightBuffer[i] = (rightBuffer[i] * (1.0f - curMix)) + (wetR * curMix);
        }
    }

    public void UpdateParameters(float timeInSec, float feedbackAmt, float mixAmt)
    {
        delayTime.SetTarget(timeInSec * Constants.SampleRate);
        feedback.SetTarget(feedbackAmt);
        mix.SetTarget(mixAmt);
    }
}