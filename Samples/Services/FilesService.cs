using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Mapping.ByCode;
using Samples.DTOs;
using Samples.Entities;
using WrappedSqlFileStream;
using WrappedSqlFileStream.Mapping;
using WrappedSqlFileStream.Mapping.NHibernate;

namespace Samples.Services
{
    public class FilesService
    {
        private readonly string _connectionstring = ConfigurationManager.ConnectionStrings["WrappedSqlFileStream"].ConnectionString;

        public ISessionFactory SessionFactory { get; protected set; }
        protected NHibernate.Cfg.Configuration configuration;
        private readonly IMappingProvider _mappingProvider;

        public FilesService()
        {
            configuration = new NHibernate.Cfg.Configuration();

            configuration.BeforeBindMapping += (sender, args) => args.Mapping.autoimport = false;

            configuration.DataBaseIntegration(x =>
            {
                x.ConnectionStringName = "WrappedSqlFileStream";
                x.Driver<SqlClientDriver>();
                x.Dialect<MsSql2012Dialect>();
            });

            var mapper = new ModelMapper();
            mapper.AddMappings(Assembly.GetAssembly(typeof(Files)).GetExportedTypes());
            configuration.AddMapping(mapper.CompileMappingForAllExplicitlyAddedEntities());
            SessionFactory = configuration.BuildSessionFactory();

            _mappingProvider = new DefaultMappingProvider<Files>("dbo", x => x.File);
        }

        public void SaveFile(string filename, Guid id, Stream file)
        {
            var context = new WrappedSqlFileStreamContext<Files>(_mappingProvider, _connectionstring);
            using (var sfs = new WrappedSqlFileStream<Files>(
                context, 
                files => files.Id == id, 
                FileMode.Create, 
                FileAccess.Write,
                () => new Files()
                {
                    Id = id,
                    FileName = filename
                })
            )
            {
                file.CopyTo(sfs);
            }
        }

        public FilesDTO GetFileDTO(Guid id)
        {
            var context = new WrappedSqlFileStreamContext<Files>(_mappingProvider, _connectionstring);
            var stream = new WrappedSqlFileStream<Files>(context, files => files.Id == id, FileMode.Open, FileAccess.Read);

            return new FilesDTO()
            {
                File = stream,
                FileName = stream.RowData.FileName,
                Id = stream.RowData.Id
            };
        }

        public Stream GetFile(Guid id)
        {
            var context = new WrappedSqlFileStreamContext<Files>(_mappingProvider, _connectionstring);
            return new WrappedSqlFileStream<Files>(context, files => files.Id == id, FileMode.Open, FileAccess.Read);
        }

        public Stream GetFileNH(Guid id)
        {
            var nhMappingProvider = new NHibernateMappingProvider<Files>(SessionFactory, files => files.File);
            var context = new WrappedSqlFileStreamContext<Files>(nhMappingProvider, _connectionstring);

            return new WrappedSqlFileStream<Files>(context, files => files.Id == id, FileMode.Open, FileAccess.Read);
        }

    }
}