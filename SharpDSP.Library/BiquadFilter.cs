namespace SharpDSP.Library;

public class BiquadFilter
{
    //coefficients 
    float b0, b1, b2, a1, a2;
    // target coefficients for smooth parameter changes
    float target_b0, target_b1, target_b2, target_a1, target_a2; 
    // step values for each coefficient to incrementally move from current to target over the smoothing period
    float step_b0 = 0, step_b1 = 0, step_b2 = 0, step_a1 = 0, step_a2 = 0; 
    
    // Adjust this for faster/slower smoothing 
    const float smoothingTimeSeconds = 0.02f; 
    // Assuming a sample rate of 44.1kHz
    const int sampleRate = 44100; 
    // Calculate the number of samples over which to smooth the transition to the new coefficients based on the desired smoothing time and sample rate
    const float samplesToTarget = smoothingTimeSeconds * sampleRate;

    // Counter for smoothing samples
    public int samplesRemaining = 0; 

    // prior input samples
    float x1 = 0, x2 = 0; 
    // prior output samples
    float y1 = 0, y2 = 0; 
    
    public BiquadFilter()
    {
        UpdateLowPass(1000, 0.707f); // default to a 1kHz low-pass filter

        // Init the current coefficients to the target coefficients to avoid a ramp-up on the first few samples
        b0 = target_b0;
        b1 = target_b1;
        b2 = target_b2;
        a1 = target_a1;
        a2 = target_a2;
    }

    public void Process(float[] buffer)
    {
        // MAIN HOT PATH: Process each sample in the buffer
        for(int i = 0; i < buffer.Length; i++)
        {
            // if we have pending coefficient changes, incrementally update them towards the target values
            if(samplesRemaining > 0)
            {
                samplesRemaining--;
                
                if(samplesRemaining == 0)
                {
                    // Ensure we end up exactly at the target coefficients after the final step
                    b0 = target_b0;
                    b1 = target_b1;
                    b2 = target_b2;
                    a1 = target_a1;
                    a2 = target_a2;
                }
                else
                {
                    // Incrementally update coefficients towards their targets
                    b0 += step_b0;
                    b1 += step_b1;
                    b2 += step_b2;
                    a1 += step_a1;
                    a2 += step_a2;
                }
            }

            //main biquad calculation: y[n] = b0*x[n] + b1*x[n-1] + b2*x[n-2] - a1*y[n-1] - a2*y[n-2]
            float x0 = buffer[i];
            float y0 = (b0*x0) + (b1*x1) + (b2*x2) - (a1*y1) - (a2*y2);

            //transition state for next sample
            x2 = x1;
            x1 = x0;
            y2 = y1;
            y1 = y0;

            // Add a denormal guard before writing back to the buffer to prevent very small values from causing CPU performance issues
            if (Math.Abs(y0) < 1e-15f) y0 = 0.0f;

            // write the processed sample back to the buffer
            buffer[i] = y0;
        }

    }


    // configure the filter as a low-pass with the given cutoff frequency and Q factor
    public void UpdateLowPass(float frequency, float q)
    {
        ClampFrequency(ref frequency);

        // 1. Calculate intermediate variables
        float omega = (float)(2.0 * Math.PI * frequency / sampleRate);
        float sn = (float)Math.Sin(omega);
        float cs = (float)Math.Cos(omega);
        float alpha = sn / (2.0f * q);

        // 2. Calculate raw coefficients
        float b0_raw = (1.0f - cs) / 2.0f;
        float b1_raw = 1.0f - cs;
        float b2_raw = (1.0f - cs) / 2.0f;
        float a0_raw = 1.0f + alpha;
        float a1_raw = -2.0f * cs;
        float a2_raw = 1.0f - alpha;

        NormalizeAndStoreCoefficients(b0_raw, b1_raw, b2_raw, a0_raw, a1_raw, a2_raw);
    }

