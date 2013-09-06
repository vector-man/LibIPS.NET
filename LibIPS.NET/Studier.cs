using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using CodeIsle.Utils;
namespace CodeIsle
{
    class Studier
    {
        public const string PatchText = "PATCH";
        public const int EndOfFile = 0x454F46;
        public enum IpsError
        {
            // Patch applied or created successfully.
            IpsOk,
            // The patch is most likely not intended for this ROM.
            IpsNotThis,
            // The patch is technically valid, but seems scrambled or malformed.
            IpsScrambled,
            // The patch is invalid.
            IpsInvalid,
            // One or both files is bigger than 16MB. The IPS format doesn't support that. 
            // The created patch contains only the differences to that point.
            Ips16MB,
            // The input buffers are identical.
            IpsIdentical,
        };

        public struct IpsStudy
        {
            public IpsError Error;
            public long OutlenMin;
            public long OutlenMax;
            public long OutlenMinMem;
        };
        public IpsStudy Study(Stream patch)
        {
            IpsStudy study = new IpsStudy();
            study.Error = IpsError.IpsInvalid;
            if (patch.Length < 8) return study;

            // If 'PATCH' text was not found, return IPS was invalid error.
            byte[] header = new byte[PatchText.Length];
            patch.Read(header, 0, PatchText.Length);
            if (!Enumerable.SequenceEqual(header, System.Text.Encoding.ASCII.GetBytes(PatchText))) return study;

            int offset = Reader.Read24(patch);
            int outlen = 0;
            int thisout = 0;
            int lastoffset = 0;
            bool w_scrambled = false;
            bool w_notthis = false;

            while (offset != EndOfFile)
            {
                int size = Reader.Read16(patch);

                if (size == 0)
                {
                    thisout = offset + Reader.Read16(patch);
                    Reader.Read8(patch);
                }
                else
                {
                    thisout = offset + size;
                    patch.Seek(size, SeekOrigin.Current);

                }
                if (offset < lastoffset) w_scrambled = true;
                lastoffset = offset;
                if (thisout > outlen) outlen = thisout;
                if (patch.Position >= patch.Length) return study;

                offset = Reader.Read24(patch);

            }
            study.OutlenMinMem = outlen;
            study.OutlenMax = 0xFFFFFFFF;

            if (patch.Position + 3 == patch.Length)
            {
                int truncate = Reader.Read24(patch);
                study.OutlenMax = truncate;
                if (outlen > truncate)
                {
                    outlen = truncate;
                    w_notthis = true;
                }

            }
            if (patch.Position != patch.Length) return study;

            study.Error = IpsError.IpsOk;
            if (w_notthis) study.Error = IpsError.IpsNotThis;
            if (w_scrambled) study.Error = IpsError.IpsScrambled;
            return study;

        }
    }
}
