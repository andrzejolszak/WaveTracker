using FFmpeg.AutoGen;

namespace WaveTracker.Audio {
    public class TickEvent {
        public TickEventType eventType;
        public int value;
        public int value2;
        public int value3;
        public int countdown;
        public int frame;
        public int row;

        public TickEvent(TickEventType eventType, int val, int val2, int val3, int timer, int frame, int row) {
            this.eventType = eventType;
            value = val;
            value2 = val2;
            value3 = val3;
            countdown = timer;
            this.frame = frame;
            this.row = row;
        }

        public void Update() {
            countdown--;
        }
    }
    public enum TickEventType {
        Note, Instrument, Volume, Effect
    }
}
