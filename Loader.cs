// RMT 1.28 

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;

//using lzss2RMT;

namespace RMT
{
    public class ModuleData
    {
        public enum RMTVersion
        {
            _128Unpatched,
            _128Patch8,
            _127Patch6,
            _128CustomTables,
            Undefined = 99,
        }

        public RMTVersion m_rmtVersion = RMTVersion.Undefined;

        public ModuleData(RMTVersion v)
        {
            m_rmtVersion = v;
        }

        const int ShiftVolume = 4;

        //-------------------------------------------------------------------------------------------------------------------
        public const byte SONGLINE_JUMP = 0xfe;
        public const byte SONGLINE_JMPIDX = 2;

        public enum EffectCmd
        {
            cmd0,       //--- make sure commands are kept together
            cmd1,
            cmd2,
            cmd3,
            cmd4,
            cmd5,
            cmd6,
            cmd7,
        }

        //-------------------------------------------------------------------------------------------------------------------
        public class Sample
        {
            public List<byte> m_data;
            public int m_loopIdx;
            public double m_step;

            public Sample()
            {
                m_data = null;
                m_loopIdx = -1;
                m_step = 1;
            }
        }

        //-------------------------------------------------------------------------------------------------------------------
        public struct TrackLine
        {
            public enum Command
            {
                Undefined = 0,
                PlayNote = 1 << 0,
                SetVolume = 1 << 1,
                Pause = 1 << 2,
                SetSpeed = 1 << 3,
                GotoLine = 1 << 4,
                TrackEnd = 1 << 5,

                TrackCommand = TrackEnd | GotoLine,// |SetSpeed,

            }

            public Command m_command;
            public byte m_note;
            public byte m_volume;
            public byte m_instrument;
            public byte m_pauseDuration;
            public byte m_gotoLine;
            public byte m_speed;
        }


        //-------------------------------------------------------------------------------------------------------------------
        public class Instrument
        {
            public enum TableType
            {
                Note = 0,
                Frequency = 0x80,
            }
            public enum TableMode
            {
                Add = 0,
                Accu = 0x40,
            }

            public bool valid;

            public List<UInt16> m_noteOrFreqTable;
            public byte m_noteTableLoopIdx;
            public List<Envelope> m_envelope;
            public int m_envelopeLoopIdx;
            public double m_envelopeStep;
            public Sample m_sample;
            public byte m_tableSpeed;//table spd (bit 0-5), 
            public TableMode m_tableMode;// (bit 6), 
            public TableType m_tableType;//(bit 7)
            public byte m_audCtl;
            public byte m_volSlide;
            public byte m_volMin;   //(bit 4-7)
            public byte m_delay;//($00 for no vibrato & no fshift)
            public byte m_vibrato;
            public byte m_fShift;
            public byte m_filterShiftFreq;
        }

        //-------------------------------------------------------------------------------------------------------------------
        public class Envelope
        {
            enum Mask
            {
                Filter = 0x80,
                Command = 0x70,
                Distortion = 0x0e,
                Portamento = 0x01,
            }

            public byte m_volumeLR;
            public EffectCmd m_command;
            public UInt32 m_distortion;
            public bool m_bFilter;
            public bool m_bPortamento;
            public UInt16 m_commandArg;
            public byte m_audCTL;
            public bool m_bUseAudCTL;

            public byte m_portaSpeed;
            public UInt16 m_portaDepth;

            public Envelope(byte b0, byte b1, byte b2)
            {
                m_volumeLR = b0;
                byte CommandDist = b1;
                m_command = (EffectCmd)(((CommandDist & (byte)Envelope.Mask.Command) >> 4));
                m_distortion = (byte)((CommandDist & (byte)Envelope.Mask.Distortion));
                m_bFilter = (CommandDist & (byte)Envelope.Mask.Filter) != 0;
                m_bPortamento = (CommandDist & (byte)Envelope.Mask.Portamento) != 0;
                m_commandArg = (UInt16)(b2 << 8);

                if (m_command == EffectCmd.cmd5)
                {
                    m_portaSpeed = (byte)(b2 >> 4);
                    m_portaDepth = (UInt16)((b2 & 0x0f) << 8);
                }

                m_bUseAudCTL = false;
            }
        }

        //-------------------------------------------------------------------------------------------------------------------
        public int m_channelsCount;

        public string m_songName;// { get; private set; }

        public byte m_playbackSpeed;
        public byte m_V_speed;
        public byte m_V_maxtracklen;

        public List<Instrument> m_instruments;
        public List<List<TrackLine>> m_tracks;

        public struct SongLine
        {
            public byte[] trackIndices;

            public SongLine(int trackCount)
            {
                trackIndices = new byte[trackCount];
            }
        }

        public List<SongLine> m_songLines;

        //-------------------------------------------------------------------------------------------------------------------

        byte[] m_tune;

