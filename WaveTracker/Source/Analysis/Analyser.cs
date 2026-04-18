using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicAnalyser.App.Analysis
{
    public class Analyser
    {
        public static int CHORD_NOTE_OCCURENCE_OFFSET = 8;

        public string CurrentKey { get; set; }
        public string CurrentMode { get; set; }

        private Music music = new Music();
        private List<Note> aggregateNotes = new List<Note>();
        private List<Note>[] chordNotes;
        private double[] notePercent = new double[12];

        /*
         * Predicts the key signature of the audio based on the distrubution of notes identified
         */
        public void FindKey()
        {
            double[] percents = new double[notePercent.Length];
            Array.Copy(notePercent, percents, percents.Length);
            Dictionary<int, double> noteDict = new Dictionary<int, double>();

            for (int i = 0; i < percents.Length; i++)
                noteDict.Add(i, percents[i]);

            double[] keyPercents = Music.FindTotalScalePercentages(noteDict);
            var maxPercent = keyPercents.Select((n, i) => (Number: n, Index: i)).Max();

            string keyRoot = Music.GetNoteName(maxPercent.Index);
            if (music.IsMinor(keyRoot, out string minorRoot)) // Checks if key is most likely the relative minor of original prediction
                CurrentKey = minorRoot + " Minor";
            else
                CurrentKey = keyRoot + " Major";

            CurrentMode = Music.GetMode(notePercent, keyRoot, minorRoot);
        }

        public bool FindChordsNotes()
        {
            if (aggregateNotes.Count == 0)
                return false;

            // UNUSED
            //List<Note>[] notesByName = new List<Note>[12];
            //for (int i = 0; i < 12; i++)
            //    notesByName[i] = new List<Note>();
            //Dictionary<string, double> noteDistributionDict = new Dictionary<string, double>();

            //for(int i = 0; i < aggregateNotes.Count; i++)
            //{
            //    string noteName = aggregateNotes[i].Name;
            //    double noteMag = aggregateNotes[i].Magnitude;
            //    int index = aggregateNotes[i].NoteIndex;
            //    notesByName[index].Add(aggregateNotes[i]);
            //    if (noteDistributionDict.ContainsKey(noteName))
            //        noteDistributionDict[noteName] += noteMag;
            //    else
            //        noteDistributionDict.Add(noteName, noteMag);
            //}
            //string[] keys = noteDistributionDict.Keys.ToArray();
            //foreach (string key in keys)
            //    noteDistributionDict[key] /= Prefs.CHORD_DETECTION_INTERVAL;

            //noteDistributionDict = noteDistributionDict.OrderByDescending(x => x.Value).Take(4).ToDictionary(x => x.Key, x => x.Value);

            //chordNotes = new List<Note>[noteDistributionDict.Count];
            //keys = noteDistributionDict.Keys.ToArray();
            //for (int i = 0; i < chordNotes.Length; i++)
            //{
            //    chordNotes[i] = new List<Note>();
            //    List<int> octaves = new List<int>();
            //    int noteIndex = Music.GetNoteIndex(keys[i] + "0");
            //    for (int j = 0; j < notesByName[noteIndex].Count; j++)
            //    {
            //        if (!octaves.Contains(notesByName[noteIndex][j].Octave)) // Stores each prominent note in collected notes only once
            //        {
            //            chordNotes[i].Add(notesByName[noteIndex][j]);
            //            octaves.Add(notesByName[noteIndex][j].Octave);
            //        }
            //    }
            //    chordNotes[i] = chordNotes[i].OrderBy(x => x.Frequency).ToList(); // Order: Frequency - low to high
            //}
            //aggregateNotes.Clear();
            //return true;

            int[,] tempNoteOccurences = new int[12, 2]; // Note index, timestamp
            List<Note>[] notesByName = new List<Note>[12];
            for (int i = 0; i < 12; i++)
                notesByName[i] = new List<Note>();

            int initialTimeStamp = aggregateNotes[0].TimeStamp;
            int timeStampOffset = 0;

            for (int i = 0; i < aggregateNotes.Count; i++)
            {
                int index = aggregateNotes[i].NoteIndex;
                notesByName[index].Add(aggregateNotes[i]);

                if (tempNoteOccurences[index, 1] != initialTimeStamp + timeStampOffset)
                {
                    tempNoteOccurences[index, 0]++;
                    tempNoteOccurences[index, 1] = aggregateNotes[i].TimeStamp;
                }
                timeStampOffset = aggregateNotes[i].TimeStamp - initialTimeStamp;
            }
            aggregateNotes.Clear();

            List<int> chordNoteIndexes = new List<int>();
            for (int i = 0; i < tempNoteOccurences.Length / 2; i++)
            {
                if (tempNoteOccurences[i, 0] >= CHORD_NOTE_OCCURENCE_OFFSET) // Prunes spurious notes
                    chordNoteIndexes.Add(i);
            }

            chordNotes = new List<Note>[chordNoteIndexes.Count];
            for (int i = 0; i < chordNotes.Length; i++)
            {
                chordNotes[i] = new List<Note>();
                List<int> octaves = new List<int>();
                for (int j = 0; j < notesByName[chordNoteIndexes[i]].Count; j++)
                {
                    if (!octaves.Contains(notesByName[chordNoteIndexes[i]][j].Octave)) // Stores each prominent note in collected notes only once
                    {
                        chordNotes[i].Add(notesByName[chordNoteIndexes[i]][j]);
                        octaves.Add(notesByName[chordNoteIndexes[i]][j].Octave);
                    }
                }
                chordNotes[i] = chordNotes[i].OrderBy(x => x.Frequency).ToList(); // Order: Frequency - low to high
            }
            return true;
        }

        /*
         *  Finds all possible chords from sequence of chord notes
         */
        public void FindChords(List<Chord> container, Chord? prevChord)
        {
            List<Note> myChordNotes = new List<Note>();
            for (int i = 0; i < chordNotes.Length; i++) // Creates iterable chord notes list
            {
                double avgMagnitude = 0;
                for(int j = 0; j < chordNotes[i].Count; j++) // Finds average magnitude of that note type and assigns it to magnitude of chord note
                    avgMagnitude += chordNotes[i][j].Magnitude;
                avgMagnitude /= chordNotes[i].Count;

                Note newChordNote = (Note)chordNotes[i][0].Clone();
                newChordNote.Magnitude = avgMagnitude;
                newChordNote.Octave = chordNotes[i].Count;
                myChordNotes.Add(newChordNote);
            }

            for (int i = 0; i < chordNotes.Length; i++)
            {
                List<int> intervals = new List<int>();
                for (int j = 1; j < chordNotes.Length; j++) // Finds interval between adjacent notes
                {
                    int noteDifference = myChordNotes[j].NoteIndex - myChordNotes[0].NoteIndex;
                    if (noteDifference < 0)
                        noteDifference = 12 + noteDifference;
                    intervals.Add(noteDifference);
                }
                string chordQuality = Music.GetChordQuality(intervals, out int fifthOmitted); // Determines chord quality from intervals
                if (chordQuality != "N/A")
                    container.Add(Chord.CreateChord(myChordNotes[0].Name, chordQuality, myChordNotes, fifthOmitted));

                myChordNotes = NextChord(myChordNotes); // Iterates chord root note
            }

            AdjustChordProbabilities(container, prevChord);
            container.Sort((x, y) => x.Probability.CompareTo(y.Probability));
        }

        /*
 *  Finds all possible chords from sequence of chord notes
 */
        public static Chord? FindChord(List<Note> notes, Chord? prevChord) 
        {
            List<int> intervals = new List<int>();
            for (int j = 1; j < notes.Count; j++) // Finds interval between adjacent notes
            {
                int noteDifference = notes[j].NoteIndex - notes[0].NoteIndex;
                if (noteDifference < 0)
                    noteDifference = 12 + noteDifference;
                intervals.Add(noteDifference);
            }
            string chordQuality = Music.GetChordQuality(intervals, out int fifthOmitted); // Determines chord quality from intervals
            if (chordQuality != "N/A")
                return Chord.CreateChord(notes[0].Name, chordQuality, notes, fifthOmitted);

            return null;
        }

        private void AdjustChordProbabilities(List<Chord> chords, Chord? prevChord)
        {
            if (chords.Count == 0)
                return;

            double[] rootMagnitudes = new double[chords.Count];
            for (int i = 0; i < chords.Count; i++)
                rootMagnitudes[i] = chords[i].Notes[0].Magnitude;
            double avgMagnitude = rootMagnitudes.Average();
            for (int i = 0; i < rootMagnitudes.Length; i++)
                rootMagnitudes[i] -= avgMagnitude;
            rootMagnitudes = Normalise(rootMagnitudes);

            double[] rootOccurences = new double[chords.Count];
            for (int i = 0; i < chords.Count; i++)
                rootOccurences[i] = chords[i].Notes[0].Octave;
            double avgOccurences = rootOccurences.Average();
            for (int i = 0; i < rootOccurences.Length; i++)
                rootOccurences[i] -= avgOccurences;
            rootOccurences = Normalise(rootOccurences);

            double[] rootFreq = new double[chords.Count];
            for (int i = 0; i < chords.Count; i++)
                rootFreq[i] = chords[i].Notes[0].Frequency;
            double avgFreq = rootFreq.Average();
            for (int i = 0; i < rootFreq.Length; i++)
                rootFreq[i] = avgFreq - rootFreq[i];
            rootFreq = Normalise(rootFreq);

            double[] chordExtensions = new double[chords.Count];
            for (int i = 0; i < chords.Count; i++)
                chordExtensions[i] = chords[i].NumExtensions;
            double avgExtensions = chordExtensions.Average();
            for (int i = 0; i < chordExtensions.Length; i++)
                chordExtensions[i] = avgExtensions - chordExtensions[i];
            chordExtensions = Normalise(chordExtensions);

            double[] notSuspended = new double[chords.Count];
            for (int i = 0; i < chords.Count; i++)
            {
                if (chords[i].Name.Contains("sus"))
                    notSuspended[i] = -1;
                else
                    notSuspended[i] = 1;
            }

            double[] fifthOmitted = new double[chords.Count];
            for (int i = 0; i < chords.Count; i++)
                fifthOmitted[i] = chords[i].FifthOmitted;
            fifthOmitted = Normalise(fifthOmitted);

            double[] chordPredictedBefore = new double[chords.Count];
            if (prevChord != null)
            {
                for (int i = 0; i < chords.Count; i++)
                {
                    if (chords[i].Root == prevChord.Root)
                        chordPredictedBefore[i] += 1;
                }
            }

            double[] overallProb = new double[chords.Count];
            for (int i = 0; i < chords.Count; i++)
                //overallProb[i] = 1.0 * rootMagnitudes[i] + 1.0 * rootOccurences[i] + 1.0 * chordExtensions[i] + 2 * rootFreq[i] + 1.0 * fifthOmitted[i] + 1.5 * chordPredictedBefore[i];
                overallProb[i] = 1.1*rootMagnitudes[i] + 1.0*rootOccurences[i] + 2.3*chordExtensions[i] + 1.8*rootFreq[i] + 2.5*notSuspended[i] + 1.0*fifthOmitted[i] + 0.7*chordPredictedBefore[i];
            overallProb = Normalise(overallProb);
            double probSum = overallProb[0] + 1;
            for (int i = 1; i < chords.Count; i++)
                probSum += overallProb[i] + 1;
            for (int i = 0; i < chords.Count; i++)
                chords[i].Probability = (overallProb[i] + 1) / probSum * 100;
        }

        private double[] Normalise(double[] values)
        {
            double max = Math.Abs(values[0]);
            for(int i = 1; i < values.Length; i++)
            {
                if (Math.Abs(values[i]) > max)
                    max = Math.Abs(values[i]);
            }
            if (max > 0)
            {
                for (int i = 0; i < values.Length; i++)
                    values[i] /= max;
            }
            return values;
        }

        private List<Note> NextChord(List<Note> chord)
        {
            Note firstNote = chord[0];
            for(int i = 0; i < chord.Count - 1; i++)
                chord[i] = chord[i + 1];

            chord[chord.Count - 1] = firstNote;
            return chord;
        }

        public void DisposeAnalyser()
        {
            aggregateNotes.Clear();
            notePercent = new double[12];
            music.DisposeMusic();
        }
    }
}
