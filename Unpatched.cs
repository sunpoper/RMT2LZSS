// RMT p4 for 1.28 unpatched

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;


namespace RMT
{
    partial class Player
    {
        bool CheckSetHighPassFilterFlags(int CH)
        {
            return ((m_currentTracks[CH].m_envelope.m_bFilter) && ((m_audC[CH] & AUDC_VOLUMEMASK) != 0));
        }


        void CheckSetHighPassFilter(ref byte tmpAudCtl, int CH)
        {
            if (CheckSetHighPassFilterFlags(CH))
            {
                m_audF[CH+2] = (byte)(m_audF[CH] + m_currentTracks[CH].m_filterShiftFreq);

                if ((m_audC[CH+2] & AUDC_VOLUMEONLY) == 0)
                    m_audC[CH+2] = 0;

                tmpAudCtl |= CH == 0 ? AUDCTL_HP1 : AUDCTL_HP3; 
            }
        }

        void CheckDist6Set16BitMode(ref byte tmpAudCtl, int CH)
        {
            if ((m_currentTracks[CH].m_envelope.m_distortion == 6) && ((m_audC[CH] & AUDC_VOLUMEMASK) != 0))
            {
                byte outNote = m_currentTracks[CH].m_outNote;

                m_audF[CH-1] = frqtabbasshi[outNote + frqtabbassloOffset];
                m_audF[CH] = frqtabbasshi[outNote];
                if ((m_audC[CH-1] & AUDC_VOLUMEONLY) == 0)
                    m_audC[CH-1] = 0;

                tmpAudCtl |= (byte)(CH == 1 ? AUDCTL_CH1_FASTCLOCK | AUDCTL_CH1CH2_LINK : AUDCTL_CH3_FASTCLOCK | AUDCTL_CH3CH4_LINK); 
            }
        }

        bool Check16BitModeFlags(byte CH, byte tmpAudCtl)
        {
            byte AUDCTLMask = 0;
            switch(CH)
            {
                case 1:
                case 5:
                    AUDCTLMask = AUDCTL_CH1CH2_LINK | AUDCTL_CH1_FASTCLOCK;
                    break;
                case 3:
                case 7:
                    AUDCTLMask = AUDCTL_CH3CH4_LINK | AUDCTL_CH3_FASTCLOCK;
                    break;
            }

            return (m_currentTracks[CH].m_envelope.m_bUseAudCTL) && (AUDCTLMask!=0) && ((tmpAudCtl & AUDCTLMask) == AUDCTLMask);
        }

        UInt16 ConvertNoteTo16bitFreq(byte CH, byte outNote)
        {
            int frqTable16Offset = tabbeganddistor[m_currentTracks[CH].m_envelope.m_distortion] << 1;

            UInt16 freq = (UInt16)(frqtabbasshi[frqTable16Offset + outNote + frqtabbassloOffset] + (frqtabbasshi[frqTable16Offset + outNote] << 8));
            freq += m_currentTracks[CH].m_frqAddCmd;

            return freq;
        }

