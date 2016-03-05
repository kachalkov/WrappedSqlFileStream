using System;
using System.IO;
using Samples.Services;

namespace Samples
{
    class Program
    {
        static void TestWrite(Guid id)
        {
            var svc = new FilesService();

            var filename = "Hemmingway_The Old Man and the Sea_1952.pdf";

            Console.WriteLine("Writing to database...");

            using (var fs = new FileStream(filename, FileMode.Open))
            {
                svc.SaveFile(filename, id, fs);
            }
        }

        static void TestRead(Guid id)
        {
            var svc = new FilesService();

            Console.WriteLine("Reading stream from database...");

            var filename_new = "Hemmingway_The Old Man and the Sea_1952_from_Database.pdf";

            using (var fs = new FileStream(filename_new, FileMode.Create))
            {
                using (var sfs = svc.GetFile(id))
                {
                    sfs.CopyTo(fs);
                }
                Console.WriteLine("Length: " + fs.Length);
            }
        }

        static void TestReadNH(Guid id)
        {
            var svc = new FilesService();

            Console.WriteLine("Reading stream from database with NHibernate mappings...");

            var filename_new = "Hemmingway_The Old Man and the Sea_1952_from_Database_NH.pdf";

            using (var fs = new FileStream(filename_new, FileMode.Create))
            {
                using (var sfs = svc.GetFileNH(id))
                {
                    sfs.CopyTo(fs);
                }
                Console.WriteLine("Length: " + fs.Length);
            }
        }

        static void TestReadDTO(Guid id)
        {
            var svc = new FilesService();

            Console.WriteLine("Reading Stream + fields from database");

            var dto = svc.GetFileDTO(id);
            Console.WriteLine("FileName: " + dto.FileName);
            Console.WriteLine("Length: " + dto.File.Length);
            dto.File.Dispose();

        }

        static void Main(string[] args)
        {
            var id = Guid.NewGuid();
            
            TestWrite(id);

            TestRead(id);

            TestReadNH(id);

            TestReadDTO(id);

            Console.WriteLine("Done");

            Console.ReadLine();
        }
    }
}