        //-------------------------------------------------------------------------------------------------------------------
        ushort Get16(byte[] vars, int idx) { return (ushort)(vars[idx] + (vars[idx + 1] << 8)); }
        byte Get8Tune(int idx, int offset = 0) { return m_tune[(int)idx + offset]; }

        //-------------------------------------------------------------------------------------------------------------------
        const int INSTRPAR = 12;   // offset of note table into instrument struct

        //;
        //; envelope, 3 values per line:
        //;
        //; $RL, $CD, $XY
        //;
        //; R = volume right (nibble)
        //; L = volume left  (nibble)
        //;
        //; CD = command/distortion
        //;     bit(s) 7   = filter
        //;            6-4 = command
        //;            3-1 = distortion
        //;            0   = portamento
        //;
        //; XY = argument to command
        //; 

        Instrument ProcessInstrument(byte[] buffer, int instrumentOffset)
        {
            Instrument instr = new ModuleData.Instrument();

            instr.valid = instrumentOffset != 0;
            if (!instr.valid)
                return instr;

            instr.m_noteOrFreqTable = new List<UInt16>();
            instr.m_envelope = new List<Envelope>();

            byte tableParams = buffer[instrumentOffset + 4];  // tspd (bit 0-5), tmode (bit 6), ttype (bit 7)
            instr.m_tableSpeed = (byte)(tableParams & 0x3f);
            instr.m_tableMode = (Instrument.TableMode)(tableParams & 0x40);
            instr.m_tableType = (Instrument.TableType)(tableParams & 0x80);

            byte tlen = (byte)(buffer[instrumentOffset + 0] + 1 - INSTRPAR);
            for (int n = 0; n < tlen; n++)
                instr.m_noteOrFreqTable.Add((UInt16)(buffer[instrumentOffset + n + INSTRPAR] << 8));
            instr.m_noteTableLoopIdx = (byte)(buffer[instrumentOffset + 1] - INSTRPAR);

            byte elen = (byte)(buffer[instrumentOffset + 2] + 3 - INSTRPAR);
            for (int n = tlen; n < elen; n += 3)
            {
                Envelope env = new Envelope(
                    buffer[instrumentOffset + n + INSTRPAR + 0],
                    buffer[instrumentOffset + n + INSTRPAR + 1],
                    buffer[instrumentOffset + n + INSTRPAR + 2]);
                instr.m_envelope.Add(env);
            }
            instr.m_envelopeLoopIdx = (byte)((buffer[instrumentOffset + 3] - (tlen + INSTRPAR)) / 3);

            instr.m_audCtl = buffer[instrumentOffset + 5];
            instr.m_volSlide = buffer[instrumentOffset + 6];
            instr.m_volMin = buffer[instrumentOffset + 7];
            instr.m_delay = buffer[instrumentOffset + 8];
            instr.m_vibrato = buffer[instrumentOffset + 9];
            instr.m_fShift = buffer[instrumentOffset + 10];
            instr.m_filterShiftFreq = 1;
            instr.m_envelopeStep = 1;
            return instr;
        }

        //-------------------------------------------------------------------------------------------------------------------
        const byte SPECIALNOTE_SetVolume = 0x3d;
        const byte SPECIALNOTE_Rest = 0x3e;
        const byte SPECIALNOTE_TrackCommand = 0x3f;

