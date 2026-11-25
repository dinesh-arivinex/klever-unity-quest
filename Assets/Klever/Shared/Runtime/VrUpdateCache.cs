using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Klever.VR.Updates
{
    public static class VrUpdateCache
    {
        // Preserve support evidence, but do not count manifests/old failures as
        // active downloads. This directory contains updater files only.
        public static string ArchiveInactive(string folder, string partial, string completed)
        {
            folder = Path.GetFullPath(folder);
            partial = Path.GetFullPath(partial); completed = Path.GetFullPath(completed);
            if (Path.GetDirectoryName(partial) != folder || Path.GetDirectoryName(completed) != folder)
                throw new InvalidDataException("Update cache paths must stay inside the updater directory.");
            var files = Directory.GetFiles(folder);
            if (files.Length < 4) return null;
            string archive = null;
            foreach (var file in files)
            {
                if (file == partial || file == completed || file == completed + ".manifest.json") continue;
                var name = Path.GetFileName(file);
                if (!Regex.IsMatch(name, @"^(?:[0-9]+-[a-f0-9]{32}\.partial|[a-f0-9]{64}\.(?:apk|partial)(?:\.[a-f0-9]{32}\.rejected)?(?:\.manifest\.json)?)$")) continue;
                if (archive == null)
                {
                    archive = Path.Combine(folder, "retained", DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(archive);
                }
                File.Move(file, Path.Combine(archive, name));
            }
            return archive;
        }
    }
}
