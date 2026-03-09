namespace SharpDSP.Library;

public class LFO
{
    private float phase = 0.0f;
    private float frequency = 1f; //hz


    public LFO(float frequency)
    {
        this.frequency = frequency;
    }

    public float Process()
    {
        // Check if the current phase is in the cache
        float output = MathF.Sin(2 * MathF.PI * phase);

        // Increment phase
        phase += frequency / Constants.SampleRate;

        // Wrap phase to [0, 1)
        if (phase >= 1.0f) phase -= 1.0f;

        return output;
    }
    
    public void UpdateFrequency(float newFrequency)
    {
        frequency = newFrequency;
    }
}