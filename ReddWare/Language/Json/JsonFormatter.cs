﻿// Copyright (c) Zain Al-Ahmary.  All rights reserved.
// Licensed under the MIT License, (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at https://mit-license.org/

using ReddWare.Collections;
using System.Text;

namespace ReddWare.Language.Json
{
    /// <summary>
    /// A static class that formats the provided json
    /// </summary>
    class JsonFormatter
    {
        private static ListStack<char> _buffer;
        private static int _indentation;
        private static string _indentNotation;
        private static char? _lastChar;

        /// <summary>
        /// Formats the json string for easy viewing
        /// </summary>
        /// <param name="json">The json string to format</param>
        /// <param name="indentation">The character(s) to use for indentation</param>
        /// <returns>The formatted string</returns>
        public static string Format(string json, string indentation = "  ")
        {
            _indentNotation = indentation;
            _indentation = 0;
            _buffer = new ListStack<char>(json);
            var result = new StringBuilder();
            var lastWasStructure = false;
            _lastChar = null;

            // loop through the string and break it down into formatted json in one pass
            while (More())
            {

                if (Peek() == '[')
                {
                    if (_lastChar == ':' && PeekNonWhiteSpace() != ']')
                    {
                        result.Append("\n");
                        result.Append(GetIndent());
                    }
                    result.Append(Pop());

                    _indentation++;
                    if (Peek() != ']' && PeekNonWhiteSpace() != ']')
                    {
                        result.Append("\n");
                        result.Append(GetIndent());
                    }

                    lastWasStructure = true;
                }
                else if (Peek() == '{')
                {
                    if (_lastChar != ',' && _lastChar != '[' && _lastChar != null)
                    {
                        result.Append("\n");
                        result.Append(GetIndent());
                    }
                    result.Append(Pop());

                    _indentation++;
                    result.Append("\n");
                    result.Append(GetIndent());

                    lastWasStructure = true;
                }
                else if (Peek() == ']' || Peek() == '}')
                {
                    _indentation--;
                    if (!(Peek() == ']' && _lastChar == '['))
                    {
                        result.Append("\n");
                        result.Append(GetIndent());
                    }
                    result.Append(Pop());

                    lastWasStructure = true;
                }
                else if (char.IsWhiteSpace(Peek()))
                {
                    // eat whitespace because we're adding our own
                    Pop();

                    lastWasStructure = false;
                }
                else if (Peek() == ':')
                {
                    result.Append(Pop());

                    lastWasStructure = false;
                }
                else if (Peek() == ',')
                {
                    result.Append(Pop());
                    result.Append("\n");
                    result.Append(GetIndent());

                    lastWasStructure = true;
                }
                else if (Peek() == '\'' || Peek() == '"')
                {
                    result.Append(GetString());

                    lastWasStructure = false;
                }
                else if (char.IsDigit(Peek()) || Peek() == '-')
                {
                    result.Append(GetNumber());

                    lastWasStructure = false;
                }
                else if (More(4) && PeekStr(0, 4).ToLower() == "null")
                {
                    result.Append(PopStr(0, 4));

                    lastWasStructure = false;
                }
                else
                {

                }
            }

            return result.ToString();
        }

        #region Character Control

        /// <summary>
        /// Peeks at the next character
        /// </summary>
        /// <param name="offset">The offset from the 0 position to peek at</param>
        /// <returns>The character at the given position</returns>
        private static char Peek(int offset = 0)
        {
            return _buffer.Peek(offset);
        }

        /// <summary>
        /// Returns the next non-whitespace character ahead of the current position, no matter how far
        /// </summary>
        /// <returns></returns>
        private static char? PeekNonWhiteSpace()
        {
            char? result = null;
            var index = 1;
            while (More(index) && char.IsWhiteSpace(Peek(index)))
            {
                index++;
            }

            if (More(index) && !char.IsWhiteSpace(Peek(index)))
            {
                result = Peek(index);
            }

            return result;
        }

        /// <summary>
        /// Removes and retursn the next character
        /// </summary>
        /// <returns>The next character</returns>
        private static char Pop()
        {
            var c = _buffer.Pop();
            if (!char.IsWhiteSpace(c))
            {
                _lastChar = c;
            }
            return c;
        }

        /// <summary>
        /// Returns the range of characters starting at the given index
        /// </summary>
        /// <param name="offset">The start index</param>
        /// <param name="length">The number of characters</param>
        /// <returns>The characters (as a string) found at the given location</returns>
        /// <exception cref="IndexOutOfRangeException"/>
        private static string PeekStr(int offset = 0, int length = 1)
        {
            StringBuilder sb = new StringBuilder();
            var terminationPoint = offset + (length - 1);
            for (int i = offset; i <= terminationPoint; i++)
            {
                sb.Append(_buffer.Peek(i));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Removes length characters, beginning at the offset, and returns them
        /// </summary>
        /// <returns>The characters</returns>
        /// <param name="length">The number of characters to return</param>
        /// <param name="offset">The index to start from</param>
        /// <exception cref="IndexOutOfRangeException"/>
        private static string PopStr(int offset = 0, int length = 1)
        {
            StringBuilder sb = new StringBuilder();
            var terminationPoint = offset + (length - 1); ;
            for (int i = offset; i <= terminationPoint; i++)
            {
                sb.Append(_buffer.Pop());
            }

            _lastChar = sb[sb.Length - 1];
            return sb.ToString();
        }

        /// <summary>
        /// Checks to see if the remaining number of characters meets or exceeds the minimum
        /// </summary>
        /// <param name="minimum">The minimum number of characters required</param>
        /// <returns></returns>
        private static bool More(int minimum = 1)
        {
            return _buffer.Count >= minimum;
        }

        #endregion

        /// <summary>
        /// Returns an indentation string
        /// </summary>
        /// <returns></returns>
        private static string GetIndent()
        {
            return string.Concat(Enumerable.Repeat<string>(_indentNotation, _indentation));
        }

        /// <summary>
        /// Retrieves and returns a string
        /// </summary>
        /// <returns>The resulting string or null</returns>
        private static string GetString()
        {
            var result = new StringBuilder();
            var border = ' ';

            if (Peek() == '"')
            {
                border = '"';
            }
            else if (Peek() == '\'')
            {
                border = '\'';
            }
            else
            {
                return null;
            }
            result.Append(Pop());

            var done = false;
            while (More() && !done)
            {
                if (Peek() == '\\')
                {
                    if (More(2))
                    {
                        result.Append(PopStr(0, 2));
                    }
                    else
                    {
                        result.Append(Pop());
                    }
                }
                else if (Peek() == border)
                {
                    done = true;
                    result.Append(Pop());
                }
                else
                {
                    result.Append(Pop());
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Retrievse and returns a decimal or whole number
        /// </summary>
        /// <returns></returns>
        private static string GetNumber()
        {
            var isNegative = Peek() == '-';
            if (isNegative) Pop();

            var result = new StringBuilder(isNegative ? "-" : string.Empty);
            var hasDecimal = false;
            while (char.IsDigit(Peek()) || 
                (Peek() == '.' && !hasDecimal))
            {
                if (Peek() == '.')
                {
                    hasDecimal = true;
                }

                result.Append(Pop());
            }

            return result.ToString();
        }
    }
}