        List<TrackLine> ProcessTrack(int trackStart, int trackLen)
        {
            if (trackStart == -1)
                return null;


            int trackOffset = 0;

            byte note = 0xff;
            byte volAndNote = 0xff;

            List<TrackLine> track = new List<TrackLine>();

            TrackLine tLine = new TrackLine();

            List<int> trackLineOffsets = new List<int>();
            List<int> trackLineIndices = new List<int>();

            while (trackOffset < trackLen)
            {
                trackLineOffsets.Add(trackOffset);
                trackLineIndices.Add(track.Count);

                //--- get note
                volAndNote = Get8Tune(trackStart + trackOffset++);

                if (trackStart + trackOffset == m_tune.Length) // some stripped tunes don't contain last track in full
                {
                    tLine.m_command = TrackLine.Command.TrackEnd;
                    break;
                }

                note = (byte)(volAndNote & 0x3f);

                if (tLine.m_command != TrackLine.Command.SetSpeed)
                    tLine = new TrackLine();

                switch (note)
                {
                    case SPECIALNOTE_TrackCommand:
                        {
                            if (volAndNote == 0xff)
                            {
                                //---- end current track, skip to next song line
                                tLine.m_command = TrackLine.Command.TrackEnd;
                            }
                            else if (volAndNote > 0x7f)
                            {
                                //--- jump to track line
                                tLine.m_command = TrackLine.Command.GotoLine;
                                byte gotoOffset = Get8Tune(trackStart + trackOffset++);
                                tLine.m_gotoLine = gotoOffset;
                            }
                            else
                            {
                                //--- set speed
                                tLine.m_command = TrackLine.Command.SetSpeed;
                                tLine.m_speed = Get8Tune(trackStart + trackOffset++);
                            }
                            break;
                        }

                    case SPECIALNOTE_Rest:
                        {
                            //--- rest command
                            tLine.m_command |= TrackLine.Command.Pause;
                            byte pause = (byte)((volAndNote & 0xc0) >> 6);

                            if (pause == 0)
                                pause = Get8Tune(trackStart + trackOffset++);
                            tLine.m_pauseDuration = pause;
                            break;
                        }
                    default:// (note <= SPECIALNOTE_SetVolume)
                        {
                            tLine.m_command |= TrackLine.Command.SetVolume;

                            byte instrVol = Get8Tune(trackStart + trackOffset++);

                            if (note < SPECIALNOTE_SetVolume)
                            {
                                //--- regular note
                                tLine.m_command |= TrackLine.Command.PlayNote;
                                tLine.m_note = note;
                                tLine.m_instrument = (byte)(instrVol >> 2);
                            }
                            //--- volume (v3v2v1v0 <<4)
                            tLine.m_volume = (byte)(((instrVol & 0x3) << (2 + 4)) + ((volAndNote & 0xc0) >> (6 - ShiftVolume)));

                            break;
                        }
                }

                if (tLine.m_command != TrackLine.Command.SetSpeed)
                {
                    if (track.Count < m_V_maxtracklen)
                        track.Add(tLine);
                    else
                        break;
                }

                if (tLine.m_command == TrackLine.Command.TrackEnd)
                    break;
            }// for

            //--- fixup goto
            TrackLine[] trackArray = track.ToArray();
            track = new List<TrackLine>();
            for (int t = 0; t < trackArray.Length; t++)
            {
                if ((trackArray[t].m_command & TrackLine.Command.GotoLine) != 0)
                {
                    int gotoIdx = (byte)trackLineOffsets.IndexOf(trackArray[t].m_gotoLine);
                    Debug.Assert(gotoIdx != -1);
                    trackArray[t].m_gotoLine = (byte)trackLineIndices[gotoIdx];
                }
                track.Add(trackArray[t]);
            }


            return track;
        }

        //-------------------------------------------------------------------------------------------------------------------

        List<int> CountTunes(int songLen, int tracksCount)
        {
            List<int> playedLines = new List<int>();
            int maxPlayedLine = 0;
            int currentLine = 0;

            List<int> tunesStartLine = new List<int>();


            bool AlltracksValid = true;
            do
            {
                AlltracksValid = true;
                tunesStartLine.Add(currentLine);

                while (!playedLines.Contains(currentLine) && AlltracksValid && (currentLine < m_songLines.Count))
                {
                    if (m_songLines[currentLine].trackIndices[0] != SONGLINE_JUMP)
                    {
                        for (int c = 0; c < m_channelsCount; c++)
                        {
                            int trackIdx = m_songLines[currentLine].trackIndices[c];
                            if (((trackIdx >= tracksCount) && (trackIdx != 0xff)) ||
                                ((trackIdx != 0xff) && (m_tracks[trackIdx] == null)))
                            {
                                AlltracksValid = false;
                                tunesStartLine.RemoveAt(tunesStartLine.Count - 1);
                                break;
                            }
                        }
                    }

                    playedLines.Add(currentLine);
                    if (m_songLines[currentLine].trackIndices[0] != SONGLINE_JUMP)
                        currentLine++;
                    else
                        currentLine = m_songLines[currentLine].trackIndices[SONGLINE_JMPIDX];
                }
                if (AlltracksValid)
                {
                    maxPlayedLine = 0;
                    foreach (int playedLine in playedLines)
                    {
                        if (playedLine > maxPlayedLine)
                            maxPlayedLine = playedLine;
                    }
                    currentLine = maxPlayedLine + 1;
                }
            }
            while ((maxPlayedLine + 1 < songLen) && AlltracksValid);

            DebugStatus.Print("\nTunes count: " + tunesStartLine.Count);

            return tunesStartLine;
        }
        //-------------------------------------------------------------------------------------------------------------------

        byte[] ReadBinaryFile(string path)
        {
            byte[] buffer = null;

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                BinaryReader binaryReader = new BinaryReader(fs);
                int fileSize = (int)fs.Length;
                buffer = binaryReader.ReadBytes(fileSize);
                binaryReader.Close();
            }

            return buffer;
        }