        void Check16BitMode_Ext(ref byte tmpAudCtl, byte CH )
        {
            if (Check16BitModeFlags(CH, tmpAudCtl) /*&& ((AudC[CH] & AUDC_VOLUMEMASK) != 0)*/)
            {
                if (m_currentTracks[CH].m_envelope.m_bPortamento)
                {
                    UInt16 freq16 = (UInt16)(m_currentTracks[CH].m_portamento.frqa + (m_currentTracks[CH].m_shiftfrq << 8));

                    m_audF[CH-1] = (byte)(freq16&0xff);
                    m_audF[CH] = (byte)(freq16>>8);
                }
                else
                {
                    if (m_currentTracks[CH].m_frqSetCmd == -1)
                    {
                        UInt16 freq = ConvertNoteTo16bitFreq(CH, m_currentTracks[CH].m_outNote);

                        m_audF[CH - 1] = (byte)(freq & 0xff);
                        m_audF[CH] = (byte)(freq >> 8);
                    }
                    else
                    {
                        UInt16 freq16 = (UInt16)m_currentTracks[CH].m_frqSetCmd;
                        m_audF[CH - 1] = (byte)(freq16 & 0xff);
                        m_audF[CH] = (byte)(freq16 >> 8);
                    }
                }

                if ((m_audC[CH - 1] & AUDC_VOLUMEONLY) == 0)
                    m_audC[CH - 1] = 0;
            }
        }

                
        void RMT_P4_128Unpatched(ref MRTState gtlState)
        {
            do
            {

                switch (gtlState)
                {
                    case MRTState.rmt_p4:
                    {
                        byte tmpAudCtl = m_audCtl;

                        CheckSetHighPassFilter(ref tmpAudCtl, 0);
                        CheckSetHighPassFilter(ref tmpAudCtl, 1);

                        if (tmpAudCtl == m_audCtl)
                        {
                            CheckDist6Set16BitMode(ref tmpAudCtl, 1);
                            CheckDist6Set16BitMode(ref tmpAudCtl, 3);
                        }
                        m_audCtl = tmpAudCtl;
/*
//	IFT TRACKS>4
//	IFT FEAT_AUDCTLMANUALSET
//	lda trackn_audctl+4
//	ora trackn_audctl+5
//	ora trackn_audctl+6
//	ora trackn_audctl+7
//	tax
//	ELS
//	ldx #0
//	EIF
//	stx v_audctl2
//	IFT FEAT_FILTER
//	IFT FEAT_FILTERG0R
//	lda trackn_command+0+4
//	bpl qs2
//	lda trackn_audc+0+4
//	and #$0f
//	beq qs2
//	lda trackn_audf+0+4
//	clc
//	adc trackn_filter+0+4
//	sta trackn_audf+2+4
//	IFT FEAT_COMMAND7VOLUMEONLY&&FEAT_VOLUMEONLYG2R
//	lda trackn_audc+2+4
//	and #$10
//	bne qs1a
//	EIF
//	lda #0
//	sta trackn_audc+2+4
//qs1a
//	txa
//	ora #4
//	tax
//	EIF
//qs2
//	IFT FEAT_FILTERG1R
//	lda trackn_command+1+4
//	bpl qs3
//	lda trackn_audc+1+4
//	and #$0f
//	beq qs3
//	lda trackn_audf+1+4
//	clc
//	adc trackn_filter+1+4
//	sta trackn_audf+3+4
//	IFT FEAT_COMMAND7VOLUMEONLY&&FEAT_VOLUMEONLYG3R
//	lda trackn_audc+3+4
//	and #$10
//	bne qs2a
//	EIF
//	lda #0
//	sta trackn_audc+3+4
//qs2a
//	txa
//	ora #2
//	tax
//	EIF
//qs3
//	IFT FEAT_FILTERG0R||FEAT_FILTERG1R
//	cpx v_audctl2
//	bne qs5
//	EIF
//	EIF
//	IFT FEAT_BASS16
//	IFT FEAT_BASS16G1R
//	lda trackn_command+1+4
//	and #$0e
//	cmp #6
//	bne qs4
//	lda trackn_audc+1+4
//	and #$0f
//	beq qs4
//	ldy trackn_outnote+1+4
//	lda frqtabbasslo,y
//	sta trackn_audf+0+4
//	lda frqtabbasshi,y
//	sta trackn_audf+1+4
//	IFT FEAT_COMMAND7VOLUMEONLY&&FEAT_VOLUMEONLYG0R
//	lda trackn_audc+0+4
//	and #$10
//	bne qs3a
//	EIF
//	lda #0
//	sta trackn_audc+0+4
//qs3a
//	txa
//	ora #$50
//	tax
//	EIF
//qs4
//	IFT FEAT_BASS16G3R
//	lda trackn_command+3+4
//	and #$0e
//	cmp #6
//	bne qs5
//	lda trackn_audc+3+4
//	and #$0f
//	beq qs5
//	ldy trackn_outnote+3+4
//	lda frqtabbasslo,y
//	sta trackn_audf+2+4
//	lda frqtabbasshi,y
//	sta trackn_audf+3+4
//	IFT FEAT_COMMAND7VOLUMEONLY&&FEAT_VOLUMEONLYG2R
//	lda trackn_audc+2+4
//	and #$10
//	bne qs4a
//	EIF
//	lda #0
//	sta trackn_audc+2+4
//qs4a
//	txa
//	ora #$28
//	tax
//	EIF
//	EIF
//qs5
//	stx v_audctl2
//	EIF
*/
//                            m_regA = V_ainstrspeed ;
                        gtlState = MRTState.Exit_rts; break;
                    }
                }
            } while (gtlState != MRTState.Exit_rts);
        }

        //-------------------------------------------------------------------------------------------------------------------
        void RMT_P4_128CustomTables(ref MRTState gtlState)
        {
            switch (gtlState)
            {
                case MRTState.rmt_p4:
                {
                     byte tmpAudCtl = m_audCtl;

                    CheckSetHighPassFilter(ref tmpAudCtl, 0);
                    CheckSetHighPassFilter(ref tmpAudCtl, 1);

                    if (tmpAudCtl == m_audCtl)
                    {
                        //--- RMT extension
                        Check16BitMode_Ext(ref tmpAudCtl, 1 );
                        Check16BitMode_Ext(ref tmpAudCtl, 3 );
                    }
                    m_audCtl = tmpAudCtl;

                    gtlState = MRTState.Exit_rts; break;
                }
            }
        }
        //-------------------------------------------------------------------------------------------------------------------
    }


}