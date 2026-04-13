using System.Collections.Generic;

namespace WaveTracker.Audio {
    public class Delay {

        List<float> bufferL;
        List<float> bufferR;

        int positionL = 0;
        int positionR = 0;
        float decay = 0f;
        int delayInSamples = (int)(44100 * 0f);

        public Delay() {
            bufferL = new List<float>(44100 * 10);
            bufferR = new List<float>(44100 * 10);
            for (int i = 0; i < bufferL.Capacity; i++) {
                bufferL.Add(0);
                bufferR.Add(0);
            }
        }

        public void SetParams(double delaySeconds, float decay) {
            this.decay = decay/16f;
            delayInSamples = (int)((double)(delaySeconds/16f) * 44100);
            positionL = 0;
            positionR = 0;
            for (int i = 0; i < bufferL.Capacity; i++) {
                bufferL[i] = 0;
                bufferR[i] = 0;
            }
        }

        public void Transform(int playbackPosition, float inputL, float inputR, out float outputL, out float outputR) {

            outputL = delayInSamples == 0 ? inputL : Apply(bufferL, inputL, ref positionL);
            outputR = delayInSamples == 0 ? inputR : Apply(bufferR, inputR, ref positionR);

            float Apply(List<float> buffer, float sample, ref int position)
            {
                buffer[position] = sample;
                int pos = mod((position - delayInSamples), buffer.Count);
                var delayedSample = buffer[pos];
                buffer[position] = (delayedSample * decay) + sample;
                position = ((position + 1) % buffer.Count);
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
