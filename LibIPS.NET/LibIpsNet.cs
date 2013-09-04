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
            if (patch.Length < 8) throw new Exceptions.IpsInvalidException();

            using (var patchReader = new BinaryReader(patch))
            {
                // If 'PATCH' text was not found, return IPS was invalid error.
                if (!patchReader.ReadChars(PatchText.Length).ToString().Equals(PatchText)) throw new Exceptions.IpsInvalidException();

                int offset = Read24(patchReader);
                int outlen = 0;
                int thisout = 0;
                int lastoffset = 0;
                bool w_scrambled = false;
                bool w_notthis = false;

                while (offset != EndOfFile)
                {
                    int size = Read16(patchReader);

                    if (size == 0)
                    {
                        thisout = offset + patchReader.ReadInt16();
                        patchReader.ReadByte();
                    }
                    else
                    {
                        thisout = offset + size;

                    }
                    if (offset < lastoffset) w_scrambled = true;
                    lastoffset = offset;
                    if (thisout > outlen) outlen = thisout;
                    if (patch.Position >= patch.Length) throw new Exceptions.IpsInvalidException();

                    offset = Read24(patchReader);

                }
                study.OutlenMinMem = outlen;
                study.OutlenMax = 0xFFFFFFFF;

                if (patch.Position == patch.Length)
                {
                    int truncate = Read24(patchReader);
                    study.OutlenMax = truncate;
                    if (outlen > truncate)
                    {
                        outlen = truncate;
                        w_notthis = true;
                    }

                }
                if (patch.Position != patch.Length) throw new Exceptions.IpsInvalidException();

                study.Error = IpsError.IpsOk;
                if (w_notthis) study.Error = IpsError.IpsNotThis;
                if (w_scrambled) study.Error = IpsError.IpsScrambled;
            }
            return study;

        }
        public void ApplyStudy(Stream patch, IpsStudy study, Stream source, Stream target)
        {
            source.CopyTo(target);
            if (study.Error == IpsError.IpsInvalid) throw new Exceptions.IpsInvalidException();
            int outlen = (int)Clamp(study.OutlenMin, target.Length, study.OutlenMax);

            using (BinaryReader patchReader = new BinaryReader(patch))
            using (BinaryWriter targetWriter = new BinaryWriter(target))
            {
                // Skip PATCH text.
                patchReader.BaseStream.Seek(5, SeekOrigin.Begin);
                int offset = Read24(patchReader);
                while (offset != EndOfFile)
                {
                    int size = Read16(patchReader);


                    targetWriter.Seek(offset, SeekOrigin.Begin);
                    // If RLE patch.
                    if (size == 0)
                    {
                        size = Read16(patchReader);
                        targetWriter.Write(Enumerable.Repeat<byte>(Read8(patchReader), offset).ToArray());
                    }
                    // If normal patch.
                    else
                    {
                        targetWriter.Write(patchReader.ReadBytes(size));

                    }
                    offset = Read24(patchReader);
                }
            }
            if (study.OutlenMax != 0xFFFFFFFF && source.Length <= study.OutlenMax) throw new Exceptions.IpsNotThisException(); // Truncate data without this being needed is a poor idea.
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

            using (BinaryReader sourceReader = new BinaryReader(source))
            using (BinaryReader targetReader = new BinaryReader(target))
            using (BinaryWriter patchWriter = new BinaryWriter(patch))
            {

                Write8((byte)'P', patchWriter);
                Write8((byte)'A', patchWriter);
                Write8((byte)'T', patchWriter);
                Write8((byte)'C', patchWriter);
                Write8((byte)'H', patchWriter);

                int lastknownchange = 0;
                while (offset < targetlen)
                {
                    while (offset < sourcelen && (offset < sourcelen ? Read8(sourceReader, offset) : 0) == Read8(targetReader, offset)) offset++;

                    // Check how much we need to edit until it starts getting similar.
                    int thislen = 0;
                    int consecutiveunchanged = 0;
                    thislen = lastknownchange - offset;
                    if (thislen < 0) thislen = 0;

                    while (true)
                    {
                        int thisbyte = offset + thislen + consecutiveunchanged;
                        if (thisbyte < sourcelen && (thisbyte < sourcelen ? Read8(sourceReader, thisbyte) : 0) == Read8(targetReader, thisbyte)) consecutiveunchanged++;
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

                    for (byteshere = 0; byteshere < thislen && Read8(targetReader, offset) == Read8(targetReader, (offset + byteshere)); byteshere++) { }


                    if (byteshere == thislen)
                    {
                        int thisbyte = Read8(targetReader, offset);
                        int i = 0;

                        while (true)
                        {
                            int pos = offset + byteshere + i - 1;
                            if (pos >= targetlen || Read8(targetReader, pos) != thisbyte || byteshere + i > 65535) break;
                            if (pos >= sourcelen || (pos < sourcelen ? Read8(sourceReader, pos) : 0) != thisbyte)
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
                        Write24(offset, patchWriter);
                        Write16(0, patchWriter);
                        Write16(byteshere, patchWriter);
                        Write8(Read8(targetReader, offset), patchWriter);
                        offset += byteshere;
                    }
                    else
                    {
                        // Check if we'd gain anything from ending the block early and switching to RLE.
                        byteshere = 0;
                        int stopat = 0;

                        while (stopat + byteshere < thislen)
                        {
                            if (Read8(targetReader, (offset + stopat)) == Read8(targetReader, (offset + stopat + byteshere))) byteshere++;
                            else
                            {
                                stopat += byteshere;
                                byteshere = 0;
                            }
                            // RLE-worthy despite two IPS headers.
                            if (byteshere > 8 + 5 ||
                                // RLE-worthy at end of data.
                                    (byteshere > 8 && stopat + byteshere == thislen) ||
                                    (byteshere > 8 && Compare(targetReader, (offset + stopat + byteshere), targetReader, (offset + stopat + byteshere + 1), 9 - 1)))//rle-worthy before another rle-worthy
                            {
                                if (stopat != 0) thislen = stopat;
                                // We don't scan the entire block if we know we'll want to RLE, that'd gain nothing.
                                break;
                            }
                        }


                        // Don't write unchanged bytes at the end of a block if we want to RLE the next couple of bytes.
                        if (offset + thislen != targetlen)
                        {
                            while (offset + thislen - 1 < sourcelen && Read8(targetReader, (offset + thislen - 1)) == (offset + thislen - 1 < sourcelen ? Read8(sourceReader, (offset + thislen - 1)) : 0)) thislen--;
                        }
                        if (thislen > 3 && Compare(targetReader, offset, targetReader, (offset + 1), (thislen - 2)))
                        {
                            Write24(offset, patchWriter);
                            Write16(0, patchWriter);
                            Write16(thislen, patchWriter);
                            Write8(Read8(targetReader, offset), patchWriter);
                        }
                        else
                        {
                            Write24(offset, patchWriter);
                            Write16(thislen, patchWriter);
                            int i;
                            for (i = 0; i < thislen; i++)
                            {
                                Write8(Read8(targetReader, (offset + i)), patchWriter);
                            }
                        }
                        offset += thislen;

                    }
                }



                Write8((byte)'E', patchWriter);
                Write8((byte)'O', patchWriter);
                Write8((byte)'F', patchWriter);

                if (sourcelen > targetlen) Write24((int)targetlen, patchWriter);

                if (sixteenmegabytes) throw new Exceptions.Ips16MBException(); ;
                if (patchWriter.BaseStream.Length == 8) throw new Exceptions.IpsIdenticalException();
            }

        }
        private byte Read8(BinaryReader reader, int offset = -1)
        {
            if (offset != -1 && reader.BaseStream.Position != offset)
            {
                reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            }
            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                return reader.ReadByte();
            }
            else
            {
                return 0;
            }
        }
        private int Read16(BinaryReader reader)
        {
            if (reader.BaseStream.Position + 1 < reader.BaseStream.Length)
            {
                byte[] data = reader.ReadBytes(2);

                return (data[0] << 8) | data[1];
            }
            else
            {
                return 0;
            }
        }
        private int Read24(BinaryReader reader)
        {
            if (reader.BaseStream.Position + 1 < reader.BaseStream.Length)
            {
                byte[] data = reader.ReadBytes(3);

                return (data[0] << 16) | (data[1] << 8) | data[2];
            }
            else
            {
                return 0;
            }
        }

        private void Write8(byte value, BinaryWriter writer)
        {
            writer.Write(value);
        }
        private void Write16(int value, BinaryWriter writer)
        {
            Write8((byte)(value >> 8), writer);
            Write8((byte)(value), writer);
        }
        private void Write24(int value, BinaryWriter writer)
        {
            Write8((byte)(value >> 16), writer);
            Write8((byte)(value >> 8), writer);
            Write8((byte)(value), writer);
        }

        // Compares two BinaryReaders with a starting point and a count of elements.
        private bool Compare(BinaryReader source, int sourceStart, BinaryReader target, int targetStart, int count)
        {
            source.BaseStream.Seek(sourceStart, SeekOrigin.Begin);
            byte[] sourceData = source.ReadBytes(count);

            target.BaseStream.Seek(targetStart, SeekOrigin.Begin);
            byte[] targetData = target.ReadBytes(count);

            for (int i = 0; i < count; i++)
            {
                if (sourceData[i] != targetData[i]) return false;
            }
            return true;
        }

        private long Min(long a, long b)
        {
            return (a) < (b) ? (a) : (b);
        }

        private long Max(long a, long b)
        {
            return (a) > (b) ? (a) : (b);
        }

        private long Clamp(long a, long b, long c)
        {
            return Max(a, Min(b, c));
        }
    }
}
