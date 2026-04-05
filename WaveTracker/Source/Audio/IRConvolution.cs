using System;
using System.Numerics;

namespace WaveTracker.Audio {
    public class IRConvolution {
        private SimdConvolver2 filterL;
        private SimdConvolver2 filterR;

        public IRConvolution() {
            filterL = new SimdConvolver2(new float[0]);
            filterR = new SimdConvolver2(new float[0]);
        }

        public void SetIR(float[] ir) {
            filterL = new SimdConvolver2(ir);
            filterR = new SimdConvolver2(ir);
        }

        public void Transform(float inputL, float inputR, out float outputL, out float outputR) {
            outputL = filterL.ProcessSample(inputL);
            outputR = filterR.ProcessSample(inputR);
        }
    }

    public class FastSimdConvolver {
        private readonly float[] impulse;
        private readonly float[] buffer;
        private int index;

        private readonly int vectorSize;

        public FastSimdConvolver(float[] impulseResponse) {
            impulse = impulseResponse;
            buffer = new float[impulse.Length * 2]; // duplicated buffer
            vectorSize = Vector<float>.Count;
        }

        public float ProcessSample(float input) {
            if (impulse.Length == 0) {
                return input;
            }

            index++;

            if (index >= impulse.Length)
                index = 0;

            buffer[index] = input;
            buffer[index + impulse.Length] = input; // mirror

            float sum = 0f;

            int start = index + impulse.Length;
            int i = 0;

            // SIMD loop
            for (; i <= impulse.Length - vectorSize; i += vectorSize) {
                var x = new Vector<float>(buffer, start - i);
                var h = new Vector<float>(impulse, i);

                sum += Vector.Dot(x, h);
            }

            // Tail
            for (; i < impulse.Length; i++) {
                sum += impulse[i] * buffer[start - i];
            }

            return sum;
        }
    }

    public class RealTimeConvolver {
        private readonly float[] impulse;
        private readonly float[] buffer;
        private int bufferIndex;

        public RealTimeConvolver(float[] impulseResponse) {
            impulse = impulseResponse;
            buffer = new float[impulse.Length];
            bufferIndex = 0;
        }

        public float ProcessSample(float input) {
            buffer[bufferIndex] = input;

            float output = 0f;
            int bufIndex = bufferIndex;

            for (int i = 0; i < impulse.Length; i++) {
                output += impulse[i] * buffer[bufIndex];

                bufIndex--;
                if (bufIndex < 0)
                    bufIndex = buffer.Length - 1;
            }

            bufferIndex++;
            if (bufferIndex >= buffer.Length)
                bufferIndex = 0;

            return output;
        }
    }

    public class SimdConvolver {
        private readonly float[] impulse;
        private readonly float[] buffer;
        private int bufferIndex;

        private readonly int vectorSize;

        public SimdConvolver(float[] impulseResponse) {
            impulse = impulseResponse;
            buffer = new float[impulse.Length];
            bufferIndex = 0;

            vectorSize = Vector<float>.Count; // typically 4, 8, or 16
        }

        public float ProcessSample(float input) {
            buffer[bufferIndex] = input;

            float sum = 0f;
            int i = 0;

            int bufIndex = bufferIndex;

            // SIMD loop
            for (; i <= impulse.Length - vectorSize; i += vectorSize) {
                Span<float> temp = stackalloc float[vectorSize];

                // Gather (because circular buffer is not contiguous)
                for (int j = 0; j < vectorSize; j++) {
                    temp[j] = buffer[bufIndex];
                    bufIndex--;
                    if (bufIndex < 0)
                        bufIndex = buffer.Length - 1;
                }

                var x = new Vector<float>(temp);
                var h = new Vector<float>(impulse, i);

                sum += Vector.Dot(x, h);
            }

            // Tail (remaining taps)
            for (; i < impulse.Length; i++) {
                sum += impulse[i] * buffer[bufIndex];

                bufIndex--;
                if (bufIndex < 0)
                    bufIndex = buffer.Length - 1;
            }

            // Advance circular buffer
            bufferIndex++;
            if (bufferIndex >= buffer.Length)
                bufferIndex = 0;

            return sum;
        }
    }

