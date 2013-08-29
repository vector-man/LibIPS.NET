using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
namespace LibIpsNet
{
    public class IpsLibNet
    {
        const string PatchText = "PATCH";
        const uint EndOfFile = 0x454F46;

        enum ipserror
        {
            ips_ok,//Patch applied or created successfully.
            ips_notthis,//The patch is most likely not intended for this ROM.
            ips_scrambled,//The patch is technically valid, but seems scrambled or malformed.
            ips_invalid,//The patch is invalid.
            ips_16MB,//One or both files is bigger than 16MB. The IPS format doesn't support that. The created
            //patch contains only the differences to that point.
            ips_identical,//The input buffers are identical.
        };

        public struct ipsstudy
        {
            public ipserror error;
            public uint outlen_min;
            public uint outlen_max;
            public uint outlen_min_mem;
        };
        public ipserror Study(Stream patch, ipsstudy study)
        {
            study.error = ipserror.ips_invalid;
            if (patch.Length < 8) return ipserror.ips_invalid;

            using (var patchReader = new BinaryReader(patch))
            {
                // If 'PATCH' text was not found, return IPS was invalid error.
                if (!patchReader.ReadChars(PatchText.Length).ToString().Equals(PatchText)) return ipserror.ips_invalid;

                uint offset = ReadUInt24(patchReader);
                uint outlen = 0;
                uint thisout = 0;
                uint lastoffset = 0;
                bool w_scrambled = false;
                bool w_notthis = false;

                while (offset != EndOfFile)
                {
                    int size = patchReader.ReadInt16();

                    if (size == 0)
                    {
                        thisout = offset + (uint)patchReader.ReadInt16();
                        patchReader.ReadByte();
                    }
                    else
                    {
                        thisout = offset + (uint)size;

                    }
                    if (offset < lastoffset) w_scrambled = true;
                    lastoffset = offset;
                    if (thisout > outlen) outlen = thisout;
                    if (patch.Position >= patch.Length) return ipserror.ips_invalid;

                    offset = ReadUInt24(patchReader);

                }
                study.outlen_min_mem = outlen;
                study.outlen_max = 0xFFFFFFFF;

                if (patch.Position == patch.Length)
                {
                    uint truncate = ReadUInt24(patchReader);
                    study.outlen_max = truncate;
                    if (outlen > truncate)
                    {
                        outlen = truncate;
                        w_notthis = true;
                    }

                }
                if (patch.Position != patch.Length) return ipserror.ips_invalid;

                study.error = ipserror.ips_ok;
                if (w_notthis) study.error = ipserror.ips_notthis;
                if (w_scrambled) study.error = ipserror.ips_scrambled;
                return study.error;

            }

        }
        public ipserror ApplyStudy(Stream patch, ipsstudy study, Stream inFile, Stream outFile)
        {
            throw new NotImplementedException();
            study.error = ipserror.ips_invalid;
            if (patch.Length < 8) return ipserror.ips_invalid;

        }

        //Known situations where this function does not generate an optimal patch:
        //In:  80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80 80
        //Out: FF FF FF FF FF FF FF FF 00 01 02 03 04 05 06 07 FF FF FF FF FF FF FF FF
        //IPS: [         RLE         ] [        Copy         ] [         RLE         ]
        //Possible improvement: RLE across the entire file, copy on top of that.
        //Rationale: It would be a huge pain to create such a multi-pass tool if it should support writing a byte
        //  more than twice, and I don't like half-assing stuff.


        //Known improvements over LIPS:
        //In:  00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F
        //Out: FF 01 02 03 04 05 FF FF FF FF FF FF FF FF FF FF
        //LIPS:[      Copy     ] [            RLE            ]
        //Mine:[] [ Unchanged  ] [            RLE            ]
        //Rationale: While LIPS can break early if it finds something RLEable in the middle of a block, it's not
        //  smart enough to back off if there's something unchanged between the changed area and the RLEable spot.

        //In:  FF FF FF FF FF FF FF
        //Out: 00 00 00 00 01 02 03
        //LIPS:[   RLE   ] [ Copy ]
        //Mine:[       Copy       ]
        //Rationale: Again, RLE is no good at RLE.

        //It is also known that I win in some other situations. I didn't bother checking which, though.

        //There are no known cases where LIPS wins over libips.

