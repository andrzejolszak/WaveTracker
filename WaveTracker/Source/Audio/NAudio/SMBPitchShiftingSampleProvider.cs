using System;

namespace WaveTracker.Audio.NAudio.Dsp 
{
    /// <summary>
    /// Author: Freefall
    /// Date: 05.08.16
    /// Based on: the port of Stephan M. Bernsee´s pitch shifting class
    /// Port site: https://sites.google.com/site/mikescoderama/pitch-shifting
    /// Test application and github site: https://github.com/Freefall63/NAudio-Pitchshifter
    /// 
    /// NOTE: I strongly advice to add a Limiter for post-processing.
    /// For my needs the FastAttackCompressor1175 provides acceptable results:
    /// https://github.com/Jiyuu/SkypeFX/blob/master/JSNet/FastAttackCompressor1175.cs
    ///
    /// UPDATE: Added a simple Limiter based on the pydirac implementation.
    /// https://github.com/echonest/remix/blob/master/external/pydirac225/source/Dirac_LE.cpp
    /// 
    ///</summary>
    public class SmbPitchShiftingSampleProvider
    {
        //Shifter objects
        private float pitch = 1f;
        private readonly int fftSize;
        private readonly long osamp;
        private readonly SmbPitchShifter shifterLeft = new SmbPitchShifter();
        private readonly SmbPitchShifter shifterRight = new SmbPitchShifter();

        //Limiter constants
        const float LIM_THRESH = 0.95f;
        const float LIM_RANGE = (1f - LIM_THRESH);
        const float M_PI_2 = (float) (Math.PI/2);

        /// <summary>
        /// Creates a new SMB Pitch Shifting Sample Provider with custom settings
        /// </summary>
        /// <param name="fftSize">FFT Size (any power of two &lt;= 4096: 4096, 2048, 1024, 512, ...)</param>
        /// <param name="osamp">Oversampling (number of overlapping windows)</param>
        /// <param name="initialPitch">Initial pitch (0.5f = octave down, 1.0f = normal, 2.0f = octave up)</param>
        public SmbPitchShiftingSampleProvider(int fftSize, long osamp, float initialPitch)
        {
            this.fftSize = fftSize;
            this.osamp = osamp;
            PitchFactor = initialPitch;
        }

        /// <summary>
        /// Read from this sample provider
        /// </summary>
        public (float[] bufferL, float[]? bufferR) Read(float[] bufferL, float[]? bufferR, int offset, int count, float sampleRate)
        {
            if (pitch == 1f)
            {
                return (bufferL, bufferR);
            }

            float[] bufferLRes = Process(bufferL);
            float[]? bufferRRes = bufferR is not null ? Process(bufferR) : null;

            return (bufferLRes, bufferRRes);

            float[] Process(float[] buffer) {
                var mono = new float[count];
                var index = 0;
                for (var sample = offset; sample <= count + offset - 1; sample++)
                {
                    mono[index] = buffer[sample];
                    index += 1;
                }
                shifterLeft.PitchShift(pitch, count, fftSize, osamp, sampleRate, mono);
                index = 0;
                for (var sample = offset; sample <= count + offset - 1; sample++)
                {
                    buffer[sample] = Limiter(mono[index]);
                    index += 1;
                }

                return mono;
            }
        }

        /// <summary>
        /// Pitch Factor (0.5f = octave down, 1.0f = normal, 2.0f = octave up)
        /// </summary>
        public float PitchFactor
        {
            get { return pitch; }
            set { pitch = value; }
        }

        private float Limiter(float sample)
        {
            float res;
            if ((LIM_THRESH < sample))
            {
                res = (sample - LIM_THRESH)/LIM_RANGE;
                res = (float) ((Math.Atan(res)/M_PI_2)*LIM_RANGE + LIM_THRESH);
            }
            else if ((sample < -LIM_THRESH))
            {
                res = -(sample + LIM_THRESH)/LIM_RANGE;
                res = -(float) ((Math.Atan(res)/M_PI_2)*LIM_RANGE + LIM_THRESH);
            }
            else
            {
                res = sample;
            }
            return res;
        }
    }
}