        public List<int> LoadRMT(string path)
        {
            DebugStatus.Print("loading file " + path);

            m_tune = ReadBinaryFile(path);

            if ((m_tune[6] != 'R') || (m_tune[7] != 'M') || (m_tune[8] != 'T') /*|| (buffer[9] != '4')*/)
            {
                DebugStatus.Print("invalid RMT file");
                return null;
            }

            //---
            int tuneOldStartAddr = m_tune[2] + (m_tune[3] << 8);
            int tuneOldEndAddr = m_tune[4] + (m_tune[5] << 8);
            int tuneLen = tuneOldEndAddr - tuneOldStartAddr + 1;

            int relocOffset = 0 - tuneOldStartAddr;

            //--- remove segment data
            byte[] strippedBuffer = new byte[m_tune.Length - 6];
            Array.Copy(m_tune, 6, strippedBuffer, 0, m_tune.Length - 6);
            m_tune = strippedBuffer;

            //--- relocate ---//

            //--- get type (4/8 tracks)
            m_channelsCount = m_tune[3] - '0';   // number of channels!

            const int OffsetMaxTrackLen = 4;
            const int OffsetVSpeed = 5;
            const int OffsetInstrSpeed = 6;
            //--- get track len
            m_V_maxtracklen = m_tune[OffsetMaxTrackLen];

            //--- get track speed
            m_V_speed = m_tune[OffsetVSpeed];

            //--- get player update freq (instrument speed)
            m_playbackSpeed = m_tune[OffsetInstrSpeed];

            //--- ptrs
            ushort pinst = (ushort)(Get16(m_tune, 8) + relocOffset);
            ushort pltrc = (ushort)(Get16(m_tune, 10) + relocOffset);
            ushort phtrc = (ushort)(Get16(m_tune, 12) + relocOffset);
            ushort ptlst = (ushort)(Get16(m_tune, 14) + relocOffset);

            //--- instruments
            m_instruments = new List<Instrument>();

            for (int inst = 0; inst < (pltrc - pinst) / 2; inst++)
            {
                int instOffset = pinst + inst * 2;

                ushort addr = Get16(m_tune, instOffset);
                ushort tmp = 0;
                if (addr != 0)
                    tmp = (ushort)(addr + relocOffset);
                m_instruments.Add(ProcessInstrument(m_tune, tmp));
            }

            //--- get tracks offsets
            List<int> tracksOffsets = new List<int>();
            m_tracks = new List<List<TrackLine>>();
            int tracksCount = phtrc - pltrc;
            for (int trck = 0; trck < tracksCount; trck++)
            {
                ushort trackOffset = (ushort)(m_tune[pltrc + trck] + (m_tune[phtrc + trck] << 8));
                if (trackOffset == 0)
                    tracksOffsets.Add(-1);
                else
                    tracksOffsets.Add(trackOffset + relocOffset);
            }

            //--- tracks
            for (int t = 0; t < tracksCount; t++)
            {
                int trackOffset = tracksOffsets[t];
                int trackLen = m_tune.Length - trackOffset;

                int ti = t + 1;
                while (ti != tracksCount)
                {
                    if (tracksOffsets[ti] == -1)
                        ti++;
                    else
                    {
                        trackLen = tracksOffsets[ti] - trackOffset;
                        break;
                    }
                }

                m_tracks.Add(ProcessTrack(trackOffset, trackLen));
            }

            //--- song lines
            m_songLines = new List<SongLine>();
            int songLen = (tuneLen - ptlst) / m_channelsCount;
            songLen = songLen > 254 ? 254 : songLen;

            for (int l = 0; l < songLen; l++)
            {
                int idx = ptlst + l * m_channelsCount;
                SongLine line = new SongLine(m_channelsCount);

                if (m_tune[idx] == SONGLINE_JUMP)
                {
                    int tmp = ((Get16(m_tune, idx + 2) + relocOffset) - ptlst) / m_channelsCount;

                    line.trackIndices[0] = SONGLINE_JUMP;
                    line.trackIndices[SONGLINE_JMPIDX] = (byte)tmp;
                }
                else
                {
                    for (int t = 0; t < m_channelsCount; t++)
                        line.trackIndices[t] = m_tune[idx + t];
                }
                m_songLines.Add(line);
            }

            DebugStatus.Print("\nTune length: " + songLen + " lines", DebugStatus.VerboseOptions.MaxVerboseOnly);

            //--- Look for all tunes
            List<int> tunesStartLine = CountTunes(songLen, tracksCount);

            //--- get name
            int songNameIdx = ptlst + (songLen + 1) * m_channelsCount;
            int songNameEndIdx = songNameIdx;

            m_songName = "";
            while ((songNameEndIdx < m_tune.Length) && (m_tune[songNameEndIdx] != 0))
            {
                m_songName += (char)m_tune[songNameEndIdx++];
            }

            if (m_rmtVersion == RMTVersion._128CustomTables)
            {
                string ertiName = Path.ChangeExtension(path, "erti");
                if (File.Exists(ertiName))
                {
                    DebugStatus.Print("Loading extended instrument file");

                    List<Extensions.ExtendedInstrument> extendedInstrList = Extensions.LoadCustomInstruments(ertiName);

                    if (extendedInstrList == null)
                        return null;

                    foreach (Extensions.ExtendedInstrument eInstr in extendedInstrList)
                    {
                        int instrIdx = eInstr.originalIdx;

                        //---
                        if (m_instruments[instrIdx].m_envelope != null)
                        {
                            if ((eInstr.EnvelopeDist.Count != 0) && (m_instruments[instrIdx].m_envelope.Count != eInstr.EnvelopeDist.Count))
                                DebugStatus.Print("warning: instrument " + instrIdx + ": envelope length(" + m_instruments[instrIdx].m_envelope.Count + ") doesn't match extended instrument Envelope dist length(" + eInstr.EnvelopeDist.Count + ")");

                            if ((eInstr.EnvelopeAUDCTL.Count != 0) && (m_instruments[instrIdx].m_envelope.Count != eInstr.EnvelopeAUDCTL.Count))
                                DebugStatus.Print("warning: instrument " + instrIdx + ": envelope length(" + m_instruments[instrIdx].m_envelope.Count + ") doesn't match extended instrument Envelope AUDCTL length(" + eInstr.EnvelopeAUDCTL.Count + ")");

                            //--- copy dist data
                            int envCount = Math.Min(m_instruments[instrIdx].m_envelope.Count, eInstr.EnvelopeDist.Count);
                            for (int i = 0; i < envCount; i++)
                                m_instruments[instrIdx].m_envelope[i].m_distortion = eInstr.EnvelopeDist[i];

                            //---copy audctl data
                            envCount = Math.Min(m_instruments[instrIdx].m_envelope.Count, eInstr.EnvelopeAUDCTL.Count);
                            for (int i = 0; i < envCount; i++)
                            {
                                m_instruments[instrIdx].m_envelope[i].m_bUseAudCTL = true;
                                m_instruments[instrIdx].m_envelope[i].m_audCTL = eInstr.EnvelopeAUDCTL[i];
                            }
                            //---copy commandArg16
                            envCount = Math.Min(m_instruments[instrIdx].m_envelope.Count, eInstr.EnvelopeParam16.Count);
                            for (int i = 0; i < envCount; i++)
                            {
                                if (m_instruments[instrIdx].m_envelope[i].m_command == EffectCmd.cmd5)
                                {
                                    m_instruments[instrIdx].m_envelope[i].m_portaDepth = eInstr.EnvelopeParam16[i];
                                    m_instruments[instrIdx].m_envelope[i].m_portaSpeed = 1;
                                }
                                else
                                    m_instruments[instrIdx].m_envelope[i].m_commandArg = eInstr.EnvelopeParam16[i];

                            }
                            //---copy table note
                            envCount = Math.Min(m_instruments[instrIdx].m_noteOrFreqTable.Count, eInstr.TableNote16.Count);
                            for (int i = 0; i < envCount; i++)
                            {
                                m_instruments[instrIdx].m_noteOrFreqTable[i] = eInstr.TableNote16[i];
                            }

                        }

                        //---
                        m_instruments[instrIdx].m_sample = eInstr.sample;
                        m_instruments[instrIdx].m_envelopeStep = eInstr.envelopeStep;
                        m_instruments[instrIdx].m_filterShiftFreq = eInstr.initialShiftFreq;

                    }

                }

            }
            m_tune = null;

            return tunesStartLine;
        }
        //-------------------------------------------------------------------------------------------------------------------
        public const string DXRMTExtLower = ".dxrmt";

