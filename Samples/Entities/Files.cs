using System;

namespace Samples.Entities
{
    public class Files
    {
        public virtual Guid Id { get; set; }
        public virtual string FileName { get; set; }
        public virtual byte[] File { get; set; }
    }
}