// RMT p4 for 1.27 patch 6

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;


namespace RMT
{
    partial class Player
    {

        void RMT_P4_127patch6(ref MRTState gtlState)
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
                                if ((m_currentTracks[1].m_envelope.m_distortion == 6) && ((m_audC[1] & AUDC_VOLUMEMASK) != 0))
                                {
                                    byte outNote = m_currentTracks[1].m_outNote;
                                    m_audF[0] = frqtabbasshi[outNote + frqtabbassloOffset];
                                    m_audF[1] = frqtabbasshi[outNote];
//                                     if ((AudC[0] & 0x10) == 0)
//                                         AudC[0] = 0;

                                    tmpAudCtl |= AUDCTL_CH1_FASTCLOCK | AUDCTL_CH1CH2_LINK; // audctl
                                }

                                if ((m_currentTracks[3].m_envelope.m_distortion == 6) && ((m_audC[3] & AUDC_VOLUMEMASK) != 0))
                                {
                                    byte outNote = m_currentTracks[3].m_outNote;
                                    m_audF[2] = frqtabbasshi[outNote + frqtabbassloOffset];
                                    m_audF[3] = frqtabbasshi[outNote];
//                                     if ((AudC[2] & 0x10) == 0)
//                                         AudC[2] = 0;

                                    tmpAudCtl |= AUDCTL_CH3_FASTCLOCK | AUDCTL_CH3CH4_LINK; // audctl
                                }
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
// 	nop ; MUX "This was lda #0"
// 	nop
// 	nop ; MUX "This was sta trackn_audc+0+4"
// 	nop
// 	nop
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
// 	nop ; MUX "This was lda #0"
// 	nop
// 	nop ; MUX "This was sta trackn_audc+2+4"
// 	nop
// 	nop
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

//                            m_regA = V_ainstrspeed;
                            gtlState = MRTState.Exit_rts; break;
                        }
                }
            } while (gtlState != MRTState.Exit_rts);
        }
    }

}