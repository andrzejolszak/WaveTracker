using Microsoft.Xna.Framework.Content;
using System;
using WaveTracker;
using WaveTracker.Midi;

namespace MusicAnalyser.App.Analysis
{
    [Serializable]
    public class Note : ICloneable
    {
        public static Note Create(string name, double freq, double gain, int timeStamp) {
            Note myNote = new Note();
            myNote.Name = name.Substring(0, name.Length - 1);
            myNote.Octave = Convert.ToInt32(name.Substring(name.Length - 1, 1));
            myNote.NoteIndex = Music.GetNoteIndex(name);
            myNote.Frequency = freq;
            myNote.Magnitude = gain;
            myNote.TimeStamp = timeStamp;
            return myNote;
        }

        public static Note Create(int noteNumber) {
            int octave = noteNumber / 12 - 1;
            int index = noteNumber % 12;
            double freq = Music.GetFrequencyFromNoteIndexFrom(noteNumber);
            Note myNote = new Note();
            myNote.Name = Helpers.MIDINoteToText(noteNumber);
            myNote.Octave = octave;
            myNote.NoteIndex = index;
            myNote.Frequency = freq;
            myNote.Magnitude = 1;
            myNote.TimeStamp = 0;
            return myNote;
        }

        public string Name { get; set; }
        public int Octave { get; set; }
        public int NoteIndex { get; set; }
        public double Frequency { get; set; }
        public double Magnitude { get; set; }
        public int TimeStamp { get; set; }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