        struct DXRMT
        {
            public const string HeaderStr = "[HEADER]";
            public const string TuneNameStr = "TuneName=";
            public const string RegionStr = "Region=";
            public const string ChannelCountStr = "ChannelCount=";
            public const string UpdatesPerFrameStr = "UpdatesPerFrame=";
            public const string SpeedStr = "TuneSpeed=";
            public const string TrackLenghtStr = "TrackLength=";
            public const string TuneStr = "[TUNE]";
            public const string TracksStr = "[TRACK]";
            public const string InstrumentsStr = "[INSTRUMENT]";
            public const string InstrValidStr = "Valid=";
            public const string NoteFreqTblStr = "NoteOrFreqTable=";
            public const string NoteFreqTblLoopStr = "NoteOrFreqTableLoop=";

            public const string bUseAudCTLStr = "EnvUseAudCtl=\t";
            public const string eaudCtlStr = "EnvAudCtl=\t";
            public const string volumeLRStr = "EnvVolumeLR=\t";
            public const string commandStr = "EnvCommand=\t";
            public const string commandArgStr = "EnvCommandArg=\t";
            public const string distortionStr = "EnvDistortion=\t";
            public const string bFilterStr = "EnvFilter=\t";
            public const string bPortamentoStr = "EnvPortamento=\t";
            public const string portaSpeedStr = "EnvPortaSpeed=\t";
            public const string portaDepthStr = "EnvPortaDepth=\t";
            public const string EnvelopeLoopStr = "EnvelopeLoop=";
            public const string EnvelopeStepStr = "EnvelopeStep=";

