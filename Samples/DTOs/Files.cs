using System;
using System.IO;

namespace Samples.DTOs
{
    public class FilesDTO
    {
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public Stream File { get; set; }
    }
}