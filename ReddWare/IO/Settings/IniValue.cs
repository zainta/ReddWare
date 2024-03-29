﻿// Copyright (c) Zain Al-Ahmary.  All rights reserved.
// Licensed under the MIT License, (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at https://mit-license.org/

using System.Text.RegularExpressions;

namespace ReddWare.IO.Settings
{
    /// <summary>
    /// Represents a value in an ini file
    /// </summary>
    public class IniValue : IniItemBase
    {
        /// <summary>
        /// The regular expression used to match values and extract their names and values
        /// </summary>
        internal const string Value_Match_Regex = @"^([^=\n]+)=(.+)";

        /// <summary>
        /// The value
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// The default value (only relevent for schema ini files)
        /// </summary>
        public string DefaultValue { get; private set; }

        /// <summary>
        /// Creates a value item with the given parent section
        /// </summary>
        /// <param name="parent">The parent subsection</param>
        /// <param name="value">The initial value</param>
        /// <param name="defaultValue">The default value</param>
        /// <param name="label">The item's name</param>
        public IniValue(string label, string value = null, string defaultValue = null, IniSubsection parent = null) : base(label, parent)
        {
            Value = value == null ? defaultValue : value;
            DefaultValue = defaultValue;
        }

        /// <summary>
        /// Verifies path and then attempts to interpret the given line of text
        /// </summary>
        /// <param name="path">The current path</param>
        /// <param name="line">The line of text to interpret</param>
        /// <returns>True if successful, false otherwise</returns>
        public override bool Read(string path, string line)
        {
            if (path != GetPath(false)) return false;

            var match = Regex.Match(line, Value_Match_Regex);
            if (match.Groups.Count == 3 && match.Groups[1].Value == Label)
            {
                // Get the value
                Value = match.Groups[2].Value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts the ini value to a line of text to be written to disk
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Label}={Value}";
        }

        /// <summary>
        /// Returns a deep clone of the IniSubsection (omitting parenting structure)
        /// </summary>
        /// <returns>The cloned instance</returns>
        public override object Clone()
        {
            return new IniValue(Label, Value, DefaultValue);
        }
    }
}