            public const string tableSpeedStr = "tableSpeed=";
            public const string tableModeStr = "tableMode=";
            public const string tableTypeStr = "tableType=";
            public const string audCtlStr = "audCtl=";
            public const string volSlideStr = "volSlide=";
            public const string volMinStr = "volMin=";
            public const string delayStr = "delay=";
            public const string vibratoStr = "vibrato=";
            public const string fShiftStr = "fShift=";
            public const string filterShiftFreqStr = "filterShiftFreq=";
            public const string GotoStr = "Goto";
            public const string emptyNoteStr = "..";
            public const string emptyOctaveStr = ".";
            public const string emptyVolStr = ".";
            public const string emptyInstStr = "..";
            public const string emptySpeedStr = "..";

        }

        static string[] s_noteTable = { "C.", "C#", "D.", "D#", "E.", "F.", "F#", "G.", "G#", "A.", "A#", "B." };

        string GetBoolStr(bool v)
        {
            return v ? "Y  " : "N  ";
        }

        public void SaveDXRMT(string path, string region)
        {
            FileStream stream0 = new FileStream(path + DXRMTExtLower, FileMode.Create, FileAccess.Write);
            StreamWriter writer = new StreamWriter(stream0);

            //--- write header
            writer.WriteLine(DXRMT.HeaderStr);
            writer.WriteLine(DXRMT.TuneNameStr + m_songName);
            writer.WriteLine(DXRMT.RegionStr + region);
            writer.WriteLine(DXRMT.ChannelCountStr + m_channelsCount);
            writer.WriteLine(DXRMT.UpdatesPerFrameStr + m_playbackSpeed);
            writer.WriteLine(DXRMT.SpeedStr + m_V_speed);
            writer.WriteLine(DXRMT.TrackLenghtStr + m_V_maxtracklen);

            writer.WriteLine();

            //--- write tune
            writer.WriteLine(DXRMT.TuneStr);

            for (int l = 0; l < m_songLines.Count; l++)
            {
                int songJump = m_songLines[l].trackIndices[0] == SONGLINE_JUMP ? m_songLines[l].trackIndices[SONGLINE_JMPIDX] : -1;

                for (int c = 0; c < m_channelsCount; c++)
                    writer.Write(m_songLines[l].trackIndices[c].ToString("X02") + " ");
                writer.WriteLine(" // " + l.ToString("000") + " ($" + l.ToString("X02") + ")  " + (songJump != -1 ? DXRMT.GotoStr + " $" + songJump.ToString("X02") : ""));
            }
            writer.WriteLine();

            //--- write tracks

            for (int t = 0; t < m_tracks.Count; t++)
            {
                writer.Write(DXRMT.TracksStr);
                writer.WriteLine(" //" + t.ToString("000") + " ($" + t.ToString("X02") + ")");

                List<TrackLine> track = m_tracks[t];

                int line = 0;

                for (int c = 0; c < track.Count; c++)
                {
                    int pauseDuration = 1;
                    string noteStr = DXRMT.emptyNoteStr;
                    string octaveStr = DXRMT.emptyOctaveStr;
                    string volStr = DXRMT.emptyVolStr;
                    string instStr = DXRMT.emptyInstStr;
                    string speedStr = DXRMT.emptySpeedStr;

                    switch (track[c].m_command)
                    {
                        case TrackLine.Command.Pause:
                            pauseDuration = track[c].m_pauseDuration;
                            break;

                        case TrackLine.Command.GotoLine:
                            noteStr = "Goto ";
                            octaveStr = track[c].m_gotoLine.ToString();
                            break;

                        default:
                            if ((track[c].m_command & TrackLine.Command.PlayNote) != 0)
                            {
                                noteStr = s_noteTable[track[c].m_note % 12];
                                octaveStr = (track[c].m_note / 12 + 1).ToString();
                                instStr = track[c].m_instrument.ToString("X02");
                            }
                            if ((track[c].m_command & TrackLine.Command.SetVolume) != 0)
                                volStr = (track[c].m_volume >> ShiftVolume).ToString("X");
                            if ((track[c].m_command & TrackLine.Command.SetSpeed) != 0)
                                speedStr = track[c].m_speed.ToString("X02");

                            break;

                    }

                    for (int i = 0; i < pauseDuration; i++)
                    {
                        writer.WriteLine(noteStr + octaveStr + " " + instStr + " " + volStr + " " + speedStr + "\t//" + line);
                        line++;
                    }

                }
                writer.WriteLine();
            }
            //--- write instruments

            for (int i = 0; i < m_instruments.Count; i++)
            {
                writer.WriteLine("//----------------");

                writer.Write(DXRMT.InstrumentsStr + i.ToString("X02"));
                writer.WriteLine(" // " + i.ToString("000"));

                writer.WriteLine(DXRMT.InstrValidStr + GetBoolStr(m_instruments[i].valid));

                //---
                writer.Write(DXRMT.NoteFreqTblStr);
                for (int n = 0; n < m_instruments[i].m_noteOrFreqTable.Count; n++)
                    writer.Write((m_instruments[i].m_noteOrFreqTable[n] >> 8).ToString("X02") + " ");

                writer.WriteLine("\n" + DXRMT.NoteFreqTblLoopStr + m_instruments[i].m_noteTableLoopIdx);

                writer.WriteLine(DXRMT.tableSpeedStr + (m_instruments[i].m_tableSpeed + 1));     //---table speed is +1 !!!
                writer.WriteLine(DXRMT.tableTypeStr + m_instruments[i].m_tableType);
                writer.WriteLine(DXRMT.tableModeStr + m_instruments[i].m_tableMode);

                //---
                string bUseAudCTL = DXRMT.bUseAudCTLStr;
                string audCtl = DXRMT.eaudCtlStr;
                string volumeLR = DXRMT.volumeLRStr;
                string command = DXRMT.commandStr;
                string commandArg = DXRMT.commandArgStr;
                string distortion = DXRMT.distortionStr;
                string bFilter = DXRMT.bFilterStr;
                string bPortamento = DXRMT.bPortamentoStr;
                string portaSpeed = DXRMT.portaSpeedStr;
                string portaDepth = DXRMT.portaDepthStr;

                for (int n = 0; n < m_instruments[i].m_envelope.Count; n++)
                {
                    Envelope env = m_instruments[i].m_envelope[n];

                    bUseAudCTL += GetBoolStr(env.m_bUseAudCTL);
                    audCtl += env.m_audCTL.ToString("X02") + " ";
                    volumeLR += env.m_volumeLR.ToString("X02") + " ";
                    command += ((int)env.m_command).ToString("X02") + " ";
                    commandArg += (env.m_commandArg >> 8).ToString("X02") + " ";
                    distortion += env.m_distortion.ToString("X04") + " ";
                    bFilter += GetBoolStr(env.m_bFilter);
                    bPortamento += GetBoolStr(env.m_bPortamento);
                    portaSpeed += env.m_portaSpeed.ToString("X02") + " ";
                    portaDepth += env.m_portaDepth.ToString("X02") + " ";
                }

                writer.WriteLine("//- Envelope " + m_instruments[i].m_envelope.Count);
                writer.WriteLine(bUseAudCTL);
                writer.WriteLine(audCtl);
                writer.WriteLine(volumeLR);
                writer.WriteLine(distortion);
                writer.WriteLine(command);
                writer.WriteLine(commandArg);
                writer.WriteLine(bFilter);
                writer.WriteLine(bPortamento);
                writer.WriteLine(portaSpeed);
                writer.WriteLine(portaDepth);
                writer.WriteLine(DXRMT.EnvelopeLoopStr + m_instruments[i].m_envelopeLoopIdx);
                writer.WriteLine(DXRMT.EnvelopeStepStr + m_instruments[i].m_envelopeStep.ToString("F3"));
                writer.WriteLine(DXRMT.volSlideStr + m_instruments[i].m_volSlide);
                writer.WriteLine(DXRMT.volMinStr + m_instruments[i].m_volMin);

                //---
                writer.WriteLine(DXRMT.delayStr + m_instruments[i].m_delay);
                writer.WriteLine(DXRMT.vibratoStr + m_instruments[i].m_vibrato);
                writer.WriteLine(DXRMT.fShiftStr + m_instruments[i].m_fShift);
                writer.WriteLine(DXRMT.audCtlStr + m_instruments[i].m_audCtl);
                writer.WriteLine(DXRMT.filterShiftFreqStr + m_instruments[i].m_filterShiftFreq);

            }



            writer.Close();

        }

