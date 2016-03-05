using NHibernate.Mapping.ByCode.Conformist;
using Samples.Entities;

namespace Samples.Services
{
    public class FilesMap : ClassMapping<Files>
    {
        public FilesMap()
        {
            Id(x => x.Id);
            Property(x => x.FileName);
            Property(x => x.File);
        }
    }
}