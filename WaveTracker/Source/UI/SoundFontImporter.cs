using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using WaveTracker.Audio;
using WaveTracker.Audio.Native;
using WaveTracker.Tracker;

namespace WaveTracker.UI {
    public class SoundFontImporter : Window {

        private SampleSelector samplesList;
        private Button selectFont;
        private Button importSelected;
        private int _selectedSampleIndex = -1;

        public SoundFontImporter() : base("Sound Font Importer", 600, 340) {
            ExitButton.SetTooltip("Close", "Close importer");

            selectFont = new Button("Open Sound Font...    ", 16, 23, this);
            selectFont.SetTooltip("", "Open a SF2 sound font file as instruments");

            importSelected = new Button("Import selected", width - 100, height - 30, this);
            importSelected.SetTooltip("", "Import selected samples as new instruments");

            samplesList = new SampleSelector(16, 40, width - 32, 23, this);
            samplesList.SetList(new List<Sample>(0));
        }

        public void Update() {
            if (WindowIsOpen) {
                if (InFocus && (ExitButton.Clicked || Input.GetKeyDown(Keys.Escape, KeyModifier.None))) {
                    Close();
                }

                DoDragging();

                samplesList.Update();

                if (this._selectedSampleIndex != samplesList.SelectedIndex)
                {
                    this._selectedSampleIndex = samplesList.SelectedIndex;
                    if (this._selectedSampleIndex == -1) {
                        AudioEngine.PreviewStream = null;
                    }
                    else {
                        Sample s = samplesList.SelectedItem;
                        AudioEngine.PreviewStream = new WaveStream(s.AsWav());
                    }
                }

                if (selectFont.Clicked) {
                    if (SaveLoad.SetFilePathThroughOpenDialog(out string filepath, SaveLoad.soundFontDialogFilters))
                    {
                        List<Sample> samples = UnpackSF2(filepath);
                        samplesList.SetList(samples);
                    }
                }

                if (importSelected.Clicked && samplesList.items.Count > 0 && samplesList.markedItems.Count > 0) {
                    foreach (int m in samplesList.markedItems.OrderBy(x => x))
                    {
                        Sample sample = samplesList.items[m];

                        if (App.Settings.SamplesWaves.AutomaticallyTrimSamples) {
                            sample.TrimSilence();
                        }

                        if (App.Settings.SamplesWaves.AutomaticallyNormalizeSamplesOnImport) {
                            sample.Normalize();
                        }

                        App.CurrentModule.Instruments.Add(new SampleInstrument { name = sample.name, sample = sample });
                    }

                    App.CurrentModule.SetDirty();
                    this.Close();
                }
            }
        }

        public new void Open() {
            base.Open();
            samplesList.SetList(new List<Sample>(0));
        }

        public new void Close() {
            base.Close();
            samplesList.SetList(new List<Sample>(0));
            AudioEngine.PreviewStream = null;
        }

        public new void Draw() {
            if (WindowIsOpen) {
                name = "Import Sound Font";

                // draw window
                base.Draw();

                DrawRoundedRect(8, 15, width - 16, height - 23, Color.White);

                selectFont.Draw();
                DrawSprite(selectFont.x + selectFont.width - 14, selectFont.y + (selectFont.IsPressed ? 3 : 2), new Rectangle(72, 81, 12, 9));

                if (samplesList.markedItems.Count > 0) {
                    importSelected.Draw();
                }

                samplesList.Draw();
            }
        }

        static List<SF2Sound> Sounds;

        class SF2Sound {
            public string SoundName;

            public uint SampleStart = 0;
            public uint SampleSize = 0;

            public uint SampleLoopStart = 0;
            public uint SampleLoopLength = 0;

            public uint SampleRate = 0;

            public ushort SampleLink = 0;
            public ushort SampleType = 0;

            //public byte Pitch = 0; //???
            //public byte PitchCorrection = 0; //???

            public SF2Sound(string Name) {
                this.SoundName = Name;
            }

            public byte OriginalKey { get; internal set; }
            public int Id { get; internal set; }
            public string InstrName { get; internal set; }
        }

