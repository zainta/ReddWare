// Copyright (c) Zain Al-Ahmary.  All rights reserved.
// Licensed under the MIT License, (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at https://mit-license.org/

using ReddWare.Threading;
using System.Collections.Concurrent;

namespace ReddWare.IO.Disk
{
    /// <summary>
    /// Utility class that provides path related static methods
    /// </summary>
    public abstract class PathHelper
    {
        /// <summary>
        /// Takes a path and returns the paths between it and the root
        /// </summary>
        /// <param name="path">The path to expand</param>
        /// <returns>The expanded set of paths</returns>
        public static IEnumerable<string> AnchorPath(string path)
        {
            var results = new List<string>();
            DirectoryInfo dir = null;
            if (File.Exists(path))
            {
                var f = new FileInfo(path);
                dir = f.Directory;
            }
            else if (Directory.Exists(path))
            {
                dir = new DirectoryInfo(path);
            }

            while (dir != null && dir.Parent != null)
            {
                if (dir.Parent != null)
                {
                    results.Add(dir.Parent.FullName);
                    dir = dir.Parent;
                }
            }

            return results;
        }

        /// <summary>
        /// Takes a path and, if it contains text, ensures that it ends in a forward slash (\)
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <param name="ignoreNonExistent">If true, then EnsurePath will return the value unmodified if the target does not exist</param>
        /// <returns></returns>
        public static string EnsurePath(string path, bool ignoreNonExistent = false)
        {
            var result = path;
            if (!string.IsNullOrWhiteSpace(path))
            {
                switch (IsDirectory(path))
                {
                    case DiskItemStatus.File:
                        // files need to end in no forward slash
                        if (EndsInDirSeperator(path))
                        {
                            result = path.Substring(0, path.Length - 1);
                        }
                        break;
                    case DiskItemStatus.Directory:
                        // directories need to end in a forward slash (\)
                        if (!EndsInDirSeperator(path))
                        {
                            result = $"{path}{Path.DirectorySeparatorChar}";
                        }
                        break;
                    case DiskItemStatus.NonExistent:
                        if (!ignoreNonExistent)
                        {
                            result = null;
                        }
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Checks to see if the given string ends in one of the accepted directory seperators
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns></returns>
        public static bool EndsInDirSeperator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Takes a set of paths and, if it contains text, ensures that they end in forward slashes (\)
        /// </summary>
        /// <param name="paths">The paths to check</param>
        /// <param name="ignoreNonExistent">If true, then EnsurePath will return the value unmodified if the target does not exist</param>
        /// <returns></returns>
        public static string[] EnsurePath(IEnumerable<string> paths, bool ignoreNonExistent = false)
        {
            var result = paths.Select(p => EnsurePath(p, ignoreNonExistent)).Where(p => p != null);
            return result.ToArray();
        }

        /// <summary>
        /// Checks to see if the given path is a file or directory
        /// </summary>
        /// <param name="path">The patch to check</param>
        /// <returns></returns>
        public static DiskItemStatus IsDirectory(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory ? DiskItemStatus.Directory : DiskItemStatus.File;
            }
            catch (Exception ex)
            {
                return DiskItemStatus.NonExistent;
            }
        }

        /// <summary>
        /// Takes a disk item type and a dependency level sorted list dictionary and adds the item to the correct dependency level list
        /// </summary>
        /// <param name="target">The dictionary to add to</param>
        /// <param name="item">The item to add it to</param>
        /// <param name="isFile">If the path is a file or directory</param>
        private static void SafelyAdd(ConcurrentDictionary<int, List<string>> target, string path, bool isFile)
        {
            var level = GetDependencyCount(path, isFile);
            target.TryAdd(level, new List<string>());
            target[level].Add(path);
            target[level].Sort(new PathDependencyDepthComparer());
        }

        /// <summary>
        /// Returns the number of levels from the root the given path is (including the root)
        /// Adds 1 for files
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <param name="isFile">If the path is a file or directory</param>
        /// <returns>The number of dependencies</returns>
        public static int GetDependencyCount(string path, bool isFile)
        {
            var primCount = path.Where(c => c == Path.DirectorySeparatorChar).Count();
            var secCount = path.Where(c => c == Path.AltDirectorySeparatorChar).Count();

            if (isFile)
            {
                return primCount + secCount;
            }

            return primCount + secCount - 1;
        }

        /// <summary>
        /// Case insensitively compares paths to check if the query is or is within the container
        /// </summary>
        /// <param name="query">The path to check the status of</param>
        /// <param name="container">The path the query is checked against</param>
        /// <param name="acceptDuplicates">Whether or not to return true on duplicate paths</param>
        /// <returns>True if it's inside, false otherwise</returns>
        public static bool IsWithinPath(string query, string container, bool acceptDuplicates = true)
        {
            // Ensure that the two paths end in slashes
            if (!EndsInDirSeperator(query)) query = query + Path.DirectorySeparatorChar;
            if (!EndsInDirSeperator(container)) container = container + Path.DirectorySeparatorChar;

            if (Uri.IsWellFormedUriString(query, UriKind.RelativeOrAbsolute) ||
                Uri.IsWellFormedUriString(container, UriKind.RelativeOrAbsolute))
            {
                return false;
            }

            // For case insensitivity
            var q = query.ToLower();
            var c = container.ToLower();

            // Check to see if they are duplicates
            if (q == c) return acceptDuplicates ? true : false;

            // Check their status
            var uri = new Uri(c);
            return uri.IsBaseOf(new Uri(q));
        }

        /// <summary>
        /// Case insensitively compares paths to check if the query is or is within any of the containers
        /// </summary>
        /// <param name="query">The path to check the status of</param>
        /// <param name="containers">The paths the query is checked against</param>
        /// <param name="acceptDuplicates">Whether or not to return true on duplicate paths</param>
        /// <returns>True if it's inside, false otherwise</returns>
        public static bool IsWithinPaths(string query, IEnumerable<string> containers, bool acceptDuplicates = true)
        {
            var any =
                (from c in containers
                 where IsWithinPath(query, c, acceptDuplicates) == true
                 select "Yes").Any();

            return any;
        }

        /// <summary>
        /// Produces a flattened, hierarchically ordered list of paths from a folder and its subfolders
        /// </summary>
        /// <param name="paths">The paths to enumerate</param>
        /// <param name="maxThreads">The maximum number of root threads to scan the disk with</param>
        /// <returns>Returns the sorted items (FileInfo and DirectoryInfo instances)</returns>
        public static FileSystemInfo[] GetDirectoryStructureContentByDepth(IEnumerable<string> paths, int maxThreads = 4)
        {
            var result = new List<FileSystemInfo>();

            // make sure our directories correctly end in forward slashes (\) and remove any bad paths
            paths = EnsurePath(paths);

            var allFiles = new ConcurrentDictionary<int, List<string>>();
            var allDirectories = new ConcurrentDictionary<int, List<string>>();
            var unlistables = new ConcurrentBag<string>();
            Func<string, ConcurrentDictionary<int, List<string>>, ConcurrentDictionary<int, List<string>>, bool> recursor = null;
            recursor = (path, files, directories) =>
            {
                if (Directory.Exists(path))
                {
                    // used for the purpose of tracking what we were scanning when the exception occurred
                    bool isFile = false;
                    try
                    {
                        var outcome = true;
                        Parallel.ForEach(Directory.GetDirectories(path).Select(p => $"{p}\\"),
                            (dir) =>
                            {
                                outcome = recursor(dir, files, directories);
                            });

                        if (outcome)
                        {
                            isFile = true;
                            foreach (var file in Directory.GetFiles(path))
                            {
                                SafelyAdd(files, file, true);
                            };
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        int start = ex.Message.IndexOf("'") + 1, end = ex.Message.LastIndexOf("'") - 1;
                        var unlistablePath = ex.Message.Substring(start, end - start);
                        unlistables.Add(unlistablePath);

                        return false;
                    }

                    var ensured = EnsurePath(path);
                    if (ensured != null)
                    {
                        SafelyAdd(directories, ensured, false);
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            };

            // figure out how many unique lettered media (disks, cd drives, etc) are getting scanned.
            // this is the maximum number of threads we want to use
            var roots = new List<string>();
            foreach (var path in paths)
            {
                string root = null;
                if (EndsInDirSeperator(path))
                {
                    root = new DirectoryInfo(path).Root.FullName;
                }
                else
                {
                    root = new FileInfo(path).Directory.Root.FullName;
                }

                if (!roots.Contains(root))
                {
                    roots.Add(root);
                }
            }
            var threads = roots.Count <= maxThreads ? roots.Count : maxThreads;
            if (threads == 0)
            {
                return new FileSystemInfo[] { };
            }

            // Figure out how many unique targets are getting scanned.
            // This eliminates paths within paths, ensuring we will not get any duplicates
            var uniqueTargets = new List<string>();
            foreach (var path in paths.OrderBy(p => p.Length))
            {
                if (uniqueTargets.Count > 0)
                {
                    if (!IsWithinPaths(path, uniqueTargets))
                    {
                        uniqueTargets.Add(path);
                    }
                }
                else
                {
                    uniqueTargets.Add(path);
                }
            }

            var tq = new ThreadedQueue<string>(
                (path) =>
                {
                    if (File.Exists(path))
                    {
                        SafelyAdd(allFiles, path, true);
                    }
                    else if (Directory.Exists(path))
                    {
                        recursor(path, allFiles, allDirectories);
                    }
                }, 1);
            tq.Start(uniqueTargets);
            tq.WaitAll();

            // the directories must reach all the way to the root.
            // check to see if each path parameter has 0 dependencies (i.e is a root path)
            // if it isn't, then add its dependencies (this will include the root)
            foreach (var path in paths)
            {
                var anchors = AnchorPath(path)
                    .Select((t) =>
                    {
                        t = EnsurePath(t);
                        if (string.IsNullOrWhiteSpace(t))
                        {
                            return null;
                        }

                        return t;
                    })
                    .Where(t => !string.IsNullOrWhiteSpace(t));
                if (anchors.Count() > 0)
                {
                    var allDirs = new List<string>();
                    foreach (var key in allDirectories.Keys)
                    {
                        allDirs.AddRange(allDirectories[key]);
                    }

                    var nonDuplicates = anchors.Where(ap => !allDirs.Where(d => d == ap).Any());
                    foreach (var item in nonDuplicates)
                    {
                        SafelyAdd(allDirectories, item, true);
                    }
                }
            }

            // now that we have everything sorted and ready to go, merge the groups with the directories first
            foreach (var dSet in (from dSet in allDirectories select (from d in dSet.Value select new DirectoryInfo(d))))
            {
                result.AddRange(dSet);
            }

            foreach (var fSet in (from fSet in allFiles select (from f in fSet.Value select new FileInfo(f))))
            {
                result.AddRange(fSet);
            }

            return result.ToArray();
        }
    }
}
