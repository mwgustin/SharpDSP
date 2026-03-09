namespace SharpDSP.Library;


public class PingPongDelay
{
    private DelayLine leftDelay;
    private DelayLine rightDelay;
    
    // We only need one set of parameters since they usually 
    // share the same timing for a rhythmic "bounce"
    private SmoothedParameter delayTime;
    private SmoothedParameter feedback;
    private SmoothedParameter mix;

    // A secret "cross-talk" variable
    private float lastLeftRead = 0f;
    private float lastRightRead = 0f;

    public PingPongDelay(float maxDelaySeconds)
    {
        int samples = (int)(maxDelaySeconds * Constants.SampleRate);
        leftDelay = new DelayLine(samples);
        rightDelay = new DelayLine(samples);
        
        // Initialize parameters...
        delayTime = new SmoothedParameter(0.5f * Constants.SampleRate, 0.02f); // default 500ms delay
        feedback = new SmoothedParameter(0.5f, 0.02f);
        mix = new SmoothedParameter(0.5f, 0.02f); // default 50% wet/dry mix
        
    }

    public void Process(float[] leftBuffer, float[] rightBuffer)
    {
        for (int i = 0; i < leftBuffer.Length; i++)
        {
            float curDelay = delayTime.Next();
            float curFeedback = feedback.Next();
            float curMix = mix.Next();

            // 1. The Ping: Left Input + Right's Previous Feedback
            // We "throw" the Right feedback into the Left line
            float leftInput = leftBuffer[i] + (lastRightRead * curFeedback);
            
            // 2. The Pong: Right Input + Left's Current Feedback
            // We "throw" the Left feedback into the Right line
            float rightInput = rightBuffer[i] + (lastLeftRead * curFeedback);

            // 3. Process the lines manually
            // We use 0 feedback internally because we are 
            // handling the cross-feedback logic here in this class
            float wetL = leftDelay.ProcessSampleManual(leftInput, curDelay, 0, 1.0f);
            float wetR = rightDelay.ProcessSampleManual(rightInput, curDelay, 0, 1.0f);

            // 4. Update our "cross-talk" memory
            lastLeftRead = wetL;
            lastRightRead = wetR;

            // 5. Final Mix
            leftBuffer[i] = (leftBuffer[i] * (1.0f - curMix)) + (wetL * curMix);
            rightBuffer[i] = (rightBuffer[i] * (1.0f - curMix)) + (wetR * curMix);
        }
    }
}