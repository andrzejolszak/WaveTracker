using JSNet;
using System;
using System.Collections.Generic;

namespace WaveTracker.Audio {
    public class Delay {

        float[] bufferL;
        float[] bufferR;
        private Effect e;
        int positionL = 0;
        int positionR = 0;
        float decay = 0f;
        int delayInSamples = (int)(44100 * 0f);

        public Delay() {
            bufferL = new float[44100 * 10];
            bufferR = new float[44100 * 10];

            this.e = new JSNet.Flanger();
            e.Init();
            e.Slider();
        }

        public void SetParams(double delay, float decay) {
            this.decay = decay/16f;
            delayInSamples = (int)((delay/16f) * 44100);
            positionL = 0;
            positionR = 0;
            Array.Clear(bufferL);
            Array.Clear(bufferR);
        }

        public void Transform(int playbackPosition, float inputL, float inputR, out float outputL, out float outputR) {
            outputL = inputL; 
            outputR = inputR;

            if (delayInSamples == 0)
                return;

            // this.e.Sample(ref outputL, ref outputR);
            // return;

            outputL = delayInSamples == 0 ? inputL : Apply(bufferL, inputL, ref positionL);
            outputR = delayInSamples == 0 ? inputR : Apply(bufferR, inputR, ref positionR);

            float Apply(float[] buffer, float sample, ref int position)
            {
                buffer[position] = sample;
                int pos = mod((position - delayInSamples), buffer.Length);
                var delayedSample = buffer[pos];
                buffer[position] = (delayedSample * decay) + sample;
                position = ((position + 1) % buffer.Length);
                return (delayedSample * decay) + sample;
            }

            int mod(int dividend, int divisor) {
                if (dividend == 0)
                    return 0;

                if ((dividend > 0) == (divisor > 0))
                    return dividend % divisor;
                else
                    return (dividend % divisor) + divisor;
            }
        }
    }
}
