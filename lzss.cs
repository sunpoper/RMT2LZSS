using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text.RegularExpressions;

using lzss2RMT;

namespace LZSS
{
    public class Encoder
    {
        // dmsc's LZSS encoder converted to C# by rensoupp

        int format_version = 0;  // LZSS format version - 0 means latest version

        ///////////////////////////////////////////////////////
        // Bit encoding functions
        struct bf
        {
            public int len;
            public byte[] buf;
            public int bnum;
            public int bpos;
            public int hpos;
            public int total;
            public MemoryStream outW;

            public bf(int dummy)
            {
                buf = new byte[65536];
                len = 0;
                bnum = 0;
                bpos = 0;
                hpos = 0;
                total = 0;
                outW = null;
            }
        };

        void init(ref bf x)
        {
            x.total = 0;
            x.len = 0;
            x.bnum = 0;
            x.bpos = -1;
            x.hpos = -1;
        }

        void bflush(ref bf x)
        {
            if( x.len != 0 )
                x.outW.Write(x.buf, 0, x.len);
            x.total += x.len;
            x.len = 0;
            x.bnum = 0;
            x.bpos = -1;
            x.hpos = -1;
        }


        void add_bit(ref bf x, int bit)
        {
            if( x.bpos < 0 )
            {
                // Adds a new byte holding bits
                x.bpos = x.len;
                x.bnum = 0;
                x.len++;
                x.buf[x.bpos] = 0;
            }
            if( bit!=0 )
                x.buf[x.bpos] |= (byte)(1 << x.bnum);
            x.bnum++;
            if( x.bnum == 8 )
            {
                x.bpos = -1;
                x.bnum = 0;
            }
        }

        void add_byte(ref bf x, byte byt)
        {
            x.buf[x.len] = byt;
            x.len++;
        }

        void add_hbyte(ref bf x, int hbyte)
        {
            if( x.hpos < 0 )
            {
                // Adds a new byte holding half-bytes
                x.hpos = x.len;
                x.len++;
                x.buf[x.hpos] = (byte)(hbyte & 0x0F);
            }
            else
            {
                // Fixes last h-byte
                x.buf[x.hpos] |= (byte)(hbyte << 4);
                x.hpos = -1;
            }
        }

        ///////////////////////////////////////////////////////
        // LZSS compression functions

        int get_mlen(byte[] aBuf, int off0, int off1, int max)
        {
            for(int i=0; i<max; i++)
                if( aBuf[i+off0] != aBuf[i+off1] )
                    return i;
            return max;
        }

        int bits_moff = 4;       // Number of bits used for OFFSET
        int bits_mlen = 4;       // Number of bits used for MATCH
        int min_mlen = 2;        // Minimum match length
        int fmt_literal_first = 0; // Always include first literal in the output
        int fmt_pos_start_zero = 0; // Match positions start at 0, else start at max

        int bits_literal =(1+8);                          // Number of bits for encoding a literal
        int bits_match ;                    // Bits for encoding a match

        int max_mlen ;                      // Maximum match length
        int max_off ;                       // Maximum offset

        // Statistics
        int[] stat_len;
        int[] stat_off;


        // Struct for LZ optimal parsing
        struct lzop
        {
            public byte[] data;// The data to compress
            public int size;           // Data size
            public int[] bits;          // Number of bits needed to code from position
            public int[] mlen;          // Best match length at position (0 == no match);
            public int[] mpos;          // Best match offset at position
        };

        void lzop_init(ref lzop lz, byte[] data, int size)
        {
            lz.data = data;
            lz.size = size;
            lz.bits = new int[size];
            lz.mlen = new int[size];
            lz.mpos = new int[size];
        }

        void lzop_free(ref lzop lz)
        {
            lz.bits = null;
            lz.mlen = null;
            lz.mpos = null;
        }

        // Returns maximal match length (and match position) at pos.
        int match(byte[] data, int pos, int size, ref int mpos)
        {
            int mxlen = -Math.Max(-max_mlen, pos - size);
            int mlen = 0;
            for(int i=Math.Max(pos-max_off,0); i<pos; i++)
            {
                int ml = get_mlen(data, pos, i, mxlen);
                if( ml > mlen )
                {
                    mlen = ml;
                    mpos = pos - i;
                }
            }
            return mlen;
        }