        static List<Sample> UnpackSF2(string SourceFile) {
            Sounds = new List<SF2Sound>();

            FileStream stream = new FileStream(SourceFile, FileMode.Open);
            BinaryReader reader = new BinaryReader(stream);

            //code here
            if (reader.ReadUInt32() != 0x46464952) //'RIFF'
                throw new Exception("Header mismatch!");

            //size of the SFBK block
            uint SFBKSize = reader.ReadUInt32();

            reader.ReadUInt32(); //'sfbk'

            /*if (reader.ReadUInt32() != 0x5453494C) //'sfbk'
                Console.WriteLine("Expected sfbk block");*/

            uint t = reader.ReadUInt32();
            if (t != 0x5453494C) //'LIST'
                throw new Exception("Expected LIST block!");

            //offset of LIST block from this point on
            uint LISTOffset = reader.ReadUInt32();
            LISTOffset += (uint)reader.BaseStream.Position; //LIST block is counter from the position at the end of the offset value

            //Console.WriteLine("LISTOffset: " + LISTOffset);

            //seek to sample data start
            reader.BaseStream.Seek((long)LISTOffset, SeekOrigin.Begin);

            if (reader.ReadUInt32() != 0x5453494C) //'LIST'
                throw new Exception("Expected LIST block!");

            //sample List size, for future reference
            uint LISTSize = reader.ReadUInt32();
            LISTOffset = (uint)reader.BaseStream.Position + LISTSize;

            if (reader.ReadUInt32() != 0x61746473) //'sdta'
                throw new Exception("Expected sdta block!");

            if (reader.ReadUInt32() != 0x6C706D73) //'smpl'
                throw new Exception("Expected smpl block!");

            //read sample data size
            uint SampleDataSize = reader.ReadUInt32();
            //read sample data
            byte[] SampleData = reader.ReadBytes((int)SampleDataSize);

            if (reader.ReadUInt32() != 0x5453494C) //'LIST'
                throw new Exception("Expected LIST block!");

            //read size
            LISTSize = reader.ReadUInt32();
            LISTOffset = (uint)reader.BaseStream.Position + LISTSize;

            //pdta block
            if (reader.ReadUInt32() != 0x61746470) //'pdta'
                throw new Exception("Expected pdta block!");

            //phdr block instrument info
            SkipSubchunk(reader);

            //pbag
            SkipSubchunk(reader);

            //pmod
            SkipSubchunk(reader);

            //pgen
            SkipSubchunk(reader);

            //inst
            string sectionName = new string(reader.ReadChars(4));
            if (sectionName != "inst") {
                throw new InvalidOperationException("Expected 'inst', but got " + sectionName);
            }

            uint listSize = reader.ReadUInt32();
            List<inst_rec> instRecs = new();
            for (int i = 0; i < listSize / inst_rec.Size; i++) {
                instRecs.Add(new inst_rec(reader));
            }

            //ibag
            sectionName = new string(reader.ReadChars(4));
            if (sectionName != "ibag") {
                throw new InvalidOperationException("Expected 'ibag', but got " + sectionName);
            }

            listSize = reader.ReadUInt32();
            Dictionary<int, ushort> ibagIdToIgen = new();
            for (int i = 0; i < listSize / 4; i++) {
                ibagIdToIgen.Add(i, reader.ReadUInt16());
                _ = reader.ReadUInt16();
            }

            //imod
            SkipSubchunk(reader);

            //igen
            sectionName = new string(reader.ReadChars(4));
            if (sectionName != "igen") {
                throw new InvalidOperationException("Expected 'igen', but got " + sectionName);
            }

            listSize = reader.ReadUInt32();
            Dictionary<int, ushort> igenIdToSampleId = new();
            for (int i = 0; i < listSize / 4; i++) {
                ushort type = reader.ReadUInt16();
                ushort val = reader.ReadUInt16();

                if (type == 53) // SampleId
                {
                    igenIdToSampleId.Add(i, val);
                }
            }

            //shdr
            //Sample data!!! finally

            //temp
            if (reader.ReadUInt32() != 0x72646873) //'shdr'
                throw new Exception("Expected shdr block!");

            uint SHDRSize = reader.ReadUInt32();
            int SHDREntryCount = ((int)SHDRSize / 46) - 1; //always has at least 2 entries, one of them is padding

            for (int i = 0; i < SHDREntryCount; i++) {
                SF2Sound sound = UnpackSample(i, reader);
                Sounds.Add(sound);

                // This is slow, but we won't see many items...
                int igen = igenIdToSampleId.FirstOrDefault(x => x.Value == i).Key;
                int ibag = ibagIdToIgen.OrderByDescending(x => x.Value).FirstOrDefault(x => x.Value <= igen).Key;
                string instrName = instRecs.OrderByDescending(x => x.wInstBagNdx).FirstOrDefault(x => x.wInstBagNdx <= ibag)?.achInstName.Trim('\0');
                sound.InstrName = instrName;
            }

            reader.Close();

            List<Sample> samples = new List<Sample>();
            for (int i = 0; i < Sounds.Count; i++) {
                SF2Sound sF2Sound = Sounds[i];
                (short[]? L, short[]? R) audioData = GetAudioData(sF2Sound, SampleData);
                if (audioData.L is not null) {
                    Sample s = new Sample() {
                        name = sF2Sound.InstrName + "_" + sF2Sound.SoundName,
                        sampleRate = (int)sF2Sound.SampleRate,
                        sampleDataL = audioData.L,
                        sampleDataR = audioData.R ?? [],
                    };
                    s.SetBaseKey(sF2Sound.OriginalKey);

                    samples.Add(s);
                }
            }

            return samples;
        }

