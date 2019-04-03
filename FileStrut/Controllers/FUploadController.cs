using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;
using TickToolCloud.Data.Data;
using TickToolCloud.Entities.Entity;

namespace FileStrut.Controllers
{
    [Route("FUpload")]
    public class FUploadController : Controller
    {
        private IConfiguration Configuration { get; }
        private readonly CloudContext _context;

        private const string root = "C:/Users/shsh/Storage/";

        public FUploadController(CloudContext context, IConfiguration configuration)
        {
            _context = context;
            Configuration = configuration;
            createFolderStructure();
        }

        private async void createFolderStructure()
        {
            const string defaultFolderName = "000";
            string counter = "";
            int parentFolderId = 1;
            string partialPath = root + defaultFolderName;
            Track.level = 0;

            for (int i = 0; i < 3; i++)
            {
                counter += defaultFolderName + "/";

                if (!Directory.Exists(root + counter))
                {
                    if (i == 0)
                    {
                        Folder Pholder = new Folder()
                        {
                            Id = 0,
                            FolderName = defaultFolderName,
                            DateCreated = DateTime.UtcNow,
                            Location = Path.GetRelativePath(root, partialPath),
                            ParentFolderId = null,
                        };

                        await _context.Folders.AddAsync(Pholder);
                        _context.SaveChanges();

                        Directory.CreateDirectory(partialPath);

                    }
                    else
                    {
                        Folder parentFolder = await _context.Folders.LastOrDefaultAsync();
                        parentFolderId++;
                        string path = root + "/" + parentFolder.Location + "/" + defaultFolderName;

                        Folder Pholder = new Folder()
                        {
                            Id = 0,
                            FolderName = defaultFolderName,
                            DateCreated = DateTime.UtcNow,
                            Location = Path.GetRelativePath(root, path),
                            ParentFolderId = parentFolder.Id,
                        };
                        Directory.CreateDirectory(path);
                        await _context.Folders.AddAsync(Pholder);
                        _context.SaveChanges();
                    }
                }
                Track.level++;
            }
            Track.FolderLevels = root + counter;
        }