        void lzop_backfill(ref lzop lz, int last_literal)
        {
            // If no bytes, nothing to do
            if (lz.size==0)
                return;

            if (last_literal!=0)
            {
                // Forced last literal - process one byte less
                lz.mlen[lz.size - 1] = 0;
                lz.size--;
                if (lz.size==0)
                    return;
            }

            lz.bits[lz.size-1] = bits_literal;

            // Go backwards in file storing best parsing
            for(int pos = lz.size - 2; pos>=0; pos--)
            {
                // Get best match at this position
                int mp = 0;
                int ml = match(lz.data , pos, lz.size, ref mp);

                // Init "no-match" case
                int best = lz.bits[pos+1] + bits_literal;

                // Check all possible match lengths, store best
                lz.bits[pos] = best;
                lz.mpos[pos] = mp;
                for(int l=ml; l>=min_mlen; l--)
                {
                    int b;
                    if( pos+l < lz.size )
                        b = lz.bits[pos+l] + bits_match;
                    else
                        b = 0;
                    if( b < best )
                    {
                        best = b;
                        lz.bits[pos] = best;
                        lz.mlen[pos] = l;
                        lz.mpos[pos] = mp;
                    }
                }
            }

            // Fixup size again
            if (last_literal!=0)
                lz.size++;
        }

        // Returns 1 if the coded stream would end in a match
        int lzop_last_is_match(ref lzop lz)
        {
            int last = 0;
            for(int pos = 0; pos < lz.size; )
            {
                int mlen = lz.mlen[pos];
                if( mlen < min_mlen )
                {
                    // Skip over one literal byte
                    last = 0;
                    pos ++;
                }
                else
                {
                    // Skip over one match
                    pos = pos + mlen;
                    last = 1;
                }
            }
            return last;
        }

        int lzop_encode(ref bf b, ref lzop lz, int pos, int lpos)
        {
            if( pos <= lpos )
                return lpos;

            int mlen = lz.mlen[pos];
            int mpos = lz.mpos[pos];

            // Encode best from filled table
            if( mlen < min_mlen )
            {
                // No match, just encode the byte
        //        fprintf(stderr,"L: %02x\n", lz->data[pos]);
                add_bit(ref b,1);
                add_byte(ref b, lz.data[pos]);
                stat_len[0] ++;
                return pos;
            }
            else
            {
                int code_pos = (pos - mpos - (fmt_pos_start_zero!=0 ? 1 : 2)) & (max_off - 1);

                int code_len = mlen - min_mlen;
        //        fprintf(stderr,"M: %02x : %02x  [%04x]\n", code_pos, code_len,
        //                       (code_pos << bits_mlen) + code_len);
                add_bit(ref b,0);
                if( bits_mlen + bits_moff <= 8 )
                    add_byte(ref b,(byte)((code_pos<<bits_mlen) + code_len));
                else if( bits_mlen + bits_moff <= 12 )
                {
                    add_byte(ref b,(byte)((code_pos<<(8-bits_moff)) + (code_len & ((1<<(8-bits_moff))-1))));
                    add_hbyte(ref b, code_len>>(8-bits_moff));
                }
                else
                {
                    int mb = m_moddedLzss16 ?
                        ((code_pos + code_len + 1) << bits_moff) + code_pos : // allow removal of adc instruction in player
                        ((code_len + 1) << bits_moff) + code_pos;

                    add_byte(ref b, (byte)(mb & 0xFF));
                    add_byte(ref b, (byte)(mb >> 8));
                }

                stat_len[mlen] ++;
                stat_off[mpos] ++;
                return pos + mlen - 1;
            }
        }

        string prog_name="";
        void cmd_error(string msg)
        {
            Debug.Assert(false,prog_name+" error, "+msg + "Try '-h' for help.\n");
        }

        //-------------------------------------------------------------------------------------------------------------------
        public int ReadBinaryFile(string name, out byte[] buffer)
        {
            using (FileStream fs = new FileStream(name, FileMode.Open, FileAccess.Read))
            {
                BinaryReader binaryReader = new BinaryReader(fs);
                int fileSize = (int)fs.Length;
                buffer = binaryReader.ReadBytes(fileSize);
                binaryReader.Close();

                return fileSize;
            }
        }

///////////////////////////////////////////////////////
        const int MaxPokeyChannel = 9;

        public enum LZSSType
        {
            LZS8,
            LZS12,
            LZS16,
//            LZS16U,
        }
        bool m_moddedLzss16 = false;