        public ipserror Create(Stream source, Stream target, Stream patch)
        {
            throw new NotImplementedException();

            uint sourcelen = (uint)source.Length;
            uint targetlen = (uint)target.Length;
            bool sixteenmegabytes = false;

            using (BinaryReader sourceReader = new BinaryReader(source), patchReader = new BinaryReader(patch))
            {
                using (BinaryWriter targetWriter = new BinaryWriter(target))
                {

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
                    uint offset = 0;
                    uint outbuflen = 4096;
                    uint outlen = 0;

                    targetWriter.Write(PatchText);
                    int lastknownchange = 0;
                    while (offset < targetlen)
                    {
                        source.Seek(offset, 0);
                        target.Seek(offset, 0);
                        while (offset < sourcelen && (offset < sourcelen ? source.ReadByte() : 0) == target.ReadByte()) offset++;

                        //check how much we need to edit until it starts getting similar
                        int thislen = 0;
                        int consecutiveunchanged = 0;
                        thislen = lastknownchange - (int)offset;
                        if (thislen < 0) thislen = 0;

                        while (true)
                        {
                            uint thisbyte = offset + (uint)thislen + (uint)consecutiveunchanged;
                            source.Seek(thisbyte, 0);
                            target.Seek(thisbyte, 0);
                            if (thisbyte < sourcelen && (thisbyte < sourcelen ? source.ReadByte() : 0) == target.ReadByte()) consecutiveunchanged++;
                            else
                            {
                                thislen += consecutiveunchanged + 1;
                                consecutiveunchanged = 0;
                            }
                            if (consecutiveunchanged >= 6 || thislen >= 65536) break;
                        }

                        //avoid premature EOF
                        if (offset == 0x454F46)
                        {
                            offset--;
                            thislen++;
                        }

                        lastknownchange = (int)offset + thislen;
                        if (thislen > 65535) thislen = 65535;
                        if (offset + thislen > targetlen) thislen = (int)(targetlen - offset);
                        if (offset == targetlen) continue;

                        //check if RLE here is worthwhile
                        int byteshere=0;

                        source.Seek(offset, 0);
                        target.Seek(offset + byteshere, 0);
                        for (byteshere = 0; byteshere < thislen && target.ReadByte() == target.ReadByte(); byteshere++) { }

                        if (byteshere == thislen)
                        {
                            target.Seek(offset, 0);
                            int thisbyte = target.ReadByte();
                            int i = 0;

                            while (true)
                            {
                                uint pos = offset + (uint)byteshere + (uint)i - 1;
                                source.Seek(pos, 0);
                                target.Seek(pos, 0);
                                if (pos >= targetlen || target.ReadByte() != thisbyte || byteshere + i > 65535) break;
                                if (pos >= sourcelen || (pos < sourcelen ? source.ReadByte() : 0) != thisbyte)
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

                            // TODO: Fix this: targetWriter.Write(offset);

                            // TODO: Fix below. They are little-endian.
                            // targetWriter.Write(
                            // write16(byteshere);
                            // write8(target[offset]);
                            // offset += byteshere;
                        }
                        else {
                            //check if we'd gain anything from ending the block early and switching to RLE
                             byteshere = 0;
                             int stopat = 0;
                            /* TODO: rewrite below:
                             while (stopat + byteshere < thislen)
                             {
                                 if (target[offset + stopat] == target[offset + stopat + byteshere]) byteshere++;
                                 else
                                 {
                                     stopat += byteshere;
                                     byteshere = 0;
                                 }
                                 if (byteshere > 8 + 5 || //rle-worthy despite two ips headers
                                         (byteshere > 8 && stopat + byteshere == thislen) || //rle-worthy at end of data
                                         (byteshere > 8 && !memcmp(&target[offset + stopat + byteshere], &target[offset + stopat + byteshere + 1], 9 - 1)))//rle-worthy before another rle-worthy
                                 {
                                     if (stopat) thislen = stopat;
                                     break;//we don't scan the entire block if we know we'll want to RLE, that'd gain nothing.
                                 } 
                             
                             
                             	  //don't write unchanged bytes at the end of a block if we want to RLE the next couple of bytes
			                     if (offset+thislen!=targetlen)
                                 {
                                 while (offset+thislen-1<sourcelen && target[offset+thislen-1]==(offset+thislen-1<sourcelen?source[offset+thislen-1]:0)) thislen--;
                                 }
                               			if (thislen>3 && !memcmp(&target[offset], &target[offset+1], thislen-2))
			{
				write24(offset);
				write16(0);
				write16(thislen);
				write8(target[offset]);
			}
			else
			{
				write24(offset);
				write16(thislen);
				int i;
				for (i=0;i<thislen;i++)
				{
					write8(target[offset+i]);
				}
			}
			offset+=thislen;
 
                             
                             */


                             }
                        }
                    targetWriter.Write("EOF");
                    // TODO: Rewrite this line:
                    // if (sourcelen>targetlen) write24(targetlen);

                    	// patchmem->ptr=out;
	// patchmem->len=outlen;

                    if (sixteenmegabytes) return ipserror.ips_16MB;
                    if (outlen == 8) return ipserror.ips_identical;
                    return ipserror.ips_ok;
 
                    }

                }

            }

        private uint ReadUInt24(this BinaryReader reader)
        {
            try
            {
                var b1 = reader.ReadByte();
                var b2 = reader.ReadByte();
                var b3 = reader.ReadByte();
                return
                    (((uint)b1) << 16) |
                    (((uint)b2) << 8) |
                    ((uint)b3);
            }
            catch
            {
                return 0u;
            }
        }


    }
}
