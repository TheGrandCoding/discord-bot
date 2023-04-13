using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class FilterListService : Service
    {
        string BaseDir => Path.Combine(Program.BASE_PATH, "data", "filters");

        public string GetDirectory(ulong userId)
        {
            var folder = Path.Combine(BaseDir, userId.ToString());
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }
        public string GetFilePath(ulong userId, string filterId, string kind = "txt")
        {
            var folder = GetDirectory(userId);
            return Path.Combine(folder, Program.GetSafePath(filterId + $".{kind}"));
        }

        bool openFile(ulong userId, string filterId, bool readOnly, out FileStream stream, string kind = "txt")
        {
            try
            {
                if (readOnly)
                    stream = File.OpenRead(GetFilePath(userId, filterId, kind));
                else
                    stream = File.Open(GetFilePath(userId, filterId, kind), FileMode.Create, FileAccess.Write);
                return true;
            } catch(FileNotFoundException)
            {
                stream = null;
                return false;
            }
        }

        IEnumerable<ulong> getUserIds()
        {
            foreach(var userDir in Directory.EnumerateDirectories(BaseDir))
            {
                yield return ulong.Parse(Path.GetFileName(userDir));
            }
        }

        public bool TryReadTemplate(ulong userId, string filterId, out FileStream fs)
            => openFile(userId, filterId, true, out fs, "template");
        public bool TryWriteTemplate(ulong userId, string filterId, out FileStream fs)
            => openFile(userId, filterId, false, out fs, "template");
        public bool TryOpenRead(string filterId, out FileStream fs)
        {
            foreach(var userId in getUserIds())
            {
                if (TryOpenRead(userId, filterId, out fs))
                    return true;
            }
            fs = null;
            return false;
        }
        public bool TryOpenRead(ulong userId, string filterId, out FileStream stream)
            => openFile(userId, filterId, true, out stream);
        public bool TryOpenWrite(ulong userId, string filterId, out FileStream stream)
            => openFile(userId, filterId, false, out stream);

        public bool TryCreateNew(ulong userId, out string id, out FileStream fs)
        {
            bool found = true;
            id = null;
            while(found)
            {
                id = Classes.PasswordHash.RandomToken(32);
                foreach(var otherId in getUserIds())
                { // verify file ID is unique accross all users
                    found = File.Exists(GetFilePath(otherId, id));
                    if (found) break;
                }
            }
            return TryOpenWrite(userId, id, out fs);
        }
    
        public bool TryDelete(ulong userId, string filterId)
        {
            var path = GetFilePath(userId, filterId);
            try
            {
                File.Delete(path);
                return true;
            } catch(FileNotFoundException)
            {
                return true; // didn't exist to begin with, so not there
            } catch(Exception ex)
            {
                Error(ex);
                return false;
            }
        }
    }


}
