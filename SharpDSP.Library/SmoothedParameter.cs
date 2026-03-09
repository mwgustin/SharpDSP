using System.Runtime.CompilerServices;

namespace SharpDSP.Library;

public record struct SmoothedParameter
{
    private float _current;
    private float _target;
    private float _step;

    private int _samplesRemaining = 0;

    private readonly float _samplesToTarget;

    public float Value => _current;

    public bool IsSmoothing => _samplesRemaining > 0;

    public SmoothedParameter(float initialValue, float smoothingTimeSeconds)
    {
        _current = _target = initialValue;
        _samplesToTarget = smoothingTimeSeconds * Constants.SampleRate;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetTarget(float newTarget)
    {
        _target = newTarget;
        _step = (_target - _current) / _samplesToTarget;
        _samplesRemaining = (int)_samplesToTarget;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Next()
    {
        if(_samplesRemaining > 0)
        {
            _current += _step;
            _samplesRemaining--;

            // ensure we hit the exact target at the end of the transition
            if(_samplesRemaining == 0)
                _current = _target; 
        }
        return _current;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SnapToTarget()
    {
        _current = _target;
        _samplesRemaining = 0;
    }

}