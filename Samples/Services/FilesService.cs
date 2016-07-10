﻿using System;
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
        private string connectionstring = ConfigurationManager.ConnectionStrings["WrappedSqlFileStream"].ConnectionString;

        public ISessionFactory SessionFactory { get; protected set; }
        protected NHibernate.Cfg.Configuration configuration;
        private IMappingProvider mappingProvider;
        private WrappedSqlFileStreamContext<Files> context;

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

            mappingProvider = new DefaultMappingProvider<Files>("dbo", x => x.File);
            context = new WrappedSqlFileStreamContext<Files>(mappingProvider, connectionstring);
        }

        public void SaveFile(string filename, Guid id, Stream file)
        {
            context.CreateRow(() => new Files()
            {
                Id = id,
                FileName = filename
            });

            using (var sfs = new WrappedSqlFileStream<Files>(context, files => files.Id == id, FileAccess.Write))
            {
                file.CopyTo(sfs);
            }
        }

        public FilesDTO GetFileDTO(Guid id)
        {
            var stream = new WrappedSqlFileStream<Files>(context, files => files.Id == id, FileAccess.Read);

            return new FilesDTO()
            {
                File = stream,
                FileName = stream.RowData.FileName,
                Id = stream.RowData.Id
            };
        }

        public Stream GetFile(Guid id)
        {
            return new WrappedSqlFileStream<Files>(context, files => files.Id == id, FileAccess.Read);
        }

        public Stream GetFileNH(Guid id)
        {
            var mappingProvider = new NHibernateMappingProvider<Files>(SessionFactory, files => files.File);
            var context = new WrappedSqlFileStreamContext<Files>(mappingProvider, connectionstring);

            return new WrappedSqlFileStream<Files>(context, files => files.Id == id, FileAccess.Read);
        }

    }
}