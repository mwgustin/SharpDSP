namespace SharpDSP.Library;

public static class Constants
{
    public const int SampleRate = 44100;
    
}

public static class MathUtils
{
    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    public static int SecondsToSamples(float seconds)
    {
        return (int)(seconds * Constants.SampleRate);
    }
}