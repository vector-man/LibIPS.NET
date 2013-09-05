using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
namespace CodeIsle
{
    public class LibIpsNet
    {
        const string PatchText = "PATCH";
        const int EndOfFile = 0x454F46;

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

            int offset = Read24(patch);
            int outlen = 0;
            int thisout = 0;
            int lastoffset = 0;
            bool w_scrambled = false;
            bool w_notthis = false;

            while (offset != EndOfFile)
            {
                int size = Read16(patch);

                if (size == 0)
                {
                    thisout = offset + Read16(patch);
                    Read8(patch);
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

                offset = Read24(patch);

            }
            study.OutlenMinMem = outlen;
            study.OutlenMax = 0xFFFFFFFF;

            if (patch.Position + 3 == patch.Length)
            {
                int truncate = Read24(patch);
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
        public void ApplyStudy(Stream patch, IpsStudy study, Stream source, Stream target)
        {
            source.CopyTo(target);
            if (study.Error == IpsError.IpsInvalid) throw new Exceptions.IpsInvalidException();
            int outlen = (int)Clamp(study.OutlenMin, target.Length, study.OutlenMax);
            // Set target file length to new size.
            target.SetLength(outlen);

            // Skip PATCH text.
            patch.Seek(5, SeekOrigin.Begin);
            int offset = Read24(patch);
            while (offset != EndOfFile)
            {
                int size = Read16(patch);


                target.Seek(offset, SeekOrigin.Begin);
                // If RLE patch.
                if (size == 0)
                {
                    size = Read16(patch);
                    target.Write(Enumerable.Repeat<byte>(Read8(patch), offset).ToArray(), 0, offset);
                }
                // If normal patch.
                else
                {
                    byte[] data = new byte[size];
                    patch.Read(data, 0, size);
                    target.Write(data, 0, size);

                }
                offset = Read24(patch);
            }
            if (study.OutlenMax != 0xFFFFFFFF && source.Length <= study.OutlenMax) throw new Exceptions.IpsNotThisException(); // Truncate data without this being needed is a poor idea.
        }
        public void Apply(Stream patch, Stream source, Stream target)
        {
            IpsStudy study = Study(patch);
            ApplyStudy(patch, study, source, target);
        }
        // Known situations where this function does not generate an optimal patch:
        // In:  80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80
        // Out: FF FF FF FF FF FF FF FF 00 01 02 03 04 05 06 07 FF FF FF FF FF FF FF FF
        // IPS: [         RLE         ] [        Copy         ] [         RLE         ]
        // Possible improvement: RLE across the entire file, copy on top of that.
        // Rationale: It would be a huge pain to create such a multi-pass tool if it should support writing a byte
        // more than twice, and I don't like half-assing stuff.


        // Known improvements over LIPS:
        // In:  00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F
        // Out: FF 01 02 03 04 05 FF FF FF FF FF FF FF FF FF FF
        // LIPS:[      Copy     ] [            RLE            ]
        // Mine:[] [ Unchanged  ] [            RLE            ]
        // Rationale: While LIPS can break early if it finds something RLEable in the middle of a block, it's not
        // smart enough to back off if there's something unchanged between the changed area and the RLEable spot.

        // In:  FF FF FF FF FF FF FF
        // Out: 00 00 00 00 01 02 03
        // LIPS:[   RLE   ] [ Copy ]
        // Mine:[       Copy       ]
        // Rationale: Again, RLE is no good at RLE.

        // It is also known that I win in some other situations. I didn't bother checking which, though.

        // There are no known cases where LIPS wins over libips.

        /// <summary>
        /// Creates an IPS patch file from a source file path and a target file path.
        /// </summary>
        /// <param name="source">The source file that contains the original data.</param>
        /// <param name="target">The target file that contains the modified data.</param>
        /// <param name="patch">The patch file to contain the resulting patch data.</param>
        /// <returns></returns>
        public void Create(string source, string target, string patch)
        {
            using (FileStream sourceStream = new FileStream(source, FileMode.Open), targetStream = new FileStream(target, FileMode.Open), patchStream = new FileStream(patch, FileMode.Create))
            {
                Create(sourceStream, targetStream, patchStream);
            }
        }
        /// <summary>
        /// Creates an IPS patch file stream from a source file stream and a target file stream.
        /// </summary>
        /// <param name="source">The source stream that contains the original data.</param>
        /// <param name="target">The target stream that contains the modified data.</param>
        /// <param name="patch">The patch file stream to contain the resulting patch data.</param>
        /// <returns></returns>
        public void Create(FileStream source, FileStream target, FileStream patch)
        {
            Create(source, target, patch);
        }
        /// <summary>
        /// Creates an IPS patch stream from a source stream and a target stream.
        /// </summary>
        /// <param name="source">The source stream that contains the original data.</param>
        /// <param name="target">The target stream that contains the modified data.</param>
        /// <param name="patch">The patch stream to contain the resulting patch data.</param>
        /// <returns></returns>
        public void Create(Stream source, Stream target, ref Stream patch)
        {
            long sourcelen = source.Length;
            long targetlen = target.Length;

            bool sixteenmegabytes = false;


            if (sourcelen > 16777216)
            {
                sourcelen = 16777216;
                sixteenmegabytes = true;
            }
            if (targetlen > 16777216)
            {
                targetlen = 16777216;
                sixteenmegabytes = true;
            }

            int offset = 0;

            {

                Write8((byte)'P', patch);
                Write8((byte)'A', patch);
                Write8((byte)'T', patch);
                Write8((byte)'C', patch);
                Write8((byte)'H', patch);

                int lastknownchange = 0;
                while (offset < targetlen)
                {
                    while (offset < sourcelen && (offset < sourcelen ? Read8(source, offset) : 0) == Read8(target, offset)) offset++;

                    // Check how much we need to edit until it starts getting similar.
                    int thislen = 0;
                    int consecutiveunchanged = 0;
                    thislen = lastknownchange - offset;
                    if (thislen < 0) thislen = 0;

                    while (true)
                    {
                        int thisbyte = offset + thislen + consecutiveunchanged;
                        if (thisbyte < sourcelen && (thisbyte < sourcelen ? Read8(source, thisbyte) : 0) == Read8(target, thisbyte)) consecutiveunchanged++;
                        else
                        {
                            thislen += consecutiveunchanged + 1;
                            consecutiveunchanged = 0;
                        }
                        if (consecutiveunchanged >= 6 || thislen >= 65536) break;
                    }

                    // Avoid premature EOF.
                    if (offset == EndOfFile)
                    {
                        offset--;
                        thislen++;
                    }

                    lastknownchange = offset + thislen;
                    if (thislen > 65535) thislen = 65535;
                    if (offset + thislen > targetlen) thislen = (int)(targetlen - offset);
                    if (offset == targetlen) continue;

                    // Check if RLE here is worthwhile.
                    int byteshere = 0;

                    for (byteshere = 0; byteshere < thislen && Read8(target, offset) == Read8(target, (offset + byteshere)); byteshere++) { }


                    if (byteshere == thislen)
                    {
                        int thisbyte = Read8(target, offset);
                        int i = 0;

                        while (true)
                        {
                            int pos = offset + byteshere + i - 1;
                            if (pos >= targetlen || Read8(target, pos) != thisbyte || byteshere + i > 65535) break;
                            if (pos >= sourcelen || (pos < sourcelen ? Read8(source, pos) : 0) != thisbyte)
                            {
                                byteshere += i;
                                thislen += i;
                                i = 0;
                            }
                            i++;
                        }

                    }
                    if ((byteshere > 8 - 5 && byteshere == thislen) || byteshere > 8)
                    {
                        Write24(offset, patch);
                        Write16(0, patch);
                        Write16(byteshere, patch);
                        Write8(Read8(target, offset), patch);
                        offset += byteshere;
                    }
                    else
                    {
                        // Check if we'd gain anything from ending the block early and switching to RLE.
                        byteshere = 0;
                        int stopat = 0;

                        while (stopat + byteshere < thislen)
                        {
                            if (Read8(target, (offset + stopat)) == Read8(target, (offset + stopat + byteshere))) byteshere++;
                            else
                            {
                                stopat += byteshere;
                                byteshere = 0;
                            }
                            // RLE-worthy despite two IPS headers.
                            if (byteshere > 8 + 5 ||
                                // RLE-worthy at end of data.
                                    (byteshere > 8 && stopat + byteshere == thislen) ||
                                    (byteshere > 8 && Compare(target, (offset + stopat + byteshere), target, (offset + stopat + byteshere + 1), 9 - 1)))//rle-worthy before another rle-worthy
                            {
                                if (stopat != 0) thislen = stopat;
                                // We don't scan the entire block if we know we'll want to RLE, that'd gain nothing.
                                break;
                            }
                        }


                        // Don't write unchanged bytes at the end of a block if we want to RLE the next couple of bytes.
                        if (offset + thislen != targetlen)
                        {
                            while (offset + thislen - 1 < sourcelen && Read8(target, (offset + thislen - 1)) == (offset + thislen - 1 < sourcelen ? Read8(source, (offset + thislen - 1)) : 0)) thislen--;
                        }
                        if (thislen > 3 && Compare(target, offset, target, (offset + 1), (thislen - 2)))
                        {
                            Write24(offset, patch);
                            Write16(0, patch);
                            Write16(thislen, patch);
                            Write8(Read8(target, offset), patch);
                        }
                        else
                        {
                            Write24(offset, patch);
                            Write16(thislen, patch);
                            int i;
                            for (i = 0; i < thislen; i++)
                            {
                                Write8(Read8(target, (offset + i)), patch);
                            }
                        }
                        offset += thislen;

                    }
                }



                Write8((byte)'E', patch);
                Write8((byte)'O', patch);
                Write8((byte)'F', patch);

                if (sourcelen > targetlen) Write24((int)targetlen, patch);

                if (sixteenmegabytes) throw new Exceptions.Ips16MBException(); ;
                if (patch.Length == 8) throw new Exceptions.IpsIdenticalException();
            }

        }
        // Helper to read 8 bit.
        private byte Read8(Stream stream, int offset = -1)
        {
            if (offset != -1 && stream.Position != offset)
            {
                stream.Seek(offset, SeekOrigin.Begin);
            }
            if (stream.Position < stream.Length)
            {
                return (byte)stream.ReadByte();
            }
            else
            {
                return 0;
            }
        }
        // Helper to read 16bit.
        private int Read16(Stream stream)
        {
            if (stream.Position + 1 < stream.Length)
            {
                byte[] data = new byte[2];
                stream.Read(data, 0, 2);
                return (data[0] << 8) | data[1];
            }
            else
            {
                return 0;
            }
        }
        // Helper to read 24bit.
        private int Read24(Stream stream)
        {
            if (stream.Position + 1 < stream.Length)
            {
                byte[] data = new byte[3];
                stream.Read(data, 0, 3);
                return (data[0] << 16) | (data[1] << 8) | data[2];
            }
            else
            {
                return 0;
            }
        }
        // Helper to write 8bit.
        private void Write8(byte value, Stream stream)
        {
            stream.WriteByte(value);
        }
        // Helper to write 16bit.
        private void Write16(int value, Stream stream)
        {
            Write8((byte)(value >> 8), stream);
            Write8((byte)(value), stream);
        }
        // Helper to write 24bit.
        private void Write24(int value, Stream stream)
        {
            Write8((byte)(value >> 16), stream);
            Write8((byte)(value >> 8), stream);
            Write8((byte)(value), stream);
        }

        // Helper to Compare two BinaryReaders with a starting point and a count of elements.
        private bool Compare(Stream source, int sourceStart, Stream target, int targetStart, int count)
        {
            source.Seek(sourceStart, SeekOrigin.Begin);
            byte[] sourceData = new byte[count];
            source.Read(sourceData, 0, count);

            target.Seek(targetStart, SeekOrigin.Begin);
            byte[] targetData = new byte[count];
            target.Read(targetData, 0, count);

            for (int i = 0; i < count; i++)
            {
                if (sourceData[i] != targetData[i]) return false;
            }
            return true;
        }
        // Helper for minimum value.
        private long Min(long a, long b)
        {
            return (a) < (b) ? (a) : (b);
        }
        // Helper for maximum value.
        private long Max(long a, long b)
        {
            return (a) > (b) ? (a) : (b);
        }
        // Helper to clamp a value to a range.
        private long Clamp(long a, long b, long c)
        {
            return Max(a, Min(b, c));
        }
    }
}