        public int Encode(string path, LZSSType lzsType = LZSSType.LZS16 )
        {           
            char[] header_line = new char[128];


            byte[] inputBuffer=null;
            int inputSize = ReadBinaryFile(path+".sap", out inputBuffer);


            // Skip SAP header
            int headerSize = 0;

            while (((inputBuffer[headerSize] != 0xd) || (inputBuffer[headerSize + 1] != 0xa) || (inputBuffer[headerSize+2] != 0xd) || (inputBuffer[headerSize + 3] != 0xa)) && (headerSize < inputSize - 4))
            {
                headerSize++;
            }
            headerSize += 4;

            Debug.Assert(headerSize<inputSize);

            byte[] noHeaderBuf = new byte[inputSize - headerSize];
            Array.Copy(inputBuffer, headerSize, noHeaderBuf, 0, noHeaderBuf.Length);

            return Encode_(0, noHeaderBuf.Length, noHeaderBuf, path, lzsType, true);
        }

        //-------------------------------------------------------------------------------------------------------------------

        public int Encode(byte[] srcBuf, string path, out string ext, LZSSType lzsType = LZSSType.LZS16, bool tweakAUDC = true)
        {
            int err = Encode_(0, srcBuf.Length, srcBuf, path, lzsType, tweakAUDC);
            ext = m_ext;
            return err;
        }


        //-------------------------------------------------------------------------------------------------------------------
        string m_ext ;

