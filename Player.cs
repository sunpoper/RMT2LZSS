// RMT 1.28 

using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace RMT
{
    partial class Player
    {
        public enum EffectRetCmd
        {
            note,
            freq,
            Invalid,
        }


        public enum MRTState
        {
            GetSongLine,
            GetSongLineInit,
            GetTrackLine,

            // PLAY
            rmt_play,
            rmt_p3,

            //
            rmt_p4,

            //
            ExitFlow,       // put all exit paths here

            Exit_DonePlaying,
            Exit_rts,
        }

        public enum PlaybackSpeed
        {
            Invalid = -1,
            _50hz = 1,
            _100hz = 2,
            _150hz = 3,
            _200hz = 4,
            _400hz = 5,
            _800hz = 6,
            MAX
        }

        const int PokeyChannelCount = 9;

        const byte AUDCTL_PLY = 0x80;
        const byte AUDCTL_CH1_FASTCLOCK = 0x40;
        const byte AUDCTL_CH3_FASTCLOCK = 0x20;
        const byte AUDCTL_CH1CH2_LINK = 0x10;
        const byte AUDCTL_CH3CH4_LINK = 0x08;
        const byte AUDCTL_HP1 = 0x4;
        const byte AUDCTL_HP3 = 0x2;
        const byte AUDCTL_15K = 0x1;

        const byte AUDC_VOLUMEONLY = 0x10;
        const byte AUDC_VOLUMEMASK = 0x0F;

        enum VibTab
        {
            vib0 = 0+4,
            vib1 = 1 + 4,
            vib2 = 1 + 4 + 4,
            vib3 = 1 + 4 + 6 + 4,
        }

        byte[] vibtabbeg = new byte[]
        {
            0,VibTab.vib1-VibTab.vib0,VibTab.vib2-VibTab.vib0,VibTab.vib3-VibTab.vib0,
/*vib0*/	0,
/*vib1*/    1,0xff,0xff,1,
/*vib2*/	1,0,0xff,0xff,0,1,
/*vib3*/	1,1,0,0xff,0xff,0xff,0xff,0,1,1,
        };

        byte[] vibtabnext = new byte[]
        {
		    VibTab.vib0-VibTab.vib0+0,
		    VibTab.vib1-VibTab.vib0+1,VibTab.vib1-VibTab.vib0+2,VibTab.vib1-VibTab.vib0+3,VibTab.vib1-VibTab.vib0+0,
		    VibTab.vib2-VibTab.vib0+1,VibTab.vib2-VibTab.vib0+2,VibTab.vib2-VibTab.vib0+3,VibTab.vib2-VibTab.vib0+4,VibTab.vib2-VibTab.vib0+5,VibTab.vib2-VibTab.vib0+0,
		    VibTab.vib3-VibTab.vib0+1,VibTab.vib3-VibTab.vib0+2,VibTab.vib3-VibTab.vib0+3,VibTab.vib3-VibTab.vib0+4,VibTab.vib3-VibTab.vib0+5,VibTab.vib3-VibTab.vib0+6,VibTab.vib3-VibTab.vib0+7,VibTab.vib3-VibTab.vib0+8,VibTab.vib3-VibTab.vib0+9,VibTab.vib3-VibTab.vib0+0,
        };

        byte[] volumetab = new byte[]
        {
	        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
	        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x01,0x01,0x01,0x01,0x01,0x01,0x01,0x01,
	        0x00,0x00,0x00,0x00,0x01,0x01,0x01,0x01,0x01,0x01,0x01,0x01,0x02,0x02,0x02,0x02,
	        0x00,0x00,0x00,0x01,0x01,0x01,0x01,0x01,0x02,0x02,0x02,0x02,0x02,0x03,0x03,0x03,
	        0x00,0x00,0x01,0x01,0x01,0x01,0x02,0x02,0x02,0x02,0x03,0x03,0x03,0x03,0x04,0x04,
	        0x00,0x00,0x01,0x01,0x01,0x02,0x02,0x02,0x03,0x03,0x03,0x04,0x04,0x04,0x05,0x05,
	        0x00,0x00,0x01,0x01,0x02,0x02,0x02,0x03,0x03,0x04,0x04,0x04,0x05,0x05,0x06,0x06,
	        0x00,0x00,0x01,0x01,0x02,0x02,0x03,0x03,0x04,0x04,0x05,0x05,0x06,0x06,0x07,0x07,
	        0x00,0x01,0x01,0x02,0x02,0x03,0x03,0x04,0x04,0x05,0x05,0x06,0x06,0x07,0x07,0x08,
	        0x00,0x01,0x01,0x02,0x02,0x03,0x04,0x04,0x05,0x05,0x06,0x07,0x07,0x08,0x08,0x09,
	        0x00,0x01,0x01,0x02,0x03,0x03,0x04,0x05,0x05,0x06,0x07,0x07,0x08,0x09,0x09,0x0A,
	        0x00,0x01,0x01,0x02,0x03,0x04,0x04,0x05,0x06,0x07,0x07,0x08,0x09,0x0A,0x0A,0x0B,
	        0x00,0x01,0x02,0x02,0x03,0x04,0x05,0x06,0x06,0x07,0x08,0x09,0x0A,0x0A,0x0B,0x0C,
	        0x00,0x01,0x02,0x03,0x03,0x04,0x05,0x06,0x07,0x08,0x09,0x0A,0x0A,0x0B,0x0C,0x0D,
	        0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x07,0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,
	        0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,0x0F,
        };

        //-------------------------------------------------------------------------------------------------------------------
        public struct Portamento
        {
            public byte speeda;
            public byte speed;
            public UInt16 frqa;
            public UInt16 frqc;
            public UInt16 depth;
        }

        //-------------------------------------------------------------------------------------------------------------------
        public class Track
        {
            public const byte InvalidInstrument = 0x80;

            //--- per channel song stuff
            public List<ModuleData.TrackLine> m_lines;
            public byte m_lineIdx;
            public byte m_pause;
            public byte m_note;
            public byte m_outNote;
            public byte m_volume;

            public bool m_instrValid;
            public byte m_newInstrIdx;
            public byte m_instrIdx;

            //--- instruments stuff
            public byte m_filterShiftFreq;
            public int m_tableSpeedAcc;
            public byte m_audctl;
            public byte m_effdelay;
            public byte m_effvibratoa;
            public byte m_volumeSlideVal;
            public bool m_instrreachend;
            public byte m_shiftfrq;
            public double m_envelopeIdx;
            public double m_prevEnvelopeIdx;
            public byte m_tableNoteIdx;
            public UInt16 m_tableNoteOrFreq;
            public UInt16 m_frqAddCmd;
            public int m_frqSetCmd;

            public ModuleData.Envelope m_envelope;

            public double m_sampleIdx;

            //--- effects
            public Portamento m_portamento;
        }


        //-------------------------------------------------------------------------------------------------------------------

        public const int MAX_CHANNELS = 8;       // Max channel count

        public byte m_v_abeat;

        public byte m_v_aspeed;
        public byte m_v_bspeed;
        public byte m_currentUpdateFreq;

        public int m_startSongLine;
        public int m_currentSongLine;

        public Track[] m_currentTracks;

        //--- Pokey
        public byte m_audCtl;
        public byte m_audCtl2;
        public byte[] m_audC;
        public byte[] m_audF;

        //-------------------------------------------------------------------------------------------------------------------

        ModuleData m_rmtData;

        //-------------------------------------------------------------------------------------------------------------------
        int m_loopFrame;
        List<int> m_recorderLinesOffsets;
        List<int> m_recorderLinesFrameIdx;
        List<int> m_recorderLinesIdx;

        bool RecorderAddSongLine()
        {
            if (!m_recorderLinesOffsets.Contains(m_currentSongLine))
            {
                int frameIndex = m_pokeyBufL.Count / PokeyChannelCount;

                m_recorderLinesOffsets.Add(m_currentSongLine);
                m_recorderLinesFrameIdx.Add(frameIndex);
                m_recorderLinesIdx.Add(m_currentSongLine - m_startSongLine);
                return true;
            }

            int loopFrameIdx = m_recorderLinesOffsets.IndexOf(m_currentSongLine);
            DebugStatus.Print("Found Loop point at frame " + (m_loopFrame = m_recorderLinesFrameIdx[loopFrameIdx]), DebugStatus.VerboseOptions.MaxVerboseOnly);
            return false;
        }

        //-------------------------------------------------------------------------------------------------------------------

        public Player(ModuleData staticData)
        {
            m_rmtData = staticData;
            SetVersion();

            m_audC = new byte[MAX_CHANNELS];
            m_audF = new byte[MAX_CHANNELS];

            m_currentTracks = new Track[MAX_CHANNELS];

            for (int c = 0; c < MAX_CHANNELS; c++)
                m_currentTracks[c] = new Track();

        }

        //-------------------------------------------------------------------------------------------------------------------

// in XY = LOHI RMT tune
// in A = starting song line    

        List<byte> m_pokeyBufL;
        List<byte> m_pokeyBufR;

        List<int> m_volumeOffsets;

        bool m_playSingleSongLine;

        static byte[] s_playbackSpeedTbl ={   0,1,2,3,4,8,16,32   };

        public PlaybackSpeed Init(int startSongLine, bool playSingleLine, List<int>volumeOffsets = null)
        {
            m_playSingleSongLine = playSingleLine;

            m_currentUpdateFreq = s_playbackSpeedTbl[m_rmtData.m_playbackSpeed];

            m_volumeOffsets = volumeOffsets;

            //--- setup recorder stuff
            m_playedFramesCount = 0;
            m_pokeyBufL = new List<byte>();
            if (m_rmtData.m_channelsCount > 4)
                m_pokeyBufR = new List<byte>();
            m_recorderLinesOffsets = new List<int>();
            m_recorderLinesFrameIdx = new List<int>();
            m_recorderLinesIdx = new List<int>();
            m_loopFrame = 0;

            //--- set start line
            m_startSongLine = m_currentSongLine = startSongLine;

            MRTState gtlState = MRTState.GetSongLineInit;
            do
            {
                gtlState = Flow(gtlState);
            }
            while (gtlState != MRTState.Exit_rts);

            return (RMT.Player.PlaybackSpeed)m_rmtData.m_playbackSpeed;    // return playback speed

            //	rts
        }   // end init
        //-------------------------------------------------------------------------------------------------------------------
        int m_playedFramesCount;

        public void Play(out List<byte> pokeyBufL, out List<byte> pokeyBufR, out int loopFrame)
        {

            MRTState gtlState;
            do
            {
                gtlState = MRTState.rmt_play;
                do
                {
                    gtlState = Flow(gtlState);
                }
                while((gtlState != MRTState.Exit_rts)&&(gtlState != MRTState.Exit_DonePlaying));

                m_playedFramesCount++;

            }
            while (gtlState != MRTState.Exit_DonePlaying);

            pokeyBufL = m_pokeyBufL;
            pokeyBufR = m_pokeyBufR;
            loopFrame = m_loopFrame;

            DebugStatus.Print("\nPlayed " + m_playedFramesCount + " frames\n");
        }

        //-------------------------------------------------------------------------------------------------------------------

        MRTState Flow(MRTState gtlState)
        {
            switch(gtlState)
            {

                case MRTState.GetSongLine:
                case MRTState.GetSongLineInit:
                {
                    if (!RecorderAddSongLine())
                    {
                        //--- song line already played -> tune looped -> bail out!
                        return MRTState.Exit_DonePlaying; 
                    }
                    if (m_playSingleSongLine && gtlState != MRTState.GetSongLineInit)
                    {
                        //--- played first line -> bail out!
                        return MRTState.Exit_DonePlaying; 
                    }

                    m_v_abeat = 0;

                    byte channelIdx = 0;

                    do
                    {
                        if (m_currentSongLine >= m_rmtData.m_songLines.Count)
                        {
                            //--- fix for pattern ptr reaching end of song lines and going past end of data
                            return MRTState.Exit_DonePlaying; 
                        }

                        byte trackIdx = m_rmtData.m_songLines[m_currentSongLine].trackIndices[channelIdx];
                        byte pause = 0x0;

                        if (trackIdx == ModuleData.SONGLINE_JUMP)
                        {
                            //--- Jump to song line
                            m_currentSongLine = m_rmtData.m_songLines[m_currentSongLine].trackIndices[ModuleData.SONGLINE_JMPIDX];

                            if (!RecorderAddSongLine())
                            {
                                //--- song line already played -> tune looped -> bail out!
                                return MRTState.Exit_DonePlaying; 
                            }
                            channelIdx = 0xff; //--- restart loop
                        }
//                         else if (trackIdx > SONGLINE_JUMP)  //--- empty track ($FF)
//                             pause = 0;
                        else if (trackIdx < ModuleData.SONGLINE_JUMP)
                        {
                            //--- regular track
                            m_currentTracks[channelIdx].m_lines = m_rmtData.m_tracks[trackIdx];
                            m_currentTracks[channelIdx].m_lineIdx = 0;
                            pause = 1;
                        }

                        if (channelIdx != 0xff)
                        {
                            m_currentTracks[channelIdx].m_pause = pause;
                            m_currentTracks[channelIdx].m_newInstrIdx = Track.InvalidInstrument;
                        }

                        //--- next channel
                        channelIdx++;
                    }
                    while (channelIdx != m_rmtData.m_channelsCount);

                    //--- all channels set, get channel data
                    m_currentSongLine++;

                    return MRTState.GetTrackLine; 
                }

                //----------------------
                //; note format 
                //; byte 1:       bits 7-0    [v1 v0 n5 n4 n3 n2 n1 n0]
                //; byte 2:       bits 7-0    [i5 i4 i3 i2 i1 i0 v3 v2]
                //;
                //; n = [n5 n4 n3 n2 n1 n0] = note
                //; v = [      v3 v2 v1 v0] = volume
                //; i = [i5 i4 i3 i2 i1 i0] = instrument

                case MRTState.GetTrackLine:
                {
                    m_v_bspeed = m_rmtData.m_V_speed;
                    byte channelIdx = 0xff;

                    do
                    {
                        channelIdx++;
                        byte v = --m_currentTracks[channelIdx].m_pause;
                        if (v != 0)
                            continue;

                        byte trackLineIdx = m_currentTracks[channelIdx].m_lineIdx++;
                        ModuleData.TrackLine trackLine = m_currentTracks[channelIdx].m_lines[trackLineIdx];

                        while ((trackLine.m_command & ModuleData.TrackLine.Command.TrackCommand) != 0)
                        {
                            if (trackLine.m_command == ModuleData.TrackLine.Command.TrackEnd)
                            {
                                //---- end current track, skip to next song line
                                return MRTState.GetSongLine;
                            }
                            else if (trackLine.m_command == ModuleData.TrackLine.Command.GotoLine)
                            {
                                //--- jump to track line
                                m_currentTracks[channelIdx].m_lineIdx = trackLine.m_gotoLine;
                            }
                            trackLineIdx = m_currentTracks[channelIdx].m_lineIdx++;
                            trackLine = m_currentTracks[channelIdx].m_lines[trackLineIdx];
                        }

                        if ((trackLine.m_command & ModuleData.TrackLine.Command.SetSpeed) != 0)
                        {
                            //--- set speed
                            m_v_bspeed = trackLine.m_speed;
                        }


                        if ((trackLine.m_command & ModuleData.TrackLine.Command.Pause) != 0)
                        {
                            //--- rest command
                            m_currentTracks[channelIdx].m_pause = trackLine.m_pauseDuration;
                        }
                        else if ((trackLine.m_command & ModuleData.TrackLine.Command.PlayNote) != 0)
                        {
                            //--- regular note
                            m_currentTracks[channelIdx].m_note = trackLine.m_note;
                            m_currentTracks[channelIdx].m_outNote = trackLine.m_note;
                            //--- instrument 
                            m_currentTracks[channelIdx].m_newInstrIdx = trackLine.m_instrument;
                        }
                        if ((trackLine.m_command & ModuleData.TrackLine.Command.SetVolume) != 0)
                        {
                            //--- volume (v3v2v1v0 <<4)
                            int volume = trackLine.m_volume;
                            //volume &= 0xff;  //--- ???????? incorrect since volume still contains the note ???? only a problem if using volume fade though
                            volume &= 0xf0;

                            //--- fade volume 
                            if (m_volumeOffsets != null)
                            {
                                volume >>= 4;
                                volume += m_volumeOffsets[channelIdx];
                                volume = volume < 0 ? 0 : volume;
                                volume = volume > 0xf ? 0xf : volume;
                                volume <<= 4;
                            }

                            m_currentTracks[channelIdx].m_pause = 1; //--- this is set for PlayNote too because PlayNote always sets the volume
                            m_currentTracks[channelIdx].m_volume = (byte)(volume & 0xf0);
                        }
                    }
                    while (channelIdx != m_rmtData.m_channelsCount - 1);

                    m_rmtData.m_V_speed = m_v_aspeed = m_v_bspeed;
                    
                    //case MRTState.InitOfNewSetInstrumentsOnly:

                    for (channelIdx = 0; channelIdx < m_rmtData.m_channelsCount; channelIdx++)
                    {
                        byte instrumentIdx = m_currentTracks[channelIdx].m_newInstrIdx;
                        if (instrumentIdx >= Track.InvalidInstrument)
                            continue;

                        m_currentTracks[channelIdx].m_instrIdx = instrumentIdx;
                        ModuleData.Instrument instr = m_rmtData.m_instruments[instrumentIdx];

                        m_currentTracks[channelIdx].m_instrValid = instr.valid;

                        //---
                        m_currentTracks[channelIdx].m_filterShiftFreq = instr.m_filterShiftFreq;

                        m_currentTracks[channelIdx].m_tableSpeedAcc = instr.m_tableSpeed;
                        m_currentTracks[channelIdx].m_audctl = instr.m_audCtl;
                        m_currentTracks[channelIdx].m_effdelay = instr.m_delay;
                        m_currentTracks[channelIdx].m_effvibratoa = vibtabbeg[instr.m_vibrato];
                        m_currentTracks[channelIdx].m_volumeSlideVal = 0x80;
                        m_currentTracks[channelIdx].m_newInstrIdx = Track.InvalidInstrument;
                        m_currentTracks[channelIdx].m_instrreachend = false;
                        m_currentTracks[channelIdx].m_shiftfrq = 0;
                        m_currentTracks[channelIdx].m_envelopeIdx = 0;
                        m_currentTracks[channelIdx].m_prevEnvelopeIdx = -1;
                        m_currentTracks[channelIdx].m_tableNoteIdx = 0;
                        m_currentTracks[channelIdx].m_tableNoteOrFreq = instr.m_noteOrFreqTable[0];
                        m_currentTracks[channelIdx].m_sampleIdx = 0;
                    }

                    return MRTState.rmt_p3;
                }

                //---------------------------------

                case MRTState.rmt_play:
                {
                    SetPokey();
                    if (--m_currentUpdateFreq != 0)
                        return MRTState.rmt_p3;

                    m_currentUpdateFreq = s_playbackSpeedTbl[m_rmtData.m_playbackSpeed];

                    if (--m_v_aspeed != 0)
                        return MRTState.rmt_p3; 

                    byte beat = ++m_v_abeat;

                    if (beat == m_rmtData.m_V_maxtracklen)
                        return MRTState.GetSongLine; 

                    return MRTState.GetTrackLine; 
                }

                //--- setup instruments
                case MRTState.rmt_p3:
                {
                    int[] frqTableOffset = new int[m_rmtData.m_channelsCount];
                    ModuleData.Instrument[] instr = new ModuleData.Instrument[m_rmtData.m_channelsCount];
                    ModuleData.Envelope[] envelope = new ModuleData.Envelope[m_rmtData.m_channelsCount];
                    int[] volume = new int[m_rmtData.m_channelsCount];
                    bool[] bProcessEnvelope = new bool[m_rmtData.m_channelsCount];

                    for (int chIdx = 0; chIdx < m_rmtData.m_channelsCount; chIdx++)
                    {
                        if (!m_currentTracks[chIdx].m_instrValid)
                        {
                            m_currentTracks[chIdx].m_envelope = new ModuleData.Envelope(0, 0, 0);
                            continue;
                        }

                        double envelopeIdx = m_currentTracks[chIdx].m_envelopeIdx;

                        instr[chIdx] = m_rmtData.m_instruments[m_currentTracks[chIdx].m_instrIdx];
                        envelope[chIdx] = instr[chIdx].m_envelope[(int)envelopeIdx];

                        bProcessEnvelope[chIdx] = (((int)m_currentTracks[chIdx].m_prevEnvelopeIdx) != ((int)envelopeIdx));

                        m_currentTracks[chIdx].m_prevEnvelopeIdx = envelopeIdx;
                        envelopeIdx += instr[chIdx].m_envelopeStep;

                        //--- check end of envelope table and loop?
                        if (envelopeIdx >= instr[chIdx].m_envelope.Count)
                        {
                            m_currentTracks[chIdx].m_instrreachend = true;
                            double nonInt = envelopeIdx - ((int)envelopeIdx);
                            envelopeIdx = instr[chIdx].m_envelopeLoopIdx + nonInt;
                            m_currentTracks[chIdx].m_prevEnvelopeIdx = -1;
                        }

                        m_currentTracks[chIdx].m_envelopeIdx = envelopeIdx;
                        m_currentTracks[chIdx].m_envelope = envelope[chIdx];

                        //---
                        byte envelopeVolume = (byte)(((chIdx >= 4) ? envelope[chIdx].m_volumeLR >> 4 : envelope[chIdx].m_volumeLR) & 0xf);
                        byte channelVolume = m_currentTracks[chIdx].m_volume;

                        volume[chIdx] = volumetab[envelopeVolume | channelVolume];

                        //--- Set AUDC distortion 
                        frqTableOffset[chIdx] = tabbeganddistor[envelope[chIdx].m_distortion];

                        m_audC[chIdx] = (byte)(volume[chIdx] | tabbeganddistor[envelope[chIdx].m_distortion + 1]);

                        //--- Custom extensions
                        if (envelope[chIdx].m_bUseAudCTL)
                            m_currentTracks[chIdx].m_audctl = envelope[chIdx].m_audCTL;
                    }

                    //---
                    if (m_rmtData.m_channelsCount > 4)
                    {
                        byte tmpAudCtl2 = 0;
                        for (int c = 4; c < 8 /*TRACKS*/; c++) // get Pokey1
                        {
                            tmpAudCtl2 |= m_currentTracks[c].m_audctl;
                        }
                        m_audCtl2 = tmpAudCtl2;
                    }

                    byte tmpAudCtl = 0;
                    for (int c = 0; c < 4 /*TRACKS*/; c++) // only get the 4 1st values for Pokey0
                    {
                        tmpAudCtl |= m_currentTracks[c].m_audctl;
                    }
                    m_audCtl = tmpAudCtl;

                    //---
                    for (byte chIdx = 0; chIdx < m_rmtData.m_channelsCount; chIdx++)
                    {
                        if (!m_currentTracks[chIdx].m_instrValid)
                            continue;

                        //InstrumentsEffects
                        byte effDelay = m_currentTracks[chIdx].m_effdelay;

                        if (effDelay == 1)
                        {
                            byte shift = (byte)(m_currentTracks[chIdx].m_shiftfrq + instr[chIdx].m_fShift);

                            byte vib = m_currentTracks[chIdx].m_effvibratoa;
                            shift += vibtabbeg[(int)VibTab.vib0 + vib];
                            m_currentTracks[chIdx].m_shiftfrq = shift;

                            m_currentTracks[chIdx].m_effvibratoa = vibtabnext[vib];
                        }
                        else if (effDelay > 1)
                        {
                            m_currentTracks[chIdx].m_effdelay--;
                        }

                        int endOfTableOffset = instr[chIdx].m_noteOrFreqTable.Count - 1;
                        if (endOfTableOffset >= 1)
                        {
                            int speed = m_currentTracks[chIdx].m_tableSpeedAcc;
                            if (speed < 0)
                            {
                                int currentIdx = m_currentTracks[chIdx].m_tableNoteIdx;
                                currentIdx = currentIdx == endOfTableOffset ? instr[chIdx].m_noteTableLoopIdx : currentIdx + 1;
                                m_currentTracks[chIdx].m_tableNoteIdx = (byte)currentIdx;

                                UInt16 tableNoteOrFreq = instr[chIdx].m_noteOrFreqTable[currentIdx];

                                if (instr[chIdx].m_tableMode == ModuleData.Instrument.TableMode.Accu)
                                {
                                    tableNoteOrFreq += m_currentTracks[chIdx].m_tableNoteOrFreq;
                                }
                                m_currentTracks[chIdx].m_tableNoteOrFreq = tableNoteOrFreq;
                                speed = instr[chIdx].m_tableSpeed; // should be +1 to compensate for -1 below ?? 
                            }

                            m_currentTracks[chIdx].m_tableSpeedAcc = speed - 1;
                        }

                        if (m_currentTracks[chIdx].m_instrreachend)
                        {
                            byte currentVol = m_currentTracks[chIdx].m_volume;
                            if (currentVol > instr[chIdx].m_volMin)
                            {
                                int reg16 = m_currentTracks[chIdx].m_volumeSlideVal + instr[chIdx].m_volSlide;
                                m_currentTracks[chIdx].m_volumeSlideVal = (byte)(reg16 & 0xff);
                                if (reg16 >= 256)
                                {
                                    m_currentTracks[chIdx].m_volume = (byte)(currentVol - 16);
                                }
                            }
                        }
                        //----
                        if (bProcessEnvelope[chIdx])       // if we haven't moved to the next envelope step don't update any of the effects or commands
                        {
                            byte note = 0;

                            m_currentTracks[chIdx].m_frqAddCmd = 0;
                            m_currentTracks[chIdx].m_frqSetCmd = -1;

                            EffectRetCmd postEnvelopeCommand = ProcessEnvelopeCommand(envelope[chIdx].m_command, chIdx, frqTableOffset[chIdx], ref note, instr[chIdx], envelope[chIdx]);

                            UInt16 freq = 0;

                            switch (postEnvelopeCommand)
                            {
                                case EffectRetCmd.note:
                                {
                                    if (instr[chIdx].m_tableType == ModuleData.Instrument.TableType.Note)
                                        note += (byte)(m_currentTracks[chIdx].m_tableNoteOrFreq >> 8);

                                    if (note >= 61)
                                    {
                                        m_audC[chIdx] = 0;
                                        note = 63;
                                    }

                                    if ( (((chIdx == 1) || (chIdx == 3)) 
                                        && !CheckSetHighPassFilterFlags(0) && !CheckSetHighPassFilterFlags(1) 
                                        && Check16BitModeFlags(chIdx, m_audCtl))
                                        ||
                                        (((chIdx == 1+4) || (chIdx == 3+4)) 
                                        && !CheckSetHighPassFilterFlags(0+4) && !CheckSetHighPassFilterFlags(1+4) 
                                        && Check16BitModeFlags(chIdx, m_audCtl)))
                                    {
                                        m_currentTracks[chIdx].m_frqSetCmd = ConvertNoteTo16bitFreq(chIdx, note);
                                        freq = 0; // ignore 8bit freq
                                    }
                                    else
                                    {
                                        freq = (UInt16)((frqtab[frqTableOffset[chIdx] + note] + m_currentTracks[chIdx].m_shiftfrq) << 8);
                                        freq += m_currentTracks[chIdx].m_frqAddCmd;
                                    }

                                    if (instr[chIdx].m_tableType == ModuleData.Instrument.TableType.Frequency)
                                    {
                                        m_currentTracks[chIdx].m_frqSetCmd += m_currentTracks[chIdx].m_tableNoteOrFreq;
                                        freq += m_currentTracks[chIdx].m_tableNoteOrFreq;
                                    }
                                    else
                                        m_currentTracks[chIdx].m_outNote = note;
                                    break;
                                }
                                case EffectRetCmd.freq:
                                {
                                    freq = (UInt16)(note << 8);
                                    break;
                                }
                                default:
                                    Debug.Assert(false);
                                    break;
                            }
                            m_audF[chIdx] = (byte)(freq>>8);

                            //---
                            if (m_currentTracks[chIdx].m_portamento.speeda != 0)
                                UpdatePortamento(chIdx);

                            //---
                            if (envelope[chIdx].m_bPortamento)
                                m_audF[chIdx] = (byte)((m_currentTracks[chIdx].m_portamento.frqa >> 8) + m_currentTracks[chIdx].m_shiftfrq);

                        }

                        //--- process sample
                        if (instr[chIdx].m_sample != null)
                        {
                            ProcessSample(chIdx, instr[chIdx]);
                        }


                    }   // for channel

                    gtlState = MRTState.rmt_p4;

                    switch(m_rmtData.m_rmtVersion)
                    {
                        case ModuleData.RMTVersion._128Unpatched:
                            RMT_P4_128Unpatched(ref gtlState);
                            break;
                        case ModuleData.RMTVersion._128CustomTables:
                            RMT_P4_128CustomTables(ref gtlState);
                            break;
                        case ModuleData.RMTVersion._128Patch8:
                            RMT_P4_128Patch8(ref gtlState);
                            break;
                        case ModuleData.RMTVersion._127Patch6:
                            RMT_P4_127patch6(ref gtlState);
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                }


                default:
                    Debug.Assert(false);
                    break;


            }
            return gtlState;
        }

        //-------------------------------------------------------------------------------------------------------------------

        EffectRetCmd ProcessEnvelopeCommand(ModuleData.EffectCmd command, byte chIdx, int frqTableOffset, ref byte noteOrFreq, ModuleData.Instrument instr, ModuleData.Envelope envelope)
        {
            noteOrFreq = m_currentTracks[chIdx].m_note;

            switch (command)
            {
                //  0	Play the base note shifted by $XY semitones. If resulting note by reason of note shifting is out of C-1 to C-6 range (hex values $00 to $3D), then output volume will be zero.
                case ModuleData.EffectCmd.cmd0:
                    {
                        noteOrFreq += (byte)(envelope.m_commandArg >> 8);
                        return EffectRetCmd.note;
                    }

                //  1	Play the frequency $XY directly.
                case ModuleData.EffectCmd.cmd1:
                    {
                        noteOrFreq = (byte)(envelope.m_commandArg >> 8);
                        m_currentTracks[chIdx].m_frqSetCmd = envelope.m_commandArg;
                        return EffectRetCmd.freq;
                    }

                //  2	Play the base note shifted by frequency $XY.
                case ModuleData.EffectCmd.cmd2:
                    {
                        m_currentTracks[chIdx].m_frqAddCmd = envelope.m_commandArg;
                        return EffectRetCmd.note;
                    }

                //  3	Add $XY semitones to base note. Play base note (new value). If resulting note by reason of note shifting is out of C-1 to C-6 range (hex values $00 to $3D), then output volume will be zero.
                case ModuleData.EffectCmd.cmd3:
                    {
                        noteOrFreq += (byte)(envelope.m_commandArg >> 8);
                        m_currentTracks[chIdx].m_note = noteOrFreq;
                        return EffectRetCmd.note;
                    }

                //  4	Add frequency $XY to FSHIFT register. Play base note.
                case ModuleData.EffectCmd.cmd4:
                    {
                        m_currentTracks[chIdx].m_shiftfrq += (byte)(envelope.m_commandArg >> 8);
                        return EffectRetCmd.note;
                    }

                //  5	Set up portamento speed $X, step $Y (each $X vbi will be "volatile portamento frequency" shifted up or down by $Y value in a direction of actual frequency). 
                //          If $XY=$00, then set current frequency directly to volatile portamento frequency.
                case ModuleData.EffectCmd.cmd5:
                    {
                        SetPortamento(chIdx, frqTableOffset, instr, envelope, noteOrFreq);

                        return EffectRetCmd.note;
                    }
                //  6	Add $XY value to FILTER_SHFRQ. (Whenever the new note in track is getting started, FILTER_SHFRQ is initialized to $01, so that default join filter generator frequency is higher by 1.)
                case ModuleData.EffectCmd.cmd6:
                    {
                        m_currentTracks[chIdx].m_filterShiftFreq += (byte)(envelope.m_commandArg >> 8);
                        return EffectRetCmd.note;
                    }
                //  7	Set the base note to $XY value directly. Play base note (new value). If $XY=$80, then use the current volume for VOLUME ONLY forced output.
                case ModuleData.EffectCmd.cmd7:
                    {
                        if (envelope.m_commandArg != (0x80<<8))
                            m_currentTracks[chIdx].m_note = noteOrFreq = (byte)(envelope.m_commandArg >> 8);
                        else
                            m_audC[chIdx] |= 0xf0;

                        return EffectRetCmd.note;
                    }
            }
            Debug.Assert(false);
            return EffectRetCmd.Invalid;
        }
        //-------------------------------------------------------------------------------------------------------------------
        void SetPortamento(byte chIdx, int frqTableOffset, ModuleData.Instrument instr, ModuleData.Envelope envelope, byte noteOrFreq)
        {
            int newNoteFrq = 0;

            byte AudCtl = chIdx < 4 ? m_audCtl : m_audCtl2;

            if (instr.m_tableType == ModuleData.Instrument.TableType.Note)
            {
                byte newNote = (byte)(noteOrFreq + (m_currentTracks[chIdx].m_tableNoteOrFreq >> 8));
                newNote = (byte)(newNote >= 61 ? 63 : newNote);
                if (Check16BitModeFlags(chIdx, AudCtl))
                {
                    int frqTable16Offset = tabbeganddistor[m_currentTracks[chIdx].m_envelope.m_distortion] << 1;
                    newNoteFrq = frqtabbasshi[frqTable16Offset + frqtabbassloOffset + newNote] + (frqtabbasshi[frqTable16Offset + newNote] << 8);
                }
                else
                    newNoteFrq = frqtab[frqTableOffset + newNote] << 8;
            }
            else
            {
                //--- frequency mode
                if (Check16BitModeFlags(chIdx, AudCtl))
                {
                    int frqTable16Offset = tabbeganddistor[m_currentTracks[chIdx].m_envelope.m_distortion] << 1;
                    int _16bitFreq = frqtabbasshi[frqTable16Offset + frqtabbassloOffset + noteOrFreq] + (frqtabbasshi[frqTable16Offset + noteOrFreq] << 8);
                    newNoteFrq = _16bitFreq + m_currentTracks[chIdx].m_tableNoteOrFreq;
                }
                else
                    newNoteFrq = (frqtab[frqTableOffset + noteOrFreq] << 8) + m_currentTracks[chIdx].m_tableNoteOrFreq;
            }

            m_currentTracks[chIdx].m_portamento.frqc = (UInt16)newNoteFrq;
            if ((envelope.m_portaSpeed == 0) && (envelope.m_portaDepth == 0))
            {
                //--- XY = 0, set target frequency
                m_currentTracks[chIdx].m_portamento.frqa = (UInt16)newNoteFrq;
            }

            m_currentTracks[chIdx].m_portamento.speed = envelope.m_portaSpeed;
            m_currentTracks[chIdx].m_portamento.speeda = envelope.m_portaSpeed;
            m_currentTracks[chIdx].m_portamento.depth = envelope.m_portaDepth;
        }

        void UpdatePortamento(int chIdx)
        {
            m_currentTracks[chIdx].m_portamento.speeda--;
            if (m_currentTracks[chIdx].m_portamento.speeda == 0)
            {
                //--- bring portaFrqa to portaFrqc

                m_currentTracks[chIdx].m_portamento.speeda = m_currentTracks[chIdx].m_portamento.speed;

                int portaFrqc = m_currentTracks[chIdx].m_portamento.frqc;
                int portaDepth = m_currentTracks[chIdx].m_portamento.depth;
                int portaFrqa = m_currentTracks[chIdx].m_portamento.frqa;

                if (portaFrqa > portaFrqc)
                {
                    portaFrqa -= portaDepth;
                    portaFrqa = portaFrqa < portaFrqc ? portaFrqc : portaFrqa;
                }
                else if (portaFrqa < portaFrqc)
                {
                    portaFrqa += portaDepth;
                    portaFrqa = portaFrqa > portaFrqc ? portaFrqc : portaFrqa;
                }
                m_currentTracks[chIdx].m_portamento.frqa = (UInt16)portaFrqa;
            }
        }

        //-------------------------------------------------------------------------------------------------------------------
        void ProcessSample(byte chIdx, ModuleData.Instrument instr)
        {
            double freqMul = 255 - m_audF[chIdx];
            freqMul /= 256;
            //                            freqMul *= 2;
            freqMul += 0.5;

            double sampleIdx = m_currentTracks[chIdx].m_sampleIdx;
            byte sampleVal = instr.m_sample.m_data[(int)sampleIdx];

            sampleIdx += instr.m_sample.m_step > 0 ? instr.m_sample.m_step * freqMul : 1;
            if ((int)sampleIdx >= instr.m_sample.m_data.Count)
            {
                sampleIdx -= instr.m_sample.m_data.Count;
                sampleIdx += instr.m_sample.m_loopIdx;
                sampleIdx = Math.Min(sampleIdx, instr.m_sample.m_loopIdx);
            }
            m_currentTracks[chIdx].m_sampleIdx = sampleIdx;

            byte channelVolume = m_currentTracks[chIdx].m_volume;
            byte volumeSample = volumetab[sampleVal | channelVolume];

            m_audC[chIdx] &= 0xf0;
            m_audC[chIdx] |= (byte)(volumeSample | AUDC_VOLUMEONLY);

        }

        //-------------------------------------------------------------------------------------------------------------------

        void SetPokey()
        {
            if (m_rmtData.m_channelsCount > 4)
            {
                for (int c = 4; c < 8; c++)
                {
                    m_pokeyBufR.Add(m_audF[c]);
                    m_pokeyBufR.Add(m_audC[c]);
                }
                m_pokeyBufR.Add(m_audCtl2);
            }
            {
                //string pokeyDataStr = "C#:  ";
                for (int c = 0; c < 4; c++)
                {
                    m_pokeyBufL.Add(m_audF[c]);
                    m_pokeyBufL.Add(m_audC[c]);
                    //pokeyDataStr += "$"+m_pokeyBuf[m_pokeyBuf.Count-1].ToString("X02")+", " ;
                    //pokeyDataStr += "$" + m_pokeyBuf[m_pokeyBuf.Count - 1].ToString("X02") + ", ";
                }
                m_pokeyBufL.Add(m_audCtl);
                //pokeyDataStr += "$" + m_pokeyBuf[m_pokeyBuf.Count - 1].ToString("X02") + ", ";

                //           Console.WriteLine(pokeyDataStr);
            }
        }
    }

}