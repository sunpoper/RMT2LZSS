using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;

// parser for RMT extensions (not part of RMT 1.28)

namespace RMT
{

    static public class Extensions
    {

        //-------------------------------------------------------------------------------------------------------------------
        const string s_customName = "../CustomNoteTables.txt";

        enum RMTTableName
        {
            unset,
            distortion,
            notes8bit,
            notes16bit,
        }

        public static void LoadCustomFrequencyTables()
        {

            DebugStatus.Print("Loading " + s_customName);

            RMTTableName tbl = RMTTableName.unset;

            using (FileStream fs = new FileStream(s_customName, FileMode.Open, FileAccess.Read))
            {
                StreamReader reader = new StreamReader(fs);
                int fileSize = (int)fs.Length;

                List<int> tabbeganddistor_Custom= new List<int>() ;
                List<byte> frqtab_Custom = new List<byte>();
                List<byte> frqtabbasshi_Custom = new List<byte>();

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line.Contains("[distortion]"))
                        tbl = RMTTableName.distortion;
                    else if (line.Contains("[FrequencyTables8Bit]"))
                        tbl = RMTTableName.notes8bit;
                    else if (line.Contains("[FrequencyTables16Bit]"))
                        tbl = RMTTableName.notes16bit;
                    else if (line.StartsWith("//"))
                        continue;
                    else
                    {
                        string[] splitLine = line.Split(new char[] { ',', });
                        foreach (string split in splitLine)
                        {
                            int indexOfHex = split.IndexOf('$', 0);
                            if (indexOfHex != -1)
                            {
                                byte value = Convert.ToByte(split.Substring(indexOfHex + 1, 2), 16);
                                switch (tbl)
                                {
                                    case RMTTableName.distortion:
                                        tabbeganddistor_Custom.Add(RMT.Player.NoteTableLen * tabbeganddistor_Custom.Count/2);
                                        tabbeganddistor_Custom.Add(value);
                                        break;
                                    case RMTTableName.notes8bit:
                                        frqtab_Custom.Add(value);
                                        break;
                                    case RMTTableName.notes16bit:
                                        frqtabbasshi_Custom.Add(value);
                                        break;
                                }
                            }
                        }
                    }
                }
                reader.Close();

                DebugStatus.Print("Found " + tabbeganddistor_Custom.Count/2 +" AUDC values", DebugStatus.VerboseOptions.MaxVerboseOnly);
                DebugStatus.Print("Found " + frqtab_Custom.Count  + " 8bit frequency values", DebugStatus.VerboseOptions.MaxVerboseOnly);
                DebugStatus.Print("Found " + frqtabbasshi_Custom.Count + " 16bit frequency values", DebugStatus.VerboseOptions.MaxVerboseOnly);

                if (tabbeganddistor_Custom.Count / 2 != frqtab_Custom.Count / RMT.Player.NoteTableLen)
                    MessageBox.Show("invalid distortion data count " + tabbeganddistor_Custom.Count / 2 + " doesn't match 8bit table count " + frqtab_Custom.Count / RMT.Player.NoteTableLen);
                if ((frqtab_Custom.Count & (RMT.Player.NoteTableLen - 1)) != 0)
                    MessageBox.Show("8bit frequency values count " + frqtab_Custom.Count + " not a multiple of " + RMT.Player.NoteTableLen);
                if ((frqtabbasshi_Custom.Count & (RMT.Player.NoteTableLen * 2 - 1)) != 0)
                    MessageBox.Show("16bit frequency values count " + frqtabbasshi_Custom.Count + " not a multiple of " + RMT.Player.NoteTableLen * 2);

                int _8bitTablesCount = frqtab_Custom.Count / RMT.Player.NoteTableLen;
                int _16bitTablesCount = frqtabbasshi_Custom.Count / (RMT.Player.NoteTableLen * 2);

                if (_8bitTablesCount != _16bitTablesCount)
                    DebugStatus.Print("8 bit table count is different from 16 bit table count: " + _8bitTablesCount+ " vs "+_16bitTablesCount);