        static void SkipSubchunk(BinaryReader reader) {
            string SubchunkName = "";

            for (int i = 0; i < 4; i++)
                SubchunkName += (char)reader.ReadByte();

            uint LISTOffset = reader.ReadUInt32();
            LISTOffset += (uint)reader.BaseStream.Position; //LIST block is counter from the position at the end of the offset value

            //seek to sample data start
            reader.BaseStream.Seek((long)LISTOffset, SeekOrigin.Begin);
        }

        static SF2Sound UnpackSample(int id, BinaryReader reader) {
            string SampleName = "";

            for (int c = 0; c < 20; c++) {
                byte NextChar = reader.ReadByte();

                if (NextChar != 0)
                    SampleName += (char)NextChar;
            }

            SF2Sound NewSound = new SF2Sound(SampleName);
            NewSound.Id = id;

            uint SampleStart = reader.ReadUInt32();
            uint SampleEnd = reader.ReadUInt32();

            NewSound.SampleStart = SampleStart;
            NewSound.SampleSize = (SampleEnd - SampleStart);

            uint LoopStart = reader.ReadUInt32();
            uint LoopEnd = reader.ReadUInt32();

            //loop start is counted from the start of the sample field
            NewSound.SampleLoopStart = LoopStart - SampleStart;
            NewSound.SampleLoopLength = (LoopEnd - LoopStart);

            uint SampleRate = reader.ReadUInt32();

            NewSound.SampleRate = SampleRate;

            NewSound.OriginalKey = reader.ReadByte();
            reader.ReadByte(); //pitch correction

            ushort SampleLink = reader.ReadUInt16(); //sample link
            ushort SampleType = reader.ReadUInt16(); //sample type - ???

            //need to be taken into account when writing sample
            NewSound.SampleLink = SampleLink;
            NewSound.SampleType = SampleType;

            return NewSound;
        }

        static (short[]? L, short[]? R) GetAudioData(SF2Sound Sound, byte[] SampleData) {
            int side = 0; //0 = mono, 1 = left, 2 = right
            SF2Sound LinkedSound = Sound;

            switch (Sound.SampleType) {
                case 8:
                case 32776:
                    // Console.WriteLine("Ignored linked sample");
                    return (null, null);
                case 1:
                    //Console.WriteLine("Mono sample");
                    side = 0;
                    break;
                case 2:
                    //Console.WriteLine("Left sample");
                    side = 1;
                    LinkedSound = Sounds[Sound.SampleLink];
                    break;
                case 3:
                    //Console.WriteLine("Right sample");
                    side = 2;
                    LinkedSound = Sounds[Sound.SampleLink];
                    break;
                case 32769:
                case 32770:
                case 32772:
                    // ROM samples unsupported
                    return (null, null);
            }

            //get channel count, calculate the chunk size
            short[] dataL = new short[Sound.SampleSize];
            short[]? dataR = null;
            if (side != 0)
                dataR = new short[Sound.SampleSize];

            int SampleStart = ((int)Sound.SampleStart * 2);
            int LinkStart = ((int)LinkedSound.SampleStart * 2);

            for (int i = 0; i < Sound.SampleSize; i++) {
                int ind = i * 2;

                if (side == 0) {
                    dataL[i] = BinaryPrimitives.ReadInt16LittleEndian(SampleData.AsSpan(SampleStart + ind));
                }
                //stereo samples
                else if (side == 1) //left sample, with right linked
                {
                    dataL[i] = BinaryPrimitives.ReadInt16LittleEndian(SampleData.AsSpan(SampleStart + ind));
                    dataR[i] = BinaryPrimitives.ReadInt16LittleEndian(SampleData.AsSpan(LinkStart + ind));
                }
                else //right sample, with left linked
                {
                    dataL[i] = BinaryPrimitives.ReadInt16LittleEndian(SampleData.AsSpan(LinkStart + ind));
                    dataR[i] = BinaryPrimitives.ReadInt16LittleEndian(SampleData.AsSpan(SampleStart + ind));
                }
            }

            return (dataL, dataR);
        }
    }