    public class SimdConvolver2 {
        private readonly float[] _ir;
        private readonly float[] _buffer;
        private int _pos;

        private readonly int _length;
        private readonly int _simdWidth;

        private readonly float _normalizationGain;

        public SimdConvolver2(float[] impulseResponse, bool normalize = true) {
            _length = impulseResponse.Length;
            _ir = new float[_length];
            Array.Copy(impulseResponse, _ir, _length);

            _buffer = new float[_length];
            _pos = 0;

            _simdWidth = Vector<float>.Count;

            if (normalize) {
                float sum = 0f;
                for (int i = 0; i < _length; i++)
                    sum += Math.Abs(_ir[i]);

                _normalizationGain = sum > 0 ? 1.0f / sum : 1.0f;
            }
            else {
                _normalizationGain = 1.0f;
            }
        }

        public float ProcessSample(float input) {
            if (_ir.Length == 0) {
                return input;
            }

            // Write incoming sample into circular buffer
            _buffer[_pos] = input;

            float result = 0f;

            int i = 0;

            // SIMD accumulation
            for (; i <= _length - _simdWidth; i += _simdWidth) {
                Span<float> temp = stackalloc float[_simdWidth];

                for (int j = 0; j < _simdWidth; j++) {
                    int idx = (_pos - (i + j) + _length) % _length;
                    temp[j] = _buffer[idx];
                }

                var xVec = new Vector<float>(temp);
                var hVec = new Vector<float>(_ir, i);

                result += Vector.Dot(xVec, hVec);
            }

            // Tail (non-SIMD)
            for (; i < _length; i++) {
                int idx = (_pos - i + _length) % _length;
                result += _buffer[idx] * _ir[i];
            }

            // Advance circular buffer
            _pos++;
            if (_pos >= _length)
                _pos = 0;

            return result * _normalizationGain;
        }
    }

    public class ZeroModuloSimdConvolver {
        private readonly float[] _ir;
        private readonly float[] _buffer;

        private int _pos;
        private readonly int _length;
        private readonly int _simdWidth;

        private readonly float _gain;

        public ZeroModuloSimdConvolver(float[] impulseResponse, bool normalize = true) {
            _length = impulseResponse.Length;
            _simdWidth = Vector<float>.Count;

            // Reverse IR for forward access
            _ir = new float[_length];
            for (int i = 0; i < _length; i++)
                _ir[i] = impulseResponse[_length - 1 - i];

            // Double buffer
            _buffer = new float[_length * 2];

            _pos = 0;

            // Normalization
            if (normalize) {
                float sum = 0f;
                for (int i = 0; i < _length; i++)
                    sum += Math.Abs(_ir[i]);

                _gain = sum > 0 ? 1f / sum : 1f;
            }
            else {
                _gain = 1f;
            }
        }

        public float ProcessSample(float input) {
            if (_ir.Length == 0) {
                return input;
            }

            int writeIndex = _pos;

            // Write sample twice (mirrored)
            _buffer[writeIndex] = input;
            _buffer[writeIndex + _length] = input;

            float result = 0f;

            // Start reading from contiguous region
            int readIndex = writeIndex + _length;

            int i = 0;

            // SIMD loop
            for (; i <= _length - _simdWidth; i += _simdWidth) {
                var xVec = new Vector<float>(_buffer, readIndex - i);
                var hVec = new Vector<float>(_ir, i);

                result += Vector.Dot(xVec, hVec);
            }

            // Tail
            for (; i < _length; i++) {
                result += _buffer[readIndex - i] * _ir[i];
            }

            // Advance pointer (only wrap once per sample)
            _pos++;
            if (_pos >= _length)
                _pos = 0;

            return result * _gain;
        }
    }
}