        int Encode_(int headerSize, int inputSize, byte[] inputBuffer, string path, LZSSType lzsType, bool tweakAUDC)
        {
            bool show_stats = true;

            byte[][] channelData = new byte[MaxPokeyChannel][];
            int[] lpos = new int[MaxPokeyChannel];
            int force_last_literal = 1;

            bf b = new bf(0);

            m_ext = "";

            bits_moff = 4;       // Number of bits used for OFFSET
            bits_mlen = 4;       // Number of bits used for MATCH
            min_mlen = 2;        // Minimum match length

            bits_literal = (1 + 8);                          // Number of bits for encoding a literal

            int bits_mtotal = bits_moff + bits_mlen;
            int bits_set = 0;

            //    prog_name = argv[0];
            //    int opt = '6';
            //    while( -1 != (opt = getopt(argc, argv, "hqvo:l:m:b:826ex")) )
            {
                switch (lzsType)
                {
                    case LZSSType.LZS12:
                        bits_moff = 7;
                        bits_mlen = 5;
                        //                        bits_mtotal = 12;
                        bits_set |= 8;
                        m_ext = ".lz12";
                        break;
                    case LZSSType.LZS8:
                        bits_moff = 4;
                        bits_mlen = 4;
                        //                        bits_mtotal = 8;
                        bits_set |= 8;
                        m_ext = ".lz8";
                        break;
                    case LZSSType.LZS16:
//                    case LZSSType.LZS16U:
                        bits_moff = 8;
                        bits_mlen = 8;
                        //                        bits_mtotal = 16;
                        min_mlen = 1;
                        bits_set |= 8;
                        m_ext = ".lz16";
//                      ext = lzsType == LZSSType.LZS16U ? ".lzs16u" : ".lzs16";
//                        m_moddedLzss16 = lzsType == LZSSType.LZS16U;
                        break;
                    //            case 'o':
                    //                bits_moff = atoi(optarg);
                    //                bits_set |= 1;
                    //                break;
                    //            case 'l':
                    //                bits_mlen = atoi(optarg);
                    //                bits_set |= 2;
                    //                break;
                    //            case 'b':
                    //                bits_mtotal = atoi(optarg);
                    //                bits_set |= 4;
                    //                break;
                    //            case 'm':
                    //                min_mlen = atoi(optarg);
                    //                break;
                    //            case 'v':
                    //                show_stats = 2;
                    //                break;
                    //            case 'q':
                    //                show_stats = 0;
                    //                break;
                    //            case 'x':
                    //                format_version = 1;
                    //                break;
                    //            case 'h':
                    default:
                        DebugStatus.Print(
                               "LZSS SAP Type-R compressor - by dmsc.\n"
                               + "\n"
                               + "Usage: [options] <input_file> <output_file>\n"
                               + "\n"
                               + "If output_file is omitted, write to standard output, and if\n"
                               + "input_file is also omitted, read from standard input.\n"
                               + "\n"
                               + "Options:\n"
                               + "  -8       Sets default 8 bit match size.\n"
                               + "  -2       Sets default 12 bit match size.\n"
                               + "  -6       Sets default 16 bit match size.\n"
                               + "  -o BITS  Sets match offset bits (default =" + bits_moff + ").\n"
                               + "  -l BITS  Sets match length bits (default = " + bits_mlen + ").\n"
                               + "  -b BITS  Sets match total bits (=offset+length) (default = " + bits_mtotal + ").\n"
                               + "  -m NUM   Sets minimum match length (default = " + min_mlen + ").\n"
                               + "  -v       Shows match length/offset statistics.\n"
                               + "  -q       Don't show per stream compression.\n"
                               + "  -h       Shows this help.\n"
                               );
                        return 1;
                }
            }

            // Set format flags:
            switch (format_version)
            {
                case 1:
                    fmt_literal_first = 0;
                    fmt_pos_start_zero = 1;
                    break;
                default:
                    fmt_literal_first = 1;
                    fmt_pos_start_zero = 0;
                    break;
            }

            //    bits_match =(1 + bits_moff + bits_mlen);      // Bits for encoding a match

            //    max_mlen =(min_mlen + (1<<bits_mlen) -1);     // Maximum match length
            //    max_off =(1<<bits_moff);                      // Maximum offset
            bits_mtotal = bits_moff + bits_mlen;


            //            if( bits_mtotal < 8 || bits_mtotal > 16 )
            //                cmd_error("total match bits should be from 8 to 16");

            // Calculate bits
            switch (bits_set)
            {
                case 0:
                case 1:
                case 4:
                case 5:
                    bits_mlen = bits_mtotal - bits_moff;
                    break;
                case 2:
                case 6:
                    bits_moff = bits_mtotal - bits_mlen;
                    break;
                case 3:
                case 8:
                    // OK
                    break;
                default:
                    cmd_error("only two of OFFSET, LENGTH and TOTAL bits should be given");
                    break;
            }

            // Check option values
            if (bits_moff < 0 || bits_moff > 12)
                cmd_error("match offset bits should be from 0 to 12");
            if (bits_mlen < 2 || bits_moff > 16)
                cmd_error("match length bits should be from 2 to 16");
            if (min_mlen < 1 || min_mlen > 16)
                cmd_error("minimum match length should be from 1 to 16");

            bits_match = (1 + bits_moff + bits_mlen);      // Bits for encoding a match

            max_mlen = (min_mlen + (1 << bits_mlen) - 1);     // Maximum match length
            max_off = (1 << bits_moff);                      // Maximum offset

            stat_len = new int[max_mlen + 1];
            stat_off = new int[max_off + 1];

            // Max size of each buffer: 128k
            const int bufSize = 128 * 1024;
            for (int i = 0; i < MaxPokeyChannel; i++)
            {
                channelData[i] = new byte[bufSize]; // calloc(128,1024);
                lpos[i] = -1;
            }

            // Read all data
            int chByteCount = 0;
            int sz;
            for( sz = 0; sz < inputSize; sz+=MaxPokeyChannel )
            {
                for(int i=0; i<MaxPokeyChannel; i++)
                {
                    byte val =inputBuffer[sz+i];

                    // Simplify patterns - rewrite silence as 0
                    if(tweakAUDC&&( (i & 1) == 1 ))
                    {
                        int vol  = val & 0x0F;
                        int dist = val & 0xF0;
                        if( vol == 0 )
                            val = 0;
                        else if(( dist & 0x10 )!=0)
                            val &= 0x1F;     // volume-only, ignore other bits
                        else if(( dist & 0x20 )!=0)
                            val &= 0xBF;     // no noise, ignore noise type bit
                    }
                    channelData[i][chByteCount] = val;
                }
                chByteCount++;
            }

            MemoryStream memWriter = new MemoryStream();

            // Open output file if needed
            b.outW = memWriter;

            // Check for empty streams and warn
            int[] chn_skip = new int[MaxPokeyChannel];

            init(ref b);

            for (int i = MaxPokeyChannel-1; i >= 0; i--)
            {
                byte[] p = channelData[i];
                byte s = p[0];
                int n = 0;

                for (int j = 0; j < chByteCount; j++)
                {
                    if (p[j] != s)
                        n++;
                }
                if (i != 0 && n == 0)
                {
                    if (show_stats)
                        DebugStatus.Print("Skipping channel #" + i + ", set with $" + s.ToString("X02") + ".\n", DebugStatus.VerboseOptions.MaxVerboseOnly);
                    add_bit(ref b, 1);
                    chn_skip[i] = 1;
                }
                else
                {
                    if (i != 0)
                        add_bit(ref b, 0);
                    chn_skip[i] = 0;
                    if (n == 0)
                    {
                        DebugStatus.Print("WARNING: stream #" + i + (s == 0 ? " is empty" : " contains only $" + s.ToString("X02")), DebugStatus.VerboseOptions.MaxVerboseOnly);
                    }
                }
            }
            bflush(ref b);

            // Now, we store initial values for all chanels:
            for (int i = 8; i >= 0; i--)
            {
                // In version 1 we only store init byte for the skipped channels
                if ((fmt_literal_first!=0) || (chn_skip[i]!=0))
                    add_byte(ref b, channelData[i][0]);
            }
            bflush(ref b);


            // Init LZ states
            lzop[] lz = new lzop[9];
            for(int i=0; i<9; i++)
                if( chn_skip[i]==0 )
                {
                    lzop_init(ref lz[i], channelData[i], chByteCount);
                    lzop_backfill(ref lz[i],0);
                }

            // Detect if at least one of the streams end in a match:
            int end_not_ok = 1;
            for (int i = 0; i < 9; i++)
                if (chn_skip[i]==0)
                    end_not_ok &= lzop_last_is_match(ref lz[i]);

            // If all streams end in a match, we need to fix at least one to end in
            // a literal - just fix stream 0, as this is always encoded:
            if((force_last_literal!=0)&&(end_not_ok != 0))
            {
                DebugStatus.Print("LZSS: fixing up stream #0 to end in a literal", DebugStatus.VerboseOptions.MaxVerboseOnly);
                lzop_backfill(ref lz[0], 1);
            }
            else if(end_not_ok != 0)
            {
                DebugStatus.Print("WARNING: stream does not end in a literal.", DebugStatus.VerboseOptions.MaxVerboseOnly);
                DebugStatus.Print("WARNING: this can produce errors at the end of decoding.", DebugStatus.VerboseOptions.MaxVerboseOnly);
            }



            // Compress
            for (int pos = fmt_literal_first!=0 ? 1 : 0; pos < sz/9; pos++)
            {
                for (int i = MaxPokeyChannel - 1; i >= 0; i--)
                    if (chn_skip[i] == 0)
                        lpos[i] = lzop_encode(ref b, ref lz[i], pos, lpos[i]);
            }
            bflush(ref b);


            //---
            FileStream stream0 = new FileStream(path + m_ext, FileMode.Create, FileAccess.Write);
            BinaryWriter binWriter = new BinaryWriter(stream0);

            binWriter.Write(memWriter.GetBuffer(), 0, (int)memWriter.Length);

            // Close file
            memWriter.Close();
            binWriter.Close();

        
            // Show stats
            DebugStatus.Print("LZSS: max offset= "+max_off+", max len= "+max_mlen+",match bits= "+(bits_match - 1)+"," +
                "ratio: " + b.total + " / " + (9 * chByteCount) + " = " + ((100.0 * b.total) / (9.0 * chByteCount)).ToString("F2") + "%\n", DebugStatus.VerboseOptions.MaxVerboseOnly);
            if( show_stats )
                for(int i=0; i<9; i++)
                    if( chn_skip[i]==0 )
                        DebugStatus.Print(" Stream #" + i + ": " + lz[i].bits[0] + " bits," + ((100.0 * lz[i].bits[0]) / (8.0 * chByteCount)).ToString("F2") + "%," + ((100.0 * lz[i].bits[0]) / (8.0 * b.total)).ToString("F2") + "% of output", DebugStatus.VerboseOptions.MaxVerboseOnly);
/*
            if( show_stats>1 )
            {
                fprintf(stderr,"\nvalue\t  POS\t  LEN\n");  
                for(int i=0; i<=max(max_mlen,max_off); i++)
                {
                    fprintf(stderr,"%2d\t%5d\t%5d\n", i,
                            (i <= max_off) ? stat_off[i] : 0,
                            (i <= max_mlen) ? stat_len[i] : 0);
                }
            }
        */
            return 0;
        }
 


    }

    //################################################################################################################
}