        //-------------------------------------------------------------------------------------------------------------------
        enum DXRMTReaderState
        {
            Undefined = 0,
            Header,
            Tune,
            Tracks,
            Instruments,
        }

        void ProcessTune(string line)
        {
            string[] subLine = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            Debug.Assert(subLine.Length == m_channelsCount);

            SongLine s = new SongLine(m_channelsCount);
            for (int c = 0; c < m_channelsCount; c++)
                s.trackIndices[c] = Convert.ToByte(subLine[c], 16);

            m_songLines.Add(s);

        }

        void ProcessHeader(string line)
        {
            string[] subLine = line.Split(new char[] { '=' });
            subLine[0] += "=";

            switch (subLine[0])
            {
                case DXRMT.TuneNameStr:
                    m_songName = subLine[1];
                    break;
                case DXRMT.RegionStr:
                    break;
                case DXRMT.ChannelCountStr:
                    m_channelsCount = Convert.ToByte(subLine[1]);
                    break;
                case DXRMT.UpdatesPerFrameStr:
                    m_playbackSpeed = Convert.ToByte(subLine[1]);
                    break;
                case DXRMT.SpeedStr:
                    m_V_speed = Convert.ToByte(subLine[1]);
                    break;
                case DXRMT.TrackLenghtStr:
                    m_V_maxtracklen = Convert.ToByte(subLine[1]);
                    break;
            }
        }

        const string NoNoteStr = "...";