                RMT.Player.tabbeganddistor_Custom = tabbeganddistor_Custom.ToArray();
                RMT.Player.frqtab_Custom = frqtab_Custom.ToArray();
                RMT.Player.frqtabbasshi_Custom = frqtabbasshi_Custom.ToArray();
            }

        }
        //-------------------------------------------------------------------------------------------------------------------
        static List<UInt32> GetLineValues(string line, string field)
        {
            string subLine = line.Substring(line.IndexOf(line, 0) + field.Length);

            List<UInt32> values = new List<UInt32>();

            string[] splitLine = subLine.Split(new string[] {"//"},StringSplitOptions.None);

            splitLine = splitLine[0].Split(new char[] { ',', ' ', '\t' });
            foreach (string split in splitLine)
            {
                int indexOfHex = split.IndexOf('$', 0);

                if (indexOfHex != -1)
                {
                    UInt32 value = Convert.ToUInt32(split.Substring(indexOfHex + 1, split.Length-1), 16);
                    values.Add(value);
                }
            }
            return values;
        }
        //-------------------------------------------------------------------------------------------------------------------
        public class ExtendedInstrument
        {
            public int originalIdx;
            public byte initialShiftFreq;
            public List<UInt32> EnvelopeDist = new List<UInt32>();
            public List<byte> EnvelopeAUDCTL = new List<byte>();
            public double envelopeStep;
            public RMT.ModuleData.Sample sample;
            public List<UInt16> EnvelopeParam16= new List<UInt16>();
            public List<UInt16> TableNote16= new List<UInt16>();

            public ExtendedInstrument()
            {
                originalIdx = -1;
                initialShiftFreq = 1;
                envelopeStep = 1;
                sample = null;
            }

        }

        public static List<ExtendedInstrument> LoadCustomInstruments(string path)
        {
            const string instrumentStr = "[instrument$";
            const string EnvelopeDistStr = "EnvelopeDistortion=";
            const string EnvelopeAUCDTLStr = "EnvelopeAUDCTL=";
            const string FilterShiftFreqStr = "FilterInitShiftFreq=";
            const string EnvelopeStepStr = "EnvelopeStep=";
            const string SampleStepStr = "SampleStep=";
            const string SampleLoopStr = "SampleLoop=";
            const string SampleStr = "Sample=";
            //const string Porta16SpeedStr = "Porta16Speed=";
            const string EnvelopeParam16 = "EnvelopeArgXY16=";
            const string TableNote16 = "TableNote16=";

            List<ExtendedInstrument> extendedInstrList = new List<ExtendedInstrument>();

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                StreamReader reader = new StreamReader(fs);
                int fileSize = (int)fs.Length;

                ExtendedInstrument extendedInstr = new ExtendedInstrument();

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line.StartsWith("//"))
                    {
                    }
                    else if (line.StartsWith(instrumentStr))
                    {
                        if (extendedInstr.originalIdx != -1)
                        {
                            extendedInstrList.Add(extendedInstr);
                            extendedInstr = new ExtendedInstrument();
                        }
                        int idxStr = line.IndexOf(instrumentStr, 0) + instrumentStr.Length;
                        extendedInstr.originalIdx = Convert.ToByte(line.Substring(idxStr,2), 16);

                        DebugStatus.Print("extended instrument "+idxStr, DebugStatus.VerboseOptions.MaxVerboseOnly);

                    }
                    else if (line.StartsWith(SampleStr))
                    {
                        int idxStr = line.IndexOf(SampleStr, 0) + SampleStr.Length;
                        string[] wavName = line.Substring(idxStr).Split(new char[]{'\n'});
                        if(Path.GetDirectoryName(wavName[0])=="")
                            wavName[0]=  Path.GetDirectoryName(path)+"\\"+wavName[0];

                        int[] leftChan, rightChan;
                        if (ReadWav(wavName[0], out leftChan, out rightChan))
                        {
                            if (extendedInstr.sample == null)
                                extendedInstr.sample = new RMT.ModuleData.Sample();
                            extendedInstr.sample.m_data = new List<byte>();
                            for (int i = 0; i < leftChan.Length; i++)
                                extendedInstr.sample.m_data.Add((byte)((leftChan[i] & 0xf000) >> 12));
                            if (extendedInstr.sample.m_loopIdx == -1)
                                extendedInstr.sample.m_loopIdx = extendedInstr.sample.m_data.Count - 1;

                            //                             for (int i = 0; i < 1; i++)
                            //                                 extendedInstr.sample.data.Add(0xf);
                            //                             for (int i = 0; i < 1; i++)
                            //                                 extendedInstr.sample.data.Add(0x0);

                        }
                        else
                            return null;
                    }
                    else if (line.StartsWith(EnvelopeStepStr))
                    {
                        string[] splitLine = line.Split(new string[] {"="},StringSplitOptions.None);
                        extendedInstr.envelopeStep = Convert.ToDouble(splitLine[1]) ;
                    }
                    else if (line.StartsWith(SampleStepStr))
                    {
                        string[] splitLine = line.Split(new string[] { "=" }, StringSplitOptions.None);
                        if (extendedInstr.sample == null)
                            extendedInstr.sample = new RMT.ModuleData.Sample();
                        extendedInstr.sample.m_step = Convert.ToDouble(splitLine[1]);
                    }
                    else if (line.StartsWith(SampleLoopStr))
                    {
                        string[] splitLine = line.Split(new string[] { "=" }, StringSplitOptions.None);
                        if (extendedInstr.sample == null)
                            extendedInstr.sample = new RMT.ModuleData.Sample();
                        extendedInstr.sample.m_loopIdx = Convert.ToInt32(splitLine[1]);
                    }
                    else if (line.StartsWith(EnvelopeDistStr))
                    {
                        //--- envelope distortion
                        extendedInstr.EnvelopeDist = GetLineValues(line, EnvelopeDistStr);

                        foreach (UInt32 dist in extendedInstr.EnvelopeDist)
                        {
                            if (dist >= RMT.Player.tabbeganddistor_Custom.Length)
                            {
                                MessageBox.Show("extended instrument " + extendedInstr.originalIdx + " EnvelopeDist $" + dist.ToString("X04") + " is outside 8bit frequency table");
                            }
                        }
                        DebugStatus.Print(EnvelopeDistStr + extendedInstr.EnvelopeDist.Count + " values", DebugStatus.VerboseOptions.MaxVerboseOnly);
                    }
                    else if (line.StartsWith(FilterShiftFreqStr))
                    {
                        //--- filter shift freq
                        extendedInstr.initialShiftFreq = (byte)(GetLineValues(line, FilterShiftFreqStr)[0]);

                        DebugStatus.Print(FilterShiftFreqStr, DebugStatus.VerboseOptions.MaxVerboseOnly);

                    }
                    else if (line.StartsWith(EnvelopeAUCDTLStr))
                    {
                        //--- envelope distortion
                        List<UInt32> vals = GetLineValues(line, EnvelopeAUCDTLStr);

                        if (vals.Count != 0)
                        {
                            foreach (UInt32 AUDCTL in vals)
                            {
                                extendedInstr.EnvelopeAUDCTL.Add((byte)AUDCTL);
                            }
                            DebugStatus.Print(EnvelopeAUCDTLStr + extendedInstr.EnvelopeAUDCTL.Count + " values", DebugStatus.VerboseOptions.MaxVerboseOnly);
                        }

                    }
