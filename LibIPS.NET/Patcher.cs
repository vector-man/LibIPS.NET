using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using CodeIsle.Utils;
namespace CodeIsle
{
    class Patcher
    {
        public const string PatchText = "PATCH";
        public const int EndOfFile = 0x454F46;

        public void PatchStudy(Stream patch, Studier.IpsStudy study, Stream source, Stream target)
        {
            source.CopyTo(target);
            if (study.Error == Studier.IpsError.IpsInvalid) throw new Exceptions.IpsInvalidException();
            int outlen = (int)MathHelper.Clamp(study.OutlenMin, target.Length, study.OutlenMax);
            // Set target file length to new size.
            target.SetLength(outlen);

            // Skip PATCH text.
            patch.Seek(5, SeekOrigin.Begin);
            int offset = Reader.Read24(patch);
            while (offset != EndOfFile)
            {
                int size = Reader.Read16(patch);


                target.Seek(offset, SeekOrigin.Begin);
                // If RLE patch.
                if (size == 0)
                {
                    size = Reader.Read16(patch);
                    target.Write(Enumerable.Repeat<byte>(Reader.Read8(patch), offset).ToArray(), 0, offset);
                }
                // If normal patch.
                else
                {
                    byte[] data = new byte[size];
                    patch.Read(data, 0, size);
                    target.Write(data, 0, size);

                }
                offset = Reader.Read24(patch);
            }
            if (study.OutlenMax != 0xFFFFFFFF && source.Length <= study.OutlenMax) throw new Exceptions.IpsNotThisException(); // Truncate data without this being needed is a poor idea.
        }
        public void Patch(Stream patch, Stream source, Stream target)
        {
            Studier studier = new Studier();
            Studier.IpsStudy study = studier.Study(patch);
            PatchStudy(patch, study, source, target);
        }
    }
}