        void ProcessTrack(string line, List<TrackLine> currentTrack)
        {
            string[] subLine = line.Split(new char[] { ' ' });

            TrackLine tl = new TrackLine();

            if (subLine[0] == DXRMT.GotoStr)
            {
                //--- goto
                byte gotoLine = Convert.ToByte(subLine[1]);
                tl.m_command = TrackLine.Command.GotoLine;
                tl.m_gotoLine = gotoLine;
            }
            else
            {
                if (subLine[0] == NoNoteStr)
                {
                    //--- pause
                    if ((subLine[2] == DXRMT.emptyVolStr) || (subLine[3] == DXRMT.emptySpeedStr))
                    {
                        tl.m_command = TrackLine.Command.Pause;
                        tl.m_pauseDuration = 1;
                    }
                }
                else
                {
                    //--- note
                    byte note = 0;
                    byte octave = Convert.ToByte(subLine[0].Substring(2, 1));

                    string noteStr = subLine[0].Substring(0, 2);
                    for (int n = 0; n < s_noteTable.Length; n++)
                    {
                        if (noteStr == s_noteTable[n])
                        {
                            note = (byte)((octave - 1) * 12 + n);
                            break;
                        }
                    }
                    tl.m_command = TrackLine.Command.PlayNote;
                    tl.m_note = note;

                    //--- instrument
                    tl.m_instrument = Convert.ToByte(subLine[1], 16);
                }

                if (subLine[2] != DXRMT.emptyVolStr)
                {
                    tl.m_command |= TrackLine.Command.SetVolume;
                    //--- volume
                    tl.m_volume = Convert.ToByte(subLine[2], 16);
                }

                if (subLine[3] != DXRMT.emptySpeedStr)
                {
                    tl.m_command |= TrackLine.Command.SetSpeed;
                    //--- volume
                    tl.m_speed = Convert.ToByte(subLine[3], 16);
                }
            }

            currentTrack.Add(tl);
        }

        List<TrackLine> CurrentTrackCompressPauses(List<TrackLine> t)
        {
            List<TrackLine> newT = new List<TrackLine>();

            byte pause = 0;

            foreach (TrackLine tl in t)
            {
                if (tl.m_command == TrackLine.Command.Pause)
                {
                    pause++;
                }
                else
                {
                    if (pause != 0)
                    {
                        newT.Add(new TrackLine() { m_command = TrackLine.Command.Pause, m_pauseDuration = pause });
                        pause = 0;
                    }
                    newT.Add(tl);
                }
            }

            if (pause != 0)
                newT.Add(new TrackLine() { m_command = TrackLine.Command.Pause, m_pauseDuration = pause });

            return newT;
        }


        public List<int> LoadDXRMT(string path)
        {
            FileStream stream0 = new FileStream(path, FileMode.Open, FileAccess.Read);
            StreamReader reader = new StreamReader(stream0);

            List<string> dxRMT = new List<string>();

            m_channelsCount = -1;

            //--- remove comments
            while (!reader.EndOfStream)
            {
                string currentLine = reader.ReadLine();
                if (!currentLine.StartsWith("//"))
                {
                    int idxOfComment = -1;
                    if ((idxOfComment = currentLine.IndexOf("//")) != -1)
                        currentLine = currentLine.Substring(0, idxOfComment);

                    currentLine = currentLine.TrimEnd(new char[] { ' ', '\t' });

                    if (currentLine != "")
                        dxRMT.Add(currentLine);
                }
            }

            //---
            DXRMTReaderState state = DXRMTReaderState.Undefined;

            List<TrackLine> currentTrack = new List<TrackLine>();

            foreach (string line in dxRMT)
            {
                switch (line)
                {
                    case DXRMT.HeaderStr:
                        state = DXRMTReaderState.Header;
                        continue;
                    case DXRMT.TuneStr:
                        state = DXRMTReaderState.Tune;
                        m_songLines = new List<SongLine>();
                        continue;
                    case DXRMT.TracksStr:
                        state = DXRMTReaderState.Tracks;
                        if (m_tracks == null)
                            m_tracks = new List<List<TrackLine>>();
                        else
                        {
                            m_tracks.Add(CurrentTrackCompressPauses(currentTrack));
                            currentTrack = new List<TrackLine>();
                        }
                        continue;
                    case DXRMT.InstrumentsStr:
                        state = DXRMTReaderState.Instruments;
                        continue;
                    //                     default:
                    //                         break;
                }

                switch (state)
                {
                    case DXRMTReaderState.Header:
                        ProcessHeader(line);
                        break;
                    case DXRMTReaderState.Tune:
                        ProcessTune(line);
                        break;
                    case DXRMTReaderState.Tracks:
                        ProcessTrack(line, currentTrack);
                        break;
                    default:
                        Debug.Assert(false);
                        break;
                }
            }
            m_tracks.Add(CurrentTrackCompressPauses(currentTrack)); //--- add last track


            //--- tune
            List<int> tunesStartLine = null;// CountTunes(songLen, tracksCount);

            return tunesStartLine;
        }

    }

}