//                     else if (line.Contains(Porta16SpeedStr))
//                     {
//                         extendedInstr.Porta16Speed = (UInt16)(GetLineValues(line, Porta16SpeedStr)[0]);
//                     }
                    else if (line.StartsWith(EnvelopeParam16))
                    {
                        //--- porta 16
                        List<UInt32> vals = GetLineValues(line, EnvelopeParam16);

                        if (vals.Count != 0)
                        {
                            foreach (UInt16 porta16 in vals)
                            {
                                extendedInstr.EnvelopeParam16.Add((UInt16)porta16);
                            }
                            DebugStatus.Print(EnvelopeParam16 + extendedInstr.EnvelopeParam16.Count + " values", DebugStatus.VerboseOptions.MaxVerboseOnly);
                        }
                    }
                    else if (line.StartsWith(TableNote16))
                    {
                        //--- table note 16
                        List<UInt32> vals = GetLineValues(line, TableNote16);
                        if (vals.Count != 0)
                        {
                            foreach (UInt16 note16 in vals)
                            {
                                extendedInstr.TableNote16.Add((UInt16)note16);
                            }
                            DebugStatus.Print(TableNote16 + extendedInstr.TableNote16.Count + " values", DebugStatus.VerboseOptions.MaxVerboseOnly);
                        }
                    }
                    else
                    {
                        DebugStatus.Print("Unknown command:"+line, DebugStatus.VerboseOptions.MaxVerboseOnly);
                    }
                }
                reader.Close();

                if (extendedInstr.originalIdx != -1)
                    extendedInstrList.Add(extendedInstr);

            }
            return extendedInstrList;

        }

        //-------------------------------------------------------------------------------------------------------------------
        static bool ReadWav(string filename, out int[] L, out int[] R)
        {
            L = R = null;

            if (!File.Exists(filename))
            {
                DebugStatus.Print("Failed to load sample:" + filename);
                return false;
            }

            using (FileStream fs = File.Open(filename, FileMode.Open))
            {
                BinaryReader reader = new BinaryReader(fs);

                // chunk 0
                int chunkID = reader.ReadInt32();
                int fileSize = reader.ReadInt32();
                int riffType = reader.ReadInt32();


                // chunk 1
                int fmtID = reader.ReadInt32();
                int fmtSize = reader.ReadInt32(); // bytes for this chunk (expect 16 or 18)

                // 16 bytes coming...
                int fmtCode = reader.ReadInt16();
                int channels = reader.ReadInt16();
                int sampleRate = reader.ReadInt32();
                int byteRate = reader.ReadInt32();
                int fmtBlockAlign = reader.ReadInt16();
                int bitDepth = reader.ReadInt16();

                if (fmtSize == 18)
                {
                    // Read any extra values
                    int fmtExtraSize = reader.ReadInt16();
                    reader.ReadBytes(fmtExtraSize);
                }

                // chunk 2
                int dataID = reader.ReadInt32();
                int bytes = reader.ReadInt32();

                // DATA!
                byte[] byteArray = reader.ReadBytes(bytes);

                int bytesForSamp = bitDepth / 8;
                int nValues = bytes / bytesForSamp;


                int[] asInt = null;
                switch (bitDepth)
                {
//                         case 64:
//                             double[]
//                                 asDouble = new double[nValues];
//                             Buffer.BlockCopy(byteArray, 0, asDouble, 0, bytes);
//                             asFloat = Array.ConvertAll(asDouble, e => (float)e);
//                             break;
//                         case 32:
//                             asFloat = new float[nValues];
//                             Buffer.BlockCopy(byteArray, 0, asFloat, 0, bytes);
//                             break;
                    case 16:
                        Int16[]
                            asInt16 = new Int16[nValues];
                        Buffer.BlockCopy(byteArray, 0, asInt16, 0, bytes);
                        asInt = Array.ConvertAll(asInt16, e => (int)(e+32768) );
                        break;
                    default:
                        DebugStatus.Print("Unsupported sample format("+bitDepth+"):" + filename);
                        return false;
                }

                switch (channels)
                {
                    case 1:
                    {
                        const int steps = 1;
                        int nSamps = nValues / steps;
                        L = new int[nSamps];
                        for (int s = 0, v = 0; s < nSamps; s++)
                        {
                            L[s] = asInt[v];
                            v += steps;
                        }

//                             using (FileStream fs2 = File.Open(filename+".4bit", FileMode.Create))
//                             {
//                                 BinaryWriter writer = new BinaryWriter(fs2);
//                                 for (int s2 = 0; s2 < nSamps; s2++)
//                                     writer.Write((byte)((L[s2]&0xf000)>>12));
//                                 writer.Close();
//                             }


                        //                            L = asInt;
                        R = null;
                        return true;
                    }

                    case 2:
                    {
                        // de-interleave
                        int nSamps = nValues / 2;
                        L = new int[nSamps];
                        R = new int[nSamps];
                        for (int s = 0, v = 0; s < nSamps; s++)
                        {
                            L[s] = asInt[v++];
                            R[s] = asInt[v++];
                        }
                        return true;
                    }
                    default:
                        return false;
                }
            }
            //return false;
        }         
        //-------------------------------------------------------------------------------------------------------------------

    }

}