# RMT2LZSS
C# RMT 1.28 player (C# version of the original 6502 asm player)

(I named it RMT2LZSS but it should really be RMT2SAPR as it doesn't contain the LZSS compressor by dmsc)

Just create an empty C# project and add the files. Then run it with the following sequence:

            string filename = "Battle Squadron Title 100hz Final.rmt";

            RMT.ModuleData rmtLoader = new RMT.ModuleData(RMT.ModuleData.RMTVersion._128Unpatched);
            RMT.Player rmtPlayer = new RMT.Player(rmtLoader);

            List<int> volumeOffsets = new List<int>();
            for (int i = 0; i < 8; i++ )
                volumeOffsets.Add(0);

            List<int> SongsStartLine = rmtLoader.LoadRMT(filename);

            RMT.Player.PlaybackSpeed playSpeed = rmtPlayer.Init(SongsStartLine[0], false, volumeOffsets);

            playSpeed = rmtPlayer.Init(SongsStartLine[0], false, volumeOffsets);

            List<byte> pokeyBufL = null;
            List<byte> pokeyBufR = null;
            int loopFrame = 0;

            rmtPlayer.Play(out pokeyBufL, out pokeyBufR, out loopFrame);

            //Asap2Wav.Main2(filename, pokeyBufL, (int)playSpeed, false, false); // only output mono for now


