using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicAnalyser.App.Analysis
{
    [Serializable]
    public class Chord
    {
        public static Chord CreateChord(string root, string quality, List<Note> notes, int fifthOmitted) {
            Note[] tempNotes = new Note[notes.Count];
            Array.Copy(notes.ToArray(), tempNotes, notes.Count);
            int numExtensions = 0;
            if (quality.Contains("(")) {
                string extensions = quality.Substring(quality.IndexOf("(") + 1);
                numExtensions = extensions.Count(f => f == ',') + 1;
            }
            Chord myChord = new Chord {
                Name = $"{root} {quality}",
                Root = root,
                Quality = quality,
                Notes = tempNotes.ToList(),
                NumExtensions = numExtensions,
                FifthOmitted = fifthOmitted
            };
            return myChord;
        }

        public string Name { get; set; }
        public string Root { get; set; }
        public string Quality { get; set; }
        public List<Note> Notes { get; set; }
        public int NumExtensions { get; set; }
        public int FifthOmitted { get; set; }
        public double Probability { get; set; }
    }
}