    // configure the filter as a high-pass with the given cutoff frequency and Q factor
    public void UpdateHighPass(float frequency, float q)
    {
        ClampFrequency(ref frequency);

        // 1. Calculate intermediate variables
        // $\omega_0 = 2\pi \frac{f_c}{f_s}$
        // $\cos\omega_0 = \cos(\omega_0)$
        // $\alpha = \frac{\sin(\omega_0)}{2Q}$
        float omega = (float)(2.0 * Math.PI * frequency / sampleRate);
        float sn = (float)Math.Sin(omega);
        float cs = (float)Math.Cos(omega);
        float alpha = sn / (2.0f * q);

        // 2. Calculate raw coefficients
        // For a high-pass filter, the raw coefficients are:
        // $b_0 = \frac{1 + \cos\omega_0}{2}$
        // $b_1 = -(1 + \cos\omega_0)$
        // $b_2 = \frac{1 + \cos\omega_0}{2}$
        // $a_0 = 1 + \alpha$
        // $a_1 = -2 \cos\omega_0$
        // $a_2 = 1 - \alpha$
        float b0_raw = (1 + cs) / 2;
        float b1_raw = -(1 + cs);
        float b2_raw = (1 + cs) / 2;
        float a0_raw = 1 + alpha;
        float a1_raw = -2 * cs;
        float a2_raw = 1 - alpha;

        NormalizeAndStoreCoefficients(b0_raw, b1_raw, b2_raw, a0_raw, a1_raw, a2_raw);
    }

    // configure the filter as a peaking EQ with the given center frequency, Q factor, and gain in dB
    public void UpdatePeakingEQ(float frequency, float q, float gainDb)
    {
        ClampFrequency(ref frequency);
        
        // 1. Calculate intermediate variables
        // Intermediate variables:
        // $A = 10^{\frac{gainDb}{40}}$ 
        // (This converts your Decibels into a linear multiplier).
        // $\omega_0 = 2\pi \frac{f_c}{f_s}$
        // $\alpha = \frac{\sin(\omega_0)}{2Q}$
        float A = (float)Math.Pow(10, gainDb / 40.0);
        float omega = (float)(2.0 * Math.PI * (frequency / sampleRate));
        float alpha = (float)(Math.Sin(omega) / (2.0 * q));
        float cs = (float)Math.Cos(omega);

        // 2. Calculate raw coefficients
        // For a peaking EQ, the raw coefficients are:
        // $b_0 = 1 + \alpha \cdot A$
        // $b_1 = -2 \cos(\omega_0)$
        // $b_2 = 1 - \alpha \cdot A$
        // $a_0 = 1 + \frac{\alpha}{A}$
        // $a_1 = -2 \cos(\omega_0)$
        // $a_2 = 1 - \frac{\alpha}{A}$
        float b0_raw = 1 + alpha * A;
        float b1_raw = -2 * cs;
        float b2_raw = 1 - alpha * A;
        float a0_raw = 1 + (alpha / A);
        float a1_raw = -2 * cs;
        float a2_raw = 1 - (alpha / A);

        NormalizeAndStoreCoefficients(b0_raw, b1_raw, b2_raw, a0_raw, a1_raw, a2_raw);
    }

    private void ClampFrequency(ref float frequency)
    {
        frequency = Math.Max(20, frequency); // Clamp to 20Hz minimum
        frequency = Math.Min(frequency, sampleRate / 2); // Clamp to Nyquist frequency
    }

    // This allows our Process() formula to be: y = b0*x + ... - a1*y1 - a2*y2
    private void NormalizeAndStoreCoefficients(float b0_raw, float b1_raw, float b2_raw, float a0_raw, float a1_raw, float a2_raw)
    {
        target_b0 = b0_raw / a0_raw;
        target_b1 = b1_raw / a0_raw;
        target_b2 = b2_raw / a0_raw;
        target_a1 = a1_raw / a0_raw;
        target_a2 = a2_raw / a0_raw; 

        //calculate steps BEFORE settings samplesRemaining
        // This ensures that the steps are calculated before we start applying them 
        // and prevent any issues with the coefficients not updating correctly initially after a change
        SetSteps();

        samplesRemaining = (int)samplesToTarget;
    }
    // This method calculates the step values for each coefficient to smoothly transition from the current coefficients to the target coefficients over the specified number of samples (smoothing period). It is called when the target coefficients are updated and the targetChanged flag is set.
    private void SetSteps()
    {
        step_b0 = (target_b0 - b0) / samplesToTarget;
        step_b1 = (target_b1 - b1) / samplesToTarget;
        step_b2 = (target_b2 - b2) / samplesToTarget;
        step_a1 = (target_a1 - a1) / samplesToTarget;
        step_a2 = (target_a2 - a2) / samplesToTarget;
    }

    public void Reset()
    {
        x1 = x2 = y1 = y2 = 0;
    }
}