        [HttpPost("up")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Uploading(IFormFile file)
        {
            string fullPath = "";
            int folderCount = Track.level;

            //store the file
            FileMaster duplicateFiles = await _context.Files.Include(p => p.FileIterations).FirstOrDefaultAsync(p => p.FileName == file.FileName);
          
            //Folder getLastFolder = await _context.Folders.LastOrDefaultAsync();
            //Folder parentFolder = await _context.Folders.FirstOrDefaultAsync(p => p.Id == getLastFolder.Id);

            if (duplicateFiles != null)
            {
                string dupliPath = root + duplicateFiles.Location + "/" + duplicateFiles.FileName;
                fullPath = formatthePath(dupliPath);
            }

            if (file.Length > 0)
            {
                if (duplicateFiles == null)
                {
                    return new ObjectResult(await upNewFile(file));
                }
                else
                {
                    Stream stream = file.OpenReadStream();
                    CheckSum checkSum = new CheckSum();

                    if (duplicateFiles.FileIterations.Any(f => f.IterationCheckSum == checkSum.CalcCRC32(stream)))
                    {
                        return new ObjectResult("Same file already exists!");
                    }
                    else
                    {
                        FileMaster fi = await updateFile(stream, checkSum, file, duplicateFiles, fullPath);
                        return Ok();

                    }
                }
            }
            else
            {
                return new ObjectResult("Empty file Choosen!");
            }
        }

        private async Task<IActionResult> upNewFile(IFormFile file)
        {
            string fname = "";
            Stream stream1 = file.OpenReadStream();
            CheckSum objCheckSum = new CheckSum();

            int leng = Track.FolderLevels.Length;

            for (int i = 0; i < 3; i++)
            {
                fname = i + "";

                if (fname.Length < 2)
                    fname = "00" + i;
                else if (fname.Length < 3)
                    fname = "0" + i;
                else if (fname.Length < 4)
                    fname = i + "";

                if (!Directory.Exists(Track.FolderLevels + fname))
                    break;
                if (i == 2)
                {
                    if (leng == 34) {
                        Track.FolderLevels = Track.FolderLevels.Remove(30);
                        i = 0;
                        leng = Track.FolderLevels.Length;
                    }
                    else if (leng == 30) {
                        Track.FolderLevels = Track.FolderLevels.Remove(26);
                        i = 0;
                        leng = Track.FolderLevels.Length;
                    }
                    else if (leng == 26) {
                        Track.FolderLevels = Track.FolderLevels.Remove(22);
                        i = 0;
                        leng = Track.FolderLevels.Length;
                    }else if (leng < 26)
                    {
                        return new ObjectResult("Storage is Full!");
                    }
                }
            }

            string nroot = Track.FolderLevels + fname;

            if (!Directory.Exists(nroot))
            {
                Folder parentfolder = await _context.Folders.LastOrDefaultAsync();
                int parentid = 0;
                if (parentfolder.Id == 3)
                {
                    parentid = (int)parentfolder.Id;
                }
                else
                {
                    parentid = (int)parentfolder.ParentFolderId;
                }

                Folder pholder = new Folder()
                {
                    Id = 0,
                    FolderName = fname,
                    DateCreated = DateTime.UtcNow,
                    Location = Path.GetRelativePath(root, nroot),
                    ParentFolderId = parentid,
                };
                EntityEntry<Folder> entry1 = await _context.Folders.AddAsync(pholder);
                _context.SaveChanges();
                Directory.CreateDirectory(nroot);
            }

            Folder uploadingfolder = _context.Folders.LastOrDefault();
            int folId = uploadingfolder.Id;


            FileMaster theFile = new FileMaster
            {
                Id = 0,
                FileName = Path.GetFileName(file.FileName),
                Name = Path.GetFileNameWithoutExtension(file.FileName),
                ClientFilePath = Path.GetFullPath(file.FileName),
                DateUploaded = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow,
                Location = ((await _context.Folders.FirstOrDefaultAsync(p => p.Id == folId)).Location),
                Type = Path.GetExtension(file.FileName),
                Size = file.Length,
                VersionNumber = 0,
                Folder = await _context.Folders.FirstOrDefaultAsync(p => p.Id == folId),
                FileIterations = new List<FileIteration>()
            };

            EntityEntry<FileMaster> entry = await _context.Files.AddAsync(theFile);
            _context.SaveChanges();

            string stName = entry.Entity.Id + "_" + entry.Entity.VersionNumber + Path.GetExtension(file.FileName);
            string stPath = Track.FolderLevels + uploadingfolder.FolderName + "/" + stName;
            FileIteration iter = new FileIteration
            {
                Id = 0,
                FileMasterId = entry.Entity.Id,
                IterationCheckSum = objCheckSum.CalcCRC32(stream1),
                IterationName = stName,
                IterationPath = stPath
            };
            EntityEntry<FileIteration> entry2 = await _context.FileIterations.AddAsync(iter);
            _context.SaveChanges();
            theFile.FileIterations.Add(entry2.Entity);

            //when the version of file is updated
            EntityEntry<FileMaster> entry3 = _context.Files.Update(theFile);
            _context.SaveChanges();
            theFile = entry3.Entity;

            using (var stream = new FileStream(stPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok();
        }

        private async Task<FileMaster> updateFile(Stream stream, CheckSum checkSum, IFormFile file, FileMaster duplicateFiles, string path)
        {
            FileIteration iteration = new FileIteration
            {
                Id = 0,
                FileMasterId = duplicateFiles.Id,
                IterationCheckSum = checkSum.CalcCRC32(stream),

            };

            EntityEntry<FileIteration> entity = await _context.FileIterations.AddAsync(iteration);
            _context.SaveChanges();

            duplicateFiles.DateUpdated = DateTime.UtcNow;
            duplicateFiles.Size = stream.Length;
            duplicateFiles.VersionNumber = duplicateFiles.VersionNumber + 1;
            duplicateFiles.FileIterations.Add(entity.Entity);

            EntityEntry<FileMaster> entry2 = _context.Files.Update(duplicateFiles);
            _context.SaveChanges();
            duplicateFiles = entry2.Entity;
            string newFileName = duplicateFiles.Id + "_" + duplicateFiles.VersionNumber + Path.GetExtension(path);
            string thenPath = root + "/" + duplicateFiles.Location + "/" + newFileName;
            string newFilePath = formatthePath(thenPath);

            entity.Entity.IterationName = newFileName;
            entity.Entity.IterationPath = newFilePath;

            EntityEntry<FileIteration> entry3 = _context.FileIterations.Update(entity.Entity);
            _context.SaveChanges();

            //iteration = entry3.Entity;

            using (FileStream fileStream = System.IO.File.Create(newFilePath))
            {
                await stream.CopyToAsync(fileStream);
            }
            await _context.SaveChangesAsync();

            return entry2.Entity;
        }

        private string formatthePath(string path)
        {
            string rePairs = "";
            string repaired = "";
            for (int i = 0; i < path.Length; i++)
            {
                rePairs = path[i].ToString();
                repaired += rePairs.Replace("\\", "/");
            }
            return repaired;
        }
    }
}