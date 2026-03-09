namespace SharpDSP.Library;

public class SmoothedParameter
{
    private float current;
    private float target;
    private float step;

    private int samplesRemaining = 0;

    private readonly float samplesToTarget;

    public float Value => current;

    public bool IsSmoothing => samplesRemaining > 0;

    public SmoothedParameter(float initialValue, float smoothingTimeSeconds)
    {
        current = target = initialValue;
        samplesToTarget = smoothingTimeSeconds * Constants.SampleRate;
    }

    public void SetTarget(float newTarget)
    {
        target = newTarget;
        step = (target - current) / samplesToTarget;
        samplesRemaining = (int)samplesToTarget;
    }

    public float Next()
    {
        if(samplesRemaining > 0)
        {
            current += step;
            samplesRemaining--;

            // ensure we hit the exact target at the end of the transition
            if(samplesRemaining == 0)
                current = target; 
        }
        return current;
    }

    public void SnapToTarget()
    {
        current = target;
        samplesRemaining = 0;
    }

}