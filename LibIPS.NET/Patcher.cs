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

        public void PatchStudy(MemoryStream patch, Studier.IpsStudy study, MemoryStream source, MemoryStream target)
        {
            PatchStudy((Stream)patch, study, (Stream)source, (Stream)target);
        }
        public void PatchStudy(string patch, Studier.IpsStudy study, string source, string target)
        {
            using (FileStream patchStream = File.OpenRead(patch), sourceStream = File.OpenRead(source), targetStream = File.Open(target, FileMode.Create))
            {
                PatchStudy(patchStream, study, sourceStream, targetStream);
            }
        }
        public void PatchStudy(FileStream patch, Studier.IpsStudy study, FileStream source, FileStream target)
        {
            PatchStudy((Stream)patch, study, (Stream)source, (Stream)target);
        }
        public void PatchStudy(Stream patch, Studier.IpsStudy study, Stream source, Stream target)
        {
            source.CopyTo(target);
            if (study.Error == Studier.IpsError.IpsInvalid) throw new Exceptions.IpsInvalidException();
            int outlen = (int)Clamp(study.OutlenMin, target.Length, study.OutlenMax);
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
        public void Patch(MemoryStream patch, MemoryStream source, MemoryStream target)
        {
            Patch((Stream)patch, (Stream)source, (Stream)target);
        }
        public void Patch(string patch, string source, string target)
        {
            using (FileStream patchStream = File.OpenRead(patch), sourceStream = File.OpenRead(source), targetStream = File.Open(target, FileMode.Create))
            {
                Patch(patchStream, sourceStream, targetStream);
            }
        }
        public void Patch(FileStream patch, FileStream source, FileStream target)
        {
            Patch((Stream)patch, (Stream)source, (Stream)target);
        }
        public void Patch(Stream patch, Stream source, Stream target)
        {
            Studier studier = new Studier();
            Studier.IpsStudy study = studier.Study(patch);
            PatchStudy(patch, study, source, target);
        }
        private static long Clamp(long value, long minimum, long maximum)
        {
            return (value < minimum) ? minimum : (value > maximum) ? maximum : value;
        }
    }
}