    public class inst_rec {
        public static int Size = 22;
        public string achInstName;
        public ushort wInstBagNdx;

        public inst_rec(BinaryReader br) {
            achInstName = new string(br.ReadChars(20));
            wInstBagNdx = br.ReadUInt16();
        }
        public override string ToString() {
            string r = "";
            r += $"{achInstName.PadRight(20)}, ibag: {wInstBagNdx.ToString().PadRight(5)}";
            return r;
        }
    }

    public class SampleSelector : Clickable {
        public List<Sample> items;
        private Scrollbar scrollbar;
        private int numRows;
        private int selectedIndex;
        public HashSet<int> markedItems;

        public int SelectedIndex { get { return items.Count == 0 ? -1 : selectedIndex; } set { selectedIndex = Math.Clamp(value, 0, Math.Max(0, items.Count - 1)); } }
        public Sample SelectedItem {
            get {
                return items == null ? default : items.Count == 0 ? default : items[SelectedIndex];
            }
        }

        public SampleSelector(int x, int y, int width, int numVisibleRows, Element parent) {
            this.x = x;
            this.y = y;
            this.width = width;
            height = numVisibleRows * 11;
            numRows = numVisibleRows;
            scrollbar = new Scrollbar(0, 0, width, height, this);
            markedItems = new HashSet<int>();
            SetParent(parent);
        }

        /// <summary>
        /// Sets the list for this box to reference
        /// </summary>
        /// <param name="list"></param>
        public void SetList(List<Sample> list) {
            items = list;
            markedItems.Clear();
        }

        public void Update() {
            if (ClickedDown) {
                if (MouseX >= 0 && MouseX < width - 7) {
                    int rowNum = 0;
                    for (int i = scrollbar.ScrollValue; i < numRows + scrollbar.ScrollValue; i++) {
                        if (MouseY > rowNum && MouseY <= rowNum + 11) {
                            SelectedIndex = i;
                        }
                        rowNum += 11;
                    }
                }
            }
            
            if (Input.GetKeyDown(Keys.Space, KeyModifier.None) 
                || (Input.GetClickDown(KeyModifier._Any) && this.MouseX > width - 21 && this.MouseX < width - 9))
            {
                if (markedItems.Contains(SelectedIndex)) {
                    markedItems.Remove(SelectedIndex);
                }
                else if (SelectedIndex >= 0) {
                    markedItems.Add(SelectedIndex);
                }
            }

            if (GlobalPointIsInBounds(Input.LastClickLocation)) {
                if (Input.GetKeyRepeat(Keys.Up, KeyModifier.None)) {
                    SelectedIndex--;
                    if (SelectedIndex < 0) {
                        SelectedIndex = 0;
                    }

                    MoveBounds();
                }
                if (Input.GetKeyRepeat(Keys.Down, KeyModifier.None)) {
                    SelectedIndex++;
                    MoveBounds();
                }
            }
            scrollbar.SetSize(items.Count, numRows);
            scrollbar.UpdateScrollValue();
            scrollbar.Update();
        }

        public void MoveBounds() {
            if (SelectedIndex > scrollbar.ScrollValue + numRows - 1) {
                scrollbar.ScrollValue = SelectedIndex - numRows + 1;
            }
            if (SelectedIndex < scrollbar.ScrollValue) {
                scrollbar.ScrollValue = SelectedIndex;
            }
        }

        public void Draw() {
            Color odd = new Color(43, 49, 81);
            Color even = new Color(59, 68, 107);
            Color selected = UIColors.selection;
            int rowNum = 0;
            DrawRect(0, 0, width, height, selected);
            for (int i = scrollbar.ScrollValue; i < numRows + scrollbar.ScrollValue; i++) {
                Color rowColor = i == SelectedIndex ? selected : i % 2 == 0 ? even : odd;
                DrawRect(0, rowNum * 11, width, 11, rowColor);
                if (items.Count > i && i >= 0) {
                    string text = items[i].name;
                    Write(Helpers.TrimTextToWidth(width - 7, text), 3, rowNum * 11 + 2, Color.White);

                }

                if (i < this.items.Count)
                {
                    if (markedItems.Contains(i)) {
                        DrawSprite(width - 20, rowNum * 11 + 1, new Rectangle(288, 48, 9, 9));
                    }
                    else {
                        DrawSprite(width - 20, rowNum * 11 + 1, new Rectangle(363, 48, 9, 9));
                    }
                }

                ++rowNum;
            }
            scrollbar.Draw();
        }
    }
}
