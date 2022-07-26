// Copyright (c) Zain Al-Ahmary.  All rights reserved.
// Licensed under the MIT License, (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at https://mit-license.org/

using System.Diagnostics.CodeAnalysis;

namespace ReddWare.IO.Disk
{
    /// <summary>
    /// Compares two strings as paths, determining which ones is furthest into the structure
    /// </summary>
    class PathDependencyDepthComparer : IComparer<string>
    {
        public int Compare(string? p1, string? p2)
        {
            var first = string.IsNullOrWhiteSpace(p1) ? 0 : PathHelper.GetDependencyCount(p1, File.Exists(p1));
            var second = string.IsNullOrWhiteSpace(p2) ? 0 : PathHelper.GetDependencyCount(p2, File.Exists(p2));

            if (first < second)
            {
                return -1;
            }
            else if (first == second)
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }
    }
